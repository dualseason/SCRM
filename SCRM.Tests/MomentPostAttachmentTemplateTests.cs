namespace SCRM.Tests;

/// <summary>
/// 朋友圈高级发布附件模板防回归测试。
/// <para>确保 SCRM UI 按 62203 的 Attachment.Content 真实格式生成内容，尤其避免普通 Link 缩略图错位和 JSON 被逗号拆分。</para>
/// </summary>
public sealed class MomentPostAttachmentTemplateTests
{
    [Fact]
    public void MomentsCenter_ShouldExposeAttachmentTemplateAssistant()
    {
        var momentsCenter = ReadSource("SCRM.UI", "Components", "Pages", "MomentsCenter.razor");

        foreach (var token in new[]
        {
            "附件模板助手",
            "用模板生成附件内容",
            "清空附件模板",
            "ApplyMomentAttachmentTemplate",
            "ClearMomentAttachmentTemplate",
            "BuildMomentAttachmentContents",
            "避免 JSON 被逗号拆分或普通 Link 缩略图错位"
        })
        {
            Assert.Contains(token, momentsCenter);
        }
    }

    [Fact]
    public void LinkTemplate_ShouldOnlyGenerateUrlAndTitle()
    {
        var momentsCenter = ReadSource("SCRM.UI", "Components", "Pages", "MomentsCenter.razor");
        var buildLink = ExtractMethod(momentsCenter, "BuildLinkAttachmentContents");
        var validate = ExtractMethod(momentsCenter, "ValidateMomentPostAttachmentTemplate");

        foreach (var token in new[]
        {
            "_linkUrl",
            "_linkTitle",
            "普通 Link 为避免 Android index 2 下载错位",
            "模板仅生成 URL/标题",
            "带缩略图请使用 ExtLink"
        })
        {
            Assert.Contains(token, momentsCenter);
        }

        Assert.Contains("AddTrimmed(contents, _linkUrl)", buildLink);
        Assert.Contains("AddTrimmed(contents, _linkTitle)", buildLink);
        Assert.Contains("Take(2)", buildLink);
        Assert.DoesNotContain("_linkDescription", momentsCenter);
        Assert.DoesNotContain("_linkThumb", momentsCenter);
        Assert.Contains("普通 Link 模板仅支持 URL/标题两段", validate);
    }

    [Fact]
    public void ExtLinkTemplate_ShouldSerializeSingleAppMsgJson()
    {
        var momentsCenter = ReadSource("SCRM.UI", "Components", "Pages", "MomentsCenter.razor");
        var buildExtLink = ExtractMethod(momentsCenter, "BuildExtLinkAttachmentContents");
        var validate = ExtractMethod(momentsCenter, "ValidateMomentPostAttachmentTemplate");

        foreach (var token in new[]
        {
            "_extLinkTitle",
            "_extLinkUrl",
            "_extLinkDescription",
            "_extLinkThumb",
            "_extLinkAppId",
            "_extLinkSourceName",
            "_extLinkPagePath",
            "_extLinkType",
            "AppMsgJsonData JSON",
            "Android 可补成 Content[1]"
        })
        {
            Assert.Contains(token, momentsCenter);
        }

        foreach (var token in new[]
        {
            "AddJsonString(payload, \"Title\", _extLinkTitle)",
            "AddJsonString(payload, \"Url\", _extLinkUrl)",
            "AddJsonString(payload, \"Des\", _extLinkDescription)",
            "AddJsonString(payload, \"Thumb\", _extLinkThumb)",
            "AddJsonString(payload, \"AppId\", _extLinkAppId)",
            "AddJsonString(payload, \"SourceName\", _extLinkSourceName)",
            "AddJsonString(payload, \"PagePath\", _extLinkPagePath)",
            "payload[\"Type\"] = type",
            "JsonSerializer.Serialize(payload, MomentPostAttachmentJsonOptions)",
            "new List<string>"
        })
        {
            Assert.Contains(token, buildExtLink);
        }

        Assert.Contains("ExtLink 模板至少填写 Title 或 Url", validate);
        Assert.Contains("ExtLink 的 Content[0] 必须是 AppMsgJsonData JSON 对象", validate);
    }

    [Fact]
    public void VideoAndFinderTemplates_ShouldKeepTypedContentShape()
    {
        var momentsCenter = ReadSource("SCRM.UI", "Components", "Pages", "MomentsCenter.razor");
        var buildMoment = ExtractMethod(momentsCenter, "BuildMomentAttachmentContents");
        var buildVideo = ExtractMethod(momentsCenter, "BuildVideoAttachmentContents");
        var validate = ExtractMethod(momentsCenter, "ValidateMomentPostAttachmentTemplate");

        Assert.Contains("MomentPostAttachmentType.ShortVideo or MomentPostAttachmentType.LongVideo => BuildVideoAttachmentContents()", buildMoment);
        Assert.Contains("AddTrimmed(contents, _videoUrl)", buildVideo);
        Assert.Contains("AddTrimmed(contents, _videoThumb)", buildVideo);
        Assert.Contains("Take(2)", buildVideo);
        Assert.Contains("封面能力待实机验证", momentsCenter);

        Assert.Contains("MomentPostAttachmentType.ShiPinHao => BuildSingleJsonAttachmentContent(_finderFeedJson, _attachmentContentText)", buildMoment);
        Assert.Contains("MomentPostAttachmentType.FinderLive => BuildSingleJsonAttachmentContent(_finderLiveJson, _attachmentContentText)", buildMoment);
        Assert.Contains("视频号动态 JSON 必须整体作为一条内容，不能按逗号拆分", momentsCenter);
        Assert.Contains("视频号直播 JSON 必须整体作为一条内容，不能按逗号拆分", momentsCenter);
        Assert.Contains("VideoChannelFeedInfo 或 VideoChannelVideoInfo JSON", validate);
        Assert.Contains("FinderFeedRecord JSON", validate);
        Assert.Contains("LooksLikeJsonObject(contents[0])", validate);
    }

    [Fact]
    public void JsonLikeAttachmentTypes_ShouldNotUseMultiValueSplit()
    {
        var momentsCenter = ReadSource("SCRM.UI", "Components", "Pages", "MomentsCenter.razor");
        var buildMoment = ExtractMethod(momentsCenter, "BuildMomentAttachmentContents");
        var buildSingleJson = ExtractMethod(momentsCenter, "BuildSingleJsonAttachmentContent");
        var apply = ExtractMethod(momentsCenter, "ApplyMomentPostTextFields");

        Assert.Contains("_postModel.attachment.content = BuildMomentAttachmentContents()", apply);
        Assert.Contains("MomentPostAttachmentType.ExtLink => BuildExtLinkAttachmentContents()", buildMoment);
        Assert.Contains("BuildSingleJsonAttachmentContent(_finderFeedJson, _attachmentContentText)", buildMoment);
        Assert.Contains("BuildSingleJsonAttachmentContent(_finderLiveJson, _attachmentContentText)", buildMoment);
        Assert.Contains("return new List<string> { normalized };", buildSingleJson);
        Assert.DoesNotContain("SplitMultiValueText", buildSingleJson);
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            source,
            $@"(?:private|protected|public|internal)\s+(?:async\s+)?[\w<>,\s\?\[\]]+\s+{System.Text.RegularExpressions.Regex.Escape(methodName)}\s*\(");
        if (!match.Success)
        {
            return string.Empty;
        }

        var start = match.Index;
        var braceStart = source.IndexOf('{', match.Index);
        if (braceStart < 0)
        {
            return string.Empty;
        }

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

        return string.Empty;
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
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SCRM.SOLUTION.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Cannot locate SCRM repo root.");
    }
}
