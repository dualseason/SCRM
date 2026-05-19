using System;
using System.Net;
using System.Text.RegularExpressions;

namespace SCRM.SHARED.Utils
{
    /// <summary>
    /// 朋友圈正文提取工具。
    /// <para>安卓端有时把正文放在 Text，有时只保留在 Ext XML；这里提供统一兜底，避免页面只看到同步统计或空卡片。</para>
    /// </summary>
    public static class MomentContentExtractor
    {
        private static readonly string[] CandidateElementNames =
        {
            "contentDesc",
            "contentdesc",
            "description",
            "desc",
            "title",
            "des",
            "summary",
            "content"
        };

        /// <summary>
        /// 返回第一段非空文本。
        /// </summary>
        public static string FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                var normalized = NormalizeText(value);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 从朋友圈 Ext XML 中提取可展示正文。
        /// </summary>
        public static string ExtractTextFromXml(string? xml)
        {
            var raw = NormalizeRawXml(xml);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            foreach (var candidate in BuildXmlCandidates(raw))
            {
                var value = TryExtractByRegex(candidate);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }

            }

            return string.Empty;
        }

        private static string[] BuildXmlCandidates(string raw)
        {
            var decoded = WebUtility.HtmlDecode(raw) ?? raw;
            if (string.Equals(decoded, raw, StringComparison.Ordinal))
            {
                return new[] { raw };
            }

            return new[] { raw, decoded };
        }

        private static string TryExtractByRegex(string xml)
        {
            foreach (var name in CandidateElementNames)
            {
                var qualifiedName = $@"(?:(?:[\w.-]+):)?{Regex.Escape(name)}";
                var tagMatch = Regex.Match(
                    xml,
                    $@"<{qualifiedName}\b[^>]*>(?<value>.*?)</{qualifiedName}>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
                var tagValue = NormalizeText(tagMatch.Success ? tagMatch.Groups["value"].Value : string.Empty);
                if (!string.IsNullOrWhiteSpace(tagValue))
                {
                    return tagValue;
                }

                var attrMatch = Regex.Match(
                    xml,
                    $@"\b{qualifiedName}\s*=\s*[""'](?<value>.*?)[""']",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
                var attrValue = NormalizeText(attrMatch.Success ? attrMatch.Groups["value"].Value : string.Empty);
                if (!string.IsNullOrWhiteSpace(attrValue))
                {
                    return attrValue;
                }
            }

            return string.Empty;
        }

        private static string NormalizeRawXml(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string NormalizeText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var text = value.Trim();
            text = Regex.Replace(text, @"^\s*<!\[CDATA\[", string.Empty, RegexOptions.CultureInvariant);
            text = Regex.Replace(text, @"\]\]>\s*$", string.Empty, RegexOptions.CultureInvariant);
            text = Regex.Replace(text, @"<[^>]+>", " ", RegexOptions.Singleline | RegexOptions.CultureInvariant);
            text = WebUtility.HtmlDecode(text) ?? text;
            text = Regex.Replace(text, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
            return text;
        }
    }
}
