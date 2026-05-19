using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Services.Core;
using SCRM.Models.Constants;
using SCRM.Services.Data;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.API.Services.Security;

/// <summary>
/// 高危设备操作授权与审计服务。
/// <para>用于删除好友、清空聊天、微信登出、群管理、截图、设备配置等会改变微信或设备状态的任务。</para>
/// </summary>
public class DeviceOperationGuard
{
    public const string ModuleName = "DeviceOperationRisk";
    public const string ActionTaskDenied = "TaskDenied";
    public const string ActionTaskQueued = "TaskQueued";
    public const string ActionDeviceDeleted = "DeviceDeleted";

    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly ApplicationDbContext _context;
    private readonly AuthService _authService;
    private readonly ISystemLogService _systemLogService;
    private readonly ILogger<DeviceOperationGuard> _logger;

    public DeviceOperationGuard(
        ApplicationDbContext context,
        AuthService authService,
        ISystemLogService systemLogService,
        ILogger<DeviceOperationGuard> logger)
    {
        _context = context;
        _authService = authService;
        _systemLogService = systemLogService;
        _logger = logger;
    }

    /// <summary>
    /// 校验高危设备任务是否允许下发，并写入允许/拒绝审计。
    /// <para>与普通读取不同：非管理员不允许操作未分配 owner 的历史设备。</para>
    /// </summary>
    public async Task<DeviceOperationGuardResult> CheckAsync(
        ClaimsPrincipal? user,
        string? deviceUuid,
        string operation,
        string requiredPermission,
        string source,
        string? accountId = null,
        string? targetId = null,
        bool destructive = false,
        IReadOnlyDictionary<string, object?>? metadata = null,
        HttpContext? httpContext = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedDeviceUuid = deviceUuid?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDeviceUuid))
        {
            var denied = "设备ID为空";
            await LogAsync("Warning", ActionTaskDenied, user, normalizedDeviceUuid, accountId, targetId, operation, requiredPermission, source, destructive, "empty_device", metadata, httpContext);
            return DeviceOperationGuardResult.Deny(denied);
        }

        var userIds = AccountAccessGuard.GetUserIdCandidates(user);
        var isAdmin = AccountAccessGuard.IsAdmin(user);
        var client = await _context.SrClients
            .AsNoTracking()
            .Where(item => item.uuid == normalizedDeviceUuid)
            .Select(item => new
            {
                item.uuid,
                item.ownerId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (client == null)
        {
            await LogAsync("Warning", ActionTaskDenied, user, normalizedDeviceUuid, accountId, targetId, operation, requiredPermission, source, destructive, "device_not_found", metadata, httpContext);
            return DeviceOperationGuardResult.Deny("设备不存在");
        }

        if (!isAdmin)
        {
            if (userIds.Count == 0)
            {
                await LogAsync("Warning", ActionTaskDenied, user, normalizedDeviceUuid, accountId, targetId, operation, requiredPermission, source, destructive, "anonymous_user", metadata, httpContext);
                return DeviceOperationGuardResult.Deny("无权执行该高危操作");
            }

            if (string.IsNullOrWhiteSpace(client.ownerId))
            {
                await LogAsync("Warning", ActionTaskDenied, user, normalizedDeviceUuid, accountId, targetId, operation, requiredPermission, source, destructive, "ownerless_device", metadata, httpContext);
                return DeviceOperationGuardResult.Deny("未分配归属的设备不允许执行高危操作");
            }

            if (!IsOwnerMatched(client.ownerId, userIds))
            {
                await LogAsync("Warning", ActionTaskDenied, user, normalizedDeviceUuid, accountId, targetId, operation, requiredPermission, source, destructive, "device_owner_mismatch", metadata, httpContext);
                return DeviceOperationGuardResult.Deny("无权访问该设备");
            }
        }

        var normalizedAccountId = accountId?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(normalizedAccountId) && !await CanOperateAccountAsync(isAdmin, userIds, normalizedAccountId, normalizedDeviceUuid, cancellationToken))
        {
            await LogAsync("Warning", ActionTaskDenied, user, normalizedDeviceUuid, normalizedAccountId, targetId, operation, requiredPermission, source, destructive, "account_owner_mismatch", metadata, httpContext);
            return DeviceOperationGuardResult.Deny("无权操作该微信账号");
        }

        if (!await HasPermissionAsync(isAdmin, userIds.FirstOrDefault(), user, requiredPermission))
        {
            await LogAsync("Warning", ActionTaskDenied, user, normalizedDeviceUuid, normalizedAccountId, targetId, operation, requiredPermission, source, destructive, "missing_permission", metadata, httpContext);
            return DeviceOperationGuardResult.Deny("缺少高危操作权限");
        }

        await LogAsync("Info", ActionTaskQueued, user, normalizedDeviceUuid, normalizedAccountId, targetId, operation, requiredPermission, source, destructive, "allowed", metadata, httpContext);
        return DeviceOperationGuardResult.Allow();
    }

    /// <summary>
    /// 群操作权限映射。
    /// </summary>
    public static string GetGroupActionPermission(int action)
    {
        return action switch
        {
            2 => Permissions.GroupOperation.MemberAdd,
            3 => Permissions.GroupOperation.MemberKick,
            7 => Permissions.GroupOperation.Exit,
            10 or 12 or 13 => Permissions.GroupOperation.Manage,
            _ => Permissions.GroupOperation.Manage
        };
    }

    /// <summary>
    /// 记录设备删除已经完成的审计事件。
    /// <para>删除前的权限判断仍由 <see cref="CheckAsync"/> 完成；本方法只用于落最终结果，便于和 TaskQueued 区分。</para>
    /// </summary>
    public async Task RecordDeviceDeletedAsync(
        ClaimsPrincipal? user,
        string? deviceUuid,
        string source,
        IReadOnlyDictionary<string, object?>? metadata = null,
        HttpContext? httpContext = null)
    {
        var normalizedDeviceUuid = deviceUuid?.Trim() ?? string.Empty;
        await LogAsync(
            "Info",
            ActionDeviceDeleted,
            user,
            normalizedDeviceUuid,
            null,
            normalizedDeviceUuid,
            "DeleteDevice",
            Permissions.DeviceTask.DeleteDevice,
            source,
            destructive: true,
            decision: "deleted",
            metadata: metadata,
            httpContext: httpContext);
    }

    private async Task<bool> CanOperateAccountAsync(
        bool isAdmin,
        IReadOnlyList<string> userIds,
        string accountId,
        string deviceUuid,
        CancellationToken cancellationToken)
    {
        if (isAdmin)
        {
            return true;
        }

        var account = await _context.WechatAccounts
            .AsNoTracking()
            .Where(item => item.wxid == accountId && !item.isDeleted)
            .Select(item => new
            {
                item.wxid,
                item.ownerId,
                item.clientUuid
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (account == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(account.ownerId))
        {
            return IsOwnerMatched(account.ownerId, userIds);
        }

        return string.Equals(account.clientUuid, deviceUuid, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> HasPermissionAsync(bool isAdmin, string? userId, ClaimsPrincipal? user, string requiredPermission)
    {
        if (isAdmin || string.IsNullOrWhiteSpace(requiredPermission))
        {
            return true;
        }

        if (HasPermissionClaim(user, requiredPermission))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(userId) && await _authService.HasPermissionAsync(userId, requiredPermission);
    }

    private static bool HasPermissionClaim(ClaimsPrincipal? user, string requiredPermission)
    {
        return user?.Claims.Any(claim =>
            (claim.Type == "permission" || claim.Type == "permissions")
            && string.Equals(claim.Value, requiredPermission, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private async Task LogAsync(
        string level,
        string action,
        ClaimsPrincipal? user,
        string deviceUuid,
        string? accountId,
        string? targetId,
        string operation,
        string requiredPermission,
        string source,
        bool destructive,
        string decision,
        IReadOnlyDictionary<string, object?>? metadata,
        HttpContext? httpContext)
    {
        try
        {
            var operatorId = AccountAccessGuard.GetUserIdCandidates(user).FirstOrDefault()
                ?? user?.Identity?.Name
                ?? string.Empty;
            var message = JsonSerializer.Serialize(new
            {
                scope = "device-operation",
                operation,
                source,
                deviceUuid,
                accountId = accountId?.Trim() ?? string.Empty,
                targetId = targetId?.Trim() ?? string.Empty,
                requiredPermission,
                destructive,
                decision,
                metadata = metadata ?? new Dictionary<string, object?>(),
                userAgent = httpContext?.Request.Headers.UserAgent.ToString(),
                requestPath = httpContext?.Request.Path.Value,
                queryKeys = httpContext?.Request.Query.Keys.ToArray()
            }, AuditJsonOptions);

            await _systemLogService.LogAsync(
                level,
                ModuleName,
                action,
                message,
                Truncate(operatorId, 450),
                BuildAuditTargetId(accountId, deviceUuid),
                Truncate(httpContext?.Connection.RemoteIpAddress?.ToString(), 50));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入高危设备操作审计失败。Action={Action}, Operation={Operation}, DeviceUuid={DeviceUuid}", action, operation, deviceUuid);
        }
    }

    private static string? BuildAuditTargetId(string? accountId, string? deviceUuid)
    {
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            return Truncate($"wx:{accountId.Trim()}", 100);
        }

        if (!string.IsNullOrWhiteSpace(deviceUuid))
        {
            return Truncate($"device:{deviceUuid.Trim()}", 100);
        }

        return null;
    }

    private static bool IsOwnerMatched(string? ownerId, IReadOnlyList<string> userIds)
    {
        return !string.IsNullOrWhiteSpace(ownerId)
            && userIds.Any(userId => string.Equals(ownerId, userId, StringComparison.OrdinalIgnoreCase));
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
/// 高危设备操作校验结果。
/// </summary>
public sealed record DeviceOperationGuardResult(bool Allowed, TaskResult? Denied)
{
    public static DeviceOperationGuardResult Allow() => new(true, null);

    public static DeviceOperationGuardResult Deny(string message) => new(false, TaskResult.Fail(message));
}
