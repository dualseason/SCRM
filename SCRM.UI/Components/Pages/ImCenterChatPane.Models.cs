using SCRM.API.Models.Entities;

namespace SCRM.UI.Components.Pages;

public partial class ImCenterChatPane
{
    /// <summary>
    /// 群聊消息前缀解析结果。
    /// </summary>
    private sealed record GroupMessagePrefix(string MemberWxid, string Body);

    /// <summary>
    /// IM 消息在页面中的展示类型。
    /// </summary>
    private enum ChatMessageKind
    {
        Text,
        Image,
        Voice,
        Video,
        File,
        Emoji,
        Link,
        WeApp,
        Location,
        NameCard,
        Quote,
        FinderFeed,
        FinderLive,
        RoomLiving,
        LuckyMoney,
        MoneyTrans,
        Unsupported
    }

    /// <summary>
    /// 页面渲染用的消息展示模型。
    /// </summary>
    private sealed class ChatMessageDisplay
    {
        public string Body { get; init; } = string.Empty;
        public string SenderDisplayName { get; init; } = string.Empty;
        public string Avatar { get; init; } = "images/default-avatar.png";
        public bool ShowSenderName { get; init; }
        public ChatMessageKind Kind { get; init; } = ChatMessageKind.Text;
        public string MediaUrl { get; init; } = string.Empty;
        public string FileName { get; init; } = "文件";
        public IReadOnlyList<MessageMediaAttachmentDto> MediaAttachments { get; init; } = Array.Empty<MessageMediaAttachmentDto>();
        public IReadOnlyList<MessageExtensionViewDto> MessageExtensions { get; init; } = Array.Empty<MessageExtensionViewDto>();
        public VoiceToTextLogViewDto? VoiceTransText { get; init; }
        public AdvancedMessageDisplay? Advanced { get; init; }
    }

    /// <summary>
    /// 高级消息结构化展示模型。
    /// </summary>
    private sealed class AdvancedMessageDisplay
    {
        public string SemanticKind { get; init; } = "Unknown";
        public string Confidence { get; init; } = string.Empty;
        public string SourceNotice { get; init; } = string.Empty;
        public short MessageType { get; init; }
        public int? OriginalMsgType { get; init; }
        public long? MsgSvrId { get; init; }
        public long? LocalMsgId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string ThumbUrl { get; init; } = string.Empty;
        public string IconUrl { get; init; } = string.Empty;
        public string SourceName { get; init; } = string.Empty;
        public string AppId { get; init; } = string.Empty;
        public string PagePath { get; init; } = string.Empty;
        public int? AppMsgType { get; init; }
        public string TypeStr { get; init; } = string.Empty;
        public string Md5 { get; init; } = string.Empty;
        public long? FileSize { get; init; }
        public string FileExtension { get; init; } = string.Empty;
        public AdvancedLocationDisplay? Location { get; init; }
        public AdvancedQuoteDisplay? Quote { get; init; }
        public AdvancedFinderDisplay? Finder { get; init; }
        public AdvancedNameCardDisplay? NameCard { get; init; }
        public AdvancedPaymentDisplay? Payment { get; init; }
    }

    /// <summary>
    /// 位置类高级消息展示模型。
    /// </summary>
    private sealed class AdvancedLocationDisplay
    {
        public string Title { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public double? Longitude { get; init; }
        public double? Latitude { get; init; }
        public string PoiId { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
    }

    /// <summary>
    /// 引用消息展示模型。
    /// </summary>
    private sealed class AdvancedQuoteDisplay
    {
        public string QuoteUser { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public int? QuoteType { get; init; }
        public long? QuoteSvrId { get; init; }
        public string Content { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
    }

    /// <summary>
    /// 视频号动态或直播展示模型。
    /// </summary>
    private sealed class AdvancedFinderDisplay
    {
        public string FeedId { get; init; } = string.Empty;
        public string LiveId { get; init; } = string.Empty;
        public string NonceId { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
        public string Nickname { get; init; } = string.Empty;
        public string CoverUrl { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }

    /// <summary>
    /// 名片类高级消息展示模型。
    /// </summary>
    private sealed class AdvancedNameCardDisplay
    {
        public string Username { get; init; } = string.Empty;
        public string Nickname { get; init; } = string.Empty;
        public string HeadImgUrl { get; init; } = string.Empty;
        public string Alias { get; init; } = string.Empty;
        public string Province { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
    }

    /// <summary>
    /// 支付、红包或转账类高级消息展示模型。
    /// </summary>
    private sealed class AdvancedPaymentDisplay
    {
        public string Key { get; init; } = string.Empty;
        public string NativeUrl { get; init; } = string.Empty;
        public string FeeDescription { get; init; } = string.Empty;
        public string TransferId { get; init; } = string.Empty;
        public string HbType { get; init; } = string.Empty;
        public int? TotalNum { get; init; }
    }
}

