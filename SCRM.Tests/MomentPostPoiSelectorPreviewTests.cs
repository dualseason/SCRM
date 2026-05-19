namespace SCRM.Tests;

/// <summary>
/// 朋友圈高级发布 POI 选择器与脱敏预览防回归测试。
/// <para>确保发圈 UI 能复用 GetPoiListTask 结果填充 POI，同时发布前预览只展示类型、数量和布尔摘要。</para>
/// </summary>
public sealed class MomentPostPoiSelectorPreviewTests
{
    [Fact]
    public void MomentsCenter_ShouldExposePoiSelectorUsingGetPoiListTask()
    {
        var momentsCenter = ReadSource("SCRM.UI", "Components", "Pages", "MomentsCenter.razor");

        foreach (var token in new[]
        {
            "POI 选择器",
            "复用 GetPoiListTask 查询附近地点",
            "查询附近 POI",
            "使用选中 POI",
            "清空 POI",
            "_momentPoiKeyword",
            "_momentPoiOptions",
            "_selectedMomentPoiRaw",
            "QueryMomentPoiListAsync",
            "ApplySelectedMomentPoi",
            "ClearMomentPoiFields"
        })
        {
            Assert.Contains(token, momentsCenter);
        }

        var query = ExtractMethod(momentsCenter, "QueryMomentPoiListAsync");
        Assert.Contains("Store.GetPoiListAsync(_postModel.poi.lat, _postModel.poi.lng, _momentPoiKeyword)", query);
        Assert.Contains("_pendingMomentPoiTaskId = result.taskId", query);
    }

    [Fact]
    public void MomentsCenter_ShouldConsumeAsyncPoiTaskResult()
    {
        var momentsCenter = ReadSource("SCRM.UI", "Components", "Pages", "MomentsCenter.razor");

        Assert.Contains("Store.OnAsyncTaskResultReceived += HandleMomentPoiTaskResult", momentsCenter);
        Assert.Contains("Store.OnAsyncTaskResultReceived -= HandleMomentPoiTaskResult", momentsCenter);

        var handler = ExtractMethod(momentsCenter, "HandleMomentPoiTaskResult");
        var isPoi = ExtractMethod(momentsCenter, "IsPoiTaskResult");
        var update = ExtractMethod(momentsCenter, "UpdateMomentPoiCandidates");

        Assert.Contains("IsPoiTaskResult(dto)", handler);
        Assert.Contains("UpdateMomentPoiCandidates(dto.data, dto.message, dto.taskId)", handler);
        Assert.Contains("dto.taskId == _pendingMomentPoiTaskId", isPoi);
        Assert.Contains("HasPoiListData(dto.data)", isPoi);
        Assert.Contains("ExtractMomentPoiRawItems(data)", update);
        Assert.Contains("_momentPoiOptions.Clear()", update);
        Assert.Contains("TryBuildMomentPoiOption(raw, index, out var option)", update);
    }

    [Fact]
    public void MomentsCenter_ShouldParsePoiJsonAndFillPostPoi()
    {
        var momentsCenter = ReadSource("SCRM.UI", "Components", "Pages", "MomentsCenter.razor");
        var parser = ExtractMethod(momentsCenter, "TryBuildMomentPoiOption");
        var apply = ExtractMethod(momentsCenter, "ApplySelectedMomentPoi");

        foreach (var token in new[]
        {
            "GetJsonString(root, \"Name\", \"name\")",
            "GetJsonString(root, \"City\", \"city\")",
            "GetJsonString(root, \"Address\", \"address\")",
            "GetJsonString(root, \"PoiId\", \"poiId\", \"POIId\", \"poiid\")",
            "GetJsonDouble(root, \"Lat\", \"lat\", \"Latitude\", \"latitude\")",
            "GetJsonDouble(root, \"Lng\", \"lng\", \"Longitude\", \"longitude\")"
        })
        {
            Assert.Contains(token, parser);
        }

        foreach (var token in new[]
        {
            "_postModel.poi.city = FirstNonEmpty(option.City, option.Province, option.District, _postModel.poi.city)",
            "_postModel.poi.name = FirstNonEmpty(option.Name, _postModel.poi.name)",
            "_postModel.poi.address = FirstNonEmpty(option.Address, _postModel.poi.address)",
            "_postModel.poi.poiId = FirstNonEmpty(option.PoiId, _postModel.poi.poiId)",
            "_postModel.poi.lat = (float)option.Lat",
            "_postModel.poi.lng = (float)option.Lng"
        })
        {
            Assert.Contains(token, apply);
        }
    }

    [Fact]
    public void MomentsCenter_ShouldShowSafePreviewWithoutRawValues()
    {
        var momentsCenter = ReadSource("SCRM.UI", "Components", "Pages", "MomentsCenter.razor");
        var preview = ExtractMethod(momentsCenter, "BuildMomentPostSafePreview");

        foreach (var token in new[]
        {
            "发布前脱敏预览",
            "只展示类型、数量和布尔状态",
            "不展示正文、附件 URL/JSON、wxid、标签名或 POI 原文",
            "BuildMomentPostSafePreview",
            "FormatHasText(_postModel.content)",
            "附件={_postModel.attachment.type}/{attachmentCount}条",
            "标签={labelsCount}",
            "好友={friendsCount}",
            "提醒={notiCount}",
            "POI={(HasPoi(_postModel.poi) ? \"已填\" : \"未填\")}"
        })
        {
            Assert.Contains(token, momentsCenter);
        }

        Assert.Contains("BuildMomentAttachmentContents().Count", preview);
        Assert.Contains("BuildSelectedMomentPostLabelNames()", preview);
        Assert.Contains("SplitMultiValueText(_visibleFriendsText)", preview);
        Assert.Contains("SplitMultiValueText(_notiUsersText)", preview);
        Assert.DoesNotContain("JsonSerializer.Serialize(_postModel", preview);
        Assert.DoesNotContain("_postModel.content}", preview);
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
