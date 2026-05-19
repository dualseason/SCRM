namespace SCRM.Tests;

/// <summary>
/// 设备截图预览接入短期 token 的防回归测试。
/// <para>截图上传返回的永久 URL 只能作为服务端签发依据，页面预览必须绑定短期代理 URL。</para>
/// </summary>
public sealed class ScreenshotTokenPreviewUiTests
{
    [Fact]
    public void DevicesAndControlDialog_ShouldUseShortLivedTokenForScreenshotPreview()
    {
        var devicesPage = ReadSource("SCRM.UI", "Components", "Pages", "Devices.razor");
        var controlDialog = ReadSource("SCRM.UI", "Components", "Dialogs", "DeviceControlDialog.razor");

        foreach (var source in new[] { devicesPage, controlDialog })
        {
            Assert.Contains("截图已通过短期链接保护", source);
            Assert.Contains("Store.LastScreenShotAccessUrl", source);
            Assert.Contains("RadzenImage Path=\"@Store.LastScreenShotAccessUrl\"", source);
            Assert.Contains("PrepareScreenShotPreview", source);
            Assert.Contains("Store.PrepareLastScreenShotAccessAsync(5)", source);
            Assert.Contains("GetScreenShotAccessButtonText", source);
            Assert.Contains("FormatScreenShotTokenExpires", source);
            Assert.Contains("Disabled=\"@Store.IsPreparingScreenShotAccess\"", source);

            Assert.DoesNotContain("RadzenImage Path=\"@Store.LastScreenShotUrl\"", source);
            Assert.DoesNotContain("href=\"@Store.LastScreenShotUrl\"", source);
        }
    }

    [Fact]
    public void CrmStoreAndInterface_ShouldExposeScreenshotTokenMethodAndState()
    {
        var contract = ReadSource("SCRM.UI", "Interfaces", "ICrmService.cs");
        var store = ReadSource("SCRM.UI", "Services", "CrmStore.cs");

        Assert.Contains("Task<MediaAccessTokenDto> CreateScreenshotAccessTokenAsync(string screenshotUrl, string deviceUuid = \"\", int expiresMinutes = 5)", contract);
        Assert.Contains("public string? LastScreenShotAccessUrl", store);
        Assert.Contains("public DateTimeOffset? LastScreenShotAccessExpiresAt", store);
        Assert.Contains("public bool IsPreparingScreenShotAccess", store);
        Assert.Contains("public async Task<MediaAccessTokenDto> CreateScreenshotAccessTokenAsync(string screenshotUrl, string deviceUuid = \"\", int expiresMinutes = 5)", store);
        Assert.Contains("_service.CreateScreenshotAccessTokenAsync(screenshotUrl, deviceUuid, expiresMinutes)", store);
        Assert.Contains("public async Task<MediaAccessTokenDto> PrepareLastScreenShotAccessAsync(int expiresMinutes = 5)", store);
        Assert.Contains("CreateScreenshotAccessTokenAsync(LastScreenShotUrl, LastScreenShotDeviceUuid, expiresMinutes)", store);
        Assert.Contains("ClearScreenShotAccessState()", store);
        Assert.Contains("LastScreenShotAccessUrl = null", store);
    }

    [Fact]
    public void CrmService_ShouldIssueScreenshotTokenWithDeviceOwnershipAndPathMatch()
    {
        var source = ReadSource("SCRM.API", "Services", "Core", "CrmService.cs");
        var method = ExtractMethod(source, "CreateScreenshotAccessTokenAsync");

        Assert.Contains("CanAccessDeviceAsync(user, normalizedDeviceUuid)", method);
        Assert.Contains("TryNormalizeUploadRelativePath(screenshotUrl", method);
        Assert.Contains("MediaAccessTokenService.InferTarget(relativePath)", method);
        Assert.Contains("inferredTarget.Kind != MediaAccessTargetKind.Device", method);
        Assert.Contains("string.Equals(inferredTarget.Value, normalizedDeviceUuid", method);
        Assert.Contains("MediaAccessTokenService.ScreenshotScope", method);
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
