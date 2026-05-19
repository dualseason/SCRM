using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SCRM.API.Services;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.API.Services.Security;

/// <summary>
/// 敏感媒体访问审计服务。
/// <para>复用现有 system_logs 审计表，不新增持久化结构；只记录范围、相对路径、归属对象和操作人，不记录短期 token 明文。</para>
/// </summary>
public class SensitiveMediaAccessAuditService
{
    public const string ModuleName = "SensitiveMediaAccess";
    public const string ActionTokenIssued = "TokenIssued";
    public const string ActionTokenDenied = "TokenDenied";
    public const string ActionTokenOpened = "TokenOpened";
    public const string ActionTokenOpenDenied = "TokenOpenDenied";

    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly ISystemLogService _systemLogService;
    private readonly ILogger<SensitiveMediaAccessAuditService> _logger;

    public SensitiveMediaAccessAuditService(
        ISystemLogService systemLogService,
        ILogger<SensitiveMediaAccessAuditService> logger)
    {
        _systemLogService = systemLogService;
        _logger = logger;
    }

    /// <summary>
    /// 记录短期 token 签发成功。
    /// </summary>
    public Task LogTokenIssuedAsync(
        ClaimsPrincipal? user,
        MediaAccessTokenDto token,
        string source,
        string? accountId = null,
        string? deviceUuid = null,
        HttpContext? httpContext = null,
        string? detail = null)
    {
        return LogAsync(
            "Info",
            ActionTokenIssued,
            user,
            token.RelativePath,
            token.Scope,
            source,
            httpContext,
            accountId,
            deviceUuid,
            detail,
            expiresAt: token.ExpiresAt);
    }

    /// <summary>
    /// 记录短期 token 签发被拒绝。
    /// </summary>
    public Task LogTokenDeniedAsync(
        ClaimsPrincipal? user,
        string scope,
        string? relativePath,
        string reason,
        string source,
        string? accountId = null,
        string? deviceUuid = null,
        HttpContext? httpContext = null,
        string? token = null)
    {
        return LogAsync(
            "Warning",
            ActionTokenDenied,
            user,
            relativePath,
            MediaAccessTokenService.NormalizeScope(scope),
            source,
            httpContext,
            accountId,
            deviceUuid,
            reason,
            tokenHash: HashToken(token));
    }

    /// <summary>
    /// 记录短期 token 实际打开成功。
    /// </summary>
    public Task LogTokenOpenedAsync(
        MediaAccessTokenPayload payload,
        DateTimeOffset expiresAt,
        string source,
        HttpContext? httpContext = null,
        string? contentType = null)
    {
        return LogAsync(
            "Info",
            ActionTokenOpened,
            user: null,
            relativePath: payload.RelativePath,
            scope: payload.Scope,
            source: source,
            httpContext: httpContext,
            accountId: null,
            deviceUuid: null,
            detail: contentType,
            operatorIdFallback: payload.UserId,
            issuedAt: payload.IssuedAtUtc,
            expiresAt: expiresAt);
    }

    /// <summary>
    /// 记录短期 token 打开失败。
    /// </summary>
    public Task LogTokenOpenDeniedAsync(
        MediaAccessTokenPayload? payload,
        string reason,
        string source,
        HttpContext? httpContext = null,
        string? token = null)
    {
        return LogAsync(
            "Warning",
            ActionTokenOpenDenied,
            user: null,
            relativePath: payload?.RelativePath,
            scope: payload?.Scope ?? MediaAccessTokenService.MediaScope,
            source: source,
            httpContext: httpContext,
            accountId: null,
            deviceUuid: null,
            detail: reason,
            operatorIdFallback: payload?.UserId,
            issuedAt: payload?.IssuedAtUtc,
            tokenHash: HashToken(token));
    }

    private async Task LogAsync(
        string level,
        string action,
        ClaimsPrincipal? user,
        string? relativePath,
        string scope,
        string source,
        HttpContext? httpContext,
        string? accountId,
        string? deviceUuid,
        string? detail,
        string? operatorIdFallback = null,
        DateTimeOffset? issuedAt = null,
        DateTimeOffset? expiresAt = null,
        string? tokenHash = null)
    {
        try
        {
            var normalizedPath = MediaAccessTokenService.NormalizeRelativePath(relativePath) ?? string.Empty;
            var target = string.IsNullOrWhiteSpace(normalizedPath)
                ? MediaAccessTarget.None
                : MediaAccessTokenService.InferTarget(normalizedPath);
            var operatorId = AccountAccessGuard.GetUserIdCandidates(user).FirstOrDefault()
                ?? user?.Identity?.Name
                ?? operatorIdFallback
                ?? string.Empty;
            var clientIp = httpContext?.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext?.Request.Headers.UserAgent.ToString();

            var message = JsonSerializer.Serialize(new
            {
                scope = MediaAccessTokenService.NormalizeScope(scope),
                relativePath = normalizedPath,
                targetKind = target.Kind.ToString(),
                targetValue = target.Value,
                accountId,
                deviceUuid,
                source,
                detail,
                tokenHash,
                issuedAt,
                expiresAt,
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
                BuildTargetId(target, normalizedPath),
                Truncate(clientIp, 50));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入敏感媒体访问审计失败。Action={Action}, Scope={Scope}, RelativePath={RelativePath}", action, scope, relativePath);
        }
    }

    private static string? BuildTargetId(MediaAccessTarget target, string relativePath)
    {
        var value = target.Kind switch
        {
            MediaAccessTargetKind.WechatAccount => $"wx:{target.Value}",
            MediaAccessTargetKind.Device => $"device:{target.Value}",
            _ => string.IsNullOrWhiteSpace(relativePath) ? null : $"path:{relativePath}"
        };

        return Truncate(value, 100);
    }

    private static string? HashToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        return Convert.ToHexString(bytes)[..16];
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
