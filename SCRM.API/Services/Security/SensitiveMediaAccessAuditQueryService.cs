using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.Models.Constants;
using SCRM.Services.Data;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.API.Services.Security;

/// <summary>
/// 敏感操作审计查询服务。
/// <para>读取 system_logs 中 SensitiveMediaAccess / SensitiveDataAccess / SensitiveContentRisk 模块的审计记录；普通用户必须具备审计权限，并且只能看到自己或可访问账号/设备相关记录。</para>
/// </summary>
public class SensitiveMediaAccessAuditQueryService
{
    private const int MaxPageSize = 200;

    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        SensitiveMediaAccessAuditService.ActionTokenIssued,
        SensitiveMediaAccessAuditService.ActionTokenDenied,
        SensitiveMediaAccessAuditService.ActionTokenOpened,
        SensitiveMediaAccessAuditService.ActionTokenOpenDenied,
        SensitiveDataAccessAuditService.ActionSensitiveFieldsReturned,
        SensitiveContentGuard.ActionContentBlocked,
        SensitiveContentGuard.ActionContentWarned,
        SensitiveContentGuard.ActionContentAuditOnly
    };

    private static readonly HashSet<string> AllowedModules = new(StringComparer.OrdinalIgnoreCase)
    {
        SensitiveMediaAccessAuditService.ModuleName,
        SensitiveDataAccessAuditService.ModuleName,
        SensitiveContentGuard.ModuleName
    };

    private readonly ApplicationDbContext _db;
    private readonly AccountAccessGuard _accountAccessGuard;
    private readonly ILogger<SensitiveMediaAccessAuditQueryService> _logger;

    public SensitiveMediaAccessAuditQueryService(
        ApplicationDbContext db,
        AccountAccessGuard accountAccessGuard,
        ILogger<SensitiveMediaAccessAuditQueryService> logger)
    {
        _db = db;
        _accountAccessGuard = accountAccessGuard;
        _logger = logger;
    }

    /// <summary>
    /// 分页查询敏感操作审计。
    /// </summary>
    public async Task<SensitiveMediaAccessAuditQueryResultDto> QueryAsync(
        ClaimsPrincipal? user,
        SensitiveMediaAccessAuditQueryDto? query,
        CancellationToken cancellationToken = default)
    {
        query ??= new SensitiveMediaAccessAuditQueryDto();
        var normalized = NormalizeQuery(query);

        if (!CanViewAudit(user))
        {
            return SensitiveMediaAccessAuditQueryResultDto.Denied("当前用户没有敏感操作审计查看权限。");
        }

        IQueryable<SystemLog> logs = _db.SystemLogs
            .AsNoTracking()
            .Where(log => log.module == SensitiveMediaAccessAuditService.ModuleName
                || log.module == SensitiveDataAccessAuditService.ModuleName
                || log.module == SensitiveContentGuard.ModuleName);

        logs = await ApplyOwnershipBoundaryAsync(logs, user, cancellationToken);
        logs = ApplyBasicFilters(logs, normalized);
        logs = ApplyJsonTextFilters(logs, normalized);

        var totalCount = await logs.CountAsync(cancellationToken);
        var rows = await logs
            .OrderByDescending(log => log.createdAt)
            .ThenByDescending(log => log.id)
            .Skip((normalized.page - 1) * normalized.pageSize)
            .Take(normalized.pageSize)
            .ToListAsync(cancellationToken);

        return new SensitiveMediaAccessAuditQueryResultDto
        {
            hasAccess = true,
            totalCount = totalCount,
            page = normalized.page,
            pageSize = normalized.pageSize,
            items = rows.Select(ParseItem).ToList()
        };
    }

    /// <summary>
    /// 判断当前用户是否具备审计查看权限。
    /// </summary>
    public static bool CanViewAudit(ClaimsPrincipal? user)
    {
        if (AccountAccessGuard.IsAdmin(user))
        {
            return true;
        }

        return HasPermissionClaim(user, Permissions.Risk.AuditView)
            || HasPermissionClaim(user, Permissions.System.Logs);
    }

    private async Task<IQueryable<SystemLog>> ApplyOwnershipBoundaryAsync(
        IQueryable<SystemLog> logs,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        if (AccountAccessGuard.IsAdmin(user))
        {
            return logs;
        }

        var userIds = AccountAccessGuard.GetUserIdCandidates(user).ToList();
        if (userIds.Count == 0)
        {
            return logs.Where(_ => false);
        }

        var accessibleAccounts = (await _accountAccessGuard.GetAccessibleAccountIdsAsync(user))
            .Select(accountId => $"wx:{accountId}")
            .ToList();
        cancellationToken.ThrowIfCancellationRequested();

        var accessibleDevices = (await _accountAccessGuard.GetAccessibleDeviceIdsAsync(user))
            .Select(deviceUuid => $"device:{deviceUuid}")
            .ToList();
        var allowedTargetIds = accessibleAccounts
            .Concat(accessibleDevices)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 目标归属命中时可见；目标无法推断的拒绝日志至少允许操作人本人回看。
        return logs.Where(log =>
            (log.targetId != null && allowedTargetIds.Contains(log.targetId))
            || (log.operatorId != null && userIds.Contains(log.operatorId)));
    }

    private static IQueryable<SystemLog> ApplyBasicFilters(IQueryable<SystemLog> logs, SensitiveMediaAccessAuditQueryDto query)
    {
        if (query.createdFrom.HasValue)
        {
            logs = logs.Where(log => log.createdAt >= query.createdFrom.Value);
        }

        if (query.createdTo.HasValue)
        {
            logs = logs.Where(log => log.createdAt <= query.createdTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.level))
        {
            logs = logs.Where(log => log.level == query.level);
        }

        if (!string.IsNullOrWhiteSpace(query.module))
        {
            logs = logs.Where(log => log.module == query.module);
        }

        if (!string.IsNullOrWhiteSpace(query.action))
        {
            logs = logs.Where(log => log.action == query.action);
        }

        if (!string.IsNullOrWhiteSpace(query.operatorId))
        {
            logs = logs.Where(log => log.operatorId != null && log.operatorId.Contains(query.operatorId));
        }

        if (!string.IsNullOrWhiteSpace(query.targetId))
        {
            logs = logs.Where(log => log.targetId != null && log.targetId.Contains(query.targetId));
        }

        if (!string.IsNullOrWhiteSpace(query.keyword))
        {
            var escapedKeyword = JsonEscapedFragment(query.keyword);
            logs = logs.Where(log =>
                (log.operatorId != null && log.operatorId.Contains(query.keyword))
                || (log.targetId != null && log.targetId.Contains(query.keyword))
                || log.action.Contains(query.keyword)
                || (log.message != null && (log.message.Contains(query.keyword) || log.message.Contains(escapedKeyword))));
        }

        return logs;
    }

    private static IQueryable<SystemLog> ApplyJsonTextFilters(IQueryable<SystemLog> logs, SensitiveMediaAccessAuditQueryDto query)
    {
        if (!string.IsNullOrWhiteSpace(query.scope))
        {
            logs = logs.Where(log => log.message != null && log.message.Contains(JsonStringPair("scope", query.scope)));
        }

        if (!string.IsNullOrWhiteSpace(query.source))
        {
            logs = logs.Where(log => log.message != null && log.message.Contains(query.source));
        }

        if (!string.IsNullOrWhiteSpace(query.accountId))
        {
            var targetId = $"wx:{query.accountId}";
            logs = logs.Where(log =>
                (log.targetId != null && log.targetId == targetId)
                || (log.message != null && log.message.Contains(JsonStringPair("accountId", query.accountId))));
        }

        if (!string.IsNullOrWhiteSpace(query.deviceUuid))
        {
            var targetId = $"device:{query.deviceUuid}";
            logs = logs.Where(log =>
                (log.targetId != null && log.targetId == targetId)
                || (log.message != null && log.message.Contains(JsonStringPair("deviceUuid", query.deviceUuid))));
        }

        if (!string.IsNullOrWhiteSpace(query.relativePath))
        {
            var escapedRelativePath = JsonEscapedFragment(query.relativePath);
            logs = logs.Where(log => log.message != null && (log.message.Contains(query.relativePath) || log.message.Contains(escapedRelativePath)));
        }

        if (!string.IsNullOrWhiteSpace(query.detail))
        {
            var escapedDetail = JsonEscapedFragment(query.detail);
            logs = logs.Where(log => log.message != null && (log.message.Contains(query.detail) || log.message.Contains(escapedDetail)));
        }

        if (!string.IsNullOrWhiteSpace(query.dataKind))
        {
            logs = logs.Where(log => log.message != null && log.message.Contains(JsonStringPair("dataKind", query.dataKind)));
        }

        return logs;
    }

    private SensitiveMediaAccessAuditItemDto ParseItem(SystemLog log)
    {
        var item = new SensitiveMediaAccessAuditItemDto
        {
            id = log.id,
            level = log.level ?? string.Empty,
            module = log.module ?? string.Empty,
            action = log.action ?? string.Empty,
            operatorId = log.operatorId ?? string.Empty,
            targetId = log.targetId ?? string.Empty,
            clientIp = log.clientIp ?? string.Empty,
            createdAt = log.createdAt
        };

        if (string.IsNullOrWhiteSpace(log.message))
        {
            return item;
        }

        try
        {
            using var doc = JsonDocument.Parse(log.message);
            var root = doc.RootElement;
            item.scope = GetString(root, "scope");
            item.relativePath = GetString(root, "relativePath");
            item.targetKind = GetString(root, "targetKind");
            item.targetValue = GetString(root, "targetValue");
            item.accountId = GetString(root, "accountId");
            item.deviceUuid = GetString(root, "deviceUuid");
            item.source = GetString(root, "source");
            item.detail = GetString(root, "detail");
            item.dataKind = GetString(root, "dataKind");
            item.scene = GetString(root, "scene");
            item.riskLevel = GetString(root, "riskLevel");
            item.matchedWords = GetStringArray(root, "matchedWords");
            item.fields = GetStringArray(root, "fields");
            item.recordCount = GetInt32(root, "recordCount");
            item.tokenHash = GetString(root, "tokenHash");
            item.issuedAt = GetDateTimeOffset(root, "issuedAt");
            item.expiresAt = GetDateTimeOffset(root, "expiresAt");
            item.userAgent = GetString(root, "userAgent");
            item.requestPath = GetString(root, "requestPath");
            item.queryKeys = GetStringArray(root, "queryKeys");
        }
        catch (Exception ex)
        {
            item.parseError = "审计消息 JSON 解析失败";
            _logger.LogWarning(ex, "解析敏感操作审计消息失败。LogId={LogId}", log.id);
        }

        return item;
    }

    private static SensitiveMediaAccessAuditQueryDto NormalizeQuery(SensitiveMediaAccessAuditQueryDto query)
    {
        return new SensitiveMediaAccessAuditQueryDto
        {
            page = Math.Max(query.page, 1),
            pageSize = Math.Clamp(query.pageSize, 1, MaxPageSize),
            createdFrom = query.createdFrom,
            createdTo = query.createdTo,
            level = NormalizeFilter(query.level),
            module = NormalizeModule(query.module),
            action = NormalizeAction(query.action),
            scope = NormalizeFilter(query.scope).ToLowerInvariant(),
            source = NormalizeFilter(query.source),
            operatorId = NormalizeFilter(query.operatorId),
            targetId = NormalizeFilter(query.targetId),
            accountId = NormalizeFilter(query.accountId),
            deviceUuid = NormalizeFilter(query.deviceUuid),
            relativePath = NormalizeFilter(query.relativePath),
            detail = NormalizeFilter(query.detail),
            dataKind = NormalizeFilter(query.dataKind),
            keyword = NormalizeFilter(query.keyword)
        };
    }

    private static string NormalizeModule(string? value)
    {
        var normalized = NormalizeFilter(value);
        return AllowedModules.Contains(normalized) ? normalized : string.Empty;
    }

    private static string NormalizeAction(string? value)
    {
        var normalized = NormalizeFilter(value);
        return AllowedActions.Contains(normalized) ? normalized : string.Empty;
    }

    private static string NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string JsonStringPair(string name, string value)
    {
        return $"\"{name}\":{JsonSerializer.Serialize(value)}";
    }

    private static string JsonEscapedFragment(string value)
    {
        var json = JsonSerializer.Serialize(value);
        return json.Length >= 2 ? json[1..^1] : value;
    }

    private static bool HasPermissionClaim(ClaimsPrincipal? user, string permission)
    {
        return user?.FindAll("permission").Any(claim => string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static string GetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)
            || value.ValueKind == JsonValueKind.Null
            || value.ValueKind == JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement root, string name)
    {
        var value = GetString(root, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int GetInt32(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(GetString(root, name), out var parsed) ? parsed : 0;
    }

    private static List<string> GetStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return new List<string>();
        }

        return value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.ToString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }
}
