namespace SCRM.Tests;

/// <summary>
/// UpgradeDeviceAppNotice / PostDeleteDeviceNotice 设备管理通知链路防回归测试。
/// <para>1094/1097 都是 Notice 语义：无 TaskId、无标准结果回包，服务端只确认写入在线 TCP 通道。</para>
/// </summary>
public sealed class DeviceManagementNoticeChainTests
{
    [Fact]
    public void DeviceManagementProto_ShouldKeepKnownFields()
    {
        var solutionRoot = FindSolutionRoot();
        var protoRoot = Path.Combine(solutionRoot, "SCRM.SHARED", "proto");
        var upgradeProto = File.ReadAllText(Path.Combine(protoRoot, "UpgradeDeviceAppNotice.proto"));
        var deleteProto = File.ReadAllText(Path.Combine(protoRoot, "PostDeleteDeviceNotice.proto"));

        Assert.Contains("message DeviceAppUpgradeMessage", upgradeProto);
        Assert.Contains("int32 VerNumber = 1;", upgradeProto);
        Assert.Contains("string Version = 2;", upgradeProto);
        Assert.Contains("string PackageName = 3;", upgradeProto);
        Assert.Contains("string PackageUrl = 4;", upgradeProto);
        Assert.Contains("message UpgradeDeviceAppNoticeMessage", upgradeProto);
        Assert.Contains("string WeChatId = 1;", upgradeProto);
        Assert.Contains("string IMEI = 2;", upgradeProto);
        Assert.Contains("repeated DeviceAppUpgradeMessage AppInfos = 3;", upgradeProto);

        Assert.Contains("message PostDeleteDeviceNoticeMessage", deleteProto);
        Assert.Contains("string IMEI = 1;", deleteProto);
    }

    [Fact]
    public void TransportEnum_ShouldKeepDeviceManagementNoticeIds()
    {
        var solutionRoot = FindSolutionRoot();
        var protoPath = Path.Combine(solutionRoot, "SCRM.SHARED", "proto", "TransportMessage.proto");
        var proto = File.ReadAllText(protoPath);

        Assert.Contains("UpgradeDeviceAppNotice = 1094;", proto);
        Assert.Contains("PostDeleteDeviceNotice = 1097;", proto);
        Assert.Contains("UpgradeAppNotice = 1093;", proto);
    }

    [Fact]
    public void ClientTaskService_ShouldSendDeviceManagementNoticesWithoutWaiting()
    {
        var solutionRoot = FindSolutionRoot();
        var servicePath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Core", "ClientTaskService.cs");
        var source = File.ReadAllText(servicePath);
        var deleteMethod = ExtractMethod(source, "public async Task<bool> SendPostDeleteDeviceNoticeAsync");
        var upgradeMethod = ExtractMethod(source, "public async Task<bool> SendUpgradeDeviceAppNoticeAsync");

        Assert.Contains("new PostDeleteDeviceNoticeMessage", deleteMethod);
        Assert.Contains("IMEI = imei ?? string.Empty", deleteMethod);
        Assert.Contains("SendMessageToNettyAsync", deleteMethod);
        Assert.Contains("EnumMsgType.PostDeleteDeviceNotice.ToString()", deleteMethod);
        Assert.DoesNotContain("SendTaskAndWaitAsync", deleteMethod);

        Assert.Contains("new UpgradeDeviceAppNoticeMessage", upgradeMethod);
        Assert.Contains("WeChatId = weChatId ?? string.Empty", upgradeMethod);
        Assert.Contains("IMEI = imei ?? string.Empty", upgradeMethod);
        Assert.Contains("notice.AppInfos.Add(new DeviceAppUpgradeMessage", upgradeMethod);
        Assert.Contains("PackageName = packageName ?? string.Empty", upgradeMethod);
        Assert.Contains("Version = version ?? string.Empty", upgradeMethod);
        Assert.Contains("VerNumber = versionCode", upgradeMethod);
        Assert.Contains("PackageUrl = packageUrl ?? string.Empty", upgradeMethod);
        Assert.Contains("SendMessageToNettyAsync", upgradeMethod);
        Assert.Contains("EnumMsgType.UpgradeDeviceAppNotice.ToString()", upgradeMethod);
        Assert.DoesNotContain("SendTaskAndWaitAsync", upgradeMethod);
    }

    [Fact]
    public void ServerDeviceCommandService_ShouldTreatDeleteAsNoAckNotice()
    {
        var solutionRoot = FindSolutionRoot();
        var servicePath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Core", "ServerDeviceCommandService.cs");
        var source = File.ReadAllText(servicePath);
        var method = ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> NotifyDeviceDeleteAsync");

        Assert.Contains("GetConnectionIdByDeviceUuidAsync(deviceUuid)", method);
        Assert.Contains("设备离线，未发送删除通知", method);
        Assert.Contains("db.SrClients.AsNoTracking()", method);
        Assert.Contains("device?.device?.IMEI", method);
        Assert.Contains("? deviceUuid", method);
        Assert.Contains("SendPostDeleteDeviceNoticeAsync(connectionId, imei)", method);
        Assert.Contains("PostDeleteDeviceNotice dispatched", method);
        Assert.Contains("TaskResult.Ok(0", method);
        Assert.Contains("删除设备通知已下发", method);
        Assert.DoesNotContain("SendTaskAndWaitAsync", method);
    }

    [Fact]
    public void ServerDeviceCommandService_ShouldValidateAndSendUpgradeNotice()
    {
        var solutionRoot = FindSolutionRoot();
        var servicePath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Core", "ServerDeviceCommandService.cs");
        var source = File.ReadAllText(servicePath);
        var method = ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> UpgradeDeviceAppAsync");

        Assert.Contains("\"com.juliao.ty.imscrm\"", method);
        Assert.Contains("versionCode.ToString()", method);
        Assert.Contains("versionCode <= 0", method);
        Assert.Contains("版本码 VerNumber 必须大于 0", method);
        Assert.Contains("string.IsNullOrWhiteSpace(normalizedPackageUrl)", method);
        Assert.Contains("升级包下载地址不能为空", method);
        Assert.Contains("GetConnectionIdByDeviceUuidAsync(deviceUuid)", method);
        Assert.Contains("设备离线，无法下发升级通知", method);
        Assert.Contains("device?.device?.IMEI", method);
        Assert.Contains("device?.device?.WeChatId", method);
        Assert.Contains("SendUpgradeDeviceAppNoticeAsync", method);
        Assert.Contains("UpgradeDeviceAppNotice dispatched", method);
        Assert.Contains("TaskResult.Ok(0", method);
        Assert.Contains("升级通知已下发", method);
        Assert.DoesNotContain("SendTaskAndWaitAsync", method);
    }

    [Fact]
    public void CrmServiceAndDevicesController_ShouldNotifyBeforeDeletingSrClient()
    {
        var solutionRoot = FindSolutionRoot();
        var crmServicePath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Core", "CrmService.cs");
        var controllerPath = Path.Combine(solutionRoot, "SCRM.API", "Controllers", "DevicesController.cs");
        var crmSource = File.ReadAllText(crmServicePath);
        var controllerSource = File.ReadAllText(controllerPath);

        var crmMethod = ExtractMethod(crmSource, "public async Task<bool> DeleteDeviceAsync");
        Assert.Contains("DbHelper.DeleteSrClient", crmSource);
        Assert.Contains("db.GetSrClient(uuid.Trim())", crmMethod);
        Assert.Contains("NotifyDeviceDeleteAsync(device.uuid)", crmMethod);
        Assert.Contains("db.DeleteSrClient(device)", crmMethod);
        Assert.DoesNotContain("SrClients.Remove", crmMethod);

        var controllerMethod = ExtractMethod(controllerSource, "public async Task<IActionResult> DeleteDevice");
        Assert.Contains("NotifyDeviceDeleteAsync(device.uuid)", controllerMethod);
        Assert.Contains("_context.DeleteSrClient(device)", controllerMethod);
        Assert.DoesNotContain("SrClients.Remove", controllerMethod);
        Assert.DoesNotContain("SaveChanges", controllerMethod);
    }

    [Fact]
    public void UiAndStore_ShouldExposeUpgradeAndDeleteNoticeSemantics()
    {
        var solutionRoot = FindSolutionRoot();
        var interfacePath = Path.Combine(solutionRoot, "SCRM.UI", "Interfaces", "ICrmService.cs");
        var storePath = Path.Combine(solutionRoot, "SCRM.UI", "Services", "CrmStore.cs");
        var pagePath = Path.Combine(solutionRoot, "SCRM.UI", "Components", "Pages", "Admin", "DeviceList.razor");
        var contract = File.ReadAllText(interfacePath);
        var store = File.ReadAllText(storePath);
        var page = File.ReadAllText(pagePath);

        Assert.Contains("在线设备会先下发 PostDeleteDeviceNotice(1097)", contract);
        Assert.Contains("UpgradeDeviceAppNotice(1094) 无结果回包", contract);
        Assert.Contains("Task<bool> DeleteDeviceAsync(string uuid)", contract);
        Assert.Contains("Task<TaskResult> UpgradeDeviceAppAsync", contract);

        Assert.Contains("public async Task<bool> DeleteDeviceAsync(string uuid)", store);
        Assert.Contains("_service.DeleteDeviceAsync(uuid)", store);
        Assert.Contains("Devices.RemoveAll", store);
        Assert.Contains("public async Task<TaskResult> UpgradeDeviceAppAsync", store);
        Assert.Contains("_service.UpgradeDeviceAppAsync", store);
        Assert.Contains("升级通知已下发", store);

        Assert.Contains("设备 App 升级通知", page);
        Assert.Contains("UpgradeDeviceAppNotice(1094)", page);
        Assert.Contains("成功只表示通知已写入在线 TCP 通道", page);
        Assert.Contains("PostDeleteDeviceNotice", page);
        Assert.Contains("Store.DeleteDeviceAsync(device.uuid)", page);
        Assert.Contains("Store.UpgradeDeviceAppAsync", page);
        Assert.Contains("Disabled=\"@(!data.isOnline)\"", page);
    }

    [Fact]
    public void UpgradeDeviceApp_ShouldRemainManualInputUntilAppVersionIsIntegrated()
    {
        var solutionRoot = FindSolutionRoot();
        var pagePath = Path.Combine(solutionRoot, "SCRM.UI", "Components", "Pages", "Admin", "DeviceList.razor");
        var page = File.ReadAllText(pagePath);

        Assert.Contains("_upgradePackageName = \"com.juliao.ty.imscrm\"", page);
        Assert.Contains("_upgradeVersion", page);
        Assert.Contains("_upgradePackageUrl", page);
        Assert.Contains("_upgradeVersionCode = 1", page);
        Assert.Contains("PackageUrl", page);
        Assert.DoesNotContain("AppVersion", page);
    }

    private static string ExtractMethod(string source, string signaturePrefix)
    {
        var start = source.IndexOf(signaturePrefix, StringComparison.Ordinal);
        Assert.True(start >= 0, $"未找到方法：{signaturePrefix}");

        var braceStart = source.IndexOf('{', start);
        Assert.True(braceStart >= 0, $"未找到方法体：{signaturePrefix}");

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

        throw new InvalidOperationException($"方法体未闭合：{signaturePrefix}");
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
