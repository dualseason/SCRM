namespace SCRM.Tests;

/// <summary>
/// 好友请求和好友关系任务 owner wxid 防回归测试。
/// <para>锁定 62203 对齐后的好友类任务归属：会改变微信好友关系或读取好友请求的任务必须使用当前在线微信号，不得依赖 Android 端空 WeChatId 兜底。</para>
/// </summary>
public sealed class FriendRequestOwnerWechatIdChainTests
{
    [Fact]
    public void AutomationAutoAccept_ShouldPassFriendRequestOwnerWechatId()
    {
        var source = ReadSource("SCRM.API", "Services", "Automation", "AutomationService.cs");
        var method = ExtractMethod(source, "private async Task HandleFriendRequest");

        Assert.Contains("SendAcceptFriendAddRequestTaskAsync", method);
        Assert.Contains("weChatId: e.weChatId", method);
        Assert.Contains("MarkFriendRequestAccepted(e.weChatId, e.friendId", method);
        Assert.DoesNotContain("SendAcceptFriendAddRequestTaskAsync(e.connectionId, e.friendId, e.friendNick, taskId)", method);
    }

    [Fact]
    public void ManualAcceptFriendRequest_ShouldUseSameOnlineOwnerForDispatchAndStatusMark()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var method = ExtractMethod(source, "public async Task<TaskResult> AcceptFriendRequestAsync");
        var resolver = ExtractMethod(source, "private async Task<string> ResolveOnlineWechatIdRequiredAsync");

        Assert.Contains("var ownerWxid = await ResolveOnlineWechatIdRequiredAsync(deviceUuid);", method);
        Assert.Contains("normalizedOperation,", method);
        Assert.Contains("ownerWxid);", method);
        Assert.Contains("MarkFriendRequestRejected(ownerWxid, friendId", method);
        Assert.Contains("MarkFriendRequestAccepted(ownerWxid, friendId", method);
        Assert.DoesNotContain("OrderByDescending(item => item.accountStatus == 1)", method);
        Assert.DoesNotContain("account.wxid", method);

        Assert.Contains("item.accountStatus == 1", resolver);
        Assert.Contains("!item.isDeleted", resolver);
        Assert.Contains("不能回退到离线历史账号或 lastKnown 快照", source);
    }

    [Fact]
    public void ServerFriendCapabilityTasks_ShouldResolveOnlineWechatIdBeforeDispatch()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "ServerDeviceCommandService.cs");

        foreach (var methodName in new[]
        {
            "AcceptFriendRequestAsync",
            "PullFriendAddReqListAsync",
            "AddFriendInChatRoomAsync",
            "AddFriendWithSceneAsync",
            "SetFriendPermissionAsync",
            "DeleteFriendAsync"
        })
        {
            var method = ExtractMethod(source, $"public async Task<TaskResult> {methodName}");
            Assert.Contains($"GetRequiredWeChatIdAsync(deviceUuid, nameof({methodName}))", method);
            Assert.Contains("ownerWxid", method);
            Assert.Contains("设备当前没有可用微信账号", method);
        }

        var findContactMethod = ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> FindContactAsync");
        Assert.Contains("GetRequiredWeChatIdAsync(deviceUuid, nameof(FindContactAsync))", findContactMethod);
        Assert.Contains("ownerWxid", findContactMethod);
        Assert.Contains("设备当前没有可用微信账号", findContactMethod);

        Assert.Contains("SendAcceptFriendAddRequestTaskAsync", ExtractMethod(source, "public async Task<TaskResult> AcceptFriendRequestAsync"));
        Assert.Contains("ownerWxid);", ExtractMethod(source, "public async Task<TaskResult> AcceptFriendRequestAsync"));
        Assert.Contains("SendPullFriendAddReqListTaskAsync(connectionId, startTime, onlyNew, getAll, ownerWxid", ExtractMethod(source, "public async Task<TaskResult> PullFriendAddReqListAsync"));
        Assert.Contains("SendFindContactTaskAsync(connectionId, content, ownerWxid", findContactMethod);
        Assert.Contains("weChatId: ownerWxid", ExtractMethod(source, "public async Task<TaskResult> AddFriendInChatRoomAsync"));
        Assert.Contains("verificationImagePath, ownerWxid", ExtractMethod(source, "public async Task<TaskResult> AddFriendWithSceneAsync"));
        Assert.Contains("SendSetFriendPermissionTaskAsync(connectionId, friendId, permissionMask, taskId, ownerWxid)", ExtractMethod(source, "public async Task<TaskResult> SetFriendPermissionAsync"));
        Assert.Contains("SendDeleteFriendTaskAsync(connectionId, friendId, taskId, ownerWxid)", ExtractMethod(source, "public async Task<TaskResult> DeleteFriendAsync"));
    }

    [Fact]
    public void ClientTaskFriendMessages_ShouldFillWechatIdFields()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "ClientTaskService.cs");

        var addWithScene = ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAddFriendWithSceneTaskAsync");
        Assert.Contains("string weChatId = \"\"", addWithScene);
        Assert.Contains("WeChatId = weChatId ?? string.Empty", addWithScene);

        var addInRoom = ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAddFriendInChatRoomTaskAsync");
        Assert.Contains("string weChatId = \"\"", addInRoom);
        Assert.Contains("WeChatId = weChatId ?? string.Empty", addInRoom);

        var deleteFriend = ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendDeleteFriendTaskAsync");
        Assert.Contains("string weChatId = \"\"", deleteFriend);
        Assert.Contains("WeChatId = weChatId ?? string.Empty", deleteFriend);

        var permission = ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendSetFriendPermissionTaskAsync");
        Assert.Contains("string weChatId = \"\"", permission);
        Assert.Contains("WeChatId = weChatId ?? string.Empty", permission);
        Assert.DoesNotContain("WeChatId = string.Empty", permission);

        var accept = ExtractMethod(source, "public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAcceptFriendAddRequestTaskAsync");
        Assert.Contains("string weChatId = \"\"", accept);
        Assert.Contains("WeChatId = weChatId ?? string.Empty", accept);
    }

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string ExtractMethod(string source, string signaturePrefix)
    {
        var start = source.IndexOf(signaturePrefix, StringComparison.Ordinal);
        Assert.True(start >= 0, $"未找到方法：{signaturePrefix}");
        return ExtractMethodFromStart(source, start, signaturePrefix);
    }

    private static string ExtractMethodFromStart(string source, int start, string label)
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
