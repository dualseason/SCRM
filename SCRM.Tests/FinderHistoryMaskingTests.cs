namespace SCRM.Tests;

/// <summary>
/// 视频号历史字段级脱敏与权限入口防回归测试。
/// </summary>
public sealed class FinderHistoryMaskingTests
{
    [Fact]
    public void SensitiveMaskingService_ShouldMaskFinderFieldsAndKeepRawContextOutByDefault()
    {
        var masking = ReadSource("SCRM.API", "Services", "Security", "SensitiveMaskingService.cs");
        var permissions = ReadSource("SCRM.API", "Models", "Constants", "Permissions.cs");
        var seed = ReadSource("SCRM.API", "Data", "SeedData.cs");

        foreach (var method in new[]
        {
            "CanReadFinderAsync",
            "MaskFinderMentionsAsync",
            "MaskFinderUserPagesAsync",
            "MaskFinderCommentsAsync",
            "BuildFinderProfileAsync",
            "MaskFinderBrief",
            "MaskFinderMentionItem",
            "MaskFinderUserPageItem",
            "MaskFinderCommentItem"
        })
        {
            Assert.Contains(method, masking);
        }

        foreach (var sensitiveField in new[]
        {
            "nonceId = profile.CanViewFinderRaw ? value.nonceId : string.Empty",
            "thumb = profile.CanViewFinderMedia ? value.thumb : string.Empty",
            "avatar = profile.CanViewFinderMedia ? value.avatar : string.Empty",
            "content = profile.Base.CanViewMessageContent ? MaskFreeText",
            "refContent = profile.Base.CanViewMessageContent ? MaskFreeText",
            "desc = profile.Base.CanViewMessageContent ? MaskFreeText"
        })
        {
            Assert.Contains(sensitiveField, masking);
        }

        foreach (var permission in new[]
        {
            "finder.export",
            "finder.raw.view",
            "finder.media.view",
            "finder.metrics.view"
        })
        {
            Assert.Contains(permission, permissions);
        }

        foreach (var seedPermission in new[]
        {
            "Permissions.FinderOperation.Export",
            "Permissions.FinderOperation.ViewRaw",
            "Permissions.FinderOperation.ViewMedia",
            "Permissions.FinderOperation.ViewMetrics"
        })
        {
            Assert.Contains(seedPermission, seed);
        }
    }

    [Fact]
    public void CrmServiceAndHub_ShouldReadFinderThroughPermissionAndMasking()
    {
        var crm = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var hub = ReadSource("SCRM.API", "Hubs", "ClientHub.cs");

        Assert.Contains("CanReadFinderAsync(user)", crm);
        Assert.Contains("MaskFinderMentionsAsync(user, raw)", crm);
        Assert.Contains("MaskFinderUserPagesAsync(user, raw)", crm);
        Assert.Contains("MaskFinderCommentsAsync(user, raw)", crm);
        Assert.Contains("_sensitiveMaskingService.MaskMomentsAsync(Context.User, list)", hub);

        foreach (var method in new[]
        {
            "SphGetMention",
            "SphGetComment",
            "SphUserPage"
        })
        {
            Assert.Contains("DenyIfHighRiskDeviceOperationAsync", ExtractMethod(hub, method));
            Assert.Contains("Permissions.FinderOperation.Read", ExtractMethod(hub, method));
        }

        foreach (var method in new[]
        {
            "SphGetMentionAsync",
            "SphGetCommentAsync",
            "SphUserPageAsync"
        })
        {
            Assert.Contains("DenyIfHighRiskDeviceOperationAsync", ExtractMethod(crm, method));
            Assert.Contains("Permissions.FinderOperation.Read", ExtractMethod(crm, method));
        }

        foreach (var method in new[] { "SphPost", "SphComment", "SphLike" })
        {
            Assert.Contains("DenyIfHighRiskDeviceOperationAsync", ExtractMethod(hub, method));
            Assert.Contains("Permissions.FinderOperation.Interact", ExtractMethod(hub, method));
        }

        foreach (var method in new[] { "SphPostAsync", "SphCommentAsync", "SphLikeAsync" })
        {
            Assert.Contains("DenyIfHighRiskDeviceOperationAsync", ExtractMethod(crm, method));
            Assert.Contains("Permissions.FinderOperation.Interact", ExtractMethod(crm, method));
        }

        Assert.Contains("Permissions.FinderOperation.DeleteComment", ExtractMethod(hub, "SphDelComment"));
        Assert.Contains("Permissions.FinderOperation.DeleteComment", ExtractMethod(crm, "SphDelCommentAsync"));
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var marker = methodName + "(";
        var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return string.Empty;
        }

        var start = source.LastIndexOf("public ", markerIndex, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var braceStart = source.IndexOf('{', markerIndex);
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
