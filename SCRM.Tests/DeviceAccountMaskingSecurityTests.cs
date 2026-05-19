using SCRM.API.Models.Entities;
using SCRM.API.Services.Security;
using SCRM.SHARED.Models;

namespace SCRM.Tests;

/// <summary>
/// 设备与微信账号列表归属过滤、出口脱敏防回归测试。
/// <para>设备列表和账号列表本身也属于客资入口，不能只保护联系人、会话和短信通话明细。</para>
/// </summary>
public sealed class DeviceAccountMaskingSecurityTests
{
    [Fact]
    public void SensitiveMaskingService_ShouldMaskDeviceAndWechatAccount()
    {
        var account = new WechatAccount
        {
            wxid = "wxid_account_abcdef1234",
            wechatNumber = "wechat_alias_123",
            clientUuid = "device-1",
            nickname = "账号A",
            mobilePhone = "13812345678",
            signature = "签名 13812345678",
            qrCodeUrl = "https://file.local/qrcode.png",
            region = "广东 深圳",
            settings = "{\"AutoAcceptFriendRequest\":true}"
        };
        var device = new SrClient
        {
            uuid = "device-1",
            tcpHost = "10.0.0.1",
            tcpPort = 9000,
            ip = "192.168.1.10",
            customConfigs = "{\"wx_can_delete\":false}",
            connectionId = "signalr-connection",
            token = "transport-token",
            ownerId = "owner-a",
            loggedInWeChatIds = new List<string> { "wxid_login_abcd1234567890" }
        };
        device.wx = new Wx { srClient = device, wechatAccount = account };

        var masked = SensitiveMaskingService.MaskDevice(device, SensitiveAccessProfile.NoAccess);

        Assert.Equal("device-1", masked.uuid);
        Assert.Equal(string.Empty, masked.tcpHost);
        Assert.Equal(0, masked.tcpPort);
        Assert.Equal(string.Empty, masked.ip);
        Assert.Null(masked.customConfigs);
        Assert.Null(masked.connectionId);
        Assert.Null(masked.token);
        Assert.Equal("wxid_****7890", Assert.Single(masked.loggedInWeChatIds));
        Assert.Null(masked.owner);

        var maskedAccount = masked.wx?.wechatAccount;
        Assert.NotNull(maskedAccount);
        Assert.Equal("wxid_account_abcdef1234", maskedAccount!.wxid);
        Assert.NotEqual("wechat_alias_123", maskedAccount.wechatNumber);
        Assert.Equal("138****5678", maskedAccount.mobilePhone);
        Assert.Equal("[已隐藏]", maskedAccount.signature);
        Assert.Equal(string.Empty, maskedAccount.qrCodeUrl);
        Assert.Equal(string.Empty, maskedAccount.region);
        Assert.Null(maskedAccount.settings);
        Assert.Null(maskedAccount.owner);
        Assert.Null(masked.wx?.srClient);
    }

    [Fact]
    public void SensitiveMaskingService_ShouldMaskWechatAccountClientWithoutCycle()
    {
        var account = new WechatAccount
        {
            wxid = "wxid_account_abcdef1234",
            mobilePhone = "13812345678",
            wechatNumber = "wechat_alias_123",
            Client = new SrClient
            {
                uuid = "device-1",
                tcpHost = "10.0.0.1",
                tcpPort = 9000,
                connectionId = "signalr-connection",
                token = "transport-token",
                loggedInWeChatIds = new List<string> { "wxid_login_abcd1234567890" }
            }
        };

        var masked = SensitiveMaskingService.MaskWechatAccount(account, SensitiveAccessProfile.NoAccess, includeClient: true);

        Assert.Equal("wxid_account_abcdef1234", masked.wxid);
        Assert.Equal("138****5678", masked.mobilePhone);
        Assert.NotNull(masked.Client);
        Assert.Equal("device-1", masked.Client!.uuid);
        Assert.Equal(string.Empty, masked.Client.tcpHost);
        Assert.Equal(0, masked.Client.tcpPort);
        Assert.Null(masked.Client.connectionId);
        Assert.Null(masked.Client.token);
        Assert.Equal("wxid_****7890", Assert.Single(masked.Client.loggedInWeChatIds));
        Assert.Null(masked.Client.wx);
    }

    [Fact]
    public void CrmService_DeviceReadMethods_ShouldGuardAndMask()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");

        var getDevices = ExtractMethod(source, "GetDevicesAsync");
        Assert.Contains("var user = await GetCurrentUserAsync();", getDevices);
        Assert.Contains("DbHelper.GetAllSrClients()", getDevices);
        Assert.Contains("AccountAccessGuard.GetUserIdCandidates(user)", getDevices);
        Assert.Contains("string.IsNullOrWhiteSpace(device.ownerId)", getDevices);
        Assert.Contains("MaskDevicesAsync(user, devices)", getDevices);

        var getDevice = ExtractMethod(source, "GetDeviceAsync");
        Assert.Contains("var user = await GetCurrentUserAsync();", getDevice);
        Assert.Contains("CanAccessDeviceAsync(user, normalizedUuid)", getDevice);
        Assert.Contains("GetSrClient(normalizedUuid)", getDevice);
        Assert.Contains("MaskDevicesAsync(user, new[] { device })", getDevice);
    }

    [Fact]
    public void ControllersAndHub_ShouldMaskDeviceAndWechatAccountOutputs()
    {
        var devices = ReadSource("SCRM.API", "Controllers", "DevicesController.cs");
        Assert.Contains("using SCRM.API.Services.Security;", devices);
        Assert.Contains("private readonly SensitiveMaskingService _sensitiveMaskingService;", devices);
        Assert.Contains("private readonly DeviceOperationGuard _deviceOperationGuard;", devices);
        Assert.Contains("SensitiveMaskingService sensitiveMaskingService", devices);
        Assert.Contains("DeviceOperationGuard deviceOperationGuard", devices);
        Assert.Contains("AccountAccessGuard.IsAdmin(User)", devices);
        Assert.Contains("AccountAccessGuard.GetUserIdCandidates(User)", devices);
        Assert.Contains("c.ownerId == null", devices);
        Assert.Contains("c.ownerId == string.Empty", devices);
        Assert.Contains("userIds.Contains(c.ownerId)", devices);
        Assert.Contains("MaskDevicesAsync(User, devices)", devices);
        Assert.Contains("_deviceOperationGuard.CheckAsync", devices);
        Assert.Contains("Permissions.DeviceTask.DeleteDevice", devices);
        Assert.Contains("RecordDeviceDeletedAsync", devices);
        Assert.DoesNotContain("_userManager.GetUserId(User)", devices);

        var authDevice = ReadSource("SCRM.API", "Controllers", "Auth", "DeviceController.cs");
        Assert.Contains("private readonly SensitiveMaskingService _sensitiveMaskingService;", authDevice);
        Assert.Contains("SensitiveMaskingService sensitiveMaskingService", authDevice);
        Assert.Contains("MaskDevicesAsync(User, devices)", authDevice);
        Assert.Contains("MaskDevicesAsync(User, new[] { client })", authDevice);

        var accounts = ReadSource("SCRM.API", "Controllers", "WeChatAccountsController.cs");
        Assert.Contains("private readonly AccountAccessGuard _accountAccessGuard;", accounts);
        Assert.Contains("private readonly SensitiveMaskingService _sensitiveMaskingService;", accounts);
        Assert.Contains("GetAccessibleAccountIdsAsync(User)", accounts);
        Assert.Contains("Include(c => c.Client)", accounts);
        Assert.Contains("MaskWechatAccountsAsync(User, accounts)", accounts);
        Assert.DoesNotContain("query.Where(c => c.ownerId == userId)", accounts);

        var hub = ReadSource("SCRM.API", "Hubs", "ClientHub.cs");
        var hubGetDevices = ExtractMethod(hub, "GetDevices()");
        Assert.Contains("GetDevicesForUserAsync(userId ?? string.Empty, isAdmin)", hubGetDevices);
        Assert.Contains("MaskDevicesAsync(Context.User, result)", hubGetDevices);
    }

    [Fact]
    public void AuthService_ShouldNotReturnAllDevicesWhenUserIdMissing()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "AuthService.cs");
        var getDevices = ExtractMethod(source, "GetDevicesForUserAsync");
        var getDevice = ExtractMethod(source, "GetDeviceAsync");

        Assert.Contains("if (!isAdmin && string.IsNullOrWhiteSpace(userId))", getDevices);
        Assert.Contains("return new List<SrClient>();", getDevices);
        Assert.Contains("c.ownerId == userId || c.ownerId == null || c.ownerId == string.Empty", getDevices);
        Assert.Contains("if (!isAdmin && string.IsNullOrWhiteSpace(userId))", getDevice);
        Assert.Contains("string.Equals(client.ownerId, userId, StringComparison.OrdinalIgnoreCase)", getDevice);
    }

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var methodNameIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(methodNameIndex >= 0, $"未找到方法：{methodName}");

        var start = Math.Max(
            source.LastIndexOf("public async Task", methodNameIndex, StringComparison.Ordinal),
            source.LastIndexOf("private async Task", methodNameIndex, StringComparison.Ordinal));
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
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SCRM.SOLUTION.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
