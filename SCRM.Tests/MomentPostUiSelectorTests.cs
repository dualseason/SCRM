namespace SCRM.Tests;

/// <summary>
/// 朋友圈高级发布 UI 选择器防回归测试。
/// <para>确保标签、可见好友和提醒谁看不再只依赖自由文本，而是优先通过已有标签/联系人数据选择并提交 62203 可消费的字段。</para>
/// </summary>
public sealed class MomentPostUiSelectorTests
{
    [Fact]
    public void MomentsCenter_ShouldUseContactTagsSelectorSubmittingTagName()
    {
        var momentsCenter = ReadSource("SCRM.UI", "Components", "Pages", "MomentsCenter.razor");

        foreach (var token in new[]
        {
            "MomentPostLabelOptions",
            "TValue=\"IEnumerable<int>\"",
            "_selectedVisibleLabelIds",
            "BuildSelectedMomentPostLabelNames",
            "label.labelId",
            "label.tagName",
            "请选择标签；下发时自动提交标签名",
            "不要填写 LabelId"
        })
        {
            Assert.Contains(token, momentsCenter);
        }

        var apply = ExtractMethod(momentsCenter, "ApplyMomentPostTextFields");
        Assert.Contains("BuildSelectedMomentPostLabelNames()", apply);
        Assert.Contains("SplitMultiValueText(_visibleLabelsText)", apply);

        var selector = ExtractMethod(momentsCenter, "BuildSelectedMomentPostLabelNames");
        Assert.Contains("label.labelId == id", selector);
        Assert.Contains("?.tagName", selector);
        Assert.DoesNotContain("id.ToString()", selector);
    }

    [Fact]
    public void MomentsCenter_ShouldUseContactSelectorsForFriendsAndNotiUsers()
    {
        var momentsCenter = ReadSource("SCRM.UI", "Components", "Pages", "MomentsCenter.razor");

        foreach (var token in new[]
        {
            "MomentPostContactOptions",
            "_selectedVisibleFriendWxids",
            "_selectedNotiUserWxids",
            "Value = contact.wxid",
            "Text = $\"{BuildContactDisplayName(contact)} / {contact.wxid}\"",
            "朋友圈可见好友必须提交 wxid/username",
            "提醒谁看必须提交 wxid/username",
            "FindManualContactNameConflicts",
            "不能使用昵称或备注"
        })
        {
            Assert.Contains(token, momentsCenter);
        }

        var apply = ExtractMethod(momentsCenter, "ApplyMomentPostTextFields");
        Assert.Contains("_selectedVisibleFriendWxids", apply);
        Assert.Contains("_selectedNotiUserWxids", apply);
        Assert.Contains("SplitMultiValueText(_visibleFriendsText)", apply);
        Assert.Contains("SplitMultiValueText(_notiUsersText)", apply);
    }

    [Fact]
    public void MomentsCenter_ShouldShowFieldLevelValidationForVisibleAttachmentAndPoi()
    {
        var momentsCenter = ReadSource("SCRM.UI", "Components", "Pages", "MomentsCenter.razor");
        var validate = ExtractMethod(momentsCenter, "ValidateMomentPostForm");

        foreach (var token in new[]
        {
            "HasMomentPostFieldError(\"content\")",
            "HasMomentPostFieldError(\"attachment\")",
            "HasMomentPostFieldError(\"visible\")",
            "HasMomentPostFieldError(\"labels\")",
            "HasMomentPostFieldError(\"friends\")",
            "HasMomentPostFieldError(\"notiUsers\")",
            "HasMomentPostFieldError(\"poi\")",
            "ValidateMomentPostForm",
            "AddMomentPostFieldError",
            "部分可见必须选择至少一个标签或好友",
            "不给谁看必须选择至少一个标签或好友",
            "当前 Android 只有 POI 城市或地点名非空时才会带位置",
            "当前 Android tK42 尚未确认消费，仅作为实验字段"
        })
        {
            Assert.Contains(token, momentsCenter);
        }

        Assert.Contains("MomentPostVisibleType.WhoVisible", validate);
        Assert.Contains("MomentPostVisibleType.WhoInvisible", validate);
        Assert.Contains("MomentPostAttachmentType.Picture", validate);
        Assert.Contains("HasPoi(_postModel.poi)", validate);
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
