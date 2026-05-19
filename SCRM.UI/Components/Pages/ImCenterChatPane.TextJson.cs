using System.Text.Json;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.UI.Components.Pages;

public partial class ImCenterChatPane
{
    #region 消息扩展摘要、JSON 读取与文本裁剪

    /// <summary>
    /// 规范化消息扩展展示列表。
    /// </summary>
    private static List<MessageExtensionViewDto> NormalizeMessageExtensions(Message msg)
    {
        return msg.messageExtensions?
            .Where(extension => extension != null && !string.IsNullOrWhiteSpace(extension.extensionKey))
            .OrderByDescending(extension => extension.updatedAt == default ? extension.createdAt : extension.updatedAt)
            .ThenByDescending(extension => extension.id)
            .ToList()
            ?? new List<MessageExtensionViewDto>();
    }

    private static string FormatMessageExtensionSummary(MessageExtensionViewDto extension)
    {
        if (extension == null || string.IsNullOrWhiteSpace(extension.extensionValue))
        {
            return "空";
        }

        try
        {
            using var doc = JsonDocument.Parse(extension.extensionValue);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                return $"历史记录 {root.GetArrayLength()} 条";
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return TruncateText(extension.extensionValue, 120);
            }

            var parts = new List<string>();
            AddJsonSummaryPart(root, parts, "SemanticKind", "语义");
            AddJsonSummaryPart(root, parts, "Confidence", "置信");
            AddJsonSummaryPart(root, parts, "Title", "标题", 38);
            AddJsonSummaryPart(root, parts, "Description", "描述", 42);
            AddJsonSummaryPart(root, parts, "SourceNotice", "来源");
            AddJsonSummaryPart(root, parts, "Url", "URL", 42);
            AddJsonSummaryPart(root, parts, "ThumbUrl", "封面", 36);
            AddJsonSummaryPart(root, parts, "AppId", "AppId", 28);
            AddJsonSummaryPart(root, parts, "PagePath", "路径", 36);
            AddJsonSummaryPart(root, parts, "Md5", "Md5", 28);
            AddJsonSummaryPart(root, parts, "MsgSvrId", "MsgSvrId");
            AddJsonSummaryPart(root, parts, "MessageType", "类型");
            AddJsonSummaryPart(root, parts, "FileSize", "大小");
            AddJsonSummaryPart(root, parts, "SubType", "SubType");
            AddJsonSummaryPart(root, parts, "FileId", "FileId", 28);
            AddJsonSummaryPart(root, parts, "CdnFileType", "CDN类型");
            AddJsonSummaryPart(root, parts, "FileFmt", "格式");
            AddJsonSummaryPart(root, parts, "TaskId", "TaskId");
            AddJsonSummaryPart(root, parts, "GetOriginal", "原图");

            return parts.Count == 0
                ? TruncateText(extension.extensionValue, 120)
                : string.Join("，", parts);
        }
        catch
        {
            return TruncateText(extension.extensionValue, 120);
        }
    }

    private static bool TryExtractJsonObjectText(string? text, out string jsonText)
    {
        jsonText = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            jsonText = trimmed;
            return true;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            jsonText = trimmed[start..(end + 1)];
            return true;
        }

        return false;
    }

    private static bool TryGetJsonProperty(JsonElement root, out JsonElement property, params string[] names)
    {
        property = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out property))
            {
                return true;
            }
        }

        foreach (var item in root.EnumerateObject())
        {
            if (names.Any(name => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                property = item.Value;
                return true;
            }
        }

        return false;
    }

    private static string GetJsonString(JsonElement root, params string[] names)
    {
        if (!TryGetJsonProperty(root, out var property, names))
        {
            return string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    private static void AddJsonSummaryPart(
        JsonElement root,
        List<string> parts,
        string propertyName,
        string label,
        int maxLength = 32)
    {
        if (!TryGetJsonProperty(root, out var property, propertyName))
        {
            return;
        }

        var value = property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "是",
            JsonValueKind.False => "否",
            _ => property.GetRawText()
        };

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (propertyName.Equals("FileSize", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(value, out var fileSize)
            && fileSize > 0)
        {
            value = FormatFileSize(fileSize);
        }

        parts.Add($"{label}={TruncateMiddle(value, maxLength)}");
    }

    private static string TruncateMiddle(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || maxLength <= 0)
        {
            return string.Empty;
        }

        var text = value.Trim();
        if (text.Length <= maxLength)
        {
            return text;
        }

        if (maxLength <= 3)
        {
            return text[..maxLength];
        }

        var left = Math.Max(1, (maxLength - 1) / 2);
        var right = Math.Max(1, maxLength - left - 1);
        return $"{text[..left]}…{text[^right..]}";
    }

    private static string TruncateText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || maxLength <= 0)
        {
            return string.Empty;
        }

        var text = value.Trim();
        return text.Length <= maxLength ? text : $"{text[..maxLength]}…";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    #endregion
}