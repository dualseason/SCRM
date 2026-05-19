using System.Reflection;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Data;
using SCRM.API.Services.Netty.Handlers;

namespace SCRM.Tests;

/// <summary>
/// P0 聊天补偿与媒体回填持久化边界测试。
/// <para>覆盖 RequestTalkContentTaskResultNotice、RequestTalkDetailTaskResultNotice、ChatMsgFilePushNotice 对应的 DbHelper/Handler 关键边界。</para>
/// </summary>
public sealed class P0ChatRecoveryPersistenceTests
{
    public static IEnumerable<object[]> CdnFileTypeFallbackCases =>
        new[]
        {
            new object[] { "chat-picture", CDNFileType.ChatMsgPicture, EnumContentType.Picture, "jpg" },
            new object[] { "note-thumb", CDNFileType.NoteMsgThumb, EnumContentType.Picture, "jpg" },
            new object[] { "chat-video", CDNFileType.ChatMsgVideo, EnumContentType.Video, "mp4" },
            new object[] { "chat-file", CDNFileType.ChatMsgFile, EnumContentType.File, "pdf" },
            new object[] { "chat-emoji", CDNFileType.ChatMsgEmoji, EnumContentType.Emoji, "gif" }
        };

    [Fact]
    public async Task RequestTalkContentResult_ShouldUpdateExistingMessageAndPreferXml()
    {
        await using var db = CreateDbContext();
        var message = CreateMessage(messageId: 301, msgSvrId: 9301, messageType: EnumContentType.Text, content: "旧正文");
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        const string xml = "<msg><appmsg><title>原始 XML</title></appmsg></msg>";
        var updated = await db.UpdateMessageContentByMsgSvrId(
            OwnerWxid,
            9301,
            (short)EnumContentType.System,
            xml,
            preferXml: true);

        Assert.NotNull(updated);
        Assert.Equal((short)EnumContentType.System, updated.messageType);
        Assert.Equal(xml, updated.content);
        Assert.Equal(xml, updated.contentXml);

        var saved = await db.Messages.AsNoTracking().SingleAsync(item => item.messageId == 301);
        Assert.Equal((short)EnumContentType.System, saved.messageType);
        Assert.Equal(xml, saved.content);
        Assert.Equal(xml, saved.contentXml);
    }

    [Fact]
    public async Task RequestTalkContentResult_ShouldNotCreateMessageWhenMsgSvrIdMissing()
    {
        await using var db = CreateDbContext();
        db.Messages.Add(CreateMessage(messageId: 302, msgSvrId: 9302, messageType: EnumContentType.Text, content: "原消息"));
        await db.SaveChangesAsync();

        var updated = await db.UpdateMessageContentByMsgSvrId(
            OwnerWxid,
            999999,
            (short)EnumContentType.System,
            "未命中正文",
            preferXml: false);

        Assert.Null(updated);
        Assert.Equal(1, await db.Messages.CountAsync());
        var original = await db.Messages.AsNoTracking().SingleAsync();
        Assert.Equal("原消息", original.content);
        Assert.Equal((short)EnumContentType.Text, original.messageType);
    }

    [Fact]
    public async Task RequestTalkContentResult_ShouldUpdatePlainContentWithoutTouchingExistingXml()
    {
        await using var db = CreateDbContext();
        const string oldXml = "<msg><appmsg><title>旧 XML</title></appmsg></msg>";
        var message = CreateMessage(
            messageId: 312,
            msgSvrId: 9312,
            messageType: EnumContentType.Link,
            content: oldXml);
        message.contentXml = oldXml;
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        var updated = await db.UpdateMessageContentByMsgSvrId(
            OwnerWxid,
            9312,
            (short)EnumContentType.Text,
            "补偿后的普通正文",
            preferXml: false);

        Assert.NotNull(updated);
        Assert.Equal((short)EnumContentType.Text, updated.messageType);
        Assert.Equal("补偿后的普通正文", updated.content);
        Assert.Equal(oldXml, updated.contentXml);

        var saved = await db.Messages.AsNoTracking().SingleAsync(item => item.messageId == 312);
        Assert.Equal("补偿后的普通正文", saved.content);
        Assert.Equal(oldXml, saved.contentXml);
    }

    [Fact]
    public async Task ChatMsgFilePushNotice_ShouldUpdateExistingMediaAndCompensationExtensions()
    {
        await using var db = CreateDbContext();
        const string oldXml = "<msg><img aeskey=\"old\" /></msg>";
        db.Messages.Add(CreateMessage(messageId: 303, msgSvrId: 9303, messageType: EnumContentType.Picture, content: oldXml));
        await db.SaveChangesAsync();

        var updated = await db.UpdateMessageMediaUrlByMsgSvrId(
            OwnerWxid,
            9303,
            "/uploads/chat/pic-9303.jpg",
            (short)EnumContentType.Picture,
            fileSize: 12345,
            subType: 7,
            sourceNotice: "ChatMsgFilePushNotice");

        Assert.NotNull(updated);
        Assert.Equal((short)EnumContentType.Picture, updated.messageType);
        Assert.Equal("/uploads/chat/pic-9303.jpg", updated.content);
        Assert.Equal(oldXml, updated.contentXml);

        var media = await db.MessageMedias.AsNoTracking().SingleAsync(item => item.messageId == 303);
        Assert.Equal((int)EnumContentType.Picture, media.mediaType);
        Assert.Equal("/uploads/chat/pic-9303.jpg", media.mediaUrl);
        Assert.Equal(12345, media.fileSize);
        Assert.Equal("jpg", media.fileExtension);
        Assert.Equal(1, media.uploadStatus);

        var extensionKeys = await db.MessageExtensions
            .AsNoTracking()
            .Where(item => item.messageId == 303)
            .Select(item => item.extensionKey)
            .ToListAsync();

        Assert.Contains("media_compensation_latest", extensionKeys);
        Assert.Contains("media_compensation_history", extensionKeys);
    }

    [Fact]
    public async Task ChatMsgFilePushNotice_ShouldNotCreateMessageWhenMsgSvrIdMissing()
    {
        await using var db = CreateDbContext();
        db.Messages.Add(CreateMessage(messageId: 304, msgSvrId: 9304, messageType: EnumContentType.Text, content: "原消息"));
        await db.SaveChangesAsync();

        var updated = await db.UpdateMessageMediaUrlByMsgSvrId(
            OwnerWxid,
            999999,
            "/uploads/chat/missing.jpg",
            (short)EnumContentType.Picture,
            fileSize: 1,
            subType: 0,
            sourceNotice: "ChatMsgFilePushNotice");

        Assert.Null(updated);
        Assert.Equal(1, await db.Messages.CountAsync());
        Assert.Empty(await db.MessageMedias.ToListAsync());
        Assert.Empty(await db.MessageExtensions.ToListAsync());
    }

    [Fact]
    public async Task CdnDownloadTask_ShouldSavePendingContextAndResultShouldReuseMetadata()
    {
        await using var db = CreateDbContext();
        const string oldXml = "<msg><videomsg cdnurl=\"old\" /></msg>";
        db.Messages.Add(CreateMessage(messageId: 310, msgSvrId: 9310, messageType: EnumContentType.Video, content: oldXml));
        await db.SaveChangesAsync();

        var saved = await db.SaveCdnDownloadPendingContextByMsgSvrId(
            OwnerWxid,
            msgSvrId: 9310,
            cdnUrl: "https://weixin-cdn.example/video.dat",
            cdnFileType: (int)CDNFileType.ChatMsgVideo,
            fileId: "cdn-file-9310",
            fileFmt: "mp4",
            fileSize: 998877,
            taskId: 639146331000000001);

        Assert.True(saved);

        var pending = await db.MessageExtensions
            .AsNoTracking()
            .SingleAsync(item => item.messageId == 310
                && item.extensionKey.StartsWith("cdn_download_pending", StringComparison.Ordinal));

        Assert.Contains("CDNDownloadFileTask", pending.extensionValue);
        Assert.Contains("\"FileId\":\"cdn-file-9310\"", pending.extensionValue);
        Assert.Contains("\"FileFmt\":\"mp4\"", pending.extensionValue);
        Assert.Contains("\"FileSize\":998877", pending.extensionValue);

        var updated = await db.UpdateMessageMediaUrlByMsgSvrId(
            OwnerWxid,
            9310,
            "/uploads/chat/video-9310",
            sourceNotice: "CDNDownloadResultNotice",
            fileId: "cdn-file-9310");

        Assert.NotNull(updated);
        Assert.Equal((short)EnumContentType.Video, updated.messageType);
        Assert.Equal("/uploads/chat/video-9310", updated.content);
        Assert.Equal(oldXml, updated.contentXml);

        var media = await db.MessageMedias.AsNoTracking().SingleAsync(item => item.messageId == 310);
        Assert.Equal((int)EnumContentType.Video, media.mediaType);
        Assert.Equal("/uploads/chat/video-9310", media.mediaUrl);
        Assert.Equal("cdn-file-9310", media.mediaHash);
        Assert.Equal(998877, media.fileSize);
        Assert.Equal("mp4", media.fileExtension);
        Assert.Equal(1, media.uploadStatus);

        var latest = await db.MessageExtensions
            .AsNoTracking()
            .SingleAsync(item => item.messageId == 310 && item.extensionKey == "media_compensation_latest");

        Assert.Contains("CDNDownloadResultNotice", latest.extensionValue);
        Assert.Contains("\"FileId\":\"cdn-file-9310\"", latest.extensionValue);
        Assert.Contains("\"CdnFileType\":6", latest.extensionValue);
        Assert.Contains("\"FileFmt\":\"mp4\"", latest.extensionValue);
        Assert.Contains("\"TaskId\":639146331000000001", latest.extensionValue);
    }

    [Theory]
    [MemberData(nameof(CdnFileTypeFallbackCases))]
    public async Task CdnDownloadResult_ShouldMapPendingCdnFileTypeWhenOriginalMessageTypeIsWeak(
        string caseName,
        CDNFileType cdnFileType,
        EnumContentType expectedContentType,
        string fileFmt)
    {
        await using var db = CreateDbContext();
        const long msgSvrId = 9320;
        const string oldXml = "<msg><appmsg><title>旧解析器未识别媒体类型</title></appmsg></msg>";
        var fileId = $"cdn-file-{caseName}";
        var mediaUrl = $"/uploads/chat/{caseName}-9320";
        db.Messages.Add(CreateMessage(messageId: 320, msgSvrId: msgSvrId, messageType: EnumContentType.Text, content: oldXml));
        await db.SaveChangesAsync();

        var saved = await db.SaveCdnDownloadPendingContextByMsgSvrId(
            OwnerWxid,
            msgSvrId,
            cdnUrl: $"https://weixin-cdn.example/{caseName}.dat",
            cdnFileType: (int)cdnFileType,
            fileId: fileId,
            fileFmt: fileFmt,
            fileSize: 123456,
            taskId: 639146331000000010);

        Assert.True(saved);

        var updated = await db.UpdateMessageMediaUrlByMsgSvrId(
            OwnerWxid,
            msgSvrId,
            mediaUrl,
            sourceNotice: "CDNDownloadResultNotice",
            fileId: fileId);

        Assert.NotNull(updated);
        Assert.Equal((short)expectedContentType, updated.messageType);
        Assert.Equal(mediaUrl, updated.content);
        Assert.Equal(oldXml, updated.contentXml);

        var media = await db.MessageMedias.AsNoTracking().SingleAsync(item => item.messageId == 320);
        Assert.Equal((int)expectedContentType, media.mediaType);
        Assert.NotEqual((int)cdnFileType, media.mediaType);
        Assert.Equal(mediaUrl, media.mediaUrl);
        Assert.Equal(fileId, media.mediaHash);
        Assert.Equal(123456, media.fileSize);
        Assert.Equal(fileFmt, media.fileExtension);

        var latest = await db.MessageExtensions
            .AsNoTracking()
            .SingleAsync(item => item.messageId == 320 && item.extensionKey == "media_compensation_latest");

        Assert.Contains("\"CDNDownloadResultNotice\"", latest.extensionValue);
        Assert.Contains($"\"MessageType\":{(int)expectedContentType}", latest.extensionValue);
        Assert.Contains($"\"CdnFileType\":{(int)cdnFileType}", latest.extensionValue);
        Assert.Contains($"\"FileFmt\":\"{fileFmt}\"", latest.extensionValue);
    }

    [Fact]
    public async Task CdnDownloadResult_ShouldKeepSpecificMessageTypeBeforePendingCdnFileType()
    {
        await using var db = CreateDbContext();
        const string oldXml = "<msg><img aeskey=\"old\" /></msg>";
        db.Messages.Add(CreateMessage(messageId: 321, msgSvrId: 9321, messageType: EnumContentType.Picture, content: oldXml));
        await db.SaveChangesAsync();

        var saved = await db.SaveCdnDownloadPendingContextByMsgSvrId(
            OwnerWxid,
            msgSvrId: 9321,
            cdnUrl: "https://weixin-cdn.example/conflict.dat",
            cdnFileType: (int)CDNFileType.ChatMsgVideo,
            fileId: "cdn-file-9321",
            fileFmt: "mp4",
            fileSize: 456789,
            taskId: 639146331000000011);

        Assert.True(saved);

        var updated = await db.UpdateMessageMediaUrlByMsgSvrId(
            OwnerWxid,
            9321,
            "/uploads/chat/conflict-9321",
            sourceNotice: "CDNDownloadResultNotice",
            fileId: "cdn-file-9321");

        Assert.NotNull(updated);
        Assert.Equal((short)EnumContentType.Picture, updated.messageType);

        var media = await db.MessageMedias.AsNoTracking().SingleAsync(item => item.messageId == 321);
        Assert.Equal((int)EnumContentType.Picture, media.mediaType);
        Assert.Equal("mp4", media.fileExtension);
    }

    [Fact]
    public async Task CdnDownloadTask_ShouldNotCreateMessageWhenMsgSvrIdMissing()
    {
        await using var db = CreateDbContext();
        db.Messages.Add(CreateMessage(messageId: 311, msgSvrId: 9311, messageType: EnumContentType.Text, content: "原消息"));
        await db.SaveChangesAsync();

        var saved = await db.SaveCdnDownloadPendingContextByMsgSvrId(
            OwnerWxid,
            msgSvrId: 999999,
            cdnUrl: "https://weixin-cdn.example/missing.dat",
            cdnFileType: (int)CDNFileType.ChatMsgFile,
            fileId: "missing-file",
            fileFmt: "dat",
            fileSize: 10,
            taskId: 639146331000000002);

        var updated = await db.UpdateMessageMediaUrlByMsgSvrId(
            OwnerWxid,
            999999,
            "/uploads/chat/missing.dat",
            sourceNotice: "CDNDownloadResultNotice",
            fileId: "missing-file");

        Assert.False(saved);
        Assert.Null(updated);
        Assert.Equal(1, await db.Messages.CountAsync());
        Assert.Empty(await db.MessageMedias.ToListAsync());
        Assert.Empty(await db.MessageExtensions.ToListAsync());
    }

    [Fact]
    public async Task RequestTalkDetailTask_ShouldSaveAndFindPendingContextByMessageExtensions()
    {
        await using var db = CreateDbContext();
        db.Messages.Add(CreateMessage(
            messageId: 305,
            msgSvrId: 9305,
            messageType: EnumContentType.Picture,
            content: "旧内容",
            localMessageId: "5305",
            friendId: FriendWxid));
        await db.SaveChangesAsync();

        var saved = await db.SaveRequestTalkDetailPendingContext(
            OwnerWxid,
            FriendWxid,
            msgId: 5305,
            msgSvrId: 9305,
            md5: "md5-9305",
            getOriginal: true,
            taskId: 123456789);

        Assert.True(saved);

        var pending = await db.FindRequestTalkDetailPendingContext(OwnerWxid, 5305, FriendWxid);
        Assert.NotNull(pending);
        Assert.Equal(OwnerWxid, pending.WeChatId);
        Assert.Equal(FriendWxid, pending.FriendId);
        Assert.Equal(5305, pending.MsgId);
        Assert.Equal(9305, pending.MsgSvrId);
        Assert.Equal("md5-9305", pending.Md5);
        Assert.True(pending.GetOriginal);
        Assert.Equal(123456789, pending.TaskId);
        Assert.Equal(305, pending.MessageTableId);

        var extensionKeys = await db.MessageExtensions
            .AsNoTracking()
            .Where(item => item.messageId == 305)
            .Select(item => item.extensionKey)
            .ToListAsync();
        Assert.Contains(extensionKeys, key => key.StartsWith("request_talk_detail_pending", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RequestTalkDetailTask_ShouldResolveTargetByLocalMessageIdAndFriendId()
    {
        await using var db = CreateDbContext();
        db.Messages.Add(CreateMessage(
            messageId: 306,
            msgSvrId: 9306,
            messageType: EnumContentType.Picture,
            content: "详情目标",
            localMessageId: "5306",
            friendId: FriendWxid));
        await db.SaveChangesAsync();

        var found = await db.FindMessageForRequestTalkDetailTask(OwnerWxid, 5306, msgSvrId: string.Empty, friendId: FriendWxid);

        Assert.NotNull(found);
        Assert.Equal(306, found.messageId);
        Assert.Equal(9306, found.msgSvrId);
    }

    [Fact]
    public async Task RequestTalkDetailTask_ShouldResolveTargetByMsgSvrIdWhenMsgIdMissing()
    {
        await using var db = CreateDbContext();
        db.Messages.Add(CreateMessage(
            messageId: 313,
            msgSvrId: 9313,
            messageType: EnumContentType.Picture,
            content: "详情目标",
            localMessageId: "5313",
            friendId: FriendWxid));
        await db.SaveChangesAsync();

        var found = await db.FindMessageForRequestTalkDetailTask(OwnerWxid, msgId: 0, msgSvrId: "9313", friendId: FriendWxid);

        Assert.NotNull(found);
        Assert.Equal(313, found.messageId);
        Assert.Equal("5313", found.localMessageId);
    }

    [Fact]
    public async Task RequestTalkDetailTask_ShouldFindPendingContextWhenReplyFriendIdEmpty()
    {
        await using var db = CreateDbContext();
        db.Messages.Add(CreateMessage(
            messageId: 314,
            msgSvrId: 9314,
            messageType: EnumContentType.Picture,
            content: "旧内容",
            localMessageId: "5314",
            friendId: FriendWxid));
        await db.SaveChangesAsync();

        var saved = await db.SaveRequestTalkDetailPendingContext(
            OwnerWxid,
            FriendWxid,
            msgId: 5314,
            msgSvrId: 9314,
            md5: "md5-9314",
            getOriginal: true,
            taskId: 93140001);

        Assert.True(saved);

        var pending = await db.FindRequestTalkDetailPendingContext(OwnerWxid, 5314, friendId: string.Empty);

        Assert.NotNull(pending);
        Assert.Equal(FriendWxid, pending.FriendId);
        Assert.Equal(9314, pending.MsgSvrId);
        Assert.Equal(93140001, pending.TaskId);
    }

    [Fact]
    public async Task RequestTalkDetailTask_ShouldFindRecentPendingContextByFriendWhenMsgIdMissing()
    {
        await using var db = CreateDbContext();
        db.Messages.Add(CreateMessage(
            messageId: 315,
            msgSvrId: 9315,
            messageType: EnumContentType.Video,
            content: "旧内容",
            localMessageId: "5315",
            friendId: FriendWxid));
        await db.SaveChangesAsync();

        var saved = await db.SaveRequestTalkDetailPendingContext(
            OwnerWxid,
            FriendWxid,
            msgId: 5315,
            msgSvrId: 9315,
            md5: "md5-9315",
            getOriginal: false,
            taskId: 93150001);

        Assert.True(saved);

        var pending = await db.FindRequestTalkDetailPendingContext(OwnerWxid, msgId: 0, friendId: FriendWxid);

        Assert.NotNull(pending);
        Assert.Equal(5315, pending.MsgId);
        Assert.Equal(9315, pending.MsgSvrId);
        Assert.Equal("md5-9315", pending.Md5);
    }

    [Fact]
    public async Task RequestTalkDetailTask_ShouldNotSavePendingContextWhenMessageMissing()
    {
        await using var db = CreateDbContext();

        var saved = await db.SaveRequestTalkDetailPendingContext(
            OwnerWxid,
            FriendWxid,
            msgId: 5999,
            msgSvrId: 9999,
            md5: "missing",
            getOriginal: false,
            taskId: 42);

        Assert.False(saved);
        Assert.Empty(await db.MessageExtensions.ToListAsync());
    }

    [Fact]
    public void RequestTalkDetailResult_ShouldSkipEmptyContent()
    {
        var message = CreateMessage(messageId: 307, msgSvrId: 9307, messageType: EnumContentType.Text, content: "保留正文");

        InvokeApplyRequestTalkDetailContent(message, "   ");

        Assert.Equal("保留正文", message.content);
        Assert.Null(message.contentXml);
    }

    [Fact]
    public void RequestTalkDetailResult_ShouldWriteXmlWithoutOverwritingExistingMediaUrl()
    {
        var message = CreateMessage(
            messageId: 308,
            msgSvrId: 9308,
            messageType: EnumContentType.Picture,
            content: "/uploads/chat/pic-9308.jpg");

        const string xml = "<msg><img aeskey=\"new\" /></msg>";
        InvokeApplyRequestTalkDetailContent(message, xml);

        Assert.Equal("/uploads/chat/pic-9308.jpg", message.content);
        Assert.Equal(xml, message.contentXml);
    }

    [Fact]
    public void RequestTalkDetailResult_ShouldFillContentWhenExistingContentIsEmptyAndXmlArrives()
    {
        var message = CreateMessage(messageId: 309, msgSvrId: 9309, messageType: EnumContentType.Picture, content: string.Empty);

        const string xml = "<msg><appmsg><title>详情 XML</title></appmsg></msg>";
        InvokeApplyRequestTalkDetailContent(message, xml);

        Assert.Equal(xml, message.content);
        Assert.Equal(xml, message.contentXml);
    }

    [Fact]
    public void RequestTalkDetailResult_ShouldWritePlainTextWithoutChangingMessageType()
    {
        var message = CreateMessage(
            messageId: 316,
            msgSvrId: 9316,
            messageType: EnumContentType.Video,
            content: "/uploads/chat/video-9316.mp4");

        InvokeApplyRequestTalkDetailContent(message, "详情补偿正文");

        Assert.Equal((short)EnumContentType.Video, message.messageType);
        Assert.Equal("详情补偿正文", message.content);
        Assert.Null(message.contentXml);
    }

    private const string OwnerWxid = "wxid_p0_owner";
    private const string FriendWxid = "wxid_p0_friend";

    private static P0ChatRecoveryTestDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<P0ChatRecoveryTestDbContext>()
            .UseInMemoryDatabase($"p0-chat-recovery-{Guid.NewGuid():N}")
            .Options;

        return new P0ChatRecoveryTestDbContext(options);
    }

    private static Message CreateMessage(
        int messageId,
        long msgSvrId,
        EnumContentType messageType,
        string content,
        string? localMessageId = null,
        string friendId = FriendWxid)
    {
        return new Message
        {
            messageId = messageId,
            accountId = OwnerWxid,
            msgSvrId = msgSvrId,
            localMessageId = localMessageId,
            senderWxid = friendId,
            receiverWxid = OwnerWxid,
            chatType = 1,
            messageType = (short)messageType,
            content = content,
            direction = 2,
            sendStatus = 3,
            readStatus = 0,
            isDeleted = false,
            isRevoked = false,
            createdAt = DateTime.UtcNow,
            updatedAt = DateTime.UtcNow
        };
    }

    private static void InvokeApplyRequestTalkDetailContent(Message message, string contentText)
    {
        var method = typeof(ChatMessageHandler).GetMethod(
            "ApplyRequestTalkDetailContent",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        method.Invoke(null, new object[] { message, contentText });
    }

    private sealed class P0ChatRecoveryTestDbContext : DbContext
    {
        public P0ChatRecoveryTestDbContext(DbContextOptions<P0ChatRecoveryTestDbContext> options)
            : base(options)
        {
        }

        public DbSet<Message> Messages => Set<Message>();

        public DbSet<MessageMedia> MessageMedias => Set<MessageMedia>();

        public DbSet<MessageExtension> MessageExtensions => Set<MessageExtension>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 测试上下文只覆盖聊天补偿所需的三张表，避免账号/设备导航递归纳入完整业务模型。
            modelBuilder.Ignore<WechatAccount>();
            modelBuilder.Ignore<SrClient>();
            modelBuilder.Ignore<Jubo.JuLiao.IM.Wx.Proto.PostDeviceInfoNoticeMessage>();

            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(item => item.messageId);
                entity.Property(item => item.messageId).ValueGeneratedNever();
                entity.Ignore(item => item.mediaAttachments);
                entity.Ignore(item => item.messageExtensions);
                entity.Ignore(item => item.voiceTransText);
                entity.Ignore(item => item.account);
                entity.Ignore(item => item.sender);
                entity.Ignore(item => item.receiver);
            });

            modelBuilder.Entity<MessageMedia>(entity =>
            {
                entity.HasKey(item => item.id);
                entity.Property(item => item.id).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<MessageExtension>(entity =>
            {
                entity.HasKey(item => item.id);
                entity.Property(item => item.id).ValueGeneratedOnAdd();
            });
        }
    }
}
