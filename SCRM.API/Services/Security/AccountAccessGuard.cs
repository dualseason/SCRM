using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.Services.Data;

namespace SCRM.API.Services.Security;

/// <summary>
/// 账号与设备访问边界校验。
/// <para>SignalR/Controller 入口只能传入 accountId 或 deviceUuid 时，统一在这里确认当前登录用户是否有权读取或下发任务。</para>
/// </summary>
public class AccountAccessGuard
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountAccessGuard> _logger;

    public AccountAccessGuard(ApplicationDbContext context, ILogger<AccountAccessGuard> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 判断当前用户是否是管理员角色。
    /// </summary>
    public static bool IsAdmin(ClaimsPrincipal? user)
    {
        return user?.IsInRole("SuperAdmin") == true || user?.IsInRole("Admin") == true;
    }

    /// <summary>
    /// 获取当前登录用户的候选标识。
    /// <para>历史代码同时用过 NameIdentifier、Context.UserIdentifier 与 Identity.Name，这里统一兼容，避免误拒绝老会话。</para>
    /// </summary>
    public static IReadOnlyList<string> GetUserIdCandidates(ClaimsPrincipal? user)
    {
        if (user == null)
        {
            return Array.Empty<string>();
        }

        var values = new[]
            {
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                user.FindFirst("sub")?.Value,
                user.FindFirst("nameid")?.Value,
                user.Identity?.Name
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return values;
    }

    /// <summary>
    /// 校验当前用户是否可访问指定微信账号。
    /// </summary>
    public async Task<bool> CanAccessAccountAsync(ClaimsPrincipal? user, string? accountId)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(accountId))
        {
            return false;
        }

        var userIds = GetUserIdCandidates(user);
        if (userIds.Count == 0)
        {
            return false;
        }

        var normalizedAccountId = accountId.Trim();
        var account = await _context.WechatAccounts
            .AsNoTracking()
            .Where(account => account.wxid == normalizedAccountId && !account.isDeleted)
            .Select(account => new
            {
                account.wxid,
                account.ownerId,
                account.clientUuid
            })
            .FirstOrDefaultAsync();

        if (account == null)
        {
            _logger.LogWarning("账号访问拒绝：账号不存在。AccountId={AccountId}", normalizedAccountId);
            return false;
        }

        if (IsOwnerMatched(account.ownerId, userIds))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(account.clientUuid))
        {
            var client = await _context.SrClients
                .AsNoTracking()
                .Where(client => client.uuid == account.clientUuid)
                .Select(client => new
                {
                    client.uuid,
                    client.ownerId
                })
                .FirstOrDefaultAsync();

            if (client == null)
            {
                _logger.LogWarning("账号访问拒绝：账号绑定设备不存在。AccountId={AccountId}, DeviceUuid={DeviceUuid}", normalizedAccountId, account.clientUuid);
                return false;
            }

            if (IsOwnerMatched(client.ownerId, userIds))
            {
                return true;
            }

            // 兼容历史数据：仅当账号自身也没有 owner 时，才把 ownerId 为空的历史设备视为公共账号来源。
            return string.IsNullOrWhiteSpace(account.ownerId) && string.IsNullOrWhiteSpace(client.ownerId);
        }

        // 兼容历史数据：早期未绑定 owner/client 的账号按公共设备处理，避免升级后直接不可见。
        return string.IsNullOrWhiteSpace(account.ownerId);
    }

    /// <summary>
    /// 获取当前用户可访问的微信账号 ID 列表。
    /// <para>REST 分页接口需要先收口账号范围，再做搜索和脱敏；这里与 CanAccessAccountAsync 保持同一套边界。</para>
    /// </summary>
    public async Task<List<string>> GetAccessibleAccountIdsAsync(ClaimsPrincipal? user)
    {
        if (IsAdmin(user))
        {
            return await _context.WechatAccounts
                .AsNoTracking()
                .Where(account => !account.isDeleted)
                .Select(account => account.wxid)
                .ToListAsync();
        }

        var userIds = GetUserIdCandidates(user);
        if (userIds.Count == 0)
        {
            return new List<string>();
        }

        var ownedDeviceIds = await _context.SrClients
            .AsNoTracking()
            .Where(client => client.ownerId != null && client.ownerId != string.Empty && userIds.Contains(client.ownerId))
            .Select(client => client.uuid)
            .ToListAsync();

        var ownerlessDeviceIds = await _context.SrClients
            .AsNoTracking()
            .Where(client => client.ownerId == null || client.ownerId == string.Empty)
            .Select(client => client.uuid)
            .ToListAsync();

        return await _context.WechatAccounts
            .AsNoTracking()
            .Where(account => !account.isDeleted
                && (
                    (account.ownerId != null && account.ownerId != string.Empty && userIds.Contains(account.ownerId))
                    || (account.clientUuid != null && account.clientUuid != string.Empty && ownedDeviceIds.Contains(account.clientUuid))
                    || ((account.ownerId == null || account.ownerId == string.Empty)
                        && (
                            account.clientUuid == null
                            || account.clientUuid == string.Empty
                            || ownerlessDeviceIds.Contains(account.clientUuid)
                        ))
                ))
            .Select(account => account.wxid)
            .ToListAsync();
    }

    /// <summary>
    /// 获取当前用户可访问的设备 UUID 列表。
    /// <para>审计查询等跨表读取入口需要先收口设备范围；这里与 CanAccessDeviceAsync 保持同一套历史 ownerless 设备兼容规则。</para>
    /// </summary>
    public async Task<List<string>> GetAccessibleDeviceIdsAsync(ClaimsPrincipal? user)
    {
        if (IsAdmin(user))
        {
            return await _context.SrClients
                .AsNoTracking()
                .Select(client => client.uuid)
                .ToListAsync();
        }

        var userIds = GetUserIdCandidates(user);
        if (userIds.Count == 0)
        {
            return new List<string>();
        }

        return await _context.SrClients
            .AsNoTracking()
            .Where(client => client.ownerId == null
                || client.ownerId == string.Empty
                || (client.ownerId != null && userIds.Contains(client.ownerId)))
            .Select(client => client.uuid)
            .ToListAsync();
    }

    /// <summary>
    /// 校验当前用户是否可访问指定设备。
    /// </summary>
    public async Task<bool> CanAccessDeviceAsync(ClaimsPrincipal? user, string? deviceUuid)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(deviceUuid))
        {
            return false;
        }

        var userIds = GetUserIdCandidates(user);
        if (userIds.Count == 0)
        {
            return false;
        }

        var normalizedDeviceUuid = deviceUuid.Trim();
        var client = await _context.SrClients
            .AsNoTracking()
            .Where(client => client.uuid == normalizedDeviceUuid)
            .Select(client => new
            {
                client.uuid,
                client.ownerId
            })
            .FirstOrDefaultAsync();

        if (client == null)
        {
            _logger.LogWarning("设备访问拒绝：设备不存在。DeviceUuid={DeviceUuid}", normalizedDeviceUuid);
            return false;
        }

        // 与 AuthService.GetDevicesForUserAsync 保持一致：ownerId 为空的历史设备视为可见。
        return string.IsNullOrWhiteSpace(client.ownerId) || IsOwnerMatched(client.ownerId, userIds);
    }

    /// <summary>
    /// 判断 ownerId 是否命中当前用户的任一候选标识。
    /// </summary>
    private static bool IsOwnerMatched(string? ownerId, IReadOnlyList<string> userIds)
    {
        return !string.IsNullOrWhiteSpace(ownerId)
            && userIds.Any(userId => string.Equals(ownerId, userId, StringComparison.OrdinalIgnoreCase));
    }
}
