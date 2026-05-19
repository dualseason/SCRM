using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Security;
using SCRM.Services.Data;

namespace SCRM.Tests;

/// <summary>
/// ClientHub 账号/设备归属校验防回归测试。
/// <para>先覆盖 426 文档指出的 P0-1 越权面：只凭 accountId/deviceUuid 不能读取或下发别人设备的数据。</para>
/// </summary>
public sealed class ClientHubAccountAccessGuardTests
{
    [Fact]
    public void ClientHub_ShouldInjectAndUseAccountAccessGuardOnHighRiskEntries()
    {
        var source = ReadSource("SCRM.API", "Hubs", "ClientHub.cs");

        Assert.Contains("using SCRM.API.Services.Security;", source);
        Assert.Contains("private readonly AccountAccessGuard _accountAccessGuard;", source);
        Assert.Contains("AccountAccessGuard accountAccessGuard", source);
        Assert.Contains("_accountAccessGuard = accountAccessGuard;", source);
        Assert.Contains("CanAccessAccountOrLogAsync", source);
        Assert.Contains("CanAccessDeviceOrLogAsync", source);

        foreach (var methodName in new[]
        {
            "GetContacts",
            "GetChatHistory",
            "GetGroupMembers",
            "GetConversations",
            "GetSmsRecords",
            "GetCallLogRecords"
        })
        {
            var method = ExtractMethod(source, $"public async Task", methodName);
            Assert.Contains($"CanAccessAccountOrLogAsync(accountId, nameof({methodName}))", method);
        }

        foreach (var methodName in new[]
        {
            "SendMessage",
            "SendSms",
            "PullSms",
            "PullCallLogs",
            "SendGroupMessage"
        })
        {
            var method = ExtractMethod(source, $"public async Task<TaskResult> {methodName}");
            Assert.True(
                method.Contains($"CanAccessDeviceOrLogAsync(deviceUuid, nameof({methodName}))", StringComparison.Ordinal)
                || (method.Contains("DenyIfDeviceOrSensitiveContentAsync", StringComparison.Ordinal)
                    && method.Contains($"nameof({methodName})", StringComparison.Ordinal)),
                $"{methodName} 未接入设备归属校验或统一发送前风控 helper。");
            Assert.True(
                method.Contains("无权访问该设备", StringComparison.Ordinal)
                || method.Contains("return denied;", StringComparison.Ordinal),
                $"{methodName} 未返回设备越权拒绝结果。");
        }
    }

    [Fact]
    public void Program_ShouldRegisterAccountAccessGuard()
    {
        var source = ReadSource("SCRM.API", "Program.cs");
        Assert.Contains("AddScoped<SCRM.API.Services.Security.AccountAccessGuard>()", source);
    }

    [Fact]
    public async Task AccountAccessGuard_ShouldAllowOwnerAndBlockOtherUserByDeviceOwner()
    {
        await using var db = CreateDbContext();
        db.SrClients.Add(new SrClient
        {
            uuid = "device-b",
            ownerId = "user-b"
        });
        db.WechatAccounts.Add(new WechatAccount
        {
            wxid = "wx-b",
            clientUuid = "device-b",
            ownerId = null,
            isDeleted = false
        });
        await db.SaveChangesAsync();

        var guard = new AccountAccessGuard(db, NullLogger<AccountAccessGuard>.Instance);

        Assert.False(await guard.CanAccessAccountAsync(CreateUser("user-a"), "wx-b"));
        Assert.True(await guard.CanAccessAccountAsync(CreateUser("user-b"), "wx-b"));
    }

    [Fact]
    public async Task AccountAccessGuard_ShouldAllowDirectAccountOwnerAndAdmin()
    {
        await using var db = CreateDbContext();
        db.WechatAccounts.Add(new WechatAccount
        {
            wxid = "wx-owner",
            ownerId = "user-a",
            isDeleted = false
        });
        await db.SaveChangesAsync();

        var guard = new AccountAccessGuard(db, NullLogger<AccountAccessGuard>.Instance);

        Assert.True(await guard.CanAccessAccountAsync(CreateUser("user-a"), "wx-owner"));
        Assert.False(await guard.CanAccessAccountAsync(CreateUser("user-b"), "wx-owner"));
        Assert.True(await guard.CanAccessAccountAsync(CreateUser("admin", "Admin"), "wx-owner"));
    }

    [Fact]
    public async Task AccountAccessGuard_ShouldKeepLegacyOwnerlessDeviceVisible()
    {
        await using var db = CreateDbContext();
        db.SrClients.Add(new SrClient
        {
            uuid = "legacy-device",
            ownerId = null
        });
        db.WechatAccounts.Add(new WechatAccount
        {
            wxid = "legacy-wx",
            clientUuid = "legacy-device",
            isDeleted = false
        });
        await db.SaveChangesAsync();

        var guard = new AccountAccessGuard(db, NullLogger<AccountAccessGuard>.Instance);

        Assert.True(await guard.CanAccessDeviceAsync(CreateUser("user-a"), "legacy-device"));
        Assert.True(await guard.CanAccessAccountAsync(CreateUser("user-a"), "legacy-wx"));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ClaimsPrincipal CreateUser(string userId, string? role = null)
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

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string ExtractMethod(string source, string signaturePrefix, string? methodName = null)
    {
        var signature = methodName == null ? signaturePrefix : $"{signaturePrefix}";
        var start = methodName == null
            ? source.IndexOf(signature, StringComparison.Ordinal)
            : source.IndexOf(methodName, source.IndexOf(signature, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.True(start >= 0, $"未找到方法：{signaturePrefix} {methodName}");

        // 回退到 public async Task... 的方法声明开头，便于提取完整方法体。
        if (methodName != null)
        {
            var publicStart = source.LastIndexOf("public async Task", start, StringComparison.Ordinal);
            Assert.True(publicStart >= 0, $"未找到方法声明：{methodName}");
            start = publicStart;
        }

        return ExtractBlockFromStart(source, start, methodName ?? signaturePrefix);
    }

    private static string ExtractBlockFromStart(string source, int start, string label)
    {
        var braceStart = source.IndexOf('{', start);
        Assert.True(braceStart >= 0, $"未找到方法体：{label}");

        var depth = 0;
        for (var index = braceStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[start..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException($"方法体未闭合：{label}");
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SCRM.SOLUTION.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("未找到 SCRM.SOLUTION.sln，无法定位 SCRM 源码。");
    }
}
