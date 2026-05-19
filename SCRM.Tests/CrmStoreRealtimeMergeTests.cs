using System.Reflection;
using SCRM.API.Models.Entities;
using SCRM.UI.Services;

namespace SCRM.Tests;

/// <summary>
/// 前端实时消息合并回归测试。
/// <para>实时推送可能只带部分展示扩展，本测试确保已有高级消息扩展和媒体附件不会被部分更新覆盖丢失。</para>
/// </summary>
public sealed class CrmStoreRealtimeMergeTests
{
    [Fact]
    public void MergeRealtimeMessage_ShouldKeepExistingAdvancedExtensionsWhenIncomingIsPartial()
    {
        var existing = CreateMessage(
            content: "旧消息",
            extensions:
            [
                CreateExtension("advanced_content_latest", """{"SemanticKind":"Quote","Title":"旧 latest"}"""),
                CreateExtension("advanced_content:quote", """{"SemanticKind":"Quote","Title":"旧 quote"}""")
            ]);
        var incoming = CreateMessage(
            content: "新消息",
            extensions:
            [
                CreateExtension("media_compensation_latest", """{"Url":"/uploads/a.jpg"}""")
            ]);

        var merged = InvokeMergeRealtimeMessage(existing, incoming);

        Assert.Same(incoming, merged);
        Assert.Equal("新消息", merged.content);
        Assert.Contains(merged.messageExtensions, item => item.extensionKey == "media_compensation_latest");
        Assert.Contains(merged.messageExtensions, item => item.extensionKey == "advanced_content_latest");
        Assert.Contains(merged.messageExtensions, item => item.extensionKey == "advanced_content:quote");
    }

    [Fact]
    public void MergeRealtimeMessage_ShouldPreferIncomingExtensionValueForSameKey()
    {
        var existing = CreateMessage(
            extensions:
            [
                CreateExtension("advanced_content_latest", """{"SemanticKind":"Quote","Title":"旧 latest"}""")
            ]);
        var incoming = CreateMessage(
            extensions:
            [
                CreateExtension("advanced_content_latest", """{"SemanticKind":"Quote","Title":"新 latest"}""")
            ]);

        var merged = InvokeMergeRealtimeMessage(existing, incoming);

        var latest = Assert.Single(merged.messageExtensions, item => item.extensionKey == "advanced_content_latest");
        Assert.Contains("新 latest", latest.extensionValue);
    }

    [Fact]
    public void MergeRealtimeMessage_ShouldKeepExistingMediaWhenIncomingAddsDifferentMedia()
    {
        var existing = CreateMessage(
            media:
            [
                CreateMedia(1, 2, "/uploads/old-image.jpg", "hash-old")
            ]);
        var incoming = CreateMessage(
            media:
            [
                CreateMedia(2, 8, "/uploads/new-file.pdf", "hash-new")
            ]);

        var merged = InvokeMergeRealtimeMessage(existing, incoming);

        Assert.Contains(merged.mediaAttachments, item => item.mediaUrl == "/uploads/new-file.pdf");
        Assert.Contains(merged.mediaAttachments, item => item.mediaUrl == "/uploads/old-image.jpg");
    }

    [Fact]
    public void MergeRealtimeMessage_ShouldKeepExistingVoiceTransTextWhenIncomingMissing()
    {
        var existing = CreateMessage();
        existing.voiceTransText = new VoiceToTextLogViewDto
        {
            transcribeStatus = 1,
            transcribedText = "旧语音识别文本"
        };
        var incoming = CreateMessage(content: "语音消息更新");

        var merged = InvokeMergeRealtimeMessage(existing, incoming);

        Assert.NotNull(merged.voiceTransText);
        Assert.Equal("旧语音识别文本", merged.voiceTransText.transcribedText);
    }

    private static Message InvokeMergeRealtimeMessage(Message existing, Message incoming)
    {
        var method = typeof(CrmStore).GetMethod(
            "MergeRealtimeMessage",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var value = method.Invoke(null, new object[] { existing, incoming });
        return Assert.IsType<Message>(value);
    }

    private static Message CreateMessage(
        string content = "",
        List<MessageExtensionViewDto>? extensions = null,
        List<MessageMediaAttachmentDto>? media = null)
    {
        return new Message
        {
            messageId = 1001,
            accountId = "wxid_owner",
            senderWxid = "wxid_friend",
            receiverWxid = "wxid_owner",
            content = content,
            createdAt = DateTime.UtcNow,
            updatedAt = DateTime.UtcNow,
            messageExtensions = extensions ?? new List<MessageExtensionViewDto>(),
            mediaAttachments = media ?? new List<MessageMediaAttachmentDto>()
        };
    }

    private static MessageExtensionViewDto CreateExtension(string key, string value)
    {
        return new MessageExtensionViewDto
        {
            extensionKey = key,
            extensionValue = value,
            createdAt = DateTime.UtcNow,
            updatedAt = DateTime.UtcNow
        };
    }

    private static MessageMediaAttachmentDto CreateMedia(int id, int mediaType, string mediaUrl, string mediaHash)
    {
        return new MessageMediaAttachmentDto
        {
            id = id,
            mediaType = mediaType,
            mediaUrl = mediaUrl,
            mediaHash = mediaHash,
            createdAt = DateTime.UtcNow,
            updatedAt = DateTime.UtcNow
        };
    }
}
