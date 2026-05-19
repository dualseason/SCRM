namespace SCRM.Tests;

/// <summary>
/// PhoneActionTask / PhoneStateTask 协议链路防回归测试。
/// <para>本组测试以静态源码约束为主，锁定 62203 对齐后容易退化的等待语义、字段投影与 UI 入口。</para>
/// </summary>
public sealed class PhoneActionStateChainTests
{
    [Fact]
    public void PhoneActionProto_ShouldKeepKnownActionsAndGapEight()
    {
        var solutionRoot = FindSolutionRoot();
        var protoPath = Path.Combine(solutionRoot, "SCRM.SHARED", "proto", "PhoneActionTask.proto");
        var proto = File.ReadAllText(protoPath);

        Assert.Contains("None = 0;", proto);
        Assert.Contains("Reboot = 1;", proto);
        Assert.Contains("UploadLog = 2;", proto);
        Assert.Contains("UploadFile = 3;", proto);
        Assert.Contains("CleanAppCache = 4;", proto);
        Assert.Contains("CleanWxCache = 5;", proto);
        Assert.Contains("CleanFileUrlCache = 6;", proto);
        Assert.Contains("PhoneCall = 7;", proto);
        Assert.DoesNotContain("= 8;", proto);
        Assert.Contains("RestartWx = 9;", proto);
        Assert.Contains("RestartSelf = 10;", proto);
    }

    [Fact]
    public void ServerDeviceCommandService_ShouldRejectNoneBeforeDispatch()
    {
        var solutionRoot = FindSolutionRoot();
        var servicePath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Core", "ServerDeviceCommandService.cs");
        var source = File.ReadAllText(servicePath);
        var method = ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> ExecutePhoneActionAsync");

        Assert.Contains("!System.Enum.IsDefined(typeof(EnumPhoneAction), action)", method);
        Assert.Contains("var phoneAction = (EnumPhoneAction)action;", method);
        Assert.Contains("phoneAction == EnumPhoneAction.None", method);
        Assert.Contains("不支持的手机操作：None", method);
        Assert.Contains("SendPhoneActionTaskAsync", method);
        Assert.Contains("phoneAction", method);
    }

    [Fact]
    public void ClientTaskService_ShouldTreatNoAckPhoneActionsAsQueued()
    {
        var solutionRoot = FindSolutionRoot();
        var servicePath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Core", "ClientTaskService.cs");
        var source = File.ReadAllText(servicePath);
        var method = ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPhoneActionTaskAsync");

        Assert.Contains("action == EnumPhoneAction.PhoneCall || action == EnumPhoneAction.Reboot", method);
        Assert.Contains("SendMessageToNettyAsync", method);
        Assert.Contains("手机通话", method);
        Assert.Contains("设备重启", method);
        Assert.Contains("TaskResult.Ok(taskId", method);
        Assert.Contains("TaskResult.Fail(taskId", method);
    }

    [Fact]
    public void ClientTaskService_ShouldUseLongerTimeoutForUploadFileAction()
    {
        var solutionRoot = FindSolutionRoot();
        var servicePath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Core", "ClientTaskService.cs");
        var source = File.ReadAllText(servicePath);
        var method = ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPhoneActionTaskAsync");

        Assert.Contains("action == EnumPhoneAction.UploadFile", method);
        Assert.Contains("SendTaskAndWaitAsync(task, EnumMsgType.PhoneActionTask.ToString(), connectionId, taskId, 60000)", method);
    }

    [Fact]
    public void PhoneStateTaskResult_ShouldProjectAllFieldsAndPublishWithoutTaskId()
    {
        var solutionRoot = FindSolutionRoot();
        var handlerPath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "Handlers", "TaskMessageHandler.cs");
        var source = File.ReadAllText(handlerPath);
        var handlerCaseIndex = source.IndexOf("case EnumMsgType.PhoneStateTaskResultNotice:", StringComparison.Ordinal);
        Assert.True(handlerCaseIndex >= 0, "TaskMessageHandler 应处理 PhoneStateTaskResultNotice。");

        var handlerTail = source[handlerCaseIndex..Math.Min(source.Length, handlerCaseIndex + 1200)];
        Assert.Contains("PhoneStateTaskResultNoticeMessage", handlerTail);
        Assert.Contains("phoneMsg.WeChatId", handlerTail);
        Assert.Contains("Imei = phoneMsg.Imei", handlerTail);
        Assert.Contains("phoneMsg.BatteryLevel", handlerTail);
        Assert.Contains("phoneMsg.ChargingState", handlerTail);
        Assert.Contains("phoneMsg.NetType", handlerTail);
        Assert.Contains("phoneMsg.SdcardFree", handlerTail);
        Assert.Contains("phoneMsg.SdcardTotal", handlerTail);

        var publishMethod = ExtractMethod(source, "private static bool ShouldPublishNoTaskIdResult");
        Assert.Contains("EnumMsgType.PhoneStateTaskResultNotice", publishMethod);
    }

    [Fact]
    public void MessageRouter_ShouldRoutePhoneStateWarningAndResultSeparately()
    {
        var solutionRoot = FindSolutionRoot();
        var routerPath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "MessageRouter.cs");
        var source = File.ReadAllText(routerPath);

        Assert.Contains("case EnumMsgType.PhoneStateWarningNotice:", source);
        Assert.Contains("HandlePhoneStateWarning(message, context)", source);
        Assert.Contains("case EnumMsgType.PhoneStateTaskResultNotice:", source);
        Assert.Contains("HandleTaskResult(message, context)", source);
    }

    [Fact]
    public void AuthMessageHandler_ShouldKeepPhoneStateWarningAsStatusSignal()
    {
        var solutionRoot = FindSolutionRoot();
        var handlerPath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "Handlers", "AuthMessageHandler.cs");
        var source = File.ReadAllText(handlerPath);
        var method = ExtractMethod(source, "public async Task HandlePhoneStateWarning");

        Assert.Contains("PhoneStateWarningNoticeMessage", method);
        Assert.Contains("warningMsg.WeChatId", method);
        Assert.Contains("warningMsg.Imei", method);
        Assert.Contains("warningMsg.NetType", method);
        Assert.Contains("UpdateConnectionWeChatInfoAsync", method);
        Assert.Contains("DeviceStatusChangedEvent", method);
    }

    [Fact]
    public void DeviceControlDialog_ShouldExposePhoneActionAndPhoneStateEntries()
    {
        var solutionRoot = FindSolutionRoot();
        var dialogPath = Path.Combine(solutionRoot, "SCRM.UI", "Components", "Dialogs", "DeviceControlDialog.razor");
        var source = File.ReadAllText(dialogPath);

        Assert.Contains("上传日志", source);
        Assert.Contains("清 App 缓存", source);
        Assert.Contains("清微信缓存", source);
        Assert.Contains("清文件 URL 缓存", source);
        Assert.Contains("重启微信", source);
        Assert.Contains("重启客户端", source);
        Assert.Contains("重启设备", source);
        Assert.Contains("拨打电话", source);
        Assert.Contains("上传文件", source);
        Assert.Contains("手机状态", source);
        Assert.Contains("OnGetPhoneState", source);
        Assert.Contains("Store.GetPhoneStateAsync", source);
        Assert.Contains("OnPhoneAction", source);
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
