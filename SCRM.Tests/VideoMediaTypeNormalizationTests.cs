using System.Reflection;
using Jubo.JuLiao.IM.Wx.Proto;
using SCRM.API.Hubs;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Core;
using SCRM.API.Services.Netty.Handlers;
using SCRM.Controllers;
using SCRM.UI.Components.Pages;

namespace SCRM.Tests;

/// <summary>
/// 视频媒体类型归一化防回归测试。
/// <para>锁定用户日志中 .mkv 视频 URL 被 Android 下载/发送链路依赖的服务端识别边界。</para>
/// </summary>
public sealed class VideoMediaTypeNormalizationTests
{
    [Theory]
    [InlineData(typeof(ServerDeviceCommandService))]
    [InlineData(typeof(ClientTaskController))]
    [InlineData(typeof(ClientHub))]
    public void TalkToFriendNormalize_ShouldDetectMkvAndWebmVideoUrls(Type ownerType)
    {
        Assert.Equal(
            (int)EnumContentType.Video,
            InvokePrivateStatic<int>(
                ownerType,
                "NormalizeTalkToFriendContentType",
                (int)EnumContentType.Text,
                "http://192.168.2.226:42718/uploads/web/chat/wxid/video/sample.mkv?token=1"));

        Assert.Equal(
            (int)EnumContentType.Video,
            InvokePrivateStatic<int>(
                ownerType,
                "NormalizeTalkToFriendContentType",
                (int)EnumContentType.UnknownContent,
                "https://example.invalid/uploads/web/chat/wxid/video/sample.WEBM#fragment"));
    }

    [Fact]
    public void GroupSendNormalize_ShouldDetectMkvAndWebmWhenOldCallerUsesTextType()
    {
        Assert.Equal(
            EnumContentType.Video,
            InvokePrivateStatic<EnumContentType>(
                typeof(ClientTaskService),
                "NormalizeGroupSendTalkContentType",
                WeChatGroupSendTaskMessage.Types.EnumGroupMsgContentType.Text,
                "http://192.168.2.226:42718/uploads/web/chat/wxid/video/group-video.mkv"));

        Assert.Equal(
            EnumContentType.Video,
            InvokePrivateStatic<EnumContentType>(
                typeof(ClientTaskService),
                "NormalizeGroupSendTalkContentType",
                WeChatGroupSendTaskMessage.Types.EnumGroupMsgContentType.Text,
                "http://192.168.2.226:42718/uploads/web/chat/wxid/video/group-video.webm?download=1"));
    }

    [Fact]
    public void RealtimeNormalize_ShouldPromoteTextMkvAndWebmToVideo()
    {
        Assert.Equal(
            EnumContentType.Video,
            InvokePrivateStatic<EnumContentType>(
                typeof(ChatMessageHandler),
                "NormalizeRealtimeContentType",
                EnumContentType.Text,
                "http://192.168.2.226:42718/uploads/web/chat/wxid/video/realtime.mkv"));

        Assert.Equal(
            EnumContentType.Video,
            InvokePrivateStatic<EnumContentType>(
                typeof(ChatMessageHandler),
                "NormalizeRealtimeContentType",
                EnumContentType.Text,
                "https://example.invalid/uploads/web/chat/wxid/video/realtime.webm#v"));
    }

    [Fact]
    public void ImCenterMedia_ShouldRenderMkvAndWebmAsVideo()
    {
        var mkvKind = InvokePrivateStatic<object>(
            typeof(ImCenterChatPane),
            "InferKindFromContent",
            "http://192.168.2.226:42718/uploads/web/chat/wxid/video/ui.mkv");
        var webmKind = InvokePrivateStatic<object>(
            typeof(ImCenterChatPane),
            "InferKindFromContent",
            "https://example.invalid/uploads/web/chat/wxid/video/ui.webm?token=1");

        Assert.Equal("Video", mkvKind.ToString());
        Assert.Equal("Video", webmKind.ToString());
    }

    [Fact]
    public void ImCenterMedia_ShouldPickSameKindPrimaryMediaBeforeFirstAttachment()
    {
        var attachments = new List<MessageMediaAttachmentDto>
        {
            new()
            {
                id = 1,
                messageId = 1001,
                mediaType = (int)EnumContentType.Picture,
                mediaUrl = "/uploads/chat/pic-first.jpg",
                createdAt = DateTime.UtcNow.AddMinutes(1)
            },
            new()
            {
                id = 2,
                messageId = 1001,
                mediaType = (int)EnumContentType.Video,
                mediaUrl = "/uploads/chat/video-second.mkv",
                createdAt = DateTime.UtcNow.AddMinutes(2)
            }
        };

        var selected = InvokeSelectPrimaryMediaAttachment(attachments, "Video");

        Assert.NotNull(selected);
        Assert.Equal("/uploads/chat/video-second.mkv", selected.mediaUrl);
    }

    [Fact]
    public void ImCenterMedia_ShouldNotLetAdvancedCardUsePlainMediaAttachment()
    {
        var attachments = new List<MessageMediaAttachmentDto>
        {
            new()
            {
                id = 1,
                messageId = 1002,
                mediaType = (int)EnumContentType.Picture,
                mediaUrl = "/uploads/chat/pic-first.jpg",
                createdAt = DateTime.UtcNow
            }
        };

        var selected = InvokeSelectPrimaryMediaAttachment(attachments, "Link");

        Assert.Null(selected);
    }

    [Theory]
    [InlineData("video/x-matroska", ".mkv")]
    [InlineData("video/webm", ".webm")]
    [InlineData("video/x-m4v", ".m4v")]
    [InlineData("video/3gpp", ".3gp")]
    [InlineData("video/x-msvideo", ".avi")]
    public void ImCenterUpload_ShouldInferVideoExtensionsFromMimeType(string contentType, string expectedExtension)
    {
        Assert.Equal(
            expectedExtension,
            InvokePrivateStatic<string>(
                typeof(ImCenterChatPane),
                "InferExtension",
                contentType,
                "chat_video"));
    }

    [Fact]
    public void Program_ShouldServeMkvAndWebmUploadsWithVideoMimeTypes()
    {
        var solutionRoot = FindSolutionRoot();
        var programPath = Path.Combine(solutionRoot, "SCRM.API", "Program.cs");
        var programText = File.ReadAllText(programPath);

        Assert.Contains("Mappings[\".mkv\"] = \"video/x-matroska\"", programText);
        Assert.Contains("Mappings[\".webm\"] = \"video/webm\"", programText);
        Assert.Contains("Mappings[\".avi\"] = \"video/x-msvideo\"", programText);
        Assert.Contains("UseStaticFiles(new StaticFileOptions", programText);
        Assert.Contains("RequestPath = uploadRequestPath", programText);
        Assert.Contains("ServeUnknownFileTypes = true", programText);
        Assert.Contains("DefaultContentType = \"application/octet-stream\"", programText);
        Assert.Contains("PhysicalFileProvider(uploadPhysicalRoot)", programText);
    }

    [Fact]
    public void ImCenterUpload_ShouldRequireStaticUrlProbeBeforeVideoAndFileSend()
    {
        var solutionRoot = FindSolutionRoot();
        var uploadPath = Path.Combine(solutionRoot, "SCRM.UI", "Components", "Pages", "ImCenterChatPane.Upload.cs");
        var uploadText = File.ReadAllText(uploadPath);

        Assert.Contains("SaveChatUploadAsync(file, \"chat_video\", \"video\", MaxChatVideoSize, requireServed: true)", uploadText);
        Assert.Contains("SaveChatUploadAsync(file, \"chat_file\", \"files\", MaxChatFileSize, requireServed: true)", uploadText);
        Assert.Contains("静态文件 URL 自检未通过，已阻止下发到安卓端", uploadText);
        Assert.Contains("无法确认静态文件 URL 可访问，已阻止下发到安卓端", uploadText);
        Assert.Contains("HttpMethod.Get, readBody: true", uploadText);
    }

    private static T InvokePrivateStatic<T>(Type ownerType, string methodName, params object?[] args)
    {
        var method = ownerType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var result = method.Invoke(null, args);
        Assert.NotNull(result);
        if (typeof(T) == typeof(object))
        {
            return (T)result;
        }

        return Assert.IsType<T>(result);
    }

    private static MessageMediaAttachmentDto? InvokeSelectPrimaryMediaAttachment(
        IReadOnlyList<MessageMediaAttachmentDto> attachments,
        string kindName)
    {
        var kindType = typeof(ImCenterChatPane).GetNestedType("ChatMessageKind", BindingFlags.NonPublic);
        Assert.NotNull(kindType);
        var kind = Enum.Parse(kindType, kindName);

        var method = typeof(ImCenterChatPane).GetMethod(
            "SelectPrimaryMediaAttachment",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return method.Invoke(null, new object[] { attachments, kind }) as MessageMediaAttachmentDto;
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

        throw new DirectoryNotFoundException("未找到 SCRM.SOLUTION.sln，无法定位 Program.cs。 ");
    }
}
