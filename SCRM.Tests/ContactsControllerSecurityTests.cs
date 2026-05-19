using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Security;
using SCRM.Services.Data;

namespace SCRM.Tests;

/// <summary>
/// REST 联系人接口归属校验与脱敏防回归测试。
/// <para>覆盖 ContactsController 不能绕过 ClientHub 已经收口的账号边界，也不能直接返回联系人敏感字段原文。</para>
/// </summary>
public sealed class ContactsControllerSecurityTests
{
    [Fact]
    public void ContactsController_ShouldUseAccountGuardAndMaskingService()
    {
        var source = ReadSource("SCRM.API", "Controllers", "ContactsController.cs");

        Assert.Contains("using SCRM.API.Services.Security;", source);
        Assert.Contains("private readonly AccountAccessGuard _accountAccessGuard;", source);
        Assert.Contains("private readonly SensitiveMaskingService _sensitiveMaskingService;", source);
        Assert.Contains("AccountAccessGuard accountAccessGuard", source);
        Assert.Contains("SensitiveMaskingService sensitiveMaskingService", source);
        Assert.Contains("_accountAccessGuard = accountAccessGuard;", source);
        Assert.Contains("_sensitiveMaskingService = sensitiveMaskingService;", source);

        var getContacts = ExtractMethod(source, "GetContacts");
        Assert.Contains("Math.Max(page, 1)", getContacts);
        Assert.Contains("Math.Clamp(pageSize, 1, 200)", getContacts);
        Assert.Contains("GetAccessibleAccountIdsAsync(User)", getContacts);
        Assert.Contains("accessibleAccountIds.Contains(c.ownerWxid)", getContacts);
        Assert.Contains("!c.isDeleted", getContacts);
        Assert.Contains("MaskContactsAsync(User, items)", getContacts);
        Assert.Contains("return Ok(maskedItems)", getContacts);
        Assert.DoesNotContain("FindFirstValue", getContacts);
        Assert.DoesNotContain("account.ownerId == userId", getContacts);

        var getLabels = ExtractMethod(source, "GetContactLabels");
        Assert.Contains("accountId.Trim()", getLabels);
        Assert.Contains("CanAccessAccountAsync(User, accountIdTrimmed)", getLabels);
        Assert.Contains("GetContactLabels(accountIdTrimmed, includeDeleted)", getLabels);
        Assert.DoesNotContain("account.ownerId == userId", getLabels);
        Assert.DoesNotContain("FindFirstValue", getLabels);
    }

    [Fact]
    public void AccountAccessGuard_ShouldExposeSharedAccessibleAccountQueryForRestControllers()
    {
        var source = ReadSource("SCRM.API", "Services", "Security", "AccountAccessGuard.cs");

        Assert.Contains("public async Task<List<string>> GetAccessibleAccountIdsAsync(ClaimsPrincipal? user)", source);
        Assert.Contains("account => !account.isDeleted", source);
        Assert.Contains("ownedDeviceIds", source);
        Assert.Contains("ownerlessDeviceIds", source);
        Assert.Contains("userIds.Contains(account.ownerId)", source);
        Assert.Contains("ownedDeviceIds.Contains(account.clientUuid)", source);
        Assert.Contains("ownerlessDeviceIds.Contains(account.clientUuid)", source);
        Assert.Contains("string.IsNullOrWhiteSpace(account.ownerId) && string.IsNullOrWhiteSpace(client.ownerId)", source);
    }

    [Fact]
    public async Task GetAccessibleAccountIdsAsync_ShouldMatchOwnerDeviceAndLegacyBoundaries()
    {
        await using var db = CreateDbContext();
        db.SrClients.AddRange(
            new SrClient { uuid = "device-a", ownerId = "user-a" },
            new SrClient { uuid = "device-b", ownerId = "user-b" },
            new SrClient { uuid = "device-legacy", ownerId = null });
        db.WechatAccounts.AddRange(
            new WechatAccount { wxid = "wx-account-owner", ownerId = "user-a", isDeleted = false },
            new WechatAccount { wxid = "wx-device-owner", ownerId = null, clientUuid = "device-a", isDeleted = false },
            new WechatAccount { wxid = "wx-legacy-device", ownerId = null, clientUuid = "device-legacy", isDeleted = false },
            new WechatAccount { wxid = "wx-public", ownerId = null, clientUuid = null, isDeleted = false },
            new WechatAccount { wxid = "wx-other", ownerId = "user-b", clientUuid = "device-b", isDeleted = false },
            new WechatAccount { wxid = "wx-other-on-legacy-device", ownerId = "user-b", clientUuid = "device-legacy", isDeleted = false },
            new WechatAccount { wxid = "wx-deleted", ownerId = "user-a", isDeleted = true });
        await db.SaveChangesAsync();

        var guard = new AccountAccessGuard(db, NullLogger<AccountAccessGuard>.Instance);

        var userAAccounts = await guard.GetAccessibleAccountIdsAsync(CreateUser("user-a"));
        Assert.Contains("wx-account-owner", userAAccounts);
        Assert.Contains("wx-device-owner", userAAccounts);
        Assert.Contains("wx-legacy-device", userAAccounts);
        Assert.Contains("wx-public", userAAccounts);
        Assert.DoesNotContain("wx-other", userAAccounts);
        Assert.DoesNotContain("wx-other-on-legacy-device", userAAccounts);
        Assert.DoesNotContain("wx-deleted", userAAccounts);

        var adminAccounts = await guard.GetAccessibleAccountIdsAsync(CreateUser("admin", "Admin"));
        Assert.Contains("wx-other", adminAccounts);
        Assert.Contains("wx-other-on-legacy-device", adminAccounts);
        Assert.DoesNotContain("wx-deleted", adminAccounts);
    }

    [Fact]
    public async Task CanAccessAccountAsync_ShouldNotLetExplicitOtherOwnerRideOnLegacyDevice()
    {
        await using var db = CreateDbContext();
        db.SrClients.Add(new SrClient
        {
            uuid = "device-legacy",
            ownerId = null
        });
        db.WechatAccounts.Add(new WechatAccount
        {
            wxid = "wx-other-on-legacy-device",
            ownerId = "user-b",
            clientUuid = "device-legacy",
            isDeleted = false
        });
        await db.SaveChangesAsync();

        var guard = new AccountAccessGuard(db, NullLogger<AccountAccessGuard>.Instance);

        Assert.False(await guard.CanAccessAccountAsync(CreateUser("user-a"), "wx-other-on-legacy-device"));
        Assert.True(await guard.CanAccessAccountAsync(CreateUser("user-b"), "wx-other-on-legacy-device"));
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

    private static string ExtractMethod(string source, string methodName)
    {
        var methodNameIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(methodNameIndex >= 0, $"未找到方法：{methodName}");

        var start = source.LastIndexOf("public async Task", methodNameIndex, StringComparison.Ordinal);
        Assert.True(start >= 0, $"未找到方法声明：{methodName}");

        var braceStart = source.IndexOf('{', start);
        Assert.True(braceStart >= 0, $"未找到方法体：{methodName}");

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

        throw new InvalidOperationException($"方法体未闭合：{methodName}");
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
