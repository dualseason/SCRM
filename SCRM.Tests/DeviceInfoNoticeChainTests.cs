namespace SCRM.Tests;

/// <summary>
/// TriggerDeviceInfo / PostDeviceInfoNotice 设备信息链路防回归测试。
/// <para>本组测试锁定 62203 对齐后的下发、Android 上报、SCRM 落库与 Web 展示边界。</para>
/// </summary>
public sealed class DeviceInfoNoticeChainTests
{
    [Fact]
    public void PostDeviceInfoProto_ShouldKeepKnownFields()
    {
        var solutionRoot = FindSolutionRoot();
        var protoPath = Path.Combine(solutionRoot, "SCRM.SHARED", "proto", "PostDeviceInfoNotice.proto");
        var proto = File.ReadAllText(protoPath);

        Assert.Contains("message PostDeviceInfoNoticeMessage", proto);
        Assert.Contains("string PhoneBrand = 1;", proto);
        Assert.Contains("string PhoneModel = 2;", proto);
        Assert.Contains("int32 OSVerNumber = 3;", proto);
        Assert.Contains("repeated DeviceAppInfoMessage AppInfos = 4;", proto);
        Assert.Contains("string NetType = 5;", proto);
        Assert.Contains("string WeChatId = 6;", proto);
        Assert.Contains("string IMEI = 7;", proto);
        Assert.Contains("string IMSI1 = 8;", proto);
        Assert.Contains("string IMSI2 = 9;", proto);
        Assert.Contains("string Number1 = 10;", proto);
        Assert.Contains("string Number2 = 11;", proto);
        Assert.Contains("bool IsHook = 12;", proto);
        Assert.Contains("bool WxSupport = 13;", proto);
        Assert.Contains("message DeviceAppInfoMessage", proto);
        Assert.Contains("string PackageName = 1;", proto);
        Assert.Contains("string AppName = 2;", proto);
        Assert.Contains("int32 VerNumber = 3;", proto);
        Assert.Contains("string Version = 4;", proto);
    }

    [Fact]
    public void TransportEnum_ShouldKeepDeviceInfoIds()
    {
        var solutionRoot = FindSolutionRoot();
        var protoPath = Path.Combine(solutionRoot, "SCRM.SHARED", "proto", "TransportMessage.proto");
        var proto = File.ReadAllText(protoPath);

        Assert.Contains("TriggerDeviceInfo = 1016;", proto);
        Assert.Contains("PostDeviceInfoNotice = 2027;", proto);
    }

    [Fact]
    public void AuthMessageHandler_ShouldPersistPostDeviceInfoIntoSrClient()
    {
        var solutionRoot = FindSolutionRoot();
        var handlerPath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "Handlers", "AuthMessageHandler.cs");
        var source = File.ReadAllText(handlerPath);
        var method = ExtractMethod(source, "public async Task HandlePostDeviceInfo");

        Assert.Contains("Unpack<PostDeviceInfoNoticeMessage>()", method);
        Assert.Contains("UpdateConnectionWeChatInfoAsync(connId, infoMsg.WeChatId)", method);
        Assert.Contains("DbHelper.GetSrClient(_dbContext, connInfo.deviceUuid)", method);
        Assert.Contains("new SrClient", method);
        Assert.Contains("srClient.device = infoMsg.Clone()", method);
        Assert.Contains("srClient.isOnline = true", method);
        Assert.Contains("srClient.connectionId = connId", method);
        Assert.Contains("srClient.lastLoginAt = DateTime.UtcNow", method);
        Assert.Contains("DbHelper.SaveSrClient(_dbContext, srClient)", method);
        Assert.Contains("DeviceStatusChangedEvent(connInfo.deviceUuid, true)", method);
        Assert.Contains("MsgReceivedAck", method);
        Assert.Contains("UpdateConnectionActivityAsync(connId)", method);
    }

    [Fact]
    public void AuthMessageHandler_ShouldAlwaysTriggerDeviceInfoAfterConfigPushAttempt()
    {
        var solutionRoot = FindSolutionRoot();
        var handlerPath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "Handlers", "AuthMessageHandler.cs");
        var source = File.ReadAllText(handlerPath);
        var method = ExtractMethod(source, "private async Task PushClientConfig");
        var setConfigIndex = method.IndexOf("EnumMsgType.SetConfigTask", StringComparison.Ordinal);
        var triggerIndex = method.IndexOf("EnumMsgType.TriggerDeviceInfo", StringComparison.Ordinal);

        Assert.True(setConfigIndex >= 0, "PushClientConfig 应保留 SetConfigTask 初始化配置下发。");
        Assert.True(triggerIndex > setConfigIndex, "TriggerDeviceInfo 应在配置推送尝试之后执行。");
        Assert.Contains("设备快照上报不应依赖配置项是否存在", method);
        Assert.Contains("Content = Any.Pack(new Empty())", method);
        Assert.Contains("请求设备上报状态", method);
    }

    [Fact]
    public void MessageRouter_ShouldRoutePostDeviceInfoNoticeToAuthHandler()
    {
        var solutionRoot = FindSolutionRoot();
        var routerPath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "MessageRouter.cs");
        var router = File.ReadAllText(routerPath);

        Assert.Contains("case EnumMsgType.PostDeviceInfoNotice:", router);
        Assert.Contains("HandlePostDeviceInfo(message, context)", router);
    }

    [Fact]
    public void SrClient_ShouldStoreDeviceAsJsonbPostDeviceInfoNotice()
    {
        var solutionRoot = FindSolutionRoot();
        var entityPath = Path.Combine(solutionRoot, "SCRM.SHARED", "Models", "Entities", "SrClient.cs");
        var entity = File.ReadAllText(entityPath);

        Assert.Contains("[Column(\"device\", TypeName = \"jsonb\")]", entity);
        Assert.Contains("PostDeviceInfoNoticeMessage device", entity);
        Assert.Contains("this.device = other.device", entity);
    }

    [Fact]
    public void DbHelper_ShouldUseSrClientCacheAndAtomicSave()
    {
        var solutionRoot = FindSolutionRoot();
        var dbHelperPath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Data", "DbHelper.cs");
        var source = File.ReadAllText(dbHelperPath);
        var getMethod = ExtractMethod(source, "public static async Task<SrClient?> GetSrClient");
        var saveMethod = ExtractMethod(source, "public static async Task<SrClient?> SaveSrClient");

        Assert.Contains("GlobalCache.srClients.TryGetValue(uuid, out var cached)", getMethod);
        Assert.Contains("AsyncLockManager.ExecuteWithLockAsync($\"SrClient_{uuid}\"", getMethod);
        Assert.Contains("context.Set<SrClient>().FindAsync(uuid)", getMethod);
        Assert.Contains("GlobalCache.srClients.TryAdd(uuid, dbItem)", getMethod);

        Assert.Contains("GlobalCache.srClients.ContainsKey(client.uuid)", saveMethod);
        Assert.Contains("context.AddAtomicGeneric(client, GlobalCache.srClients)", saveMethod);
        Assert.Contains("context.UpdateAtomicGeneric(client.uuid, GlobalCache.srClients", saveMethod);
        Assert.Contains("c.CopyFrom(client)", saveMethod);
    }

    [Fact]
    public void DevicesPage_ShouldExposePersistedDeviceSnapshotFields()
    {
        var solutionRoot = FindSolutionRoot();
        var devicesPath = Path.Combine(solutionRoot, "SCRM.UI", "Components", "Pages", "Devices.razor");
        var source = File.ReadAllText(devicesPath);

        Assert.Contains("设备: @GetDeviceModelSummary(device)", source);
        Assert.Contains("IMEI: @GetDeviceImei(device)", source);
        Assert.Contains("网络/Hook: @GetDeviceRuntimeSummary(device)", source);
        Assert.Contains("device.device?.PhoneBrand", source);
        Assert.Contains("device.device?.PhoneModel", source);
        Assert.Contains("device.device?.OSVerNumber", source);
        Assert.Contains("device.device?.IMEI", source);
        Assert.Contains("device.device?.NetType", source);
        Assert.Contains("device.device?.IsHook", source);
        Assert.Contains("device.device?.WxSupport", source);
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
