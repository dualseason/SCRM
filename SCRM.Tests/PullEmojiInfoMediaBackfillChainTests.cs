namespace SCRM.Tests;

/// <summary>
/// PullEmojiInfoTask 消息级表情补图闭环静态验收。
/// <para>锁定 1272/1273 -> 1269/1271 的服务端衔接边界，避免把 1273 结果直接写成消息内容。</para>
/// </summary>
public sealed class PullEmojiInfoMediaBackfillChainTests
{
    [Fact]
    public void ClientTaskService_ShouldKeepMessageLevelPullEmojiContext()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "ClientTaskService.cs");

        Assert.Contains("PullEmojiInfoTaskContext", source);
        Assert.Contains("_pullEmojiInfoTaskContexts", source);
        Assert.Contains("PullEmojiInfoTaskContextTtl", source);
        Assert.Contains("RegisterPullEmojiInfoTaskContext", source);
        Assert.Contains("TryTakePullEmojiInfoTaskContext", source);
        Assert.Contains("RemovePullEmojiInfoTaskContext", source);
        Assert.Contains("PruneExpiredPullEmojiInfoTaskContexts", source);
        Assert.Contains("SendPullEmojiInfoForMessageTaskAsync", source);
        Assert.Contains("MsgSvrId = msgSvrId", source);
        Assert.Contains("FriendId = friendId?.Trim()", source);
        Assert.Contains("Failed to send to Netty", source);
    }

    [Fact]
    public void TaskMessageHandler_ShouldChainPullEmojiResultToCdnDownloadWithoutDirectMessageWrite()
    {
        var source = ReadSource("SCRM.API", "Services", "Netty", "Handlers", "TaskMessageHandler.cs");
        var caseSource = ExtractPullEmojiInfoCase(source);

        Assert.Contains("TryTakePullEmojiInfoTaskContext", caseSource);
        Assert.Contains("TryStartEmojiCdnDownloadAsync", source);
        Assert.Contains("SendCDNDownloadFileTaskAsync", source);
        Assert.Contains("CDNFileType.ChatMsgEmoji", source);
        Assert.Contains("ResolveEmojiDownloadUrl", source);
        Assert.Contains("emoji.Encrypturl", source);
        Assert.Contains("emoji.CdnUrl", source);
        Assert.Contains("emoji.ExternUrl", source);
        Assert.Contains("selectedEmoji.Aeskey", source);
        Assert.Contains("emojiContext.MsgSvrId", source);

        Assert.DoesNotContain("UpdateMessageMediaUrlByMsgSvrId", caseSource);
        Assert.DoesNotContain("UpdateMessageContentByMsgSvrId", caseSource);
        Assert.DoesNotContain("SaveMessages", caseSource);
    }

    [Fact]
    public void SCRM_Surface_ShouldExposeMessageLevelEmojiBackfillEntry()
    {
        var hub = ReadSource("SCRM.API", "Hubs", "ClientHub.cs");
        var command = ReadSource("SCRM.API", "Services", "Core", "ServerDeviceCommandService.cs");
        var contract = ReadSource("SCRM.UI", "Interfaces", "ICrmService.cs");
        var realtimeService = ReadSource("SCRM.UI", "Services", "WeChatService.cs");
        var store = ReadSource("SCRM.UI", "Services", "CrmStore.cs");

        Assert.Contains("PullEmojiInfoForMessage", hub);
        Assert.Contains("SendPullEmojiInfoForMessageTaskAsync", hub);
        Assert.Contains("PullEmojiInfoForMessageAsync", command);
        Assert.Contains("GetRequiredWeChatIdAsync", command);
        Assert.Contains("PullEmojiInfoForMessageAsync", contract);
        Assert.Contains("PullEmojiInfoForMessage", realtimeService);
        Assert.Contains("PullEmojiInfoForMessageAsync", store);
        Assert.Contains("表情补图指令已下发，等待 CDN 回填", store);
        Assert.Contains("RefreshCurrentChatAfterTaskResultAsync(targetDeviceUuid, \"表情补图\")", store);
    }

    [Fact]
    public void IM_UI_ShouldShowEmojiBackfillButtonOnlyForMissingEmojiMedia()
    {
        var markup = ReadSource("SCRM.UI", "Components", "Pages", "ImCenterChatPane.razor");
        var actions = ReadSource("SCRM.UI", "Components", "Pages", "ImCenterChatPane.Actions.cs");

        Assert.Contains("补表情", markup);
        Assert.Contains("CanPullEmojiInfoForMessage(msg, display)", markup);
        Assert.Contains("PullEmojiMediaForMessage(msg, display)", markup);
        Assert.Contains("display.Kind == ChatMessageKind.Emoji", actions);
        Assert.Contains("string.IsNullOrWhiteSpace(display.MediaUrl)", actions);
        Assert.Contains("ResolveEmojiMd5", actions);
        Assert.Contains("ExtractMd5FromExtensionValue", actions);
        Assert.Contains("ExtractMd5FromText", actions);
        Assert.Contains("Store.PullEmojiInfoForMessageAsync", actions);
    }

    [Fact]
    public void PullEmojiInfoProto_ShouldStillKeepFieldNineGap()
    {
        var proto = ReadSource("SCRM.SHARED", "proto", "PullEmojiInfoTaskResultNotice.proto");

        Assert.DoesNotContain("= 9;", proto);
        Assert.Contains("string encrypturl = 10;", proto);
        Assert.Contains("string aeskey = 11;", proto);
    }

    private static string ExtractPullEmojiInfoCase(string source)
    {
        var caseStart = source.IndexOf("case EnumMsgType.PullEmojiInfoTaskResultNotice:", StringComparison.Ordinal);
        Assert.True(caseStart >= 0, "未找到 PullEmojiInfoTaskResultNotice case。");

        var nextCase = source.IndexOf("case EnumMsgType.TakeMoneyTaskResultNotice:", caseStart, StringComparison.Ordinal);
        Assert.True(nextCase > caseStart, "未找到 PullEmojiInfoTaskResultNotice case 的结束位置。");

        return source[caseStart..nextCase];
    }

    private static string ReadSource(params string[] parts)
    {
        var path = Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray());
        return File.ReadAllText(path);
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
