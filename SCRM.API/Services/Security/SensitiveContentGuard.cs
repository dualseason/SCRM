using System.Security.Claims;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using SCRM.API.Services;

namespace SCRM.API.Services.Security;

/// <summary>
/// 服务端发送前敏感内容风控。
/// <para>所有后台下发任务应先经过本服务；命中阻断词时不再下发 Android 任务，并写入 system_logs 审计。</para>
/// </summary>
public class SensitiveContentGuard
{
    public const string ModuleName = "SensitiveContentRisk";
    public const string ActionContentBlocked = "ContentBlocked";
    public const string ActionContentWarned = "ContentWarned";
    public const string ActionContentAuditOnly = "ContentAuditOnly";

    public const string LevelBlock = "block";
    public const string LevelWarn = "warn";
    public const string LevelAudit = "audit";
    public const string LevelNone = "none";

    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly SensitiveWordPolicyService _policyService;
    private readonly ISystemLogService _systemLogService;
    private readonly ILogger<SensitiveContentGuard> _logger;

    public SensitiveContentGuard(
        SensitiveWordPolicyService policyService,
        ISystemLogService systemLogService,
        ILogger<SensitiveContentGuard> logger)
    {
        _policyService = policyService;
        _systemLogService = systemLogService;
        _logger = logger;
    }

    /// <summary>
    /// 检查发送内容是否命中敏感词策略。
    /// <para>只检查传入字段值；审计只记录字段名、长度、命中词和上下文，不记录发送原文。</para>
    /// </summary>
    public async Task<SensitiveContentCheckResult> CheckAsync(
        ClaimsPrincipal? user,
        string scene,
        string? deviceUuid,
        string? accountId,
        string? targetId,
        IReadOnlyDictionary<string, string>? contents,
        string source,
        HttpContext? httpContext = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedContents = NormalizeContents(contents);
        if (normalizedContents.Count == 0)
        {
            return SensitiveContentCheckResult.Allow();
        }

        var policy = await _policyService.GetPolicyAsync(deviceUuid, accountId, cancellationToken);
        if (policy.IsEmpty)
        {
            return SensitiveContentCheckResult.Allow();
        }

        var blockMatches = FindMatches(normalizedContents, policy.BlockWords);
        if (blockMatches.Count > 0)
        {
            var result = SensitiveContentCheckResult.Deny(
                blockMatches,
                "发送内容命中敏感词策略，已阻断下发。",
                LevelBlock);
            await LogAsync("Warning", ActionContentBlocked, user, scene, deviceUuid, accountId, targetId, normalizedContents, source, result, httpContext);
            return result;
        }

        var warnMatches = FindMatches(normalizedContents, policy.WarnWords);
        if (warnMatches.Count > 0)
        {
            var result = SensitiveContentCheckResult.AllowWithMatches(
                warnMatches,
                "发送内容命中敏感词警告策略，已记录审计。",
                LevelWarn);
            await LogAsync("Warning", ActionContentWarned, user, scene, deviceUuid, accountId, targetId, normalizedContents, source, result, httpContext);
            return result;
        }

        var auditMatches = FindMatches(normalizedContents, policy.AuditWords);
        if (auditMatches.Count > 0)
        {
            var result = SensitiveContentCheckResult.AllowWithMatches(
                auditMatches,
                "发送内容命中敏感词审计策略，已记录审计。",
                LevelAudit);
            await LogAsync("Info", ActionContentAuditOnly, user, scene, deviceUuid, accountId, targetId, normalizedContents, source, result, httpContext);
            return result;
        }

        return SensitiveContentCheckResult.Allow();
    }

    private async Task LogAsync(
        string level,
        string action,
        ClaimsPrincipal? user,
        string scene,
        string? deviceUuid,
        string? accountId,
        string? targetId,
        IReadOnlyDictionary<string, string> contents,
        string source,
        SensitiveContentCheckResult result,
        HttpContext? httpContext)
    {
        try
        {
            var normalizedDeviceUuid = deviceUuid?.Trim() ?? string.Empty;
            var normalizedAccountId = accountId?.Trim() ?? string.Empty;
            var operatorId = AccountAccessGuard.GetUserIdCandidates(user).FirstOrDefault()
                ?? user?.Identity?.Name
                ?? string.Empty;
            var clientIp = httpContext?.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
            var fields = contents.Keys.ToList();
            var contentLengths = contents.ToDictionary(item => item.Key, item => item.Value.Length, StringComparer.OrdinalIgnoreCase);

            var message = JsonSerializer.Serialize(new
            {
                scope = "send-content",
                scene = string.IsNullOrWhiteSpace(scene) ? "unknown" : scene.Trim(),
                deviceUuid = normalizedDeviceUuid,
                accountId = normalizedAccountId,
                targetId = targetId?.Trim() ?? string.Empty,
                source,
                riskLevel = result.Level,
                matchedWords = result.MatchedWords,
                fields,
                contentLengths,
                detail = result.Message,
                userAgent,
                requestPath = httpContext?.Request.Path.Value,
                queryKeys = httpContext?.Request.Query.Keys.ToArray()
            }, AuditJsonOptions);

            await _systemLogService.LogAsync(
                level,
                ModuleName,
                action,
                message,
                Truncate(operatorId, 450),
                BuildAuditTargetId(normalizedAccountId, normalizedDeviceUuid),
                Truncate(clientIp, 50));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "写入敏感内容风控审计失败。Action={Action}, Scene={Scene}, DeviceUuid={DeviceUuid}, AccountId={AccountId}",
                action,
                scene,
                deviceUuid,
                accountId);
        }
    }

    private static Dictionary<string, string> NormalizeContents(IReadOnlyDictionary<string, string>? contents)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (contents == null)
        {
            return result;
        }

        foreach (var item in contents)
        {
            var field = item.Key?.Trim() ?? string.Empty;
            var value = item.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            result[field] = value;
        }

        return result;
    }

    private static IReadOnlyList<string> FindMatches(IReadOnlyDictionary<string, string> contents, IReadOnlyList<string> words)
    {
        if (contents.Count == 0 || words.Count == 0)
        {
            return Array.Empty<string>();
        }

        var normalizedValues = contents.Values
            .Select(NormalizeForRiskMatch)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        if (normalizedValues.Count == 0)
        {
            return Array.Empty<string>();
        }

        var matches = new List<string>();
        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                continue;
            }

            var normalizedWord = NormalizeForRiskMatch(word);
            if (string.IsNullOrWhiteSpace(normalizedWord))
            {
                continue;
            }

            if (normalizedValues.Any(value => value.Contains(normalizedWord, StringComparison.Ordinal)))
            {
                matches.Add(word);
            }
        }

        return matches.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// 归一化敏感词匹配文本。
    /// <para>用于降低全半角、大小写、零宽字符、空白/符号插入、常见 URL/HTML 编码变体造成的绕过。</para>
    /// </summary>
    public static string NormalizeForRiskMatch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decoded = DecodeCommonEncodings(value.Trim());
        var normalized = decoded.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (ShouldIgnoreForRiskMatch(ch, category))
            {
                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static string DecodeCommonEncodings(string value)
    {
        var current = value;
        for (var i = 0; i < 2; i++)
        {
            var decodedHtml = WebUtility.HtmlDecode(current) ?? current;
            var decodedUrl = TryUrlDecode(decodedHtml);
            if (decodedUrl == current)
            {
                return decodedUrl;
            }

            current = decodedUrl;
        }

        return current;
    }

    private static string TryUrlDecode(string value)
    {
        if (!value.Contains('%', StringComparison.Ordinal) && !value.Contains('+', StringComparison.Ordinal))
        {
            return value;
        }

        try
        {
            return WebUtility.UrlDecode(value) ?? value;
        }
        catch
        {
            return value;
        }
    }

    private static bool ShouldIgnoreForRiskMatch(char ch, UnicodeCategory category)
    {
        return char.IsWhiteSpace(ch)
            || char.IsPunctuation(ch)
            || char.IsSymbol(ch)
            || char.IsControl(ch)
            || category is UnicodeCategory.Format
                or UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark;
    }

    private static string? BuildAuditTargetId(string accountId, string deviceUuid)
    {
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            return Truncate($"wx:{accountId}", 100);
        }

        if (!string.IsNullOrWhiteSpace(deviceUuid))
        {
            return Truncate($"device:{deviceUuid}", 100);
        }

        return null;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}

/// <summary>
/// 敏感内容检查结果。
/// </summary>
public sealed record SensitiveContentCheckResult(
    bool Allowed,
    IReadOnlyList<string> MatchedWords,
    string Message,
    string Level)
{
    public static SensitiveContentCheckResult Allow()
    {
        return new SensitiveContentCheckResult(true, Array.Empty<string>(), string.Empty, SensitiveContentGuard.LevelNone);
    }

    public static SensitiveContentCheckResult AllowWithMatches(IReadOnlyList<string> matchedWords, string message, string level)
    {
        return new SensitiveContentCheckResult(true, matchedWords, message, level);
    }

    public static SensitiveContentCheckResult Deny(IReadOnlyList<string> matchedWords, string message, string level)
    {
        var safeWords = matchedWords.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var finalMessage = safeWords.Count == 0 ? message : $"{message}命中词数量：{safeWords.Count}。";
        return new SensitiveContentCheckResult(false, safeWords, finalMessage, level);
    }
}
