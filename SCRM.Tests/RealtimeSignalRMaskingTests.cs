namespace SCRM.Tests;

/// <summary>
/// 朋友圈与视频号实时推送脱敏防回归测试。
/// <para>实时链路只允许广播变更通知，真实正文和媒体字段必须经服务层重拉后按权限脱敏。</para>
/// </summary>
public sealed class RealtimeSignalRMaskingTests
{
    [Fact]
    public void NettyAndUiRealtimePath_ShouldUseChangedNoticeInsteadOfRawDto()
    {
        var noticeDto = ReadSource("SCRM.SHARED", "Models", "Dtos", "RealtimeDataChangedNoticeDto.cs");
        var events = ReadSource("SCRM.SHARED", "Models", "Events", "DomainEvents.cs");
        var momentsHandler = ReadSource("SCRM.API", "Services", "Netty", "Handlers", "MomentsMessageHandler.cs");
        var taskHandler = ReadSource("SCRM.API", "Services", "Netty", "Handlers", "TaskMessageHandler.cs");
        var weChatService = ReadSource("SCRM.UI", "Services", "WeChatService.cs");
        var store = ReadSource("SCRM.UI", "Services", "CrmStore.cs");

        Assert.Contains("public sealed class RealtimeDataChangedNoticeDto", noticeDto);
        Assert.Contains("summary", noticeDto);
        Assert.Contains("MomentTimelineChangedEvent", events);
        Assert.Contains("FinderResultChangedEvent", events);

        Assert.Contains("MomentTimelineChanged", momentsHandler);
        Assert.Contains("FinderResultChanged", momentsHandler);
        Assert.Contains("RealtimeDataChangedNoticeDto", momentsHandler);
        Assert.DoesNotContain("SendAsync(\"MomentTimelineReceived\"", momentsHandler);
        Assert.DoesNotContain("FinderMentionListReceived", momentsHandler);
        Assert.DoesNotContain("FinderUserPageReceived", momentsHandler);
        Assert.DoesNotContain("FinderCommentListReceived", momentsHandler);

        Assert.Contains("MomentTimelineChanged", taskHandler);
        Assert.DoesNotContain("SendAsync(\"MomentTimelineReceived\"", taskHandler);

        Assert.Contains("OnRealtimeDataChanged", weChatService);
        Assert.Contains("_hubConnection.On<RealtimeDataChangedNoticeDto>(\"MomentTimelineChanged\"", weChatService);
        Assert.Contains("_hubConnection.On<RealtimeDataChangedNoticeDto>(\"FinderResultChanged\"", weChatService);
        Assert.DoesNotContain("_hubConnection.On<MomentsTimelineDto>(\"MomentTimelineReceived\"", weChatService);
        Assert.DoesNotContain("_hubConnection.On<FinderMentionNoticeDto>(\"FinderMentionListReceived\"", weChatService);

        Assert.Contains("HandleRealtimeDataChanged", store);
        Assert.Contains("SubscribeToEvent<RealtimeDataChangedNoticeDto>(\"MomentTimelineChanged\"", store);
        Assert.Contains("SubscribeToEvent<RealtimeDataChangedNoticeDto>(\"FinderResultChanged\"", store);
        Assert.Contains("QueueMomentsReload(dto.deviceUuid)", store);
        Assert.Contains("ReloadFinderHistoryForSelectedDeviceAsync()", store);
        Assert.DoesNotContain("SubscribeToEvent<MomentsTimelineDto>(\"MomentTimelineReceived\"", store);
        Assert.DoesNotContain("SubscribeToEvent<FinderMentionNoticeDto>(\"FinderMentionListReceived\"", store);
        Assert.DoesNotContain("MergeMomentDtoIntoCurrentList(dto)", store);
        Assert.DoesNotContain("AddFinderMentionHistory(dto)", store);
    }

    private static string ReadSource(params string[] parts)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(new[] { root }.Concat(parts).ToArray());
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
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

        throw new InvalidOperationException("无法定位 SCRM 仓库根目录");
    }
}
