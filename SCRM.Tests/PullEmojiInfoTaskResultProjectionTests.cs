namespace SCRM.Tests;

/// <summary>
/// PullEmojiInfoTaskResultNotice 表情信息结果投影防回归测试。
/// <para>锁定 1273 任务结果只完成等待任务并完整保留 EmojiMessage 字段，避免后续改动丢失 encrypturl/aeskey/cdnUrl 等补全表情所需参数。</para>
/// </summary>
public sealed class PullEmojiInfoTaskResultProjectionTests
{
    [Fact]
    public void TaskMessageHandler_ShouldUseTaskIdAndReturnSuccessForPullEmojiInfoResult()
    {
        var caseSource = ExtractPullEmojiInfoCase();

        Assert.Contains("message.Content.Unpack<PullEmojiInfoTaskResultNoticeMessage>()", caseSource);
        Assert.Contains("taskIdRequest = emojiMsg.TaskId", caseSource);
        Assert.Contains("success = true", caseSource);
        Assert.Contains("表情信息拉取成功：Count={emojiMsg.Emojis.Count}", caseSource);
        Assert.Contains("_clientTaskService.CompleteTask(taskIdRequest, success, resultMessage, resultData)", ReadTaskMessageHandlerSource());
    }

    [Theory]
    [InlineData("Md5")]
    [InlineData("CdnUrl")]
    [InlineData("Catalog")]
    [InlineData("Type")]
    [InlineData("State")]
    [InlineData("Width")]
    [InlineData("Height")]
    [InlineData("Size")]
    [InlineData("Encrypturl")]
    [InlineData("Aeskey")]
    [InlineData("ExternUrl")]
    [InlineData("ExternMd5")]
    [InlineData("Name")]
    [InlineData("GroupId")]
    [InlineData("ThumbUrl")]
    [InlineData("Desc")]
    public void TaskMessageHandler_ShouldKeepEmojiResultProjectionFields(string fieldName)
    {
        var caseSource = ExtractPullEmojiInfoCase();

        Assert.Contains($"emoji.{fieldName}", caseSource);
    }

    [Fact]
    public void TaskMessageHandler_ShouldNotWriteEmojiResultDirectlyToMessageContentOrMedia()
    {
        var caseSource = ExtractPullEmojiInfoCase();

        Assert.DoesNotContain("UpdateMessageMediaUrlByMsgSvrId", caseSource);
        Assert.DoesNotContain("UpdateMessageContentByMsgSvrId", caseSource);
        Assert.DoesNotContain("SaveMessages", caseSource);
    }

    [Fact]
    public void PullEmojiInfoProto_ShouldKeepReservedFieldNineGapAndAllKnownFields()
    {
        var solutionRoot = FindSolutionRoot();
        var protoPath = Path.Combine(solutionRoot, "SCRM.SHARED", "proto", "PullEmojiInfoTaskResultNotice.proto");
        var proto = File.ReadAllText(protoPath);

        Assert.Contains("string md5 = 1;", proto);
        Assert.Contains("string cdnUrl = 2;", proto);
        Assert.Contains("int32 catalog = 3;", proto);
        Assert.Contains("int32 type = 4;", proto);
        Assert.Contains("int32 state = 5;", proto);
        Assert.Contains("int32 width = 6;", proto);
        Assert.Contains("int32 height = 7;", proto);
        Assert.Contains("int32 size = 8;", proto);
        Assert.DoesNotContain("= 9;", proto);
        Assert.Contains("string encrypturl = 10;", proto);
        Assert.Contains("string aeskey = 11;", proto);
        Assert.Contains("string externUrl = 12;", proto);
        Assert.Contains("string externMd5 = 13;", proto);
        Assert.Contains("string name = 14;", proto);
        Assert.Contains("string groupId = 15;", proto);
        Assert.Contains("string thumbUrl = 16;", proto);
        Assert.Contains("string desc = 17;", proto);
    }

    private static string ExtractPullEmojiInfoCase()
    {
        var source = ReadTaskMessageHandlerSource();
        var caseStart = source.IndexOf("case EnumMsgType.PullEmojiInfoTaskResultNotice:", StringComparison.Ordinal);
        Assert.True(caseStart >= 0, "未找到 PullEmojiInfoTaskResultNotice case。");

        var nextCase = source.IndexOf("case EnumMsgType.TakeMoneyTaskResultNotice:", caseStart, StringComparison.Ordinal);
        Assert.True(nextCase > caseStart, "未找到 PullEmojiInfoTaskResultNotice case 的结束位置。");

        return source[caseStart..nextCase];
    }

    private static string ReadTaskMessageHandlerSource()
    {
        var solutionRoot = FindSolutionRoot();
        var handlerPath = Path.Combine(solutionRoot, "SCRM.API", "Services", "Netty", "Handlers", "TaskMessageHandler.cs");
        return File.ReadAllText(handlerPath);
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
