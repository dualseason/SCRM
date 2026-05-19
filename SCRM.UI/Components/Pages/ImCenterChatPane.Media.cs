using System.Text.Json;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.UI.Components.Pages;

public partial class ImCenterChatPane
{
    #region 媒体、文件与语音展示解析

    private readonly Dictionary<string, MediaAccessTokenDto> _chatMediaAccessTokens = new(StringComparer.Ordinal);
    private readonly HashSet<string> _preparingChatMediaAccessKeys = new(StringComparer.Ordinal);

    /// <summary>
    /// 为聊天媒体生成短期访问链接。
    /// <para>永久 mediaUrl 只留在服务端状态中用于签发 token，不直接渲染到 DOM。</para>
    /// </summary>
    private async Task PrepareChatMediaAccessAsync(string mediaUrl, string? accountId = null, string? deviceUuid = null, int expiresMinutes = 5)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl))
        {
            NoticeService.Notify(Radzen.NotificationSeverity.Warning, "媒体链接为空", "该消息暂未回填可访问媒体。");
            return;
        }

        var key = BuildChatMediaAccessKey(mediaUrl);
        if (!_preparingChatMediaAccessKeys.Add(key))
        {
            return;
        }

        try
        {
            var token = await Store.CreateMediaAccessTokenAsync(
                mediaUrl,
                accountId ?? string.Empty,
                deviceUuid ?? string.Empty,
                expiresMinutes);
            if (!token.Success || string.IsNullOrWhiteSpace(token.Url))
            {
                NoticeService.Notify(Radzen.NotificationSeverity.Warning, "媒体链接生成失败", token.Message);
                return;
            }

            _chatMediaAccessTokens[key] = token;
            NoticeService.Notify(Radzen.NotificationSeverity.Success, "媒体短期链接已生成", "已切换为后端短期 token 链接。", duration: 2200);
        }
        finally
        {
            _preparingChatMediaAccessKeys.Remove(key);
        }
    }

    private bool HasChatMediaAccessUrl(string mediaUrl)
    {
        return !string.IsNullOrWhiteSpace(GetChatMediaAccessUrl(mediaUrl));
    }

    private string GetChatMediaAccessUrl(string mediaUrl)
    {
        return _chatMediaAccessTokens.TryGetValue(BuildChatMediaAccessKey(mediaUrl), out var token)
            ? token.Url
            : string.Empty;
    }

    private bool IsPreparingChatMediaAccess(string mediaUrl)
    {
        return _preparingChatMediaAccessKeys.Contains(BuildChatMediaAccessKey(mediaUrl));
    }

    private string GetChatMediaAccessButtonText(string mediaUrl, string action)
    {
        if (IsPreparingChatMediaAccess(mediaUrl))
        {
            return "生成中";
        }

        return HasChatMediaAccessUrl(mediaUrl) ? "刷新链接" : action;
    }

    private string FormatChatMediaTokenExpires(string mediaUrl)
    {
        return _chatMediaAccessTokens.TryGetValue(BuildChatMediaAccessKey(mediaUrl), out var token)
            ? $"有效至 {token.ExpiresAt.LocalDateTime:HH:mm:ss}"
            : string.Empty;
    }

    private static string BuildChatMediaAccessKey(string mediaUrl)
    {
        return mediaUrl?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// 规范化媒体附件展示列表。
    /// <para>仅保留有 URL 的记录，避免空附件撑开消息气泡。</para>
    /// </summary>
    private static List<MessageMediaAttachmentDto> NormalizeMediaAttachments(Message msg)
    {
        return msg.mediaAttachments?
            .Where(media => media != null && !string.IsNullOrWhiteSpace(media.mediaUrl))
            .OrderBy(media => media.createdAt == default ? media.updatedAt : media.createdAt)
            .ThenBy(media => media.id)
            .ToList()
            ?? new List<MessageMediaAttachmentDto>();
    }


    /// <summary>
    /// 从结构化附件中选择当前气泡的主媒体。
    /// </summary>
    private static MessageMediaAttachmentDto? SelectPrimaryMediaAttachment(
        IReadOnlyList<MessageMediaAttachmentDto> mediaAttachments,
        ChatMessageKind kind)
    {
        if (mediaAttachments == null || mediaAttachments.Count == 0)
        {
            return null;
        }

        if (IsAdvancedCardKind(kind))
        {
            return null;
        }

        if (kind != ChatMessageKind.Text && kind != ChatMessageKind.Emoji)
        {
            var sameKind = mediaAttachments.FirstOrDefault(media => ResolveMessageKindFromMediaType(media.mediaType, media.mediaUrl) == kind);
            if (sameKind != null)
            {
                return sameKind;
            }
        }

        return mediaAttachments[0];
    }

    private static ChatMessageKind ResolveMessageKindFromMediaType(int mediaType, string mediaUrl)
    {
        return mediaType switch
        {
            2 => ChatMessageKind.Image,
            3 => ChatMessageKind.Voice,
            4 => ChatMessageKind.Video,
            8 => ChatMessageKind.File,
            14 => ChatMessageKind.Emoji,
            _ => InferKindFromContent(mediaUrl)
        };
    }

    private static ChatMessageKind InferKindFromContent(string body)
    {
        if (!TryExtractUrl(body, out var url))
        {
            return ChatMessageKind.Text;
        }

        var path = url.Split('?', '#')[0].ToLowerInvariant();
        if (path.EndsWith(".jpg") || path.EndsWith(".jpeg") || path.EndsWith(".png") || path.EndsWith(".gif") || path.EndsWith(".webp") || path.EndsWith(".bmp"))
        {
            return ChatMessageKind.Image;
        }

        if (path.EndsWith(".mp4") || path.EndsWith(".mov") || path.EndsWith(".m4v")
            || path.EndsWith(".3gp") || path.EndsWith(".avi")
            || path.EndsWith(".mkv") || path.EndsWith(".webm"))
        {
            return ChatMessageKind.Video;
        }

        if (path.EndsWith(".amr") || path.EndsWith(".mp3") || path.EndsWith(".wav") || path.EndsWith(".m4a") || path.EndsWith(".aac") || path.EndsWith(".ogg"))
        {
            return ChatMessageKind.Voice;
        }

        return ChatMessageKind.Text;
    }

    private static string ResolveMediaUrl(ChatMessageKind kind, string body)
    {
        if (kind != ChatMessageKind.Image && kind != ChatMessageKind.Video && kind != ChatMessageKind.Voice && kind != ChatMessageKind.File)
        {
            return string.Empty;
        }

        if (TryParseFileJson(body, out _, out var jsonUrl) && !string.IsNullOrWhiteSpace(jsonUrl))
        {
            return jsonUrl;
        }

        return TryExtractUrl(body, out var url) ? url : body;
    }

    private static string ResolveFileDisplayName(string body, string mediaUrl, AdvancedMessageDisplay? advanced = null)
    {
        if (advanced != null)
        {
            var advancedName = FirstNonEmpty(advanced.Title, advanced.Description);
            if (!string.IsNullOrWhiteSpace(advancedName) && !LooksLikeUrl(advancedName))
            {
                return advancedName;
            }
        }

        if (TryParseFileJson(body, out var jsonName, out _) && !string.IsNullOrWhiteSpace(jsonName))
        {
            return jsonName;
        }

        var source = string.IsNullOrWhiteSpace(mediaUrl) ? body : mediaUrl;
        if (string.IsNullOrWhiteSpace(source))
        {
            return "文件";
        }

        try
        {
            var path = Uri.TryCreate(source, UriKind.Absolute, out var uri) ? uri.LocalPath : source;
            var name = Path.GetFileName(path);
            return string.IsNullOrWhiteSpace(name) ? "文件" : Uri.UnescapeDataString(name);
        }
        catch
        {
            return "文件";
        }
    }

    private static bool TryExtractUrl(string text, out string url)
    {
        url = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidate = parts.FirstOrDefault(p => p.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        url = candidate;
        return true;
    }

    private static bool LooksLikeUrl(string text)
    {
        return !string.IsNullOrWhiteSpace(text)
            && (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParseFileJson(string body, out string name, out string url)
    {
        name = string.Empty;
        url = string.Empty;
        if (string.IsNullOrWhiteSpace(body) || !TryExtractJsonObjectText(body, out var jsonText))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;
            name = FirstNonEmpty(
                GetJsonString(root, "name", "Name", "fileName", "FileName", "filename", "Title", "title"),
                GetJsonString(root, "Md5", "md5"));
            url = FirstNonEmpty(
                GetJsonString(root, "url", "Url", "DataUrl", "dataUrl", "dataurl"),
                GetJsonString(root, "Thumb", "thumb", "ThumbUrl", "thumbUrl", "searchCachePath"));
            return !string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(url);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasMediaMetadata(ChatMessageDisplay display)
    {
        return display.MediaAttachments.Any() || display.MessageExtensions.Any();
    }

    private static string GetVoiceTransTextTitle(VoiceToTextLogViewDto voiceTransText)
    {
        return voiceTransText.transcribeStatus == 1 ? "语音转文字" : "语音转文字失败";
    }

    private static string GetVoiceTransTextBody(VoiceToTextLogViewDto voiceTransText)
    {
        if (voiceTransText.transcribeStatus == 1)
        {
            return string.IsNullOrWhiteSpace(voiceTransText.transcribedText)
                ? "识别成功，但文本为空。"
                : voiceTransText.transcribedText.Trim();
        }

        return string.IsNullOrWhiteSpace(voiceTransText.errorMessage)
            ? "识别失败，客户端未返回具体原因。"
            : voiceTransText.errorMessage.Trim();
    }

    private static string GetVoiceTransTextBlockStyle(VoiceToTextLogViewDto voiceTransText)
    {
        var color = voiceTransText.transcribeStatus == 1
            ? "rgba(40, 167, 69, 0.08)"
            : "rgba(220, 53, 69, 0.08)";
        var borderColor = voiceTransText.transcribeStatus == 1
            ? "var(--rz-success)"
            : "var(--rz-danger)";

        return $"margin-top: 4px; padding: 6px 8px; border-left: 3px solid {borderColor}; background: {color}; border-radius: 4px; max-width: 260px; font-size: 12px;";
    }

    private static string FormatMediaType(int mediaType)
    {
        return mediaType switch
        {
            2 => "图片",
            3 => "语音",
            4 => "视频",
            8 => "文件",
            14 => "表情",
            _ => $"类型 {mediaType}"
        };
    }

    private static string FormatFileSize(long fileSize)
    {
        if (fileSize <= 0)
        {
            return string.Empty;
        }

        string[] units = { "B", "KB", "MB", "GB" };
        var value = (double)fileSize;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{fileSize} {units[unitIndex]}"
            : $"{value:0.##} {units[unitIndex]}";
    }


    #endregion
}
