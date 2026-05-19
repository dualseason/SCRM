using System.Text.Json;
using SCRM.API.Models.Entities;

namespace SCRM.UI.Components.Pages;

public partial class ImCenterChatPane
{
    #region 高级消息展示解析

    private static ChatMessageKind ResolveMessageKindFromAdvanced(AdvancedMessageDisplay? advanced)
    {
        if (advanced == null || IsUnknownAdvancedKind(advanced.SemanticKind))
        {
            return ChatMessageKind.Text;
        }

        return advanced.SemanticKind.Trim().ToLowerInvariant() switch
        {
            "link" => ChatMessageKind.Link,
            "weapp" => ChatMessageKind.WeApp,
            "location" => ChatMessageKind.Location,
            "namecard" or "kefunamecard" or "qiyenamecard" => ChatMessageKind.NameCard,
            "quote" => ChatMessageKind.Quote,
            "finderfeed" => ChatMessageKind.FinderFeed,
            "finderlive" => ChatMessageKind.FinderLive,
            "roomliving" => ChatMessageKind.RoomLiving,
            "luckymoney" => ChatMessageKind.LuckyMoney,
            "moneytrans" => ChatMessageKind.MoneyTrans,
            "payment" => ChatMessageKind.MoneyTrans,
            "file" => ChatMessageKind.File,
            "emoji" => ChatMessageKind.Emoji,
            _ => ChatMessageKind.Text
        };
    }

    private static bool IsAdvancedCardKind(ChatMessageKind kind)
    {
        return kind is ChatMessageKind.Link
            or ChatMessageKind.WeApp
            or ChatMessageKind.Location
            or ChatMessageKind.NameCard
            or ChatMessageKind.Quote
            or ChatMessageKind.FinderFeed
            or ChatMessageKind.FinderLive
            or ChatMessageKind.RoomLiving
            or ChatMessageKind.LuckyMoney
            or ChatMessageKind.MoneyTrans
            or ChatMessageKind.Unsupported;
    }

    /// <summary>
    /// 判断当前气泡是否应直接渲染高级消息卡片。
    /// <para>文件和表情在已回填媒体 URL 时优先展示可点击文件/表情本体，高级解析信息继续在下方补偿上下文展示。</para>
    /// </summary>
    private static bool ShouldRenderAdvancedCard(ChatMessageDisplay display)
    {
        if (display.Advanced == null)
        {
            return false;
        }

        if (ShouldPreferPrimaryMedia(display.Kind) && !string.IsNullOrWhiteSpace(display.MediaUrl))
        {
            return false;
        }

        return true;
    }

    private static bool ShouldPreferPrimaryMedia(ChatMessageKind kind)
    {
        return kind is ChatMessageKind.File or ChatMessageKind.Emoji;
    }


    /// <summary>
    /// 从 MessageExtensions 中读取后端解析出的高级消息卡片数据。
    /// </summary>
    private static AdvancedMessageDisplay? ResolveAdvancedMessageDisplay(IReadOnlyList<MessageExtensionViewDto> messageExtensions)
    {
        if (messageExtensions == null || messageExtensions.Count == 0)
        {
            return null;
        }

        var candidates = messageExtensions
            .Where(item => string.Equals(item.extensionKey, AdvancedContentLatestExtensionKey, StringComparison.Ordinal))
            .Concat(messageExtensions.Where(item => item.extensionKey.StartsWith("advanced_content:", StringComparison.Ordinal)));

        foreach (var extension in candidates)
        {
            if (string.IsNullOrWhiteSpace(extension.extensionValue))
            {
                continue;
            }

            try
            {
                var display = JsonSerializer.Deserialize<AdvancedMessageDisplay>(
                    extension.extensionValue,
                    AdvancedMessageDisplayJsonOptions);
                if (display != null && !IsUnknownAdvancedKind(display.SemanticKind))
                {
                    return display;
                }
            }
            catch
            {
                // 单条扩展损坏时继续尝试 kind 扩展，避免 latest 异常导致整条高级消息无法展示。
            }
        }

        return null;
    }


    private static string GetAdvancedKindLabel(string? semanticKind)
    {
        return semanticKind?.Trim().ToLowerInvariant() switch
        {
            "link" => "链接",
            "weapp" => "小程序",
            "location" => "位置",
            "quote" => "引用消息",
            "emoji" => "表情",
            "file" => "文件",
            "finderfeed" => "视频号",
            "finderlive" => "视频号直播",
            "roomliving" => "群直播",
            "namecard" => "名片",
            "kefunamecard" => "客服名片",
            "qiyenamecard" => "企微名片",
            "luckymoney" => "红包",
            "moneytrans" => "转账",
            "payment" => "支付消息",
            _ => "高级消息"
        };
    }

    private static string GetAdvancedTitle(AdvancedMessageDisplay advanced)
    {
        return FirstNonEmpty(
            advanced.Title,
            advanced.Location?.Title,
            advanced.Quote?.Title,
            advanced.Quote?.Content,
            advanced.Finder?.Nickname,
            advanced.NameCard?.Nickname,
            advanced.Payment?.FeeDescription,
            advanced.SourceName,
            GetAdvancedKindLabel(advanced.SemanticKind));
    }

    private static string GetAdvancedDescription(AdvancedMessageDisplay advanced)
    {
        return FirstNonEmpty(
            advanced.Description,
            advanced.Finder?.Description,
            advanced.Location?.Label,
            advanced.Location?.Title,
            advanced.NameCard?.Username,
            advanced.NameCard?.Alias,
            advanced.Payment?.NativeUrl);
    }

    private static string GetAdvancedCoverUrl(AdvancedMessageDisplay advanced)
    {
        return FirstNonEmpty(
            advanced.ThumbUrl,
            advanced.IconUrl,
            advanced.Finder?.CoverUrl,
            advanced.NameCard?.HeadImgUrl);
    }

    private static string GetAdvancedPrimaryUrl(AdvancedMessageDisplay advanced)
    {
        return FirstNonEmpty(
            advanced.Url,
            advanced.Payment?.NativeUrl);
    }

    private static string GetAdvancedMeta(AdvancedMessageDisplay advanced)
    {
        var parts = new List<string>();
        AddMetaPart(parts, "来源", advanced.SourceName, 28);
        AddMetaPart(parts, "类型", advanced.TypeStr, 24);
        AddMetaPart(parts, "AppId", advanced.AppId, 28);
        AddMetaPart(parts, "路径", advanced.PagePath, 44);
        AddMetaPart(parts, "Md5", advanced.Md5, 28);
        AddMetaPart(parts, "MsgSvrId", advanced.MsgSvrId?.ToString(), 24);
        AddMetaPart(parts, "原始类型", advanced.OriginalMsgType?.ToString(), 16);
        if (advanced.FileSize.HasValue && advanced.FileSize.Value > 0)
        {
            AddMetaPart(parts, "大小", FormatFileSize(advanced.FileSize.Value), 20);
        }
        AddMetaPart(parts, "FeedId", advanced.Finder?.FeedId, 28);
        AddMetaPart(parts, "LiveId", advanced.Finder?.LiveId, 28);
        AddMetaPart(parts, "NonceId", advanced.Finder?.NonceId, 28);
        AddMetaPart(parts, "FinderUser", advanced.Finder?.Username, 28);
        AddMetaPart(parts, "QuoteSvrId", advanced.Quote?.QuoteSvrId?.ToString(), 24);
        return string.Join(" ｜ ", parts);
    }

    private static void AddMetaPart(List<string> parts, string label, string? value, int maxLength)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{label}：{TruncateMiddle(value, maxLength)}");
        }
    }

    private static string FormatCoordinate(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.######") : "-";
    }

    private static bool IsUnknownAdvancedKind(string? semanticKind)
    {
        return string.IsNullOrWhiteSpace(semanticKind)
            || string.Equals(semanticKind, "Unknown", StringComparison.OrdinalIgnoreCase);
    }


    #endregion
}
