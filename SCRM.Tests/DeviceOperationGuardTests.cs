namespace SCRM.Tests;

/// <summary>
/// 高危设备操作授权与审计防回归测试。
/// <para>删除好友、清空聊天、微信登出、群管理、截图、钱包和定位等操作不能只做普通设备归属校验。</para>
/// </summary>
public sealed class DeviceOperationGuardTests
{
    [Fact]
    public void DeviceOperationGuard_ShouldProvidePermissionAuditAndStrictOwnerBoundary()
    {
        var guard = ReadSource("SCRM.API", "Services", "Security", "DeviceOperationGuard.cs");
        var permissions = ReadSource("SCRM.API", "Models", "Constants", "Permissions.cs");
        var seed = ReadSource("SCRM.API", "Data", "SeedData.cs");
        var program = ReadSource("SCRM.API", "Program.cs");

        Assert.Contains("public class DeviceOperationGuard", guard);
        Assert.Contains("ModuleName = \"DeviceOperationRisk\"", guard);
        Assert.Contains("ActionTaskDenied", guard);
        Assert.Contains("ActionTaskQueued", guard);
        Assert.Contains("ownerless_device", guard);
        Assert.Contains("missing_permission", guard);
        Assert.Contains("HasPermissionAsync", guard);
        Assert.Contains("GetGroupActionPermission", guard);
        Assert.Contains("RecordDeviceDeletedAsync", guard);
        Assert.Contains("ActionDeviceDeleted", guard);
        Assert.Contains("ISystemLogService", guard);

        foreach (var permission in new[]
        {
            "DeviceTask",
            "WechatOperation",
            "MessageOperation",
            "ContactOperation",
            "GroupOperation",
            "MomentOperation",
            "FinderOperation",
            "device_task.screenshot",
            "device_task.delete_device",
            "wechat.location.query",
            "wechat.wallet.query",
            "wechat.account.logout",
            "message.revoke",
            "message.clear_wechat",
            "contact.delete_wechat",
            "contact.permission_set",
            "group.manage",
            "moment.delete",
            "finder.read",
            "finder.delete_comment"
        })
        {
            Assert.Contains(permission, permissions);
        }

        foreach (var permissionSeed in new[]
        {
            "Permissions.DeviceTask.Sync",
            "Permissions.DeviceTask.ConfigManage",
            "Permissions.DeviceTask.SensitiveConfigManage",
            "Permissions.DeviceTask.Screenshot",
            "Permissions.DeviceTask.DeleteDevice",
            "Permissions.WechatOperation.LocationQuery",
            "Permissions.WechatOperation.WalletQuery",
            "Permissions.WechatOperation.Logout",
            "Permissions.MessageOperation.Revoke",
            "Permissions.MessageOperation.ClearWechat",
            "Permissions.ContactOperation.DeleteWechat",
            "Permissions.ContactOperation.PermissionSet",
            "Permissions.GroupOperation.Manage",
            "Permissions.MomentOperation.Delete",
            "Permissions.FinderOperation.Read",
            "Permissions.FinderOperation.DeleteComment"
        })
        {
            Assert.Contains(permissionSeed, seed);
        }

        Assert.Contains("BuildSensitivePermission", seed);
        Assert.Contains("AddScoped<SCRM.API.Services.Security.DeviceOperationGuard>()", program);
    }

    [Fact]
    public void HubAndCrmService_HighRiskEntrypoints_ShouldUseDeviceOperationGuard()
    {
        var hub = ReadSource("SCRM.API", "Hubs", "ClientHub.cs");
        var crm = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var devicesController = ReadSource("SCRM.API", "Controllers", "DevicesController.cs");

        Assert.Contains("DeviceOperationGuard deviceOperationGuard", hub);
        Assert.Contains("DenyIfHighRiskDeviceOperationAsync", hub);
        Assert.Contains("DeviceOperationGuard deviceOperationGuard", crm);
        Assert.Contains("DenyIfHighRiskDeviceOperationAsync", crm);
        Assert.Contains("DeviceOperationGuard deviceOperationGuard", devicesController);
        Assert.Contains("Permissions.DeviceTask.DeleteDevice", devicesController);

        foreach (var method in new[]
        {
            "GetWeChatLocation",
            "GetWalletBalance",
            "SetDeviceConfig",
            "SetForbiddenWord",
            "RevokeMessage",
            "ClearAllChatMsg",
            "WechatLogout",
            "ExecuteGroupAction",
            "DeleteFriend",
            "SetFriendPermission",
            "RequestScreenShot"
        })
        {
            Assert.Contains("DenyIfHighRiskDeviceOperationAsync", ExtractMethod(hub, method));
        }

        foreach (var method in new[]
        {
            "DeleteDeviceAsync",
            "RequestScreenShotAsync",
            "GetWeChatLocationAsync",
            "GetWalletBalanceAsync",
            "RevokeMessageAsync",
            "ClearAllChatMsgAsync",
            "SetDeviceConfigAsync",
            "SetForbiddenWordAsync",
            "WechatLogoutAsync",
            "ExecuteGroupActionAsync",
            "SetFriendPermissionAsync",
            "DeleteFriendAsync"
        })
        {
            Assert.Contains("DenyIfHighRiskDeviceOperationAsync", ExtractMethod(crm, method));
        }

        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(hub, "ExecuteGroupAction"));
        Assert.Contains("DenyIfDeviceOrSensitiveContentAsync", ExtractMethod(crm, "ExecuteGroupActionAsync"));
        Assert.Contains("RecordDeviceDeletedAsync", ExtractMethod(crm, "DeleteDeviceAsync"));
        Assert.Contains("CheckAsync", ExtractMethod(devicesController, "DeleteDevice"));
        Assert.Contains("RecordDeviceDeletedAsync", ExtractMethod(devicesController, "DeleteDevice"));
    }

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var methodNameIndex = source.IndexOf($" {methodName}(", StringComparison.Ordinal);
        if (methodNameIndex < 0)
        {
            methodNameIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        }

        Assert.True(methodNameIndex >= 0, $"未找到方法：{methodName}");

        var start = source.LastIndexOf("public async Task", methodNameIndex, StringComparison.Ordinal);
        Assert.True(start >= 0, $"未找到方法声明：{methodName}");

        var braceStart = source.IndexOf('{', start);
        Assert.True(braceStart >= 0, $"未找到方法体：{methodName}");

        var depth = 0;
        for (var i = braceStart; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            if (source[i] == '}') depth--;
            if (depth == 0)
            {
                return source[start..(i + 1)];
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
