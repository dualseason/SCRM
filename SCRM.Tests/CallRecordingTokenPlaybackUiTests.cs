namespace SCRM.Tests;

/// <summary>
/// 通话录音播放接入短期 token 的防回归测试。
/// <para>录音列表只保留 hasRecording 作为可申请播放 token 的标记，不能再把永久 recordUrl 下发并绑定到浏览器。</para>
/// </summary>
public sealed class CallRecordingTokenPlaybackUiTests
{
    [Fact]
    public void PhoneRecords_ShouldUseShortLivedTokenInsteadOfDirectRecordUrl()
    {
        var source = ReadSource("SCRM.UI", "Components", "Pages", "PhoneRecords.razor");

        Assert.Contains("CreateCallRecordingAccessTokenAsync(item.id, 5)", source);
        Assert.Contains("_activeCallRecordingUrl", source);
        Assert.Contains("<audio src=\"@_activeCallRecordingUrl\"", source);
        Assert.Contains("打开短期链接", source);
        Assert.Contains("后端短期 token 链接播放", source);
        Assert.Contains("无/无权限", source);
        Assert.Contains("ClearActiveRecording()", source);
        Assert.Contains("!item.hasRecording", source);

        Assert.DoesNotContain("string.IsNullOrWhiteSpace(item.recordUrl)", source);
        Assert.DoesNotContain("href=\"@item.recordUrl\"", source);
        Assert.DoesNotContain("src=\"@item.recordUrl\"", source);
        Assert.DoesNotContain("打开录音</a>", source);
    }

    [Fact]
    public void CrmStoreAndInterface_ShouldExposeCallRecordingTokenMethod()
    {
        var store = ReadSource("SCRM.UI", "Services", "CrmStore.cs");
        var contract = ReadSource("SCRM.UI", "Interfaces", "ICrmService.cs");
        var dto = ReadSource("SCRM.SHARED", "Models", "Dtos", "MediaAccessTokenDto.cs");

        Assert.Contains("Task<MediaAccessTokenDto> CreateCallRecordingAccessTokenAsync(int callLogId, int expiresMinutes = 5)", contract);
        Assert.Contains("public async Task<MediaAccessTokenDto> CreateCallRecordingAccessTokenAsync(int callLogId, int expiresMinutes = 5)", store);
        Assert.Contains("_service.CreateCallRecordingAccessTokenAsync(callLogId, expiresMinutes)", store);

        Assert.Contains("public class MediaAccessTokenDto", dto);
        Assert.Contains("public bool Success { get; set; }", dto);
        Assert.Contains("public string Url { get; set; }", dto);
        Assert.Contains("public static MediaAccessTokenDto Fail", dto);
        Assert.Contains("public static MediaAccessTokenDto Ok", dto);
    }

    [Fact]
    public void CrmService_ShouldIssueCallRecordingTokenWithOwnershipAndPermissionChecks()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var method = ExtractMethod(source, "CreateCallRecordingAccessTokenAsync");

        Assert.Contains("MediaAccessTokenService mediaAccessTokenService", source);
        Assert.Contains("_mediaAccessTokenService = mediaAccessTokenService;", source);
        Assert.Contains("db.CallLogRecords", method);
        Assert.Contains("AsNoTracking()", method);
        Assert.Contains("CanAccessAccountAsync(user, record.ownerWxid)", method);
        Assert.Contains("BuildProfileAsync(user)", method);
        Assert.Contains("!profile.CanViewCallRecordUrl", method);
        Assert.Contains("TryNormalizeUploadRelativePath(record.recordUrl", method);
        Assert.Contains("MediaAccessTokenService.CallRecordingScope", method);
        Assert.Contains("CreateToken(", method);
        Assert.Contains("/api/media-access/open?token=", method);
        Assert.Contains("MediaAccessTokenDto.Ok", method);
        Assert.Contains("MediaAccessTokenDto.Fail", method);
    }

    [Fact]
    public void MediaAccessController_ShouldReturnSharedMediaAccessTokenDto()
    {
        var source = ReadSource("SCRM.API", "Controllers", "MediaAccessController.cs");

        Assert.Contains("using SCRM.SHARED.Models.Dtos;", source);
        Assert.Contains("ActionResult<MediaAccessTokenDto>", source);
        Assert.Contains("private MediaAccessTokenDto CreateResponse", source);
        Assert.DoesNotContain("class MediaAccessTokenResponse", source);
    }

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var methodNameIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(methodNameIndex >= 0, $"未找到方法：{methodName}");

        var lineStart = source.LastIndexOf('\n', methodNameIndex);
        var start = lineStart < 0 ? 0 : lineStart + 1;
        var braceStart = source.IndexOf('{', methodNameIndex);
        Assert.True(braceStart >= 0, $"未找到方法体：{methodName}");

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

        throw new InvalidOperationException($"方法体未闭合：{methodName}");
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SCRM.SOLUTION.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
