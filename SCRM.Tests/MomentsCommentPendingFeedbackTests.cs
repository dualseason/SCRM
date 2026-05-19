namespace SCRM.Tests;

/// <summary>
/// 朋友圈评论即时反馈防回归测试。
/// <para>锁定“先临时显示、失败撤销、回执校准”的前端行为，避免评论再次退化为等待 Android 完成回调后才出现在页面。</para>
/// </summary>
public sealed class MomentsCommentPendingFeedbackTests
{
    [Fact]
    public void CrmStore_ShouldCreatePendingCommentBeforeAwaitingAndroidResult()
    {
        var solutionRoot = FindSolutionRoot();
        var crmStorePath = Path.Combine(solutionRoot, "SCRM.UI", "Services", "CrmStore.cs");
        var source = File.ReadAllText(crmStorePath);
        var methodSource = ExtractMethod(source, "public async Task<TaskResult> ReplyMomentCommentAsync");

        var pendingIndex = methodSource.IndexOf("var pendingCommentId = ApplyMomentCommentPendingUpdate", StringComparison.Ordinal);
        var sendIndex = methodSource.IndexOf("_service.ReplyMomentCommentAsync", StringComparison.Ordinal);
        var rollbackIndex = methodSource.IndexOf("RemoveMomentCommentPendingUpdate(circleId, pendingCommentId)", StringComparison.Ordinal);

        Assert.True(pendingIndex >= 0, "ReplyMomentCommentAsync 应先创建本地临时评论。");
        Assert.True(sendIndex >= 0, "ReplyMomentCommentAsync 应继续下发安卓任务。");
        Assert.True(pendingIndex < sendIndex, "本地临时评论必须早于等待安卓回执创建，用户才能立即看到待同步评论。");
        Assert.True(rollbackIndex > sendIndex, "服务端明确下发失败后应撤销本地临时评论。");
        Assert.Contains("IsMomentCommentTimeoutAfterClientExecution(result)", methodSource);
        Assert.Contains("朋友圈评论已临时显示", methodSource);
    }

    [Fact]
    public void CrmStore_ShouldUseNegativeCommentIdAsPendingMarker()
    {
        var solutionRoot = FindSolutionRoot();
        var crmStorePath = Path.Combine(solutionRoot, "SCRM.UI", "Services", "CrmStore.cs");
        var source = File.ReadAllText(crmStorePath);
        var methodSource = ExtractMethod(source, "private long ApplyMomentCommentPendingUpdate");

        Assert.Contains("var pendingCommentId = -DateTime.UtcNow.Ticks", methodSource);
        Assert.Contains("commentId = pendingCommentId", methodSource);
        Assert.Contains("RecordPendingMomentComment(circleId, pendingComment)", methodSource);
        Assert.Contains("return pendingCommentId", methodSource);
    }

    [Fact]
    public void MomentsFeedItem_ShouldDisplayPendingCommentBadgeAndBlockReply()
    {
        var solutionRoot = FindSolutionRoot();
        var feedItemPath = Path.Combine(solutionRoot, "SCRM.UI", "Components", "Pages", "MomentsFeedItem.razor");
        var source = File.ReadAllText(feedItemPath);

        Assert.Contains("IsPendingComment", source);
        Assert.Contains("待同步", source);
        Assert.Contains("待微信回执确认，暂不能回复", source);
        Assert.Contains("comment?.commentId < 0", source);
        Assert.Contains("该评论正在等待微信回执确认", source);
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
