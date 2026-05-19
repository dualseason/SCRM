using System.Security.Claims;
using System.Text.Json;
using SCRM.API.Services;

namespace SCRM.API.Services.Security;

/// <summary>
/// 敏感数据查看审计服务。
/// <para>复用 system_logs，不新增专用表；只记录查看范围、字段名、数量和归属，不记录手机号、短信正文、录音 URL 等原文。</para>
/// </summary>
public class SensitiveDataAccessAuditService
{
    public const string ModuleName = "SensitiveDataAccess";
    public const string ActionSensitiveFieldsReturned = "SensitiveFieldsReturned";
    public const string ActionFinderHistoryExported = "FinderHistoryExported";
    public const string ActionFinderHistoryExportDenied = "FinderHistoryExportDenied";

    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly ISystemLogService _systemLogService;
    private readonly ILogger<SensitiveDataAccessAuditService> _logger;

    public SensitiveDataAccessAuditService(
        ISystemLogService systemLogService,
        ILogger<SensitiveDataAccessAuditService> logger)
    {
        _systemLogService = systemLogService;
        _logger = logger;
    }

    /// <summary>
    /// 记录本次响应体中返回了完整或更高敏感级别字段。
    /// </summary>
    public Task LogSensitiveFieldsReturnedAsync(
        ClaimsPrincipal? user,
        string dataKind,
        string accountId,
        IEnumerable<string> fields,
        int recordCount,
        string source,
        string? deviceUuid = null,
        string? imei = null,
        string? detail = null,
        HttpContext? httpContext = null,
        string? clientIp = null)
    {
        var fieldList = fields
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recordCount <= 0 || fieldList.Count == 0)
        {
            return Task.CompletedTask;
        }

        return LogAsync(
            user,
            dataKind,
            accountId,
            fieldList,
            recordCount,
            source,
            deviceUuid,
            imei,
            detail,
            httpContext,
            clientIp);
    }

    /// <summary>
    /// 记录视频号历史导出成功。
    /// <para>仅记录筛选条件、数量、字段策略和权限，不记录评论正文、NonceId、FeedAuth、头像地址等原文。</para>
    /// </summary>
    public Task LogFinderHistoryExportedAsync(
        ClaimsPrincipal? user,
        string deviceUuid,
        int requestedCount,
        int mentionCount,
        int userPageCount,
        int commentCount,
        bool? successFilter,
        long? taskIdFilter,
        DateTimeOffset? receivedFrom,
        DateTimeOffset? receivedTo,
        string source,
        HttpContext? httpContext = null,
        string? clientIp = null)
    {
        var message = JsonSerializer.Serialize(new
        {
            dataKind = "finder-history-export",
            deviceUuid = deviceUuid?.Trim() ?? string.Empty,
            requestedCount,
            mentionCount,
            userPageCount,
            commentCount,
            totalCount = mentionCount + userPageCount + commentCount,
            successFilter,
            taskIdFilter,
            receivedFrom,
            receivedTo,
            exportPermission = SCRM.Models.Constants.Permissions.FinderOperation.Export,
            fieldPolicyVersion = "finder-export-v1",
            masked = true,
            rawPayloadIncluded = false,
            source,
            userAgent = httpContext?.Request.Headers.UserAgent.ToString(),
            requestPath = httpContext?.Request.Path.Value,
            queryKeys = httpContext?.Request.Query.Keys.ToArray()
        }, AuditJsonOptions);

        return LogAuditMessageAsync(
            user,
            ActionFinderHistoryExported,
            message,
            deviceUuid,
            httpContext,
            clientIp,
            source);
    }

    /// <summary>
    /// 记录视频号历史导出拒绝。
    /// </summary>
    public Task LogFinderHistoryExportDeniedAsync(
        ClaimsPrincipal? user,
        string deviceUuid,
        string reason,
        int requestedCount,
        bool? successFilter,
        long? taskIdFilter,
        DateTimeOffset? receivedFrom,
        DateTimeOffset? receivedTo,
        string source,
        HttpContext? httpContext = null,
        string? clientIp = null)
    {
        var message = JsonSerializer.Serialize(new
        {
            dataKind = "finder-history-export",
            deviceUuid = deviceUuid?.Trim() ?? string.Empty,
            reason = reason?.Trim() ?? string.Empty,
            requestedCount,
            successFilter,
            taskIdFilter,
            receivedFrom,
            receivedTo,
            exportPermission = SCRM.Models.Constants.Permissions.FinderOperation.Export,
            source,
            userAgent = httpContext?.Request.Headers.UserAgent.ToString(),
            requestPath = httpContext?.Request.Path.Value,
            queryKeys = httpContext?.Request.Query.Keys.ToArray()
        }, AuditJsonOptions);

        return LogAuditMessageAsync(
            user,
            ActionFinderHistoryExportDenied,
            message,
            deviceUuid,
            httpContext,
            clientIp,
            source);
    }

    private async Task LogAsync(
        ClaimsPrincipal? user,
        string dataKind,
        string accountId,
        IReadOnlyList<string> fields,
        int recordCount,
        string source,
        string? deviceUuid,
        string? imei,
        string? detail,
        HttpContext? httpContext,
        string? clientIp)
    {
        try
        {
            var normalizedAccountId = accountId?.Trim() ?? string.Empty;
            var normalizedDeviceUuid = deviceUuid?.Trim() ?? string.Empty;
            var operatorId = AccountAccessGuard.GetUserIdCandidates(user).FirstOrDefault()
                ?? user?.Identity?.Name
                ?? string.Empty;
            var resolvedClientIp = clientIp ?? httpContext?.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext?.Request.Headers.UserAgent.ToString();

            var message = JsonSerializer.Serialize(new
            {
                dataKind = string.IsNullOrWhiteSpace(dataKind) ? "unknown" : dataKind.Trim(),
                accountId = normalizedAccountId,
                deviceUuid = normalizedDeviceUuid,
                imei = imei?.Trim() ?? string.Empty,
                source,
                fields,
                recordCount,
                detail,
                userAgent,
                requestPath = httpContext?.Request.Path.Value,
                queryKeys = httpContext?.Request.Query.Keys.ToArray()
            }, AuditJsonOptions);

            await _systemLogService.LogAsync(
                "Info",
                ModuleName,
                ActionSensitiveFieldsReturned,
                message,
                Truncate(operatorId, 450),
                BuildTargetId(normalizedAccountId, normalizedDeviceUuid),
                Truncate(resolvedClientIp, 50));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "写入敏感数据查看审计失败。DataKind={DataKind}, AccountId={AccountId}, Source={Source}",
                dataKind,
                accountId,
                source);
        }
    }

    private async Task LogAuditMessageAsync(
        ClaimsPrincipal? user,
        string action,
        string message,
        string? deviceUuid,
        HttpContext? httpContext,
        string? clientIp,
        string source)
    {
        try
        {
            var normalizedDeviceUuid = deviceUuid?.Trim() ?? string.Empty;
            var operatorId = AccountAccessGuard.GetUserIdCandidates(user).FirstOrDefault()
                ?? user?.Identity?.Name
                ?? string.Empty;
            var resolvedClientIp = clientIp ?? httpContext?.Connection.RemoteIpAddress?.ToString();

            await _systemLogService.LogAsync(
                "Info",
                ModuleName,
                action,
                message,
                Truncate(operatorId, 450),
                string.IsNullOrWhiteSpace(normalizedDeviceUuid) ? null : Truncate($"device:{normalizedDeviceUuid}", 100),
                Truncate(resolvedClientIp, 50));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "写入视频号导出审计失败。Action={Action}, DeviceUuid={DeviceUuid}, Source={Source}",
                action,
                deviceUuid,
                source);
        }
    }

    private static string? BuildTargetId(string accountId, string deviceUuid)
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
