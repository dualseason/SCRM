namespace SCRM.Tests;

/// <summary>
/// IM 聊天媒体查看、播放、下载接入短期 token 的防回归测试。
/// <para>聊天图片、视频、表情、语音、文件等媒体入口不能再把数据库中的永久 mediaUrl 直接渲染为 href/src/Path。</para>
/// </summary>
public sealed class ChatMediaTokenPlaybackUiTests
{
    [Fact]
    public void ImCenterChatPane_ShouldUseShortLivedTokenForPrimaryMedia()
    {
        var source = ReadSource("SCRM.UI", "Components", "Pages", "ImCenterChatPane.razor");

        Assert.Contains("PrepareChatMediaAccessAsync(display.MediaUrl, msg.accountId, Store.SelectedDevice?.uuid)", source);
        Assert.Contains("PrepareChatMediaAccessAsync(media.mediaUrl, msg.accountId, Store.SelectedDevice?.uuid)", source);
        Assert.Contains("GetChatMediaAccessUrl(display.MediaUrl)", source);
        Assert.Contains("GetChatMediaAccessUrl(media.mediaUrl)", source);

        Assert.Contains("图片已通过短期链接保护", source);
        Assert.Contains("视频已通过短期链接保护", source);
        Assert.Contains("表情已通过短期链接保护", source);
        Assert.Contains("语音已通过短期链接保护", source);
        Assert.Contains("媒体链接已隐藏", source);
        Assert.Contains("打开短期链接", source);

        Assert.Contains("<audio src=\"@GetChatMediaAccessUrl(display.MediaUrl)\"", source);
        Assert.Contains("<video src=\"@GetChatMediaAccessUrl(display.MediaUrl)\"", source);
        Assert.Contains("<RadzenImage Path=\"@GetChatMediaAccessUrl(display.MediaUrl)\"", source);
        Assert.Contains("GetChatMediaAccessButtonText(display.MediaUrl, \"打开文件\")", source);
        Assert.Contains("GetChatMediaAccessButtonText(media.mediaUrl, \"生成短期链接\")", source);
        Assert.Contains("语音源：已通过短期链接保护", source);

        Assert.DoesNotContain("href=\"@display.MediaUrl\"", source);
        Assert.DoesNotContain("Path=\"@display.MediaUrl\"", source);
        Assert.DoesNotContain("src=\"@display.MediaUrl\"", source);
        Assert.DoesNotContain("href=\"@media.mediaUrl\"", source);
        Assert.DoesNotContain("title=\"@media.mediaUrl\"", source);
        Assert.DoesNotContain("语音源：@", source);
    }

    [Fact]
    public void MediaPartial_ShouldMaintainTokenCacheAndCallStore()
    {
        var source = ReadSource("SCRM.UI", "Components", "Pages", "ImCenterChatPane.Media.cs");

        Assert.Contains("_chatMediaAccessTokens", source);
        Assert.Contains("_preparingChatMediaAccessKeys", source);
        Assert.Contains("PrepareChatMediaAccessAsync", source);
        Assert.Contains("Store.CreateMediaAccessTokenAsync", source);
        Assert.Contains("HasChatMediaAccessUrl", source);
        Assert.Contains("GetChatMediaAccessUrl", source);
        Assert.Contains("FormatChatMediaTokenExpires", source);
        Assert.Contains("BuildChatMediaAccessKey", source);
        Assert.Contains("媒体短期链接已生成", source);
    }

    [Fact]
    public void CrmStoreAndInterface_ShouldExposeMediaTokenMethod()
    {
        var contract = ReadSource("SCRM.UI", "Interfaces", "ICrmService.cs");
        var store = ReadSource("SCRM.UI", "Services", "CrmStore.cs");

        Assert.Contains("Task<MediaAccessTokenDto> CreateMediaAccessTokenAsync(string mediaUrl, string accountId = \"\", string deviceUuid = \"\", int expiresMinutes = 5)", contract);
        Assert.Contains("public async Task<MediaAccessTokenDto> CreateMediaAccessTokenAsync(string mediaUrl, string accountId = \"\", string deviceUuid = \"\", int expiresMinutes = 5)", store);
        Assert.Contains("_service.CreateMediaAccessTokenAsync(mediaUrl, accountId, deviceUuid, expiresMinutes)", store);
    }

    [Fact]
    public void CrmService_ShouldIssueMediaTokenWithRawPermissionAndOwnershipChecks()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var method = ExtractMethod(source, "CreateMediaAccessTokenAsync");

        Assert.Contains("BuildProfileAsync(user)", method);
        Assert.Contains("!profile.CanViewMessageRaw", method);
        Assert.Contains("TryNormalizeUploadRelativePath(mediaUrl", method);
        Assert.Contains("CanAccessAccountAsync(user, normalizedAccountId)", method);
        Assert.Contains("CanAccessDeviceAsync(user, normalizedDeviceUuid)", method);
        Assert.Contains("MediaAccessTokenService.InferTarget(relativePath)", method);
        Assert.Contains("MediaAccessTargetKind.WechatAccount", method);
        Assert.Contains("MediaAccessTargetKind.Device", method);
        Assert.Contains("AccountAccessGuard.IsAdmin(user)", method);
        Assert.Contains("MediaAccessTokenService.MediaScope", method);
        Assert.Contains("/api/media-access/open?token=", method);
        Assert.Contains("MediaAccessTokenDto.Ok", method);
        Assert.Contains("MediaAccessTokenDto.Fail", method);
    }

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var searchFrom = 0;
        while (true)
        {
            var methodNameIndex = source.IndexOf(methodName, searchFrom, StringComparison.Ordinal);
            Assert.True(methodNameIndex >= 0, $"未找到方法：{methodName}");

            var start = Math.Max(
                source.LastIndexOf("public async Task", methodNameIndex, StringComparison.Ordinal),
                source.LastIndexOf("private async Task", methodNameIndex, StringComparison.Ordinal));
            start = Math.Max(start, source.LastIndexOf("public Task", methodNameIndex, StringComparison.Ordinal));
            Assert.True(start >= 0, $"未找到方法声明：{methodName}");

            var braceStart = source.IndexOf('{', start);
            Assert.True(braceStart >= 0, $"未找到方法体：{methodName}");

            var header = source[start..braceStart];
            if (!header.Contains(methodName, StringComparison.Ordinal))
            {
                searchFrom = methodNameIndex + methodName.Length;
                continue;
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

            throw new InvalidOperationException($"方法体未闭合：{methodName}");
        }
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
