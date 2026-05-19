namespace SCRM.Tests;

/// <summary>
/// ScreenShotTask / ScreenShotTaskResultNotice 截图链路防回归测试。
/// <para>本组测试锁定 62203 枚举编号、服务端等待唤醒、截图 URL 透传与 Web 展示状态。</para>
/// </summary>
public sealed class ScreenShotTaskChainTests
{
    [Fact]
    public void ScreenShotProto_ShouldKeepKnownFields()
    {
        var solutionRoot = FindSolutionRoot();
        var taskProto = File.ReadAllText(Path.Combine(solutionRoot, "SCRM.SHARED", "proto", "ScreenShotTask.proto"));
        var resultProto = File.ReadAllText(Path.Combine(solutionRoot, "SCRM.SHARED", "proto", "ScreenShotTaskResultNotice.proto"));

        Assert.Contains("message ScreenShotTaskMessage", taskProto);
        Assert.Contains("string WeChatId = 1;", taskProto);
        Assert.Contains("int32 Type = 2;", taskProto);
        Assert.Contains("string Param = 3;", taskProto);
        Assert.Contains("int64 Param2 = 4;", taskProto);
        Assert.Contains("int64 TaskId = 5;", taskProto);

        Assert.Contains("message ScreenShotTaskResultNoticeMessage", resultProto);
        Assert.Contains("string WeChatId = 1;", resultProto);
        Assert.Contains("bool Success = 2;", resultProto);
        Assert.Contains("string ErrMsg = 3;", resultProto);
        Assert.Contains("string Url = 4;", resultProto);
        Assert.Contains("int64 TaskId = 5;", resultProto);
    }

    [Fact]
    public void TransportEnum_ShouldKeepScreenShotIds()
    {
        var solutionRoot = FindSolutionRoot();
        var proto = File.ReadAllText(Path.Combine(solutionRoot, "SCRM.SHARED", "proto", "TransportMessage.proto"));

        Assert.Contains("TriggerUnReadTask = 1281;", proto);
        Assert.Contains("ScreenShotTask = 1282;", proto);
        Assert.Contains("ScreenShotTaskResultNotice = 1283;", proto);
    }

    [Fact]
    public void ServerComments_ShouldNotMislabelScreenShotResultNoticeAs1282()
    {
        var solutionRoot = FindSolutionRoot();
        var handler = File.ReadAllText(Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "Handlers", "TaskMessageHandler.cs"));
        var router = File.ReadAllText(Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "MessageRouter.cs"));

        Assert.Contains("ScreenShotTaskResultNotice - 1283", handler);
        Assert.DoesNotContain("ScreenShotTaskResultNotice - 1282", handler);
        Assert.Contains("case EnumMsgType.ScreenShotTaskResultNotice: // 1283", router);
        Assert.DoesNotContain("case EnumMsgType.ScreenShotTaskResultNotice: // 1282", router);
    }

    [Fact]
    public void ClientTaskService_ShouldSendTypeZeroEmptyParamAndWaitForResult()
    {
        var solutionRoot = FindSolutionRoot();
        var source = File.ReadAllText(Path.Combine(solutionRoot, "SCRM.API", "Services", "Core", "ClientTaskService.cs"));
        var method = ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendScreenShotTaskAsync");

        Assert.Contains("new ScreenShotTaskMessage", method);
        Assert.Contains("Type = 0", method);
        Assert.Contains("Param = \"\"", method);
        Assert.Contains("TaskId = taskId", method);
        Assert.Contains("原生 screencap", method);
        Assert.Contains("EnumMsgType.ScreenShotTask.ToString()", method);
        Assert.Contains("SendTaskAndWaitAsync(task, EnumMsgType.ScreenShotTask.ToString(), connectionId, taskId, 45000)", method);
    }

    [Fact]
    public void TaskMessageHandler_ShouldCompleteScreenShotByTaskIdAndPublishEvents()
    {
        var solutionRoot = FindSolutionRoot();
        var source = File.ReadAllText(Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "Handlers", "TaskMessageHandler.cs"));

        Assert.Contains("case EnumMsgType.ScreenShotTaskResultNotice:", source);
        Assert.Contains("Unpack<ScreenShotTaskResultNoticeMessage>()", source);
        Assert.Contains("var screenshotUrl = ssMsg.Url ?? string.Empty", source);
        Assert.Contains("success = ssMsg.Success && !string.IsNullOrWhiteSpace(screenshotUrl)", source);
        Assert.Contains("taskIdRequest = ssMsg.TaskId", source);
        Assert.Contains("$\"截图成功：{screenshotUrl}\"", source);
        Assert.Contains("resultData = success ? screenshotUrl : null", source);
        Assert.Contains("new ScreenShotUploadedEvent(screenshotUrl, deviceUuid)", source);
        Assert.Contains("_clientTaskService.CompleteTask(taskIdRequest, success, resultMessage, resultData)", source);
        Assert.Contains("new TaskResultReceivedEvent(taskIdRequest, success, eventMessage, string.Empty, deviceUuid)", source);
    }

    [Fact]
    public void ClientHubAndStore_ShouldExposeScreenShotRequestAndState()
    {
        var solutionRoot = FindSolutionRoot();
        var hub = File.ReadAllText(Path.Combine(solutionRoot, "SCRM.API", "Hubs", "ClientHub.cs"));
        var service = File.ReadAllText(Path.Combine(solutionRoot, "SCRM.UI", "Services", "WeChatService.cs"));
        var store = File.ReadAllText(Path.Combine(solutionRoot, "SCRM.UI", "Services", "CrmStore.cs"));

        Assert.Contains("public async Task<TaskResult> RequestScreenShot(string deviceUuid)", hub);
        Assert.Contains("GetConnectionIdByDeviceUuidAsync(deviceUuid)", hub);
        Assert.Contains("SendScreenShotTaskAsync(connectionId, DateTime.UtcNow.Ticks)", hub);
        Assert.Contains("截图失败", hub);

        Assert.Contains("InvokeAsync<TaskResult>(\"RequestScreenShot\", deviceUuid)", service);
        Assert.Contains("public string? LastScreenShotUrl", store);
        Assert.Contains("public string? LastScreenShotDeviceUuid", store);
        Assert.Contains("public string? LastScreenShotStatusMessage", store);
        Assert.Contains("public bool? LastScreenShotSuccess", store);
        Assert.Contains("public DateTimeOffset? LastScreenShotUpdatedAt", store);
        Assert.Contains("public string? LastScreenShotAccessUrl", store);
        Assert.Contains("public DateTimeOffset? LastScreenShotAccessExpiresAt", store);
        Assert.Contains("public bool IsPreparingScreenShotAccess", store);
        Assert.Contains("SubscribeToEvent<SCRM.SHARED.Models.Events.ScreenShotUploadedEvent>(\"OnScreenShotUploaded\"", store);
        Assert.Contains("UpdateScreenShotStatus(targetDeviceUuid, true, \"截图已上传，预览链接已更新\", screenshotEvent.Url)", store);
        Assert.Contains("public async Task<TaskResult> RequestScreenShotAsync(string deviceUuid)", store);
        Assert.Contains("var screenshotUrl = result.data as string", store);
        Assert.Contains("UpdateScreenShotStatus(deviceUuid, true, result.message ?? \"截图任务已完成\", screenshotUrl)", store);
        Assert.Contains("public async Task<MediaAccessTokenDto> PrepareLastScreenShotAccessAsync(int expiresMinutes = 5)", store);
        Assert.Contains("CreateScreenshotAccessTokenAsync(LastScreenShotUrl, LastScreenShotDeviceUuid, expiresMinutes)", store);
        Assert.Contains("clearPreviewOnFailure: true", store);
        Assert.Contains("HandleTaskResultReceived", store);
        Assert.Contains("message.Contains(\"截图\", StringComparison.OrdinalIgnoreCase)", store);
    }

    [Fact]
    public void DevicesPages_ShouldDisplayScreenShotResult()
    {
        var solutionRoot = FindSolutionRoot();
        var devicesPage = File.ReadAllText(Path.Combine(solutionRoot, "SCRM.UI", "Components", "Pages", "Devices.razor"));
        var controlDialog = File.ReadAllText(Path.Combine(solutionRoot, "SCRM.UI", "Components", "Dialogs", "DeviceControlDialog.razor"));

        Assert.Contains("Text=\"截屏\"", devicesPage);
        Assert.Contains("CaptureScreen(device)", devicesPage);
        Assert.Contains("Store.RequestScreenShotAsync(dev.uuid)", devicesPage);
        Assert.Contains("截图已通过短期链接保护", devicesPage);
        Assert.Contains("PrepareScreenShotPreview", devicesPage);
        Assert.Contains("Store.PrepareLastScreenShotAccessAsync(5)", devicesPage);
        Assert.Contains("RadzenImage Path=\"@Store.LastScreenShotAccessUrl\"", devicesPage);
        Assert.DoesNotContain("RadzenImage Path=\"@Store.LastScreenShotUrl\"", devicesPage);
        Assert.Contains("HasSelectedDeviceScreenShotState()", devicesPage);
        Assert.Contains("Store.LastScreenShotStatusMessage", devicesPage);
        Assert.Contains("Store.LastScreenShotUpdatedAt", devicesPage);

        Assert.Contains("Capture Screen", controlDialog);
        Assert.Contains("OnCaptureScreen", controlDialog);
        Assert.Contains("Store.RequestScreenShotAsync(Device.uuid)", controlDialog);
        Assert.Contains("截图已通过短期链接保护", controlDialog);
        Assert.Contains("PrepareScreenShotPreview", controlDialog);
        Assert.Contains("Store.PrepareLastScreenShotAccessAsync(5)", controlDialog);
        Assert.Contains("RadzenImage Path=\"@Store.LastScreenShotAccessUrl\"", controlDialog);
        Assert.DoesNotContain("RadzenImage Path=\"@Store.LastScreenShotUrl\"", controlDialog);
        Assert.Contains("IsCurrentDeviceScreenShotState()", controlDialog);
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
