using System.Reflection;
using SCRM.API.Controllers;

namespace SCRM.Tests;

/// <summary>
/// Android 文件上传 bizType 目录归档防回归测试。
/// <para>锁定 SmRun 上报 chat_pic / video / voice 后，SCRM 不再把视频素材误归到 files。</para>
/// </summary>
public sealed class FileUploadBizTypeMappingTests
{
    [Theory]
    [InlineData("avatar", "pic")]
    [InlineData("chat_pic", "pic")]
    [InlineData("pic", "pic")]
    [InlineData("image", "pic")]
    [InlineData("voice", "voice")]
    [InlineData("chat_voice", "voice")]
    [InlineData("audio", "voice")]
    [InlineData("video", "video")]
    [InlineData("chat_video", "video")]
    [InlineData("video_thumb", "video")]
    [InlineData("thumb_video", "video")]
    [InlineData("file", "files")]
    [InlineData("files", "files")]
    [InlineData("chat_file", "files")]
    [InlineData("unknown", "files")]
    public void ResolveWechatUploadSubFolder_ShouldMapKnownBizTypes(string bizType, string expected)
    {
        Assert.Equal(expected, ResolveWechatUploadSubFolder(bizType));
    }

    [Fact]
    public void ResolveWechatUploadSubFolder_ShouldTreatEmptyAsFiles()
    {
        Assert.Equal("files", ResolveWechatUploadSubFolder(null));
        Assert.Equal("files", ResolveWechatUploadSubFolder(""));
        Assert.Equal("files", ResolveWechatUploadSubFolder("   "));
    }

    [Fact]
    public void ResolveWechatUploadSubFolder_ShouldTrimAndIgnoreCase()
    {
        Assert.Equal("video", ResolveWechatUploadSubFolder(" VIDEO "));
        Assert.Equal("pic", ResolveWechatUploadSubFolder(" Chat_Pic "));
        Assert.Equal("voice", ResolveWechatUploadSubFolder(" Voice "));
    }

    [Theory]
    [InlineData("screenshot", true)]
    [InlineData(" Screenshot ", true)]
    [InlineData("video", false)]
    [InlineData("chat_video", false)]
    [InlineData("video_thumb", false)]
    [InlineData("log", false)]
    [InlineData(null, false)]
    public void IsScreenshotUploadBizType_ShouldOnlyPublishScreenshotEventForScreenshots(string? bizType, bool expected)
    {
        var method = typeof(FileUploadController).GetMethod(
            "IsScreenshotUploadBizType",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var result = method.Invoke(null, new object?[] { bizType });
        Assert.Equal(expected, Assert.IsType<bool>(result));
    }

    [Fact]
    public void FileUploadController_ShouldGateScreenshotEventByBizType()
    {
        var solutionRoot = FindSolutionRoot();
        var controllerPath = Path.Combine(solutionRoot, "SCRM.API", "Controllers", "FileUploadController.cs");
        var source = File.ReadAllText(controllerPath);

        Assert.Contains("&& IsScreenshotUploadBizType(bizType)", source);
        Assert.Contains("Android 视频缩略图也会携带 device，但不能误推成 OnScreenShotUploaded", source);
    }

    private static string ResolveWechatUploadSubFolder(string? bizType)
    {
        var method = typeof(FileUploadController).GetMethod(
            "ResolveWechatUploadSubFolder",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var result = method.Invoke(null, new object?[] { bizType });
        return Assert.IsType<string>(result);
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

        throw new DirectoryNotFoundException("未找到 SCRM.SOLUTION.sln。");
    }
}
