using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Security;
using SCRM.Models.Constants;
using SCRM.Services.Data;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.Tests;

/// <summary>
/// 敏感媒体访问审计查询防回归测试。
/// <para>v377 已写入 system_logs；本组测试保证 v378 查询页/接口不会绕过审计权限、账号/设备归属边界或泄露 token 明文。</para>
/// </summary>
public sealed class SensitiveMediaAccessAuditQueryTests
{
    [Fact]
    public void QueryFeature_ShouldExposeDtoServiceControllerCrmServiceAndUi()
    {
        var dto = ReadSource("SCRM.SHARED", "Models", "Dtos", "SensitiveMediaAccessAuditDtos.cs");
        var service = ReadSource("SCRM.API", "Services", "Security", "SensitiveMediaAccessAuditQueryService.cs");
        var controller = ReadSource("SCRM.API", "Controllers", "SensitiveMediaAccessAuditController.cs");
        var crm = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var contract = ReadSource("SCRM.UI", "Interfaces", "ICrmService.cs");
        var page = ReadSource("SCRM.UI", "Components", "Pages", "SensitiveMediaAccessAudit.razor");
        var nav = ReadSource("SCRM.UI", "Components", "Layout", "NavMenu.razor");
        var program = ReadSource("SCRM.API", "Program.cs");
        var permissions = ReadSource("SCRM.API", "Models", "Constants", "Permissions.cs");
        var seed = ReadSource("SCRM.API", "Data", "SeedData.cs");

        Assert.Contains("public class SensitiveMediaAccessAuditQueryDto", dto);
        Assert.Contains("public class SensitiveMediaAccessAuditQueryResultDto", dto);
        Assert.Contains("public class SensitiveMediaAccessAuditItemDto", dto);
        Assert.Contains("tokenHash", dto);
        Assert.DoesNotContain("public string token ", dto, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("public class SensitiveMediaAccessAuditQueryService", service);
        Assert.Contains("SensitiveMediaAccessAuditService.ModuleName", service);
        Assert.Contains("GetAccessibleAccountIdsAsync(user)", service);
        Assert.Contains("GetAccessibleDeviceIdsAsync(user)", service);
        Assert.Contains("Permissions.Risk.AuditView", service);
        Assert.Contains("Permissions.System.Logs", service);
        Assert.Contains("JsonDocument.Parse", service);

        Assert.Contains("[Route(\"api/sensitive-media-access-audits\")]", controller);
        Assert.Contains("SensitiveMediaAccessAuditQueryService queryService", controller);
        Assert.Contains("return Forbid();", controller);

        Assert.Contains("GetSensitiveMediaAccessAuditsAsync(SensitiveMediaAccessAuditQueryDto query)", contract);
        Assert.Contains("SensitiveMediaAccessAuditQueryService sensitiveMediaAccessAuditQueryService", crm);
        Assert.Contains("_sensitiveMediaAccessAuditQueryService.QueryAsync(user, query)", crm);
        Assert.Contains("AddScoped<SCRM.API.Services.Security.SensitiveMediaAccessAuditQueryService>()", program);

        Assert.Contains("public const string AuditView = \"risk.audit.view\";", permissions);
        Assert.Contains("EnsureSecurityAuditPermissionsAsync", seed);
        Assert.Contains("Permissions.Risk.AuditView", seed);
        Assert.Contains("查看敏感媒体访问审计", seed);
        Assert.Contains("@page \"/security/sensitive-media-audit\"", page);
        Assert.Contains("CrmService.GetSensitiveMediaAccessAuditsAsync(_query)", page);
        Assert.Contains("token 哈希", page);
        Assert.Contains("敏感媒体审计", nav);
    }

    [Fact]
    public void AccountAccessGuard_ShouldExposeAccessibleDeviceQueryForAuditBoundaries()
    {
        var source = ReadSource("SCRM.API", "Services", "Security", "AccountAccessGuard.cs");

        Assert.Contains("public async Task<List<string>> GetAccessibleDeviceIdsAsync(ClaimsPrincipal? user)", source);
        Assert.Contains("IsAdmin(user)", source);
        Assert.Contains("client.ownerId == null", source);
        Assert.Contains("userIds.Contains(client.ownerId)", source);
    }

    [Fact]
    public async Task QueryAsync_ShouldRequireAuditPermissionAndFilterByOwnership()
    {
        await using var db = CreateDbContext();
        db.SrClients.AddRange(
            new SrClient { uuid = "device-a", ownerId = "user-a" },
            new SrClient { uuid = "device-b", ownerId = "user-b" });
        db.WechatAccounts.AddRange(
            new WechatAccount { wxid = "wx-a", clientUuid = "device-a", isDeleted = false },
            new WechatAccount { wxid = "wx-b", clientUuid = "device-b", isDeleted = false });
        db.SystemLogs.AddRange(
            BuildLog(1, "Info", "TokenIssued", "user-x", "wx:wx-a", Message("media", "wx/wx-a/pic/a.jpg", "WechatAccount", "wx-a", "CreateMediaAccessTokenAsync", tokenHash: "ABCDEF1234567890")),
            BuildLog(2, "Info", "TokenOpened", "user-b", "device:device-b", Message("screenshot", "devices/device-b/screenshot/b.png", "Device", "device-b", "Open")),
            BuildLog(3, "Warning", "TokenDenied", "user-a", null, Message("media", "", "None", "", "CreateToken", detail: "URL 不属于 uploads")));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var denied = await service.QueryAsync(CreateUser("user-a"), new SensitiveMediaAccessAuditQueryDto());
        Assert.False(denied.hasAccess);

        var userResult = await service.QueryAsync(
            CreateUser("user-a", permission: Permissions.Risk.AuditView),
            new SensitiveMediaAccessAuditQueryDto { pageSize = 20 });
        Assert.True(userResult.hasAccess);
        Assert.Equal(2, userResult.totalCount);
        Assert.Contains(userResult.items, item => item.id == 1 && item.relativePath == "wx/wx-a/pic/a.jpg" && item.tokenHash == "ABCDEF1234567890");
        Assert.Contains(userResult.items, item => item.id == 3 && item.operatorId == "user-a" && item.detail.Contains("uploads", StringComparison.Ordinal));
        Assert.DoesNotContain(userResult.items, item => item.id == 2);

        var adminResult = await service.QueryAsync(CreateUser("admin", role: "Admin"), new SensitiveMediaAccessAuditQueryDto { pageSize = 20 });
        Assert.True(adminResult.hasAccess);
        Assert.Equal(3, adminResult.totalCount);
    }

    [Fact]
    public async Task QueryAsync_ShouldSupportActionScopeAccountAndKeywordFilters()
    {
        await using var db = CreateDbContext();
        db.SrClients.Add(new SrClient { uuid = "device-a", ownerId = "user-a" });
        db.WechatAccounts.Add(new WechatAccount { wxid = "wx-a", clientUuid = "device-a", isDeleted = false });
        db.SystemLogs.AddRange(
            BuildLog(1, "Info", "TokenIssued", "user-a", "wx:wx-a", Message("media", "wx/wx-a/video/a.mp4", "WechatAccount", "wx-a", "CreateMediaAccessTokenAsync", accountId: "wx-a")),
            BuildLog(2, "Warning", "TokenOpenDenied", "user-a", "wx:wx-a", Message("media", "wx/wx-a/video/missing.mp4", "WechatAccount", "wx-a", "Open", detail: "媒体文件不存在")),
            BuildLog(3, "Info", "TokenIssued", "user-a", "device:device-a", Message("screenshot", "devices/device-a/screenshot/a.png", "Device", "device-a", "CreateScreenshotAccessTokenAsync", deviceUuid: "device-a")));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var user = CreateUser("user-a", permission: Permissions.System.Logs);

        var issuedMedia = await service.QueryAsync(user, new SensitiveMediaAccessAuditQueryDto
        {
            action = "TokenIssued",
            scope = "media",
            accountId = "wx-a",
            keyword = "video",
            pageSize = 10
        });

        Assert.Single(issuedMedia.items);
        Assert.Equal(1, issuedMedia.items[0].id);
        Assert.Equal("CreateMediaAccessTokenAsync", issuedMedia.items[0].source);

        var denied = await service.QueryAsync(user, new SensitiveMediaAccessAuditQueryDto
        {
            action = "TokenOpenDenied",
            detail = "不存在",
            pageSize = 10
        });

        Assert.Single(denied.items);
        Assert.Equal(2, denied.items[0].id);
    }

    private static SensitiveMediaAccessAuditQueryService CreateService(ApplicationDbContext db)
    {
        var guard = new AccountAccessGuard(db, NullLogger<AccountAccessGuard>.Instance);
        return new SensitiveMediaAccessAuditQueryService(db, guard, NullLogger<SensitiveMediaAccessAuditQueryService>.Instance);
    }

    private static SystemLog BuildLog(long id, string level, string action, string operatorId, string? targetId, string message)
    {
        return new SystemLog
        {
            id = id,
            level = level,
            module = SensitiveMediaAccessAuditService.ModuleName,
            action = action,
            operatorId = operatorId,
            targetId = targetId,
            clientIp = "127.0.0.1",
            createdAt = DateTime.UtcNow.AddMinutes(id),
            message = message
        };
    }

    private static string Message(
        string scope,
        string relativePath,
        string targetKind,
        string targetValue,
        string source,
        string accountId = "",
        string deviceUuid = "",
        string detail = "",
        string tokenHash = "")
    {
        return JsonSerializer.Serialize(new
        {
            scope,
            relativePath,
            targetKind,
            targetValue,
            accountId = string.IsNullOrWhiteSpace(accountId) ? null : accountId,
            deviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? null : deviceUuid,
            source,
            detail = string.IsNullOrWhiteSpace(detail) ? null : detail,
            tokenHash = string.IsNullOrWhiteSpace(tokenHash) ? null : tokenHash,
            issuedAt = DateTimeOffset.UtcNow,
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            userAgent = "xunit",
            requestPath = "/api/media-access/open",
            queryKeys = new[] { "token" }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ClaimsPrincipal CreateUser(string userId, string? role = null, string? permission = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId)
        };
        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (!string.IsNullOrWhiteSpace(permission))
        {
            claims.Add(new Claim("permission", permission));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SCRM.SOLUTION.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
