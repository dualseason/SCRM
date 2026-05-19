using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Data;
using SCRM.API.Services.Netty.Parsing;

namespace SCRM.Tests;

/// <summary>
/// 高级消息解析历史持久化回归测试。
/// <para>只验证 DbHelper 对 MessageExtensions 的 latest、kind、history 写入、去重、限长和坏 JSON 容错。</para>
/// </summary>
public sealed class AdvancedMessageContentHistoryPersistenceTests
{
    private const string LatestKey = "advanced_content_latest";
    private const string HistoryKey = "advanced_content_history";

    [Fact]
    public async Task SaveAdvancedMessageContentMetadata_ShouldWriteLatestKindAndHistory()
    {
        await using var db = CreateDbContext();
        var message = CreateMessage(101);

        var advanced = CreateAdvanced(
            rawHash: "hash-file-001",
            title: "report.pdf",
            semanticKind: "File");

        var saved = await db.SaveAdvancedMessageContentMetadata(message, advanced);

        Assert.True(saved);

        var latest = await FindRequiredExtensionAsync(db, 101, LatestKey);
        var kind = await FindRequiredExtensionAsync(db, 101, "advanced_content:file");
        var history = await FindRequiredExtensionAsync(db, 101, HistoryKey);

        var latestContent = DeserializeAdvanced(latest.extensionValue);
        Assert.Equal("File", latestContent.SemanticKind);
        Assert.Equal("hash-file-001", latestContent.RawHash);
        Assert.Equal("report.pdf", latestContent.Title);

        var kindContent = DeserializeAdvanced(kind.extensionValue);
        Assert.Equal("File", kindContent.SemanticKind);
        Assert.Equal("hash-file-001", kindContent.RawHash);

        var historyItems = DeserializeHistory(history.extensionValue);
        Assert.Single(historyItems);
        Assert.Equal("File", historyItems[0].SemanticKind);
        Assert.Equal("hash-file-001", historyItems[0].RawHash);
        Assert.Equal("report.pdf", historyItems[0].Title);
    }

    [Fact]
    public async Task SaveAdvancedMessageContentMetadata_ShouldDeduplicateSameHistoryFingerprint()
    {
        await using var db = CreateDbContext();
        var message = CreateMessage(102);

        await db.SaveAdvancedMessageContentMetadata(
            message,
            CreateAdvanced(
                rawHash: "same-hash",
                title: "first-title.pdf",
                semanticKind: "File"));

        await db.SaveAdvancedMessageContentMetadata(
            message,
            CreateAdvanced(
                rawHash: "same-hash",
                title: "second-title.pdf",
                semanticKind: "File"));

        var history = await FindRequiredExtensionAsync(db, 102, HistoryKey);
        var historyItems = DeserializeHistory(history.extensionValue);

        Assert.Single(historyItems);
        Assert.Equal("same-hash", historyItems[0].RawHash);
        Assert.Equal("second-title.pdf", historyItems[0].Title);

        var latest = await FindRequiredExtensionAsync(db, 102, LatestKey);
        var latestContent = DeserializeAdvanced(latest.extensionValue);
        Assert.Equal("second-title.pdf", latestContent.Title);
    }

    [Fact]
    public async Task SaveAdvancedMessageContentMetadata_ShouldKeepDifferentRawHashHistory()
    {
        await using var db = CreateDbContext();
        var message = CreateMessage(103);

        await db.SaveAdvancedMessageContentMetadata(
            message,
            CreateAdvanced(
                rawHash: "hash-pending-thumb",
                title: "TODO-Wait-Thumb",
                thumbUrl: "TODO-Wait-Thumb",
                semanticKind: "Emoji"));

        await db.SaveAdvancedMessageContentMetadata(
            message,
            CreateAdvanced(
                rawHash: "hash-resolved-thumb",
                title: "emoji.dat",
                thumbUrl: "/cache/emoji/emoji.dat",
                semanticKind: "Emoji"));

        var history = await FindRequiredExtensionAsync(db, 103, HistoryKey);
        var historyItems = DeserializeHistory(history.extensionValue);

        Assert.Equal(2, historyItems.Count);
        Assert.Equal("hash-resolved-thumb", historyItems[0].RawHash);
        Assert.Equal("hash-pending-thumb", historyItems[1].RawHash);
        Assert.Equal("/cache/emoji/emoji.dat", historyItems[0].ThumbUrl);
        Assert.Equal("TODO-Wait-Thumb", historyItems[1].ThumbUrl);
    }

    [Fact]
    public async Task SaveAdvancedMessageContentMetadata_ShouldLimitHistoryTo20()
    {
        await using var db = CreateDbContext();
        var message = CreateMessage(104);

        for (var index = 0; index < 25; index++)
        {
            await db.SaveAdvancedMessageContentMetadata(
                message,
                CreateAdvanced(
                    rawHash: $"hash-{index:00}",
                    title: $"file-{index:00}.pdf",
                    semanticKind: "File"));
        }

        var history = await FindRequiredExtensionAsync(db, 104, HistoryKey);
        var historyItems = DeserializeHistory(history.extensionValue);

        Assert.Equal(20, historyItems.Count);
        Assert.Equal("hash-24", historyItems[0].RawHash);
        Assert.Equal("hash-05", historyItems[^1].RawHash);
        Assert.DoesNotContain(historyItems, item => item.RawHash == "hash-00");
        Assert.DoesNotContain(historyItems, item => item.RawHash == "hash-04");
    }

    [Fact]
    public async Task SaveAdvancedMessageContentMetadata_ShouldRecoverFromBrokenHistoryJson()
    {
        await using var db = CreateDbContext();
        var message = CreateMessage(105);

        db.MessageExtensions.Add(new MessageExtension
        {
            messageId = 105,
            extensionKey = HistoryKey,
            extensionValue = "not json",
            createdAt = DateTime.UtcNow,
            updatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var saved = await db.SaveAdvancedMessageContentMetadata(
            message,
            CreateAdvanced(
                rawHash: "hash-after-broken-json",
                title: "after-broken.pdf",
                semanticKind: "File"));

        Assert.True(saved);

        var latest = await FindRequiredExtensionAsync(db, 105, LatestKey);
        var history = await FindRequiredExtensionAsync(db, 105, HistoryKey);
        var latestContent = DeserializeAdvanced(latest.extensionValue);
        var historyItems = DeserializeHistory(history.extensionValue);

        Assert.Equal("hash-after-broken-json", latestContent.RawHash);
        Assert.Single(historyItems);
        Assert.Equal("hash-after-broken-json", historyItems[0].RawHash);
    }

    [Fact]
    public async Task EnrichMessagesWithMediaMetadataAsync_ShouldReturnAdvancedExtensionsAndSkipLookalikeKeys()
    {
        await using var db = CreateDbContext();
        var message = CreateMessage(106);
        var now = DateTime.UtcNow;

        db.MessageExtensions.AddRange(
            CreateExtension(106, LatestKey, """{"SemanticKind":"File","Title":"latest.pdf"}""", now.AddMinutes(4)),
            CreateExtension(106, HistoryKey, """[{"SemanticKind":"File","Title":"latest.pdf"}]""", now.AddMinutes(3)),
            CreateExtension(106, "advanced_content:file", """{"SemanticKind":"File","Title":"kind.pdf"}""", now.AddMinutes(2)),
            CreateExtension(106, "advanced_content_file", """{"SemanticKind":"File","Title":"不应回传"}""", now.AddMinutes(1)),
            CreateExtension(106, "unknown_extension", """{"Value":"不应回传"}""", now));
        await db.SaveChangesAsync();

        var messages = new List<Message> { message };

        await db.EnrichMessagesWithMediaMetadataAsync(messages);

        var keys = message.messageExtensions.Select(item => item.extensionKey).ToArray();
        Assert.Contains(LatestKey, keys);
        Assert.Contains(HistoryKey, keys);
        Assert.Contains("advanced_content:file", keys);
        Assert.DoesNotContain("advanced_content_file", keys);
        Assert.DoesNotContain("unknown_extension", keys);
    }

    [Fact]
    public async Task EnrichMessagesWithMediaMetadataAsync_ShouldReturnMediaAndLatestVoiceText()
    {
        await using var db = CreateDbContext();
        var message = CreateMessage(107);
        var now = DateTime.UtcNow;

        db.MessageMedias.AddRange(
            new MessageMedia
            {
                messageId = 107,
                mediaType = (int)Jubo.JuLiao.IM.Wx.Proto.EnumContentType.Picture,
                mediaUrl = "/uploads/chat/pic-later.jpg",
                mediaHash = "pic-hash-later",
                fileSize = 200,
                fileExtension = "jpg",
                uploadStatus = 1,
                createdAt = now.AddMinutes(2),
                updatedAt = now.AddMinutes(2)
            },
            new MessageMedia
            {
                messageId = 107,
                mediaType = (int)Jubo.JuLiao.IM.Wx.Proto.EnumContentType.Video,
                mediaUrl = "/uploads/chat/video-earlier.mp4",
                mediaHash = "video-hash-earlier",
                fileSize = 300,
                fileExtension = "mp4",
                uploadStatus = 1,
                createdAt = now.AddMinutes(1),
                updatedAt = now.AddMinutes(1)
            });

        db.VoiceToTextLogs.AddRange(
            new VoiceToTextLog
            {
                messageId = 107,
                voiceUrl = "/uploads/chat/voice-old.amr",
                transcribedText = "旧识别结果",
                transcribeStatus = 1,
                accuracy = 0.5,
                transcribeTime = now.AddMinutes(1),
                createdAt = now.AddMinutes(1),
                updatedAt = now.AddMinutes(1)
            },
            new VoiceToTextLog
            {
                messageId = 107,
                voiceUrl = "/uploads/chat/voice-new.amr",
                transcribedText = "最新识别结果",
                transcribeStatus = 1,
                accuracy = 0.96,
                transcribeTime = now.AddMinutes(3),
                createdAt = now.AddMinutes(3),
                updatedAt = now.AddMinutes(3)
            });

        await db.SaveChangesAsync();

        var messages = new List<Message> { message };

        await db.EnrichMessagesWithMediaMetadataAsync(messages);

        Assert.Equal(2, message.mediaAttachments.Count);
        Assert.Equal("/uploads/chat/video-earlier.mp4", message.mediaAttachments[0].mediaUrl);
        Assert.Equal("/uploads/chat/pic-later.jpg", message.mediaAttachments[1].mediaUrl);
        Assert.Equal("video-hash-earlier", message.mediaAttachments[0].mediaHash);
        Assert.Equal(300, message.mediaAttachments[0].fileSize);
        Assert.Equal("mp4", message.mediaAttachments[0].fileExtension);

        Assert.NotNull(message.voiceTransText);
        Assert.Equal("/uploads/chat/voice-new.amr", message.voiceTransText.voiceUrl);
        Assert.Equal("最新识别结果", message.voiceTransText.transcribedText);
        Assert.Equal(0.96, message.voiceTransText.accuracy);
    }

    private static AdvancedMessageHistoryTestDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AdvancedMessageHistoryTestDbContext>()
            .UseInMemoryDatabase($"advanced-history-{Guid.NewGuid():N}")
            .Options;

        return new AdvancedMessageHistoryTestDbContext(options);
    }

    private static Message CreateMessage(int messageId)
    {
        return new Message
        {
            messageId = messageId,
            accountId = "wxid_history_test",
            senderWxid = "wxid_history_test",
            receiverWxid = "wxid_friend",
            chatType = 1,
            messageType = 14,
            content = string.Empty,
            direction = 2,
            createdAt = DateTime.UtcNow,
            updatedAt = DateTime.UtcNow
        };
    }

    private static MessageExtension CreateExtension(
        int messageId,
        string extensionKey,
        string extensionValue,
        DateTime updatedAt)
    {
        return new MessageExtension
        {
            messageId = messageId,
            extensionKey = extensionKey,
            extensionValue = extensionValue,
            createdAt = updatedAt,
            updatedAt = updatedAt
        };
    }

    private static AdvancedMessageContent CreateAdvanced(
        string rawHash,
        string title,
        string semanticKind,
        string thumbUrl = "/cache/file/report.pdf")
    {
        return new AdvancedMessageContent
        {
            SourceNotice = "FriendTalkNotice",
            SemanticKind = semanticKind,
            Confidence = "High",
            MessageType = 14,
            OriginalMsgType = 1048625,
            Title = title,
            Description = "测试描述",
            ThumbUrl = thumbUrl,
            Md5 = "0123456789abcdef0123456789abcdef",
            FileSize = 123456,
            RawKind = "Json",
            RawPreview = title,
            RawHash = rawHash
        };
    }

    private static async Task<MessageExtension> FindRequiredExtensionAsync(
        AdvancedMessageHistoryTestDbContext db,
        int messageId,
        string extensionKey)
    {
        var extension = await db.MessageExtensions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.messageId == messageId
                && item.extensionKey == extensionKey);

        Assert.NotNull(extension);
        return extension;
    }

    private static AdvancedMessageContent DeserializeAdvanced(string json)
    {
        var content = JsonSerializer.Deserialize<AdvancedMessageContent>(json);
        Assert.NotNull(content);
        return content;
    }

    private static List<AdvancedMessageContent> DeserializeHistory(string json)
    {
        var history = JsonSerializer.Deserialize<List<AdvancedMessageContent>>(json);
        Assert.NotNull(history);
        return history;
    }

    private sealed class AdvancedMessageHistoryTestDbContext : DbContext
    {
        public AdvancedMessageHistoryTestDbContext(DbContextOptions<AdvancedMessageHistoryTestDbContext> options)
            : base(options)
        {
        }

        public DbSet<MessageExtension> MessageExtensions => Set<MessageExtension>();

        public DbSet<MessageMedia> MessageMedias => Set<MessageMedia>();

        public DbSet<VoiceToTextLog> VoiceToTextLogs => Set<VoiceToTextLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MessageExtension>(entity =>
            {
                entity.HasKey(item => item.id);
                entity.Property(item => item.id).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<MessageMedia>(entity =>
            {
                entity.HasKey(item => item.id);
                entity.Property(item => item.id).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<VoiceToTextLog>(entity =>
            {
                entity.HasKey(item => item.id);
                entity.Property(item => item.id).ValueGeneratedOnAdd();
            });
        }
    }
}
