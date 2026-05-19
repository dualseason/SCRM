using SCRM.API.Models.Entities;
using SCRM.API.Services.Security;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.Tests;

/// <summary>
/// 敏感字段脱敏服务防回归测试。
/// </summary>
public sealed class SensitiveMaskingServiceTests
{
    [Fact]
    public void MaskContact_ShouldMaskCustomerSensitiveFieldsButKeepRoutingWxid()
    {
        var contact = new Contact
        {
            id = 1,
            wxid = "wxid_customer_abcdef1234",
            ownerWxid = "wx-owner",
            friendNo = "friend_alias_123",
            nickname = "客户A",
            remarks = "张三 13812345678",
            signature = "联系 13812345678",
            phone = "13812345678",
            email = "foo@example.com",
            description = "wxid_abcd1234567890"
        };

        var masked = SensitiveMaskingService.MaskContact(contact, SensitiveAccessProfile.NoAccess);

        Assert.Equal("wxid_customer_abcdef1234", masked.wxid);
        Assert.Equal("fr****23", masked.friendNo);
        Assert.Equal("138****5678", masked.phone);
        Assert.Equal("f***@example.com", masked.email);
        Assert.Equal(string.Empty, masked.remarks);
        Assert.Equal("[已隐藏]", masked.signature);
        Assert.Equal("[已隐藏]", masked.description);
    }

    [Fact]
    public void MaskMessage_ShouldHideContentAndRawPayloadWhenNoPermission()
    {
        var message = new Message
        {
            messageId = 10,
            accountId = "wx-owner",
            senderWxid = "wxid_sender",
            receiverWxid = "wxid_receiver",
            content = "手机号 13812345678",
            contentXml = "<msg>13812345678</msg>",
            mediaAttachments = new List<MessageMediaAttachmentDto>
            {
                new() { id = 1, messageId = 10, mediaUrl = "https://file.local/a.jpg" }
            },
            messageExtensions = new List<MessageExtensionViewDto>
            {
                new() { id = 2, messageId = 10, extensionKey = "raw", extensionValue = "secret" }
            },
            voiceTransText = new VoiceToTextLogViewDto
            {
                id = 3,
                messageId = 10,
                voiceUrl = "https://file.local/a.amr",
                transcribedText = "电话 13812345678"
            }
        };

        var masked = SensitiveMaskingService.MaskMessage(message, SensitiveAccessProfile.NoAccess);

        Assert.Equal("[消息内容已隐藏]", masked.content);
        Assert.Equal(string.Empty, masked.contentXml);
        Assert.Empty(masked.mediaAttachments);
        Assert.Empty(masked.messageExtensions);
        Assert.Null(masked.voiceTransText);
    }

    [Fact]
    public void MaskMessage_ShouldMaskPhoneAndWxidFragmentsWhenContentAllowedButCustomerFieldsForbidden()
    {
        var profile = SensitiveAccessProfile.NoAccess with { CanViewMessageContent = true };
        var message = new Message
        {
            messageId = 11,
            accountId = "wx-owner",
            content = "客户电话 13812345678，微信 wxid_abcd1234567890"
        };

        var masked = SensitiveMaskingService.MaskMessage(message, profile);

        Assert.Equal("客户电话 138****5678，微信 wxid_****7890", masked.content);
    }

    [Fact]
    public void MaskMessage_ShouldHideMediaUrlsUnlessRawPermissionGranted()
    {
        var message = new Message
        {
            messageId = 12,
            accountId = "wx-owner",
            content = "图片消息",
            mediaAttachments = new List<MessageMediaAttachmentDto>
            {
                new()
                {
                    id = 1,
                    messageId = 12,
                    mediaType = 2,
                    mediaUrl = "https://file.local/uploads/wx/wx-owner/pic/a.jpg",
                    localPath = "/data/user/0/com.tencent.mm/a.jpg",
                    mediaHash = "raw-hash",
                    fileSize = 123,
                    fileExtension = ".jpg",
                    uploadStatus = 1
                }
            },
            voiceTransText = new VoiceToTextLogViewDto
            {
                id = 2,
                messageId = 12,
                voiceUrl = "https://file.local/uploads/wx/wx-owner/voice/a.amr",
                transcribedText = "语音转文字 13812345678"
            }
        };

        var contentOnlyProfile = SensitiveAccessProfile.NoAccess with { CanViewMessageContent = true };
        var masked = SensitiveMaskingService.MaskMessage(message, contentOnlyProfile);

        var media = Assert.Single(masked.mediaAttachments);
        Assert.Equal(2, media.mediaType);
        Assert.Equal(123, media.fileSize);
        Assert.Equal(".jpg", media.fileExtension);
        Assert.Equal(string.Empty, media.mediaUrl);
        Assert.Equal(string.Empty, media.localPath);
        Assert.Equal(string.Empty, media.mediaHash);
        Assert.NotNull(masked.voiceTransText);
        Assert.Equal(string.Empty, masked.voiceTransText!.voiceUrl);
        Assert.Equal("语音转文字 138****5678", masked.voiceTransText.transcribedText);

        var rawProfile = contentOnlyProfile with { CanViewMessageRaw = true };
        var raw = SensitiveMaskingService.MaskMessage(message, rawProfile);

        Assert.Equal("https://file.local/uploads/wx/wx-owner/pic/a.jpg", Assert.Single(raw.mediaAttachments).mediaUrl);
        Assert.Equal("/data/user/0/com.tencent.mm/a.jpg", Assert.Single(raw.mediaAttachments).localPath);
        Assert.Equal("raw-hash", Assert.Single(raw.mediaAttachments).mediaHash);
        Assert.Equal("https://file.local/uploads/wx/wx-owner/voice/a.amr", raw.voiceTransText?.voiceUrl);
    }

    [Fact]
    public void MaskSmsAndCallLog_ShouldHidePhoneContentAndRecordUrlWithoutPermission()
    {
        var sms = new SmsRecordDto
        {
            number = "13812345678",
            content = "验证码 123456，电话 13812345678"
        };
        var call = new CallLogRecordDto
        {
            number = "13812345678",
            recordUrl = "https://file.local/record.amr"
        };

        var maskedSms = SensitiveMaskingService.MaskSmsRecord(sms, SensitiveAccessProfile.NoAccess);
        var maskedCall = SensitiveMaskingService.MaskCallLogRecord(call, SensitiveAccessProfile.NoAccess);

        Assert.Equal("138****5678", maskedSms.number);
        Assert.Equal("[短信内容已隐藏]", maskedSms.content);
        Assert.Equal("138****5678", maskedCall.number);
        Assert.False(maskedCall.hasRecording);
        Assert.Equal(string.Empty, maskedCall.recordUrl);
    }

    [Fact]
    public void MaskConversation_ShouldHideLastMessageWithoutMessagePermission()
    {
        var conversation = new Conversation
        {
            id = 1,
            wechatAccountId = "wx-owner",
            conversationWxid = "wxid_friend",
            displayName = "客户A",
            lastMessageContent = "手机号 13812345678"
        };

        var masked = SensitiveMaskingService.MaskConversation(conversation, SensitiveAccessProfile.NoAccess);

        Assert.Equal("wxid_friend", masked.conversationWxid);
        Assert.Equal("[消息内容已隐藏]", masked.lastMessageContent);
    }

    [Fact]
    public void MaskPhoneAndEmail_ShouldBeStable()
    {
        Assert.Equal("138****5678", SensitiveMaskingService.MaskPhone("13812345678"));
        Assert.Equal("f***@example.com", SensitiveMaskingService.MaskEmail("foo@example.com"));
        Assert.Equal("wxid_****7890", SensitiveMaskingService.MaskWechatId("wxid_abcd1234567890"));
    }
}
