namespace SCRM.Tests;

/// <summary>
/// 媒体 URL、录音 URL 与导出类入口的权限闭环防回归测试。
/// <para>无原始查看权限时，后端响应体不能携带可直接打开的图片、视频、语音、文件 URL 或本地路径。</para>
/// </summary>
public sealed class MediaUrlExportSecurityTests
{
    [Fact]
    public void SensitiveMaskingService_ShouldRequireRawPermissionForMessageMediaUrls()
    {
        var source = ReadSource("SCRM.API", "Services", "Security", "SensitiveMaskingService.cs");

        var maskMessage = ExtractMethod(source, "MaskMessage(");
        Assert.Contains("var canViewRaw = profile.CanViewMessageRaw;", maskMessage);
        Assert.Contains("CloneMediaAttachments(message.mediaAttachments, canViewRaw)", maskMessage);
        Assert.Contains("CloneVoiceTransText(message.voiceTransText, profile, canViewRaw)", maskMessage);

        var cloneMedia = ExtractMethod(source, "CloneMediaAttachments(");
        Assert.Contains("bool includeRawUrl", cloneMedia);
        Assert.Contains("mediaUrl = includeRawUrl ? media.mediaUrl : string.Empty", cloneMedia);
        Assert.Contains("localPath = includeRawUrl ? media.localPath : string.Empty", cloneMedia);
        Assert.Contains("mediaHash = includeRawUrl ? media.mediaHash : string.Empty", cloneMedia);

        var cloneVoice = ExtractMethod(source, "CloneVoiceTransText(");
        Assert.Contains("voiceUrl = includeRawUrl ? value.voiceUrl : string.Empty", cloneVoice);
    }

    [Fact]
    public void CrmService_ShouldGuardScreenshotAndCdnDownload()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");

        var screenshot = ExtractMethod(source, "RequestScreenShotAsync");
        Assert.True(
            screenshot.Contains("var user = await GetCurrentUserAsync();", StringComparison.Ordinal)
                && screenshot.Contains("CanAccessDeviceAsync(user, deviceUuid)", StringComparison.Ordinal)
                && screenshot.Contains("TaskResult.Fail(\"无权访问该设备\")", StringComparison.Ordinal)
            || screenshot.Contains("DenyIfHighRiskDeviceOperationAsync", StringComparison.Ordinal)
                && screenshot.Contains("Permissions.DeviceTask.Screenshot", StringComparison.Ordinal),
            "截图请求必须接入设备归属校验或高危设备操作授权。");

        var download = ExtractMethod(source, "DownloadCdnFileAsync");
        Assert.Contains("var user = await GetCurrentUserAsync();", download);
        Assert.Contains("CanAccessDeviceAsync(user, deviceUuid)", download);
        Assert.Contains("BuildProfileAsync(user)", download);
        Assert.Contains("!profile.CanViewMessageRaw", download);
        Assert.Contains("TaskResult.Fail(\"无权下载原始媒体文件\")", download);
        Assert.Contains("CanAccessAccountAsync(user, weChatId)", download);
    }

    [Fact]
    public void ClientHub_ShouldGuardScreenshotAndCdnDownload()
    {
        var source = ReadSource("SCRM.API", "Hubs", "ClientHub.cs");

        var screenshot = ExtractMethod(source, "RequestScreenShot");
        Assert.True(
            screenshot.Contains("CanAccessDeviceOrLogAsync(deviceUuid, nameof(RequestScreenShot))", StringComparison.Ordinal)
                && screenshot.Contains("TaskResult.Fail(\"无权访问该设备\")", StringComparison.Ordinal)
            || screenshot.Contains("DenyIfHighRiskDeviceOperationAsync", StringComparison.Ordinal)
                && screenshot.Contains("Permissions.DeviceTask.Screenshot", StringComparison.Ordinal),
            "截图请求必须接入设备归属校验或高危设备操作授权。");

        var download = ExtractMethod(source, "DownloadCdnFile");
        Assert.Contains("CanAccessDeviceOrLogAsync(deviceUuid, nameof(DownloadCdnFile))", download);
        Assert.Contains("BuildProfileAsync(Context.User)", download);
        Assert.Contains("!profile.CanViewMessageRaw", download);
        Assert.Contains("TaskResult.Fail(\"无权下载原始媒体文件\")", download);
        Assert.Contains("CanAccessAccountOrLogAsync(weChatId, nameof(DownloadCdnFile))", download);
    }

    [Fact]
    public void SensitiveMaskingTests_ShouldCoverMediaUrlMasking()
    {
        var source = ReadSource("SCRM.Tests", "SensitiveMaskingServiceTests.cs");

        Assert.Contains("MaskMessage_ShouldHideMediaUrlsUnlessRawPermissionGranted", source);
        Assert.Contains("mediaUrl = \"https://file.local/uploads/wx/wx-owner/pic/a.jpg\"", source);
        Assert.Contains("Assert.Equal(string.Empty, media.mediaUrl)", source);
        Assert.Contains("CanViewMessageRaw = true", source);
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
                source.LastIndexOf("public static", methodNameIndex, StringComparison.Ordinal));
            start = Math.Max(start, source.LastIndexOf("private static", methodNameIndex, StringComparison.Ordinal));
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
