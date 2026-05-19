namespace SCRM.Tests;

/// <summary>
/// 微信在线、离线、登录态和任务默认 wxid 选择链路防回归测试。
/// <para>本组测试锁定 62203 对齐后的 1019/1020/1021/3050/3051/3054/3055 边界，避免离线 lastKnown 污染连接态或任务默认账号。</para>
/// </summary>
public sealed class WeChatOnlineLoginStateChainTests
{
    [Fact]
    public void TransportMessageProto_ShouldKeepWechatOnlineLoginStateEnums()
    {
        var solutionRoot = FindSolutionRoot();
        var protoPath = Path.Combine(solutionRoot, "SCRM.SHARED", "proto", "TransportMessage.proto");
        var proto = File.ReadAllText(protoPath);

        Assert.Contains("TriggerWechatPushTask = 1019;", proto);
        Assert.Contains("WeChatOnlineNotice = 1020;", proto);
        Assert.Contains("WeChatOfflineNotice = 1021;", proto);
        Assert.Contains("GetWeChatsReq = 3050;", proto);
        Assert.Contains("GetWeChatsRsp = 3051;", proto);
        Assert.Contains("AccountLogoutNotice = 3054;", proto);
        Assert.Contains("WeChatLoginNotice = 3055;", proto);
    }

    [Fact]
    public void SystemMessageHandler_ShouldGateGetWeChatsRspConnectionUpdateByOnlineFlags()
    {
        var solutionRoot = FindSolutionRoot();
        var handlerPath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "Handlers", "SystemMessageHandler.cs");
        var source = File.ReadAllText(handlerPath);
        var method = ExtractMethod(source, "private async Task HandleGetWeChatsRsp");

        Assert.Contains("var isCurrentOnline = current.IsLogined || current.IsOnline;", method);
        Assert.Contains("if (isCurrentOnline)", method);
        Assert.Contains("UpdateConnectionWeChatInfoAsync", method);
        Assert.Contains("UpsertWechatAccountSnapshotFromGetWeChatsAsync", method);
        Assert.Contains("离线/lastKnown 快照", method);
        Assert.Contains("不更新连接当前微信", method);

        var gateIndex = method.IndexOf("if (isCurrentOnline)", StringComparison.Ordinal);
        var updateIndex = method.IndexOf("UpdateConnectionWeChatInfoAsync", StringComparison.Ordinal);
        var offlineLogIndex = method.IndexOf("离线/lastKnown 快照", StringComparison.Ordinal);
        Assert.True(gateIndex >= 0 && updateIndex > gateIndex, "3051 只有在线/已登录账号才能更新连接当前 wxid。");
        Assert.True(offlineLogIndex > updateIndex, "3051 离线 lastKnown 分支应只记录诊断。");
    }

    [Fact]
    public void SystemMessageHandler_ShouldGateWeChatLoginNoticeByIsLoginTrue()
    {
        var solutionRoot = FindSolutionRoot();
        var handlerPath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "Handlers", "SystemMessageHandler.cs");
        var source = File.ReadAllText(handlerPath);
        var method = ExtractMethod(source, "private async Task HandleWeChatLoginNotice");

        Assert.Contains("FirstOrDefault(w => w.IsLogin && !string.IsNullOrWhiteSpace(w.WeChatId))", method);
        Assert.DoesNotContain("?? notice.WeChats.FirstOrDefault", method);
        Assert.Contains("UpdateConnectionWeChatInfoAsync(connId, login.WeChatId)", method);
        Assert.Contains("未包含 IsLogin=true", method);
        Assert.Contains("不更新连接当前微信", method);
    }

    [Fact]
    public void SystemMessageHandler_ShouldPreferOfflineNoticeWechatIdAndAvoidFirstAccountFallback()
    {
        var solutionRoot = FindSolutionRoot();
        var handlerPath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "Handlers", "SystemMessageHandler.cs");
        var source = File.ReadAllText(handlerPath);
        var offlineMethod = ExtractMethod(source, "private async Task HandleWeChatOffline");
        var resolveMethod = ExtractMethod(source, "private async Task<WechatAccount?> ResolveWechatAccountForOfflineAsync");
        var markMethod = ExtractMethod(source, "private async Task<bool> MarkWechatAccountOfflineAsync");

        Assert.Contains("ResolveWechatAccountForOfflineAsync", offlineMethod);
        Assert.Contains("notice.WeChatId", offlineMethod);
        Assert.Contains("connInfo?.wechatId", offlineMethod);
        Assert.DoesNotContain("FirstOrDefaultAsync(a => a.clientUuid == deviceUuid)", offlineMethod);

        Assert.Contains("DbHelper.GetWechatAccount(_db, preferred)", resolveMethod);
        Assert.Contains("IsAccountBelongsToDeviceOrUnbound", resolveMethod);
        Assert.Contains("accountStatus == WechatAccountStatusOnline", resolveMethod);
        Assert.Contains(".Select(a => a.wxid)", resolveMethod);
        Assert.Contains("DbHelper.GetWechatAccount(_db, fallbackWxid)", resolveMethod);

        Assert.Contains("account.accountStatus = WechatAccountStatusOffline", markMethod);
        Assert.Contains("DbHelper.SaveWechatAccount(_db, account)", markMethod);
    }

    [Fact]
    public void SystemMessageHandler_ShouldUseConservativeAccountLogoutOfflineFallback()
    {
        var solutionRoot = FindSolutionRoot();
        var handlerPath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "Handlers", "SystemMessageHandler.cs");
        var source = File.ReadAllText(handlerPath);
        var method = ExtractMethod(source, "private async Task HandleAccountLogoutNotice");

        Assert.Contains("ResolveWechatAccountForOfflineAsync", method);
        Assert.Contains("preferredWxid: null", method);
        Assert.Contains("connectionWxid: connInfo?.wechatId", method);
        Assert.Contains("MarkWechatAccountOfflineAsync", method);
        Assert.Contains("未找到可保守离线的当前微信账号", method);
        Assert.DoesNotContain("foreach", method);
    }

    [Fact]
    public void SystemMessageHandler_ShouldAppendLoggedInWeChatIdsOnOnlineAndSnapshotChains()
    {
        var solutionRoot = FindSolutionRoot();
        var handlerPath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "Handlers", "SystemMessageHandler.cs");
        var source = File.ReadAllText(handlerPath);
        var onlineMethod = ExtractMethod(source, "private async Task HandleWeChatOnline");
        var snapshotMethod = ExtractMethod(source, "private async Task UpsertWechatAccountSnapshotFromGetWeChatsAsync");
        var appendMethod = ExtractMethod(source, "private static bool AppendLoggedInWeChatId");

        Assert.Contains("AppendLoggedInWeChatId(srClient, account.wxid)", onlineMethod);
        Assert.Contains("AppendLoggedInWeChatId(srClient, current.WeChatId)", snapshotMethod);
        Assert.Contains("StringComparison.OrdinalIgnoreCase", appendMethod);
    }

    [Fact]
    public void WechatAccountStatus_ShouldUseZeroOfflineOneOnlineConstantsAndCorrectEntityComment()
    {
        var solutionRoot = FindSolutionRoot();
        var handlerPath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "Handlers", "SystemMessageHandler.cs");
        var handler = File.ReadAllText(handlerPath);
        var entityPath = Path.Combine(solutionRoot, "SCRM.SHARED", "Models", "Entities", "WechatAccount.cs");
        var entity = File.ReadAllText(entityPath);

        Assert.Contains("private const short WechatAccountStatusOffline = 0;", handler);
        Assert.Contains("private const short WechatAccountStatusOnline = 1;", handler);
        Assert.Contains("0-离线 1-正常在线", entity);
        Assert.DoesNotContain("2-离线", entity);
    }

    [Fact]
    public void TaskDefaultWechatId_ShouldNotFallbackToOfflineHistoryAccounts()
    {
        var solutionRoot = FindSolutionRoot();
        var servicePath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Core", "ServerDeviceCommandService.cs");
        var service = File.ReadAllText(servicePath);
        var serviceMethod = ExtractMethod(service, "private async Task<string> GetRequiredWeChatIdAsync");

        Assert.Contains("a.accountStatus == 1", serviceMethod);
        Assert.Contains("offline history/lastKnown will not be used", serviceMethod);
        Assert.DoesNotContain("account ??=", serviceMethod);
        Assert.DoesNotContain("FirstOrDefaultAsync(a => a.clientUuid == deviceUuid && !a.isDeleted);", serviceMethod);

        var hubPath = Path.Combine(solutionRoot, "SCRM.API", "Hubs", "ClientHub.cs");
        var hub = File.ReadAllText(hubPath);
        var hubMethod = ExtractMethod(hub, "private async Task<string> GetPrimaryWechatIdAsync");

        Assert.Contains("u.accountStatus == 1", hubMethod);
        Assert.Contains("lastKnown 快照只能用于展示/诊断", hubMethod);
        Assert.DoesNotContain("account ??=", hubMethod);
        Assert.DoesNotContain("FirstOrDefaultAsync(u => u.clientUuid == deviceUuid && !u.isDeleted);", hubMethod);
    }

    [Fact]
    public void PhoneCapabilityTasks_ShouldUseOptionalWechatIdWithoutOfflineHistoryFallback()
    {
        var solutionRoot = FindSolutionRoot();
        var servicePath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Core", "ServerDeviceCommandService.cs");
        var source = File.ReadAllText(servicePath);
        var optionalMethod = ExtractMethod(source, "private async Task<string> ResolveOptionalCurrentWechatIdAsync");

        Assert.Contains("a.accountStatus == 1", optionalMethod);
        Assert.Contains("绝不返回离线历史账号/lastKnown", optionalMethod);
        Assert.DoesNotContain("account ??=", optionalMethod);

        foreach (var methodName in new[]
        {
            "ExecutePhoneActionAsync",
            "SendSmsAsync",
            "PullSmsAsync",
            "PullCallLogsAsync",
            "SyncFriendListAsync"
        })
        {
            var method = ExtractMethod(source, $"public async Task<SCRM.SHARED.Models.Dtos.TaskResult> {methodName}");
            Assert.Contains("ResolveOptionalCurrentWechatIdAsync(deviceUuid)", method);
            Assert.DoesNotContain($"GetRequiredWeChatIdAsync(deviceUuid, nameof({methodName}))", method);
        }

        Assert.DoesNotContain("设备当前没有可用微信账号", ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendSmsAsync"));
        Assert.DoesNotContain("设备当前没有可用微信账号", ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> PullSmsAsync"));
        Assert.DoesNotContain("设备当前没有可用微信账号", ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> PullCallLogsAsync"));
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
