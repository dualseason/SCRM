using SCRM.Shared.Interfaces;

using SCRM.API.Models.Entities;

using SCRM.SHARED.Models.Dtos;

using Microsoft.AspNetCore.Components.Authorization;

using Microsoft.EntityFrameworkCore;

using SCRM.Services.Data; // For ApplicationDbContext

using System.Collections.Generic;

using System.Security.Claims;

using System.Threading.Tasks;

using SCRM.SHARED.Models;

using System.Text.Json;

using SCRM.API.Services.Data;

using SCRM.API.Services.Security;
using SCRM.Models.Constants;



namespace SCRM.API.Services.Core

{

    public class CrmService : ICrmService

    {

        private const string FinderResultTypeMention = "mention";

        private const string FinderResultTypeUserPage = "userpage";

        private const string FinderResultTypeComment = "comment";

        private readonly ServerDeviceCommandService _deviceCommandService;

        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

        private readonly AuthenticationStateProvider _authenticationStateProvider;

        private readonly AccountAccessGuard _accountAccessGuard;

        private readonly SensitiveMaskingService _sensitiveMaskingService;

        private readonly MediaAccessTokenService _mediaAccessTokenService;

        private readonly SensitiveMediaAccessAuditService _sensitiveMediaAccessAuditService;

        private readonly SensitiveMediaAccessAuditQueryService _sensitiveMediaAccessAuditQueryService;

        private readonly SensitiveDataAccessAuditService _sensitiveDataAccessAuditService;

        private readonly SensitiveWordPolicyService _sensitiveWordPolicyService;

        private readonly SensitiveContentGuard _sensitiveContentGuard;

        private readonly DeviceOperationGuard _deviceOperationGuard;

        private readonly ILogger<CrmService> _logger;

        

        public CrmService(
            ServerDeviceCommandService deviceCommandService,
            IDbContextFactory<ApplicationDbContext> dbContextFactory,
            AuthenticationStateProvider authenticationStateProvider,
            AccountAccessGuard accountAccessGuard,
            SensitiveMaskingService sensitiveMaskingService,
            MediaAccessTokenService mediaAccessTokenService,
            SensitiveMediaAccessAuditService sensitiveMediaAccessAuditService,
            SensitiveMediaAccessAuditQueryService sensitiveMediaAccessAuditQueryService,
            SensitiveDataAccessAuditService sensitiveDataAccessAuditService,
            SensitiveWordPolicyService sensitiveWordPolicyService,
            SensitiveContentGuard sensitiveContentGuard,
            DeviceOperationGuard deviceOperationGuard,
            ILogger<CrmService> logger)

        {

            _deviceCommandService = deviceCommandService;

            _dbContextFactory = dbContextFactory;

            _authenticationStateProvider = authenticationStateProvider;

            _accountAccessGuard = accountAccessGuard;

            _sensitiveMaskingService = sensitiveMaskingService;

            _mediaAccessTokenService = mediaAccessTokenService;

            _sensitiveMediaAccessAuditService = sensitiveMediaAccessAuditService;

            _sensitiveMediaAccessAuditQueryService = sensitiveMediaAccessAuditQueryService;

            _sensitiveDataAccessAuditService = sensitiveDataAccessAuditService;

            _sensitiveWordPolicyService = sensitiveWordPolicyService;

            _sensitiveContentGuard = sensitiveContentGuard;

            _deviceOperationGuard = deviceOperationGuard;

            _logger = logger;

        }

        /// <summary>
        /// 获取当前 Blazor/HTTP 调用用户。
        /// <para>CrmService 是 UI 直连服务入口，不能默认拥有全部数据读取权；取不到用户时按无权限处理。</para>
        /// </summary>
        private async Task<ClaimsPrincipal?> GetCurrentUserAsync()
        {
            try
            {
                var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
                return state.User;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CrmService 获取当前用户失败，按无权限处理。");
                return null;
            }
        }

        /// <summary>
        /// UI 直连服务下发设备任务前的统一边界：先校验设备归属，再做服务端敏感词风控。
        /// </summary>
        private async Task<TaskResult?> DenyIfDeviceOrSensitiveContentAsync(
            string? deviceUuid,
            string scene,
            string targetId,
            IReadOnlyDictionary<string, string> contents,
            string accountId = "")
        {
            var normalizedDeviceUuid = deviceUuid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedDeviceUuid))
            {
                return TaskResult.Fail("设备ID为空");
            }

            var user = await GetCurrentUserAsync();
            if (!await _accountAccessGuard.CanAccessDeviceAsync(user, normalizedDeviceUuid))
            {
                _logger.LogWarning("CrmService {Scene} 拒绝越权设备下发。DeviceUuid={DeviceUuid}", scene, normalizedDeviceUuid);
                return TaskResult.Fail("无权访问该设备");
            }

            var normalizedAccountId = accountId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedAccountId))
            {
                normalizedAccountId = await GetPrimaryWechatIdAsync(normalizedDeviceUuid);
            }

            var check = await _sensitiveContentGuard.CheckAsync(
                user,
                scene,
                normalizedDeviceUuid,
                normalizedAccountId,
                targetId,
                contents,
                $"CrmService.{scene}");

            return check.Allowed ? null : TaskResult.Fail(check.Message);
        }

        /// <summary>
        /// UI 直连服务设备任务下发前的统一设备归属校验。
        /// <para>用于无文字内容的设备任务；发送类任务继续走 DenyIfDeviceOrSensitiveContentAsync。</para>
        /// </summary>
        private async Task<TaskResult?> DenyIfNoDeviceAccessAsync(string? deviceUuid, string operation)
        {
            var normalizedDeviceUuid = deviceUuid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedDeviceUuid))
            {
                return TaskResult.Fail("设备ID为空");
            }

            var user = await GetCurrentUserAsync();
            if (!await _accountAccessGuard.CanAccessDeviceAsync(user, normalizedDeviceUuid))
            {
                _logger.LogWarning("CrmService {Operation} 拒绝越权设备下发。DeviceUuid={DeviceUuid}", operation, normalizedDeviceUuid);
                return TaskResult.Fail("无权访问该设备");
            }

            return null;
        }

        /// <summary>
        /// UI 直连服务高危设备操作下发前的统一授权与审计。
        /// <para>比普通设备归属更严格：非管理员不能操作未分配 owner 的历史设备，并且必须具备对应操作权限。</para>
        /// </summary>
        private async Task<TaskResult?> DenyIfHighRiskDeviceOperationAsync(
            string? deviceUuid,
            string operation,
            string requiredPermission,
            string targetId = "",
            string accountId = "",
            bool destructive = false,
            IReadOnlyDictionary<string, object?>? metadata = null)
        {
            var user = await GetCurrentUserAsync();
            var result = await _deviceOperationGuard.CheckAsync(
                user,
                deviceUuid,
                operation,
                requiredPermission,
                $"CrmService.{operation}",
                accountId,
                targetId,
                destructive,
                metadata);

            return result.Allowed ? null : result.Denied;
        }

        /// <summary>
        /// 构造待风控字段字典；只传字段名和值给风控，审计时不会记录字段原文。
        /// </summary>
        private static IReadOnlyDictionary<string, string> ContentFields(params (string field, string? value)[] fields)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (field, value) in fields)
            {
                var normalizedField = field?.Trim() ?? string.Empty;
                var normalizedValue = value?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedField) || string.IsNullOrWhiteSpace(normalizedValue))
                {
                    continue;
                }

                result[normalizedField] = normalizedValue;
            }

            return result;
        }

        /// <summary>
        /// 标准化高级发圈请求，避免 UI/JSON 传入 null 集合导致任务构造异常。
        /// </summary>
        private static MomentPostRequestDto NormalizeMomentPostRequestForService(MomentPostRequestDto? request)
        {
            return MomentPostRequestValidator.Normalize(request);
        }

        /// <summary>
        /// 构造高级发圈的文本风控字段。
        /// <para>只交给风控服务做匹配；审计仍只记录字段名、长度和命中词，不记录字段原文。</para>
        /// </summary>
        private static IReadOnlyDictionary<string, string> MomentPostContentFields(MomentPostRequestDto request)
        {
            var fields = new Dictionary<string, string>(
                ContentFields(
                    ("content", request.content),
                    ("comment", request.comment),
                    ("poiCity", request.poi.city),
                    ("poiName", request.poi.name),
                    ("poiAddress", request.poi.address)),
                StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < request.extComment.Count; index++)
            {
                var value = request.extComment[index];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    fields[$"extComment[{index}]"] = value;
                }
            }

            return fields;
        }

        /// <summary>
        /// 构造高级发圈审计 metadata。
        /// <para>只记录类型、数量和布尔状态，不记录附件 URL、好友 wxid、标签、提醒人或 POI 原文。</para>
        /// </summary>
        private static Dictionary<string, object?> MomentPostAuditMetadata(MomentPostRequestDto request)
        {
            return MomentPostRequestValidator.BuildSafeAuditMetadata(request);
        }

        /// <summary>
        /// 补充账号绑定上下文到高级发圈审计 metadata。
        /// <para>只写 hash 和布尔，不写请求 wxid、当前 wxid 或执行 wxid 原文。</para>
        /// </summary>
        private static void EnrichMomentPostAuditMetadata(
            Dictionary<string, object?> metadata,
            string? requestedWeChatId,
            string? currentWeChatId,
            string? effectiveWeChatId,
            bool hasExplicitWeChatId,
            int validationWarningCount)
        {
            metadata["hasExplicitWeChatId"] = hasExplicitWeChatId;
            metadata["requestedWeChatIdHash"] = MomentPostRequestValidator.HashAuditValue(requestedWeChatId);
            metadata["currentWeChatIdHash"] = MomentPostRequestValidator.HashAuditValue(currentWeChatId);
            metadata["effectiveWeChatIdHash"] = MomentPostRequestValidator.HashAuditValue(effectiveWeChatId);
            metadata["weChatIdBound"] = MomentPostRequestValidator.IsSameWechatId(effectiveWeChatId, currentWeChatId);
            metadata["validationWarningCount"] = validationWarningCount;
        }

        private static List<string> NormalizeStringList(IEnumerable<string>? values)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (values == null)
            {
                return result;
            }

            foreach (var value in values)
            {
                var normalized = value?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalized) || !seen.Add(normalized))
                {
                    continue;
                }

                result.Add(normalized);
            }

            return result;
        }

        private static bool HasMomentPoi(MomentPostPoiDto poi)
        {
            return !string.IsNullOrWhiteSpace(poi.city)
                || !string.IsNullOrWhiteSpace(poi.name)
                || !string.IsNullOrWhiteSpace(poi.address)
                || !string.IsNullOrWhiteSpace(poi.poiId)
                || Math.Abs(poi.lat) > 0.000001f
                || Math.Abs(poi.lng) > 0.000001f;
        }

        /// <summary>
        /// 构造转发旧消息的风控字段。
        /// <para>除转发附言外，尽量从服务端历史消息表读取原消息正文/XML 一并检查，避免按 msgSvrId 转发旧敏感消息绕过发送前风控。</para>
        /// </summary>
        private async Task<IReadOnlyDictionary<string, string>> BuildForwardMessageContentFieldsAsync(
            string? deviceUuid,
            string? accountId,
            string? talker,
            IEnumerable<long>? msgSvrIds,
            string? extMsg,
            string? directContent = null)
        {
            var fields = new Dictionary<string, string>(ContentFields(("extMsg", extMsg), ("content", directContent)), StringComparer.OrdinalIgnoreCase);
            var ids = (msgSvrIds ?? Array.Empty<long>())
                .Where(id => id > 0)
                .Distinct()
                .Take(20)
                .ToList();
            if (ids.Count == 0)
            {
                return fields;
            }

            var normalizedAccountId = accountId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedAccountId) && !string.IsNullOrWhiteSpace(deviceUuid))
            {
                normalizedAccountId = await GetPrimaryWechatIdAsync(deviceUuid.Trim());
            }

            if (string.IsNullOrWhiteSpace(normalizedAccountId))
            {
                return fields;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var normalizedTalker = talker?.Trim() ?? string.Empty;
            var query = db.Messages
                .AsNoTracking()
                .Where(message => message.accountId == normalizedAccountId
                    && message.msgSvrId.HasValue
                    && ids.Contains(message.msgSvrId.Value)
                    && !message.isDeleted
                    && !message.isRevoked);

            if (!string.IsNullOrWhiteSpace(normalizedTalker))
            {
                query = query.Where(message => message.senderWxid == normalizedTalker || message.receiverWxid == normalizedTalker);
            }

            var messages = await query
                .OrderByDescending(message => message.sentAt ?? message.receivedAt ?? message.createdAt)
                .ThenByDescending(message => message.messageId)
                .Take(20)
                .Select(message => new
                {
                    message.content,
                    message.contentXml
                })
                .ToListAsync();

            for (var index = 0; index < messages.Count; index++)
            {
                var message = messages[index];
                AddContentField(fields, $"originalContent{index}", message.content);
                AddContentField(fields, $"originalContentXml{index}", message.contentXml);
            }

            return fields;
        }

        private static void AddContentField(IDictionary<string, string> fields, string field, string? value)
        {
            var normalizedValue = value?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(field) && !string.IsNullOrWhiteSpace(normalizedValue))
            {
                fields[field] = normalizedValue;
            }
        }

        /// <summary>
        /// 查询设备当前在线微信账号，用于合并账号级敏感词策略。
        /// </summary>
        private async Task<string> GetPrimaryWechatIdAsync(string deviceUuid)
        {
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return string.Empty;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var normalizedDeviceUuid = deviceUuid.Trim();
            var account = await db.WechatAccounts
                .AsNoTracking()
                .OrderByDescending(item => item.lastOnlineAt)
                .ThenByDescending(item => item.updatedAt)
                .FirstOrDefaultAsync(item => item.clientUuid == normalizedDeviceUuid && !item.isDeleted && item.accountStatus == 1);

            return account?.wxid ?? string.Empty;
        }



        public async Task<List<SrClient>> GetDevicesAsync()
        {
            var user = await GetCurrentUserAsync();
            var devices = DbHelper.GetAllSrClients();

            if (!AccountAccessGuard.IsAdmin(user))
            {
                var userIds = AccountAccessGuard.GetUserIdCandidates(user);
                if (userIds.Count == 0)
                {
                    return new List<SrClient>();
                }

                // GlobalCache 中的历史 ownerless 设备继续可见；显式归属他人的设备必须过滤掉。
                devices = devices
                    .Where(device => string.IsNullOrWhiteSpace(device.ownerId)
                        || userIds.Any(userId => string.Equals(device.ownerId, userId, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            return await _sensitiveMaskingService.MaskDevicesAsync(user, devices);
        }



        public async Task<SrClient?> GetDeviceAsync(string uuid)
        {
            var user = await GetCurrentUserAsync();
            var normalizedUuid = uuid?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedUuid))
            {
                return null;
            }

            if (!await _accountAccessGuard.CanAccessDeviceAsync(user, normalizedUuid))
            {
                _logger.LogWarning("CrmService GetDeviceAsync 拒绝越权设备读取。DeviceUuid={DeviceUuid}", normalizedUuid);
                return null;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var device = await db.GetSrClient(normalizedUuid);
            if (device == null)
            {
                return null;
            }

            return (await _sensitiveMaskingService.MaskDevicesAsync(user, new[] { device })).FirstOrDefault();
        }

        /// <summary>
        /// 删除设备。
        /// <para>
        /// 如果设备在线，先下发 PostDeleteDeviceNotice(1097) 让 Android 主动断开；
        /// 随后通过 DbHelper.DeleteSrClient 删除本地记录并同步 GlobalCache。
        /// </para>
        /// </summary>
        public async Task<bool> DeleteDeviceAsync(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
            {
                return false;
            }

            if (await DenyIfHighRiskDeviceOperationAsync(
                    uuid,
                    nameof(DeleteDeviceAsync),
                    Permissions.DeviceTask.DeleteDevice,
                    uuid,
                    destructive: true) is not null)
            {
                return false;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var device = await db.GetSrClient(uuid.Trim());
            if (device == null)
            {
                return false;
            }

            // 1097 无回执；通知失败不阻塞本地删除，避免离线设备无法从后台移除。
            try
            {
                await _deviceCommandService.NotifyDeviceDeleteAsync(device.uuid);
            }
            catch
            {
                // 删除设备是后台管理动作，通知失败只影响客户端主动断开，不影响本地清理。
            }

            await db.DeleteSrClient(device);
            await _deviceOperationGuard.RecordDeviceDeletedAsync(
                await GetCurrentUserAsync(),
                device.uuid,
                $"CrmService.{nameof(DeleteDeviceAsync)}");
            return true;
        }

        /// <summary>
        /// 下发设备 App 升级通知。
        /// <para>该动作不改 AppVersion 表；只把 1094 通知写入在线设备 TCP 通道。</para>
        /// </summary>
        public async Task<TaskResult> UpgradeDeviceAppAsync(
            string deviceUuid,
            string packageName,
            string version,
            int versionCode,
            string packageUrl,
            string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(UpgradeDeviceAppAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.UpgradeDeviceAppAsync(
                deviceUuid,
                packageName,
                version,
                versionCode,
                packageUrl,
                weChatId);
        }



        // --- Phase 3 Implementation ---

        

        public async Task<List<Contact>> GetContactsAsync(string? accountId = null)
        {
            var user = await GetCurrentUserAsync();
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            if (string.IsNullOrWhiteSpace(accountId))
            {
                var accessibleAccountIds = await _accountAccessGuard.GetAccessibleAccountIdsAsync(user);
                if (accessibleAccountIds.Count == 0)
                {
                    return new List<Contact>();
                }

                // 联系人不走 GlobalCache，读取全部联系人时直接查库。
                // 这里主要服务 /contacts/all 页面，必须带出所属微信账号、设备和归属用户，
                // 否则总览页无法下发“删除好友”等需要 deviceUuid 的操作。
                var contacts = await db.Contacts
                    .AsNoTracking()
                    .Include(c => c.Account)
                        .ThenInclude(a => a!.Client)
                    .Include(c => c.Account)
                        .ThenInclude(a => a!.owner)
                    .Where(c => !c.isDeleted && accessibleAccountIds.Contains(c.ownerWxid))
                    .OrderByDescending(c => c.updatedAt)
                    .ToListAsync();
                return await _sensitiveMaskingService.MaskContactsAsync(user, contacts);
            }

            accountId = accountId.Trim();
            if (!await _accountAccessGuard.CanAccessAccountAsync(user, accountId))
            {
                _logger.LogWarning("CrmService GetContactsAsync 拒绝越权账号读取。AccountId={AccountId}", accountId);
                return new List<Contact>();
            }

            var accountContacts = await db.GetContacts(accountId);
            return await _sensitiveMaskingService.MaskContactsAsync(user, accountContacts);
        }

        public async Task<List<Conversation>> GetConversationsAsync(string? accountId = null)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return new List<Conversation>();
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            accountId = accountId.Trim();
            var user = await GetCurrentUserAsync();
            if (!await _accountAccessGuard.CanAccessAccountAsync(user, accountId))
            {
                _logger.LogWarning("CrmService GetConversationsAsync 拒绝越权账号读取。AccountId={AccountId}", accountId);
                return new List<Conversation>();
            }

            var conversations = await db.Conversations
                .AsNoTracking()
                .Where(c => c.wechatAccountId == accountId && !c.isDeleted)
                .OrderByDescending(c => c.lastMessageTime == default ? c.updatedAt : c.lastMessageTime)
                .Take(100)
                .ToListAsync();
            return await _sensitiveMaskingService.MaskConversationsAsync(user, conversations);
        }

        public async Task<List<Message>> GetMessagesAsync(string accountId, string conversationId, int count = 50)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(conversationId))
            {
                return new List<Message>();
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            accountId = accountId.Trim();
            conversationId = conversationId.Trim();
            count = Math.Clamp(count, 1, 500);
            var user = await GetCurrentUserAsync();
            if (!await _accountAccessGuard.CanAccessAccountAsync(user, accountId))
            {
                _logger.LogWarning("CrmService GetMessagesAsync 拒绝越权账号读取。AccountId={AccountId}", accountId);
                return new List<Message>();
            }

            var messages = await db.Messages
                .AsNoTracking()
                .Where(m => m.accountId == accountId
                    && (m.senderWxid == conversationId
                        || m.receiverWxid == conversationId
                        || (m.chatType == 2
                            && (m.senderWxid == conversationId || m.receiverWxid == conversationId)))
                    && !m.isDeleted)
                .OrderByDescending(m => m.createdAt)
                .Take(count)
                .OrderBy(m => m.createdAt)
                .ToListAsync();

            await db.EnrichMessagesWithMediaMetadataAsync(messages);
            return await _sensitiveMaskingService.MaskMessagesAsync(user, messages);
        }

        /// <summary>
        /// 获取指定群聊的成员列表。
        /// <para>只读查询，不触碰 DbHelper 缓存；成员入库仍由群资料同步链路和 DbHelper.SaveChatRooms 负责。</para>
        /// </summary>
        public async Task<List<GroupMemberDto>> GetGroupMembersAsync(string accountId, string chatRoomId)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(chatRoomId))
            {
                return new List<GroupMemberDto>();
            }

            var user = await GetCurrentUserAsync();
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            accountId = accountId.Trim();
            if (!await _accountAccessGuard.CanAccessAccountAsync(user, accountId))
            {
                _logger.LogWarning("CrmService GetGroupMembersAsync 拒绝越权账号读取。AccountId={AccountId}", accountId);
                return new List<GroupMemberDto>();
            }

            var accountKey = BuildWechatAccountNumericKey(accountId);
            var roomId = chatRoomId.Trim();

            var group = await db.Set<Group>()
                .AsNoTracking()
                .Where(g => g.groupWxid == roomId
                    && !g.isDeleted
                    && (g.wechatAccountId == accountKey || g.wechatAccountId == 0))
                .OrderByDescending(g => g.wechatAccountId == accountKey)
                .ThenByDescending(g => g.updatedAt)
                .FirstOrDefaultAsync();

            if (group == null)
            {
                return new List<GroupMemberDto>();
            }

            var members = await db.Set<GroupMember>()
                .AsNoTracking()
                .Where(m => m.groupId == group.id && !m.isDeleted)
                .OrderByDescending(m => m.memberRole)
                .ThenBy(m => string.IsNullOrWhiteSpace(m.memberRemarks) ? m.memberNickname : m.memberRemarks)
                .Select(m => new GroupMemberDto
                {
                    memberWxid = m.memberWxid,
                    memberNickname = m.memberNickname,
                    memberAvatar = m.memberAvatar ?? string.Empty,
                    alias = m.alias ?? string.Empty,
                    memberRemarks = m.memberRemarks,
                    memberRole = m.memberRole
                })
                .ToListAsync();
            return await _sensitiveMaskingService.MaskGroupMembersAsync(user, members);
        }

        /// <summary>
        /// 读取指定微信账号的群邀请审批列表。
        /// <para>该方法只读查询；群邀请写入统一由 GroupMessageHandler + DbHelper.SaveGroupInvitations 完成。</para>
        /// </summary>
        public async Task<List<GroupInvitationDto>> GetGroupInvitationsAsync(string accountId, string chatRoomId = "", int count = 100, bool pendingOnly = false)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return new List<GroupInvitationDto>();
            }

            var user = await GetCurrentUserAsync();
            accountId = accountId.Trim();
            if (!await _accountAccessGuard.CanAccessAccountAsync(user, accountId))
            {
                _logger.LogWarning("CrmService GetGroupInvitationsAsync 拒绝越权账号读取。AccountId={AccountId}", accountId);
                return new List<GroupInvitationDto>();
            }

            count = Math.Clamp(count, 1, 200);
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var invitations = await db.GetGroupInvitations(accountId, chatRoomId, count, pendingOnly);
            return await _sensitiveMaskingService.MaskGroupInvitationsAsync(user, invitations);
        }

        private static long BuildWechatAccountNumericKey(string ownerWxid)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid))
            {
                return 0;
            }

            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (char ch in ownerWxid)
                {
                    hash ^= ch;
                    hash *= 1099511628211UL;
                }

                return (long)(hash & 0x7FFFFFFFFFFFFFFFUL);
            }
        }



        public async Task<TaskResult> SendMessageAsync(string deviceUuid, string conversationId, string content, int type = 1, string atIds = "")

        {

            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(SendMessageAsync),
                    "conversation",
                    ContentFields(("content", content), ("atIds", atIds))) is { } denied)
            {
                return denied;
            }

            // Delegate to DeviceCommandService (Netty)

            // Assuming ServerDeviceCommandService has SendMessageAsync

            return await _deviceCommandService.SendMessageAsync(deviceUuid, conversationId, content, type, atIds);

        }



        public async Task<TaskResult> RequestScreenShotAsync(string deviceUuid)

        {
            if (await DenyIfHighRiskDeviceOperationAsync(
                    deviceUuid,
                    nameof(RequestScreenShotAsync),
                    Permissions.DeviceTask.Screenshot,
                    destructive: false) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.RequestScreenShotAsync(deviceUuid);

        }

        /// <summary>
        /// 为设备截图 URL 签发短期预览 token。
        /// <para>截图属于设备级敏感画面，签发前必须校验设备归属，并要求上传路径能推断到同一设备。</para>
        /// </summary>
        public async Task<MediaAccessTokenDto> CreateScreenshotAccessTokenAsync(string screenshotUrl, string deviceUuid = "", int expiresMinutes = 5)
        {
            if (string.IsNullOrWhiteSpace(screenshotUrl))
            {
                return MediaAccessTokenDto.Fail("截图 URL 不能为空。");
            }

            var user = await GetCurrentUserAsync();
            var normalizedDeviceUuid = deviceUuid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedDeviceUuid))
            {
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.ScreenshotScope,
                    null,
                    "设备 ID 不能为空。",
                    nameof(CreateScreenshotAccessTokenAsync));
                return MediaAccessTokenDto.Fail("设备 ID 不能为空。");
            }

            if (!await _accountAccessGuard.CanAccessDeviceAsync(user, normalizedDeviceUuid))
            {
                _logger.LogWarning("CrmService CreateScreenshotAccessTokenAsync 拒绝越权截图预览。DeviceUuid={DeviceUuid}", normalizedDeviceUuid);
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.ScreenshotScope,
                    null,
                    "无权访问该设备。",
                    nameof(CreateScreenshotAccessTokenAsync),
                    deviceUuid: normalizedDeviceUuid);
                return MediaAccessTokenDto.Fail("无权访问该设备。");
            }

            if (!_mediaAccessTokenService.TryNormalizeUploadRelativePath(screenshotUrl, out var relativePath, out var error))
            {
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.ScreenshotScope,
                    null,
                    error,
                    nameof(CreateScreenshotAccessTokenAsync),
                    deviceUuid: normalizedDeviceUuid);
                return MediaAccessTokenDto.Fail(error);
            }

            var inferredTarget = MediaAccessTokenService.InferTarget(relativePath);
            if (inferredTarget.Kind != MediaAccessTargetKind.Device)
            {
                _logger.LogWarning(
                    "CrmService CreateScreenshotAccessTokenAsync 拒绝非设备截图路径。RelativePath={RelativePath}, TargetKind={TargetKind}, TargetValue={TargetValue}",
                    relativePath,
                    inferredTarget.Kind,
                    inferredTarget.Value);
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.ScreenshotScope,
                    relativePath,
                    "截图路径缺少设备归属。",
                    nameof(CreateScreenshotAccessTokenAsync),
                    deviceUuid: normalizedDeviceUuid);
                return MediaAccessTokenDto.Fail("截图路径缺少设备归属。");
            }

            if (!string.Equals(inferredTarget.Value, normalizedDeviceUuid, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "CrmService CreateScreenshotAccessTokenAsync 拒绝设备与截图路径不一致。DeviceUuid={DeviceUuid}, PathDevice={PathDevice}",
                    normalizedDeviceUuid,
                    inferredTarget.Value);
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.ScreenshotScope,
                    relativePath,
                    "截图不属于当前设备。",
                    nameof(CreateScreenshotAccessTokenAsync),
                    deviceUuid: normalizedDeviceUuid);
                return MediaAccessTokenDto.Fail("截图不属于当前设备。");
            }

            if (!await _accountAccessGuard.CanAccessDeviceAsync(user, inferredTarget.Value))
            {
                _logger.LogWarning("CrmService CreateScreenshotAccessTokenAsync 拒绝路径归属校验失败的截图访问。PathDevice={PathDevice}", inferredTarget.Value);
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.ScreenshotScope,
                    relativePath,
                    "无权访问该截图。",
                    nameof(CreateScreenshotAccessTokenAsync),
                    deviceUuid: normalizedDeviceUuid);
                return MediaAccessTokenDto.Fail("无权访问该截图。");
            }

            var lifetimeMinutes = Math.Clamp(expiresMinutes, 1, 30);
            var userId = AccountAccessGuard.GetUserIdCandidates(user).FirstOrDefault()
                ?? user?.Identity?.Name
                ?? string.Empty;
            var issued = _mediaAccessTokenService.CreateToken(
                relativePath,
                MediaAccessTokenService.ScreenshotScope,
                userId,
                TimeSpan.FromMinutes(lifetimeMinutes));
            var openUrl = $"/api/media-access/open?token={Uri.EscapeDataString(issued.Token)}";

            var result = MediaAccessTokenDto.Ok(
                issued.Token,
                openUrl,
                issued.RelativePath,
                issued.Scope,
                issued.ExpiresAt);
            await _sensitiveMediaAccessAuditService.LogTokenIssuedAsync(
                user,
                result,
                nameof(CreateScreenshotAccessTokenAsync),
                deviceUuid: normalizedDeviceUuid);
            return result;
        }



        public async Task<WechatAccountSettings> GetAccountSettingsAsync(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return new WechatAccountSettings();
            }

            accountId = accountId.Trim();
            var user = await GetCurrentUserAsync();
            if (!await _accountAccessGuard.CanAccessAccountAsync(user, accountId))
            {
                _logger.LogWarning("CrmService GetAccountSettingsAsync 拒绝越权账号读取。AccountId={AccountId}", accountId);
                return new WechatAccountSettings();
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var account = await db.WechatAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.wxid == accountId);

            if (account == null || string.IsNullOrEmpty(account.settings))
            {
                return new WechatAccountSettings();
            }

            try
            {
                return JsonSerializer.Deserialize<WechatAccountSettings>(account.settings) ?? new WechatAccountSettings();
            }
            catch
            {
                return new WechatAccountSettings();
            }
        }

        public async Task<bool> UpdateAccountSettingsAsync(string accountId, WechatAccountSettings settings)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return false;
            }

            accountId = accountId.Trim();
            var user = await GetCurrentUserAsync();
            if (!await _accountAccessGuard.CanAccessAccountAsync(user, accountId))
            {
                _logger.LogWarning("CrmService UpdateAccountSettingsAsync 拒绝越权账号配置修改。AccountId={AccountId}", accountId);
                return false;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var account = await db.WechatAccounts.FirstOrDefaultAsync(a => a.wxid == accountId);

            if (account == null) return false;

            try
            {
                account.settings = JsonSerializer.Serialize(settings);
                account.updatedAt = DateTime.UtcNow;

                db.WechatAccounts.Update(account);
                await db.SaveChangesAsync();

                // 推送到已连接的客户端；账号配置已先保存到数据库，避免推送失败影响持久化。
                if (!string.IsNullOrEmpty(account.clientUuid))
                {
                    await _deviceCommandService.PushAccountSettingsAsync(account.clientUuid, settings);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CrmService] Error updating account settings: {ex.Message}");
                return false;
            }
        }



        public async Task<TaskResult> SendGroupMessageAsync(string deviceUuid, List<string> friendIds, string content, int contentType = 0, int duration = 0, bool original = false)

        {

            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(SendGroupMessageAsync),
                    "mass-send",
                    ContentFields(("content", content))) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.SendGroupMessageAsync(deviceUuid, friendIds, content, contentType, duration, original);

        }

        /// <summary>
        /// 请求设备同步微信“群发助手”历史。
        /// </summary>
        public async Task<TaskResult> SyncMassSendHistoryAsync(string deviceUuid, long endTime = 0, string weChatId = "")
        {
            var user = await GetCurrentUserAsync();
            if (!await _accountAccessGuard.CanAccessDeviceAsync(user, deviceUuid))
            {
                _logger.LogWarning("CrmService SyncMassSendHistoryAsync 拒绝越权设备下发。DeviceUuid={DeviceUuid}", deviceUuid);
                return TaskResult.Fail("无权访问该设备");
            }

            return await _deviceCommandService.SyncMassSendHistoryAsync(deviceUuid, endTime, weChatId);
        }

        /// <summary>
        /// 读取联系人标签字典快照。
        /// <para>
        /// 标签写入由 TaskMessageHandler 调用 DbHelper 完成；这里仅复用 DbHelper 的账号键计算和查询口径，
        /// 不直接修改 ContactTags / ContactTagRelations。
        /// </para>
        /// </summary>
        public async Task<List<ContactLabelDto>> GetContactLabelsAsync(string accountId, bool includeDeleted = false)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return new List<ContactLabelDto>();
            }

            var user = await GetCurrentUserAsync();
            accountId = accountId.Trim();
            if (!await _accountAccessGuard.CanAccessAccountAsync(user, accountId))
            {
                _logger.LogWarning("CrmService GetContactLabelsAsync 拒绝越权账号读取。AccountId={AccountId}", accountId);
                return new List<ContactLabelDto>();
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            return await db.GetContactLabels(accountId, includeDeleted);
        }

        /// <summary>
        /// 读取群发助手历史。
        /// <para>只读查询，不修改 DbHelper 维护的持久化缓存；写入仍由 DbHelper.SaveMassSendHistory 统一完成。</para>
        /// </summary>
        public async Task<List<MassSendHistoryDto>> GetMassSendHistoryAsync(string accountId, int count = 50)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return new List<MassSendHistoryDto>();
            }

            var user = await GetCurrentUserAsync();
            accountId = accountId.Trim();
            if (!await _accountAccessGuard.CanAccessAccountAsync(user, accountId))
            {
                _logger.LogWarning("CrmService GetMassSendHistoryAsync 拒绝越权账号读取。AccountId={AccountId}", accountId);
                return new List<MassSendHistoryDto>();
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var ownerWxid = accountId;
            var accountKey = BuildWechatAccountNumericKey(ownerWxid);
            var normalizedCount = Math.Clamp(count, 1, 200);

            var histories = await db.Set<MassMessage>()
                .AsNoTracking()
                .Where(m => m.wechatAccountId == accountKey)
                .OrderByDescending(m => m.sentTime == default ? m.updatedAt : m.sentTime)
                .ThenByDescending(m => m.id)
                .Take(normalizedCount)
                .ToListAsync();

            if (!histories.Any())
            {
                return new List<MassSendHistoryDto>();
            }

            var historyIds = histories.Select(m => m.id).ToList();
            var details = await db.Set<MassMessageDetail>()
                .AsNoTracking()
                .Where(d => historyIds.Contains(d.massMessageId))
                .OrderBy(d => d.id)
                .ToListAsync();

            var recipientIds = details
                .Select(d => d.recipientWxid)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var contactNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (recipientIds.Any())
            {
                var contacts = await db.Contacts
                    .AsNoTracking()
                    .Where(c => c.ownerWxid == ownerWxid && !c.isDeleted && recipientIds.Contains(c.wxid))
                    .Select(c => new { c.wxid, c.remarks, c.nickname })
                    .ToListAsync();

                foreach (var contact in contacts)
                {
                    if (!string.IsNullOrWhiteSpace(contact.wxid))
                    {
                        contactNames[contact.wxid] = ResolveContactDisplayName(contact.remarks, contact.nickname, contact.wxid);
                    }
                }
            }

            var detailsByHistory = details
                .GroupBy(d => d.massMessageId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = histories.Select(message => new MassSendHistoryDto
            {
                id = message.id,
                ownerWxid = ownerWxid,
                wechatAccountId = message.wechatAccountId,
                messageTitle = message.messageTitle ?? string.Empty,
                messageContent = message.messageContent ?? string.Empty,
                messageType = message.messageType,
                targetType = message.targetType,
                totalRecipients = message.totalRecipients,
                successSentCount = message.successSentCount,
                failedSentCount = message.failedSentCount,
                sendStatus = message.sendStatus,
                scheduledTime = message.scheduledTime,
                sentTime = message.sentTime,
                createdAt = message.createdAt,
                updatedAt = message.updatedAt,
                details = detailsByHistory.TryGetValue(message.id, out var itemDetails)
                    ? itemDetails.Select(detail => new MassSendHistoryDetailDto
                    {
                        id = detail.id,
                        recipientWxid = detail.recipientWxid ?? string.Empty,
                        recipientDisplayName = ResolveRecipientDisplayName(detail.recipientWxid, contactNames),
                        sendStatus = detail.sendStatus,
                        errorMessage = detail.errorMessage ?? string.Empty,
                        retryCount = detail.retryCount,
                        sentTime = detail.sentTime
                    }).ToList()
                    : new List<MassSendHistoryDetailDto>()
            }).ToList();
            return await _sensitiveMaskingService.MaskMassSendHistoryAsync(user, result);
        }

        private static string ResolveRecipientDisplayName(string? wxid, Dictionary<string, string> contactNames)
        {
            if (!string.IsNullOrWhiteSpace(wxid)
                && contactNames.TryGetValue(wxid.Trim(), out var displayName)
                && !string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            return wxid ?? string.Empty;
        }

        public async Task<bool> ExecuteGroupActionAsync(string deviceUuid, string chatRoomId, int action, string content, int intValue)

        {
            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(ExecuteGroupActionAsync),
                    "chatroom-action",
                    ContentFields(("content", content))) is not null)
            {
                return false;
            }

            if (await DenyIfHighRiskDeviceOperationAsync(
                    deviceUuid,
                    nameof(ExecuteGroupActionAsync),
                    DeviceOperationGuard.GetGroupActionPermission(action),
                    chatRoomId,
                    destructive: action is 3 or 7 or 10 or 12 or 13,
                    metadata: new Dictionary<string, object?>
                    {
                        ["action"] = action,
                        ["intValue"] = intValue
                    }) is not null)
            {
                return false;
            }

            return await _deviceCommandService.ExecuteGroupActionAsync(deviceUuid, chatRoomId, action, content, intValue);

        }



        /// <summary>
        /// 读取好友请求列表。
        /// <para>只读查询，不修改 FriendRequests；写入由 ContactMessageHandler + DbHelper.SaveFriendRequestFromNotice 完成。</para>
        /// </summary>
        public async Task<List<FriendRequestDto>> GetFriendRequestsAsync(string accountId, int count = 50, bool pendingOnly = false)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return new List<FriendRequestDto>();
            }

            var user = await GetCurrentUserAsync();
            accountId = accountId.Trim();
            if (!await _accountAccessGuard.CanAccessAccountAsync(user, accountId))
            {
                _logger.LogWarning("CrmService GetFriendRequestsAsync 拒绝越权账号读取。AccountId={AccountId}", accountId);
                return new List<FriendRequestDto>();
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var ownerWxid = accountId;
            var accountKey = BuildWechatAccountNumericKey(ownerWxid);
            var normalizedCount = Math.Clamp(count, 1, 200);

            var query = db.Set<FriendRequest>()
                .AsNoTracking()
                .Where(request => request.wechatAccountId == accountKey);

            if (pendingOnly)
            {
                query = query.Where(request => request.status == 0);
            }

            var requests = await query
                .OrderByDescending(request => request.requestTime == default ? request.createdAt : request.requestTime)
                .ThenByDescending(request => request.id)
                .Take(normalizedCount)
                .ToListAsync();

            var result = requests.Select(request => new FriendRequestDto
            {
                id = request.id,
                ownerWxid = ownerWxid,
                wechatAccountId = request.wechatAccountId,
                requestWxid = request.requestWxid ?? string.Empty,
                nickname = request.nickname ?? string.Empty,
                avatar = request.avatar ?? string.Empty,
                gender = request.gender,
                region = request.region ?? string.Empty,
                source = request.source ?? string.Empty,
                requestMessage = request.requestMessage ?? string.Empty,
                status = request.status,
                requestTime = request.requestTime,
                responseTime = request.responseTime,
                responseMessage = request.responseMessage ?? string.Empty,
                createdAt = request.createdAt,
                updatedAt = request.updatedAt
            }).ToList();
            return await _sensitiveMaskingService.MaskFriendRequestsAsync(user, result);
        }

        /// <summary>
        /// 请求客户端主动拉取微信好友申请历史/补偿列表。
        /// <para>
        /// 该方法只下发 PullFriendAddReqListTask(1234)，不直接写 FriendRequests；
        /// 实际持久化由安卓端异步回传 FriendAddReqListNotice(2036) 后，经 ContactMessageHandler
        /// 复用 DbHelper.SaveFriendRequestFromNotice 完成。
        /// </para>
        /// </summary>
        public async Task<TaskResult> PullFriendAddReqListAsync(string deviceUuid, long startTime = 0, bool onlyNew = true, bool getAll = false)
        {
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return TaskResult.Fail("请先选择执行设备");
            }

            var user = await GetCurrentUserAsync();
            if (!await _accountAccessGuard.CanAccessDeviceAsync(user, deviceUuid))
            {
                _logger.LogWarning("CrmService PullFriendAddReqListAsync 拒绝越权设备下发。DeviceUuid={DeviceUuid}", deviceUuid);
                return TaskResult.Fail("无权访问该设备");
            }

            return await _deviceCommandService.PullFriendAddReqListAsync(deviceUuid, startTime, onlyNew, getAll);
        }

        public async Task<TaskResult> AcceptFriendRequestAsync(
            string deviceUuid,
            string friendId,
            string friendNick,
            string remark = "",
            string replyMsg = "",
            bool addWithWW = false,
            bool onlyWW = false,
            int permission = 0,
            int operation = 1)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(AcceptFriendRequestAsync)) is { } denied)
             {
                 return denied;
             }

            if (string.IsNullOrWhiteSpace(deviceUuid) || string.IsNullOrWhiteSpace(friendId))
            {
                return TaskResult.Fail("请先选择设备并填写请求人 wxid");
            }

            var normalizedOperation = System.Enum.IsDefined(typeof(Jubo.JuLiao.IM.Wx.Proto.AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation), operation)
                ? (Jubo.JuLiao.IM.Wx.Proto.AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation)operation
                : Jubo.JuLiao.IM.Wx.Proto.AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation.Accept;

            var ownerWxid = await ResolveOnlineWechatIdRequiredAsync(deviceUuid);
            if (string.IsNullOrWhiteSpace(ownerWxid))
            {
                return TaskResult.Fail("设备当前没有可用微信账号");
            }

            var result = await _deviceCommandService.AcceptFriendRequestAsync(
                deviceUuid,
                friendId,
                friendNick,
                remark,
                replyMsg,
                addWithWW,
                onlyWW,
                permission,
                normalizedOperation,
                ownerWxid);
            if (!result.success)
            {
                return result;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            if (normalizedOperation == Jubo.JuLiao.IM.Wx.Proto.AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation.Reject)
            {
                await db.MarkFriendRequestRejected(ownerWxid, friendId, string.IsNullOrWhiteSpace(replyMsg) ? "拒绝好友请求任务已成功回执" : replyMsg);
            }
            else if (normalizedOperation == Jubo.JuLiao.IM.Wx.Proto.AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation.Accept)
            {
                await db.MarkFriendRequestAccepted(ownerWxid, friendId, "通过好友请求任务已成功回执");
            }

            return result;
        }

        /// <summary>
        /// 解析当前在线微信号，供会改变好友关系的手动任务下发和状态落库共用。
        /// <para>只允许使用当前在线账号，不能回退到离线历史账号或 lastKnown 快照，避免 1075 下发 owner 与 FriendRequests 标记 owner 不一致。</para>
        /// </summary>
        private async Task<string> ResolveOnlineWechatIdRequiredAsync(string deviceUuid)
        {
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return string.Empty;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var normalizedDeviceUuid = deviceUuid.Trim();
            var account = await db.Set<WechatAccount>()
                .AsNoTracking()
                .OrderByDescending(item => item.lastOnlineAt)
                .ThenByDescending(item => item.updatedAt)
                .FirstOrDefaultAsync(item => item.clientUuid == normalizedDeviceUuid && !item.isDeleted && item.accountStatus == 1);

            return account?.wxid ?? string.Empty;
        }





        public async Task<TaskResult> AddFriendInChatRoomAsync(string deviceUuid, string chatRoomId, string friendId, string message, string remark = "", int permission = 0)

        {

            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(AddFriendInChatRoomAsync),
                    "chatroom-friend",
                    ContentFields(("message", message), ("remark", remark))) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.AddFriendInChatRoomAsync(deviceUuid, chatRoomId, friendId, message, remark, permission);

        }

        public async Task<TaskResult> JoinGroupByQrAsync(string deviceUuid, string qrUrl = "", string qrContent = "")

        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(JoinGroupByQrAsync)) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.JoinGroupByQrAsync(deviceUuid, qrUrl, qrContent);

        }

        public async Task<TaskResult> PullChatRoomQrCodeAsync(string deviceUuid, string chatRoomId)

        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullChatRoomQrCodeAsync)) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.PullChatRoomQrCodeAsync(deviceUuid, chatRoomId);

        }

        /// <summary>
        /// 执行手机/微信进程侧设备操作。
        /// </summary>
        public async Task<TaskResult> ExecutePhoneActionAsync(string deviceUuid, int action, string strParam = "", int intParam = 0, string weChatId = "", string imei = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(ExecutePhoneActionAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.ExecutePhoneActionAsync(deviceUuid, action, strParam, intParam, weChatId, imei);
        }

        public async Task<TaskResult> PullWeChatQrCodeAsync(string deviceUuid)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullWeChatQrCodeAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.PullWeChatQrCodeAsync(deviceUuid);
        }

        public async Task<TaskResult> GetPoiListAsync(string deviceUuid, double lat, double lng, string keyword = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(GetPoiListAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.GetPoiListAsync(deviceUuid, lat, lng, keyword);
        }

        public async Task<TaskResult> PullEmojiInfoAsync(string deviceUuid, string md5)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullEmojiInfoAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.PullEmojiInfoAsync(deviceUuid, md5);
        }

        public async Task<TaskResult> PullEmojiInfoForMessageAsync(string deviceUuid, string md5, long msgSvrId, string friendId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullEmojiInfoForMessageAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.PullEmojiInfoForMessageAsync(deviceUuid, md5, msgSvrId, friendId);
        }

        public async Task<TaskResult> FindContactAsync(string deviceUuid, string content)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(FindContactAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.FindContactAsync(deviceUuid, content);
        }

        public async Task<TaskResult> GetWeChatLocationAsync(string deviceUuid, bool noCache = false)
        {
            if (await DenyIfHighRiskDeviceOperationAsync(
                    deviceUuid,
                    nameof(GetWeChatLocationAsync),
                    Permissions.WechatOperation.LocationQuery,
                    destructive: false,
                    metadata: new Dictionary<string, object?> { ["noCache"] = noCache }) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.GetWeChatLocationAsync(deviceUuid, noCache);
        }

        public async Task<TaskResult> GetWalletBalanceAsync(string deviceUuid, int flag = 0)
        {
            if (await DenyIfHighRiskDeviceOperationAsync(
                    deviceUuid,
                    nameof(GetWalletBalanceAsync),
                    Permissions.WechatOperation.WalletQuery,
                    destructive: false,
                    metadata: new Dictionary<string, object?> { ["flag"] = flag }) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.GetWalletBalanceAsync(deviceUuid, flag);
        }

        public async Task<TaskResult> GetPhoneStateAsync(string deviceUuid, string imei = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(GetPhoneStateAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.GetPhoneStateAsync(deviceUuid, imei);
        }

        /// <summary>
        /// 发送手机短信。
        /// </summary>
        public async Task<TaskResult> SendSmsAsync(string deviceUuid, string number, string content, string weChatId = "", string imei = "")
        {
            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(SendSmsAsync),
                    "sms-recipient",
                    ContentFields(("number", number), ("content", content)),
                    weChatId) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.SendSmsAsync(deviceUuid, number, content, weChatId, imei);
        }

        /// <summary>
        /// 拉取手机短信历史。
        /// </summary>
        public async Task<TaskResult> PullSmsAsync(string deviceUuid, long startTime, long endTime, string weChatId = "", string imei = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullSmsAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.PullSmsAsync(deviceUuid, startTime, endTime, weChatId, imei);
        }

        /// <summary>
        /// 拉取手机通话记录。
        /// </summary>
        public async Task<TaskResult> PullCallLogsAsync(string deviceUuid, long startTime, long endTime, string weChatId = "", string imei = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullCallLogsAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.PullCallLogsAsync(deviceUuid, startTime, endTime, weChatId, imei);
        }

        /// <summary>
        /// 读取已落库短信记录。
        /// </summary>
        public async Task<List<SmsRecordDto>> GetSmsRecordsAsync(string accountId, string imei = "", int count = 200)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return new List<SmsRecordDto>();
            }

            accountId = accountId.Trim();
            count = Math.Clamp(count, 1, 500);
            var user = await GetCurrentUserAsync();
            if (!await _accountAccessGuard.CanAccessAccountAsync(user, accountId))
            {
                _logger.LogWarning("CrmService GetSmsRecordsAsync 拒绝越权账号读取。AccountId={AccountId}", accountId);
                return new List<SmsRecordDto>();
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var records = await db.GetSmsRecords(accountId, imei, count);
            var profile = await _sensitiveMaskingService.BuildProfileAsync(user);
            var sensitiveFields = BuildSmsSensitiveFields(records, profile);
            await _sensitiveDataAccessAuditService.LogSensitiveFieldsReturnedAsync(
                user,
                "SmsRecords",
                accountId,
                sensitiveFields,
                records.Count,
                nameof(GetSmsRecordsAsync),
                imei: imei,
                detail: $"count={count}");

            return records.Select(record => SensitiveMaskingService.MaskSmsRecord(record, profile)).ToList();
        }

        /// <summary>
        /// 读取已落库通话记录。
        /// </summary>
        public async Task<List<CallLogRecordDto>> GetCallLogRecordsAsync(string accountId, string imei = "", int count = 200)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return new List<CallLogRecordDto>();
            }

            accountId = accountId.Trim();
            count = Math.Clamp(count, 1, 500);
            var user = await GetCurrentUserAsync();
            if (!await _accountAccessGuard.CanAccessAccountAsync(user, accountId))
            {
                _logger.LogWarning("CrmService GetCallLogRecordsAsync 拒绝越权账号读取。AccountId={AccountId}", accountId);
                return new List<CallLogRecordDto>();
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var records = await db.GetCallLogRecords(accountId, imei, count);
            var profile = await _sensitiveMaskingService.BuildProfileAsync(user);
            var sensitiveFields = BuildCallLogSensitiveFields(records, profile);
            await _sensitiveDataAccessAuditService.LogSensitiveFieldsReturnedAsync(
                user,
                "CallLogRecords",
                accountId,
                sensitiveFields,
                records.Count,
                nameof(GetCallLogRecordsAsync),
                imei: imei,
                detail: $"count={count}");

            return records.Select(record => SensitiveMaskingService.MaskCallLogRecord(record, profile)).ToList();
        }

        private static IReadOnlyList<string> BuildSmsSensitiveFields(IEnumerable<SmsRecordDto> records, SensitiveAccessProfile profile)
        {
            var list = records as IReadOnlyCollection<SmsRecordDto> ?? records.ToList();
            var fields = new List<string>();
            if (profile.CanViewPhoneNumber && list.Any(record => !string.IsNullOrWhiteSpace(record.number)))
            {
                fields.Add("number");
            }

            if (profile.CanViewSmsContent && list.Any(record => !string.IsNullOrWhiteSpace(record.content)))
            {
                fields.Add("smsContent");
            }

            return fields;
        }

        private static IReadOnlyList<string> BuildCallLogSensitiveFields(IEnumerable<CallLogRecordDto> records, SensitiveAccessProfile profile)
        {
            var list = records as IReadOnlyCollection<CallLogRecordDto> ?? records.ToList();
            var fields = new List<string>();
            if (profile.CanViewPhoneNumber && list.Any(record => !string.IsNullOrWhiteSpace(record.number)))
            {
                fields.Add("number");
            }

            if (profile.CanViewCallRecordUrl && list.Any(record => record.hasRecording || !string.IsNullOrWhiteSpace(record.recordUrl)))
            {
                fields.Add("recordingAvailable");
            }

            return fields;
        }

        /// <summary>
        /// 为通话录音签发短期播放 token。
        /// <para>该方法服务 Blazor Server 直连 UI；REST 入口仍由 MediaAccessController 提供。</para>
        /// </summary>
        public async Task<MediaAccessTokenDto> CreateCallRecordingAccessTokenAsync(int callLogId, int expiresMinutes = 5)
        {
            if (callLogId <= 0)
            {
                return MediaAccessTokenDto.Fail("通话记录 ID 无效。");
            }

            var user = await GetCurrentUserAsync();
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var record = await db.CallLogRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.id == callLogId);

            if (record == null)
            {
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.CallRecordingScope,
                    null,
                    "通话记录不存在。",
                    nameof(CreateCallRecordingAccessTokenAsync));
                return MediaAccessTokenDto.Fail("通话记录不存在。");
            }

            if (!await _accountAccessGuard.CanAccessAccountAsync(user, record.ownerWxid))
            {
                _logger.LogWarning(
                    "CrmService CreateCallRecordingAccessTokenAsync 拒绝越权录音播放。CallLogId={CallLogId}, OwnerWxid={OwnerWxid}",
                    callLogId,
                    record.ownerWxid);
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.CallRecordingScope,
                    null,
                    "无权访问该通话记录。",
                    nameof(CreateCallRecordingAccessTokenAsync),
                    accountId: record.ownerWxid);
                return MediaAccessTokenDto.Fail("无权访问该通话记录。");
            }

            var profile = await _sensitiveMaskingService.BuildProfileAsync(user);
            if (!profile.CanViewCallRecordUrl)
            {
                _logger.LogWarning(
                    "CrmService CreateCallRecordingAccessTokenAsync 拒绝无录音查看权限的播放请求。CallLogId={CallLogId}",
                    callLogId);
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.CallRecordingScope,
                    null,
                    "无录音查看权限。",
                    nameof(CreateCallRecordingAccessTokenAsync),
                    accountId: record.ownerWxid);
                return MediaAccessTokenDto.Fail("无录音查看权限。");
            }

            if (string.IsNullOrWhiteSpace(record.recordUrl))
            {
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.CallRecordingScope,
                    null,
                    "该通话记录没有录音 URL。",
                    nameof(CreateCallRecordingAccessTokenAsync),
                    accountId: record.ownerWxid);
                return MediaAccessTokenDto.Fail("该通话记录没有录音 URL。");
            }

            if (!_mediaAccessTokenService.TryNormalizeUploadRelativePath(record.recordUrl, out var relativePath, out var error))
            {
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.CallRecordingScope,
                    null,
                    error,
                    nameof(CreateCallRecordingAccessTokenAsync),
                    accountId: record.ownerWxid);
                return MediaAccessTokenDto.Fail(error);
            }

            var lifetimeMinutes = Math.Clamp(expiresMinutes, 1, 30);
            var userId = AccountAccessGuard.GetUserIdCandidates(user).FirstOrDefault()
                ?? user?.Identity?.Name
                ?? string.Empty;
            var issued = _mediaAccessTokenService.CreateToken(
                relativePath,
                MediaAccessTokenService.CallRecordingScope,
                userId,
                TimeSpan.FromMinutes(lifetimeMinutes));
            var openUrl = $"/api/media-access/open?token={Uri.EscapeDataString(issued.Token)}";

            var result = MediaAccessTokenDto.Ok(
                issued.Token,
                openUrl,
                issued.RelativePath,
                issued.Scope,
                issued.ExpiresAt);
            await _sensitiveMediaAccessAuditService.LogTokenIssuedAsync(
                user,
                result,
                nameof(CreateCallRecordingAccessTokenAsync),
                accountId: record.ownerWxid,
                detail: $"CallLogId={callLogId}");
            return result;
        }

        /// <summary>
        /// 为聊天媒体 URL 签发短期访问 token。
        /// <para>该方法服务 Blazor Server 直连 UI；不信任前端传入的 URL，签发前重新校验 raw 权限和路径归属。</para>
        /// </summary>
        public async Task<MediaAccessTokenDto> CreateMediaAccessTokenAsync(string mediaUrl, string accountId = "", string deviceUuid = "", int expiresMinutes = 5)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl))
            {
                return MediaAccessTokenDto.Fail("媒体 URL 不能为空。");
            }

            var user = await GetCurrentUserAsync();
            var profile = await _sensitiveMaskingService.BuildProfileAsync(user);
            if (!profile.CanViewMessageRaw)
            {
                _logger.LogWarning("CrmService CreateMediaAccessTokenAsync 拒绝无原始媒体权限的访问请求。AccountId={AccountId}, DeviceUuid={DeviceUuid}", accountId, deviceUuid);
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.MediaScope,
                    null,
                    "无权查看原始媒体文件。",
                    nameof(CreateMediaAccessTokenAsync),
                    accountId,
                    deviceUuid);
                return MediaAccessTokenDto.Fail("无权查看原始媒体文件。");
            }

            if (!_mediaAccessTokenService.TryNormalizeUploadRelativePath(mediaUrl, out var relativePath, out var error))
            {
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.MediaScope,
                    null,
                    error,
                    nameof(CreateMediaAccessTokenAsync),
                    accountId,
                    deviceUuid);
                return MediaAccessTokenDto.Fail(error);
            }

            var normalizedAccountId = accountId?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedAccountId)
                && !await _accountAccessGuard.CanAccessAccountAsync(user, normalizedAccountId))
            {
                _logger.LogWarning("CrmService CreateMediaAccessTokenAsync 拒绝越权账号媒体访问。AccountId={AccountId}", normalizedAccountId);
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.MediaScope,
                    relativePath,
                    "无权访问该微信账号。",
                    nameof(CreateMediaAccessTokenAsync),
                    normalizedAccountId,
                    deviceUuid);
                return MediaAccessTokenDto.Fail("无权访问该微信账号。");
            }

            var normalizedDeviceUuid = deviceUuid?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedDeviceUuid)
                && !await _accountAccessGuard.CanAccessDeviceAsync(user, normalizedDeviceUuid))
            {
                _logger.LogWarning("CrmService CreateMediaAccessTokenAsync 拒绝越权设备媒体访问。DeviceUuid={DeviceUuid}", normalizedDeviceUuid);
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.MediaScope,
                    relativePath,
                    "无权访问该设备。",
                    nameof(CreateMediaAccessTokenAsync),
                    accountId,
                    normalizedDeviceUuid);
                return MediaAccessTokenDto.Fail("无权访问该设备。");
            }

            var inferredTarget = MediaAccessTokenService.InferTarget(relativePath);
            var canAccessInferredTarget = inferredTarget.Kind switch
            {
                MediaAccessTargetKind.WechatAccount => await _accountAccessGuard.CanAccessAccountAsync(user, inferredTarget.Value),
                MediaAccessTargetKind.Device => await _accountAccessGuard.CanAccessDeviceAsync(user, inferredTarget.Value),
                _ => AccountAccessGuard.IsAdmin(user)
            };
            if (!canAccessInferredTarget)
            {
                _logger.LogWarning(
                    "CrmService CreateMediaAccessTokenAsync 拒绝路径归属校验失败的媒体访问。RelativePath={RelativePath}, TargetKind={TargetKind}, TargetValue={TargetValue}",
                    relativePath,
                    inferredTarget.Kind,
                    inferredTarget.Value);
                await _sensitiveMediaAccessAuditService.LogTokenDeniedAsync(
                    user,
                    MediaAccessTokenService.MediaScope,
                    relativePath,
                    "无权访问该媒体文件。",
                    nameof(CreateMediaAccessTokenAsync),
                    accountId,
                    deviceUuid);
                return MediaAccessTokenDto.Fail("无权访问该媒体文件。");
            }

            var lifetimeMinutes = Math.Clamp(expiresMinutes, 1, 30);
            var userId = AccountAccessGuard.GetUserIdCandidates(user).FirstOrDefault()
                ?? user?.Identity?.Name
                ?? string.Empty;
            var issued = _mediaAccessTokenService.CreateToken(
                relativePath,
                MediaAccessTokenService.MediaScope,
                userId,
                TimeSpan.FromMinutes(lifetimeMinutes));
            var openUrl = $"/api/media-access/open?token={Uri.EscapeDataString(issued.Token)}";

            var result = MediaAccessTokenDto.Ok(
                issued.Token,
                openUrl,
                issued.RelativePath,
                issued.Scope,
                issued.ExpiresAt);
            await _sensitiveMediaAccessAuditService.LogTokenIssuedAsync(
                user,
                result,
                nameof(CreateMediaAccessTokenAsync),
                accountId,
                deviceUuid);
            return result;
        }

        /// <summary>
        /// 查询敏感媒体访问审计。
        /// <para>Blazor Server 直连入口与 REST 查询接口共用同一个 QueryService，避免 UI 绕过归属过滤。</para>
        /// </summary>
        public async Task<SensitiveMediaAccessAuditQueryResultDto> GetSensitiveMediaAccessAuditsAsync(SensitiveMediaAccessAuditQueryDto query)
        {
            var user = await GetCurrentUserAsync();
            return await _sensitiveMediaAccessAuditQueryService.QueryAsync(user, query);
        }

        public async Task<TaskResult> SyncQwUsersAsync(string deviceUuid)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncQwUsersAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.SyncQwUsersAsync(deviceUuid);
        }

        public async Task<TaskResult> SyncChatMsgIdsAsync(string deviceUuid, long startTime, long endTime)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncChatMsgIdsAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.SyncChatMsgIdsAsync(deviceUuid, startTime, endTime);
        }

        /// <summary>
        /// 请求客户端回传历史聊天消息。
        /// </summary>
        public async Task<TaskResult> SyncHistoryMessagesAsync(string deviceUuid, string friendId = "", long startTime = 0, long endTime = 0, int flag = 0, int count = 50, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncHistoryMessagesAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.SyncHistoryMessagesAsync(deviceUuid, friendId, startTime, endTime, flag, count, weChatId);
        }

        /// <summary>
        /// 请求客户端同步指定会话已读状态。
        /// </summary>
        public async Task<TaskResult> SyncMessageReadAsync(string deviceUuid, string friendId, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncMessageReadAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.SyncMessageReadAsync(deviceUuid, friendId, weChatId);
        }

        /// <summary>
        /// 请求客户端回传未读会话列表。
        /// </summary>
        public async Task<TaskResult> SyncUnreadListAsync(string deviceUuid, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncUnreadListAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.SyncUnreadListAsync(deviceUuid, weChatId);
        }

        /// <summary>
        /// 请求客户端同步单个会话未读状态。
        /// </summary>
        public async Task<TaskResult> SyncConversationUnreadAsync(string deviceUuid, string friendId, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncConversationUnreadAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.SyncConversationUnreadAsync(deviceUuid, friendId, weChatId);
        }

        /// <summary>
        /// 请求客户端异步同步当前微信好友列表。
        /// <para>该方法只下发 3056，不直接写联系人表；联系人持久化仍由 FriendPushNotice 进入 DbHelper.SaveContacts。</para>
        /// </summary>
        public async Task<TaskResult> SyncFriendListAsync(string deviceUuid, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncFriendListAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.SyncFriendListAsync(deviceUuid, weChatId);
        }

        /// <summary>
        /// 请求客户端回传当前微信账号快照。
        /// </summary>
        public async Task<TaskResult> RefreshWeChatAccountsAsync(string deviceUuid)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(RefreshWeChatAccountsAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.RefreshWeChatAccountsAsync(deviceUuid);
        }

        /// <summary>
        /// 请求客户端回传业务联系人列表。
        /// </summary>
        public async Task<TaskResult> SyncBizContactsAsync(string deviceUuid, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncBizContactsAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.SyncBizContactsAsync(deviceUuid, weChatId);
        }

        /// <summary>
        /// 请求客户端回传企微会话列表。
        /// </summary>
        public async Task<TaskResult> SyncQwConversationsAsync(string deviceUuid, long startTime = 0, long endTime = 0, int limit = 100, int offset = 0, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncQwConversationsAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.SyncQwConversationsAsync(deviceUuid, startTime, endTime, limit, offset, weChatId);
        }

        /// <summary>
        /// 请求客户端同步联系人标签列表。
        /// </summary>
        public async Task<TaskResult> SyncContactLabelsAsync(string deviceUuid, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncContactLabelsAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.SyncContactLabelsAsync(deviceUuid, weChatId);
        }

        /// <summary>
        /// 创建或重命名联系人标签，也可通过 AddList/DelList 调整标签成员。
        /// </summary>
        public async Task<TaskResult> SaveContactLabelAsync(string deviceUuid, string labelName, int labelId = 0, string addList = "", string delList = "", string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SaveContactLabelAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.SaveContactLabelAsync(deviceUuid, labelName, labelId, addList, delList, weChatId);
        }

        /// <summary>
        /// 删除联系人标签。
        /// </summary>
        public async Task<TaskResult> DeleteContactLabelAsync(string deviceUuid, int labelId, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(DeleteContactLabelAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.DeleteContactLabelAsync(deviceUuid, labelId, weChatId);
        }

        /// <summary>
        /// 设置单个好友的完整标签 ID 集合。
        /// </summary>
        public async Task<TaskResult> SetContactLabelsAsync(string deviceUuid, string friendId, IEnumerable<int>? labelIds, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SetContactLabelsAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.SetContactLabelsAsync(deviceUuid, friendId, labelIds, weChatId);
        }

        /// <summary>
        /// 请求客户端按 MsgSvrId 补偿单条聊天消息。
        /// </summary>
        public async Task<TaskResult> RequestTalkMsgAsync(string deviceUuid, long msgSvrId, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(RequestTalkMsgAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.RequestTalkMsgAsync(deviceUuid, msgSvrId, weChatId);
        }

        /// <summary>
        /// 请求客户端按 MsgSvrId 补偿原始聊天正文/XML。
        /// </summary>
        public async Task<TaskResult> RequestTalkContentAsync(string deviceUuid, long msgSvrId, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(RequestTalkContentAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.RequestTalkContentAsync(deviceUuid, msgSvrId, weChatId);
        }

        /// <summary>
        /// 请求客户端补偿聊天消息详情。
        /// </summary>
        public async Task<TaskResult> RequestTalkDetailAsync(
            string deviceUuid,
            string friendId,
            long msgId,
            string msgSvrId = "",
            string md5 = "",
            bool getOriginal = false,
            string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(RequestTalkDetailAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.RequestTalkDetailAsync(deviceUuid, friendId, msgId, msgSvrId, md5, getOriginal, weChatId);
        }

        /// <summary>
        /// 请求客户端对指定语音消息执行语音转文字。
        /// </summary>
        public async Task<TaskResult> VoiceTransTextAsync(string deviceUuid, string friendId, long msgSvrId, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(VoiceTransTextAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.VoiceTransTextAsync(deviceUuid, friendId, msgSvrId, weChatId);
        }

        /// <summary>
        /// 请求客户端撤回指定聊天消息。
        /// </summary>
        public async Task<TaskResult> RevokeMessageAsync(string deviceUuid, string friendId, long msgSvrId, string weChatId = "")
        {
            if (await DenyIfHighRiskDeviceOperationAsync(
                    deviceUuid,
                    nameof(RevokeMessageAsync),
                    Permissions.MessageOperation.Revoke,
                    friendId,
                    weChatId,
                    destructive: true,
                    metadata: new Dictionary<string, object?> { ["msgSvrId"] = msgSvrId }) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.RevokeMessageAsync(deviceUuid, friendId, msgSvrId, weChatId);
        }

        /// <summary>
        /// 请求客户端转发一条已有聊天消息。
        /// </summary>
        public async Task<TaskResult> ForwardMessageAsync(string deviceUuid, string talker, long msgSvrId, string friendIds, string extMsg = "", string weChatId = "")
        {
            var riskFields = await BuildForwardMessageContentFieldsAsync(deviceUuid, weChatId, talker, new[] { msgSvrId }, extMsg);
            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(ForwardMessageAsync),
                    "forward-targets",
                    riskFields,
                    weChatId) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.ForwardMessageAsync(deviceUuid, talker, msgSvrId, friendIds, extMsg, weChatId);
        }

        /// <summary>
        /// 请求客户端转发多条已有聊天消息。
        /// </summary>
        public async Task<TaskResult> ForwardMultiMessageAsync(string deviceUuid, string talker, IEnumerable<long>? msgIds, string friendIds, string extMsg = "", bool sendRecord = false, string weChatId = "")
        {
            var riskFields = await BuildForwardMessageContentFieldsAsync(deviceUuid, weChatId, talker, msgIds, extMsg);
            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(ForwardMultiMessageAsync),
                    "forward-targets",
                    riskFields,
                    weChatId) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.ForwardMultiMessageAsync(deviceUuid, talker, msgIds, friendIds, extMsg, sendRecord, weChatId);
        }

        /// <summary>
        /// 请求客户端按原始内容转发消息。
        /// </summary>
        public async Task<TaskResult> ForwardMessageByContentAsync(string deviceUuid, string friendIds, long msgSvrId, int msgType, string content, string thumb = "", string extMsg = "", string weChatId = "")
        {
            var riskFields = await BuildForwardMessageContentFieldsAsync(deviceUuid, weChatId, null, new[] { msgSvrId }, extMsg, content);
            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(ForwardMessageByContentAsync),
                    "forward-content-targets",
                    riskFields,
                    weChatId) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.ForwardMessageByContentAsync(deviceUuid, friendIds, msgSvrId, msgType, content, thumb, extMsg, weChatId);
        }

        /// <summary>
        /// 请求客户端清空微信端聊天记录。
        /// </summary>
        public async Task<TaskResult> ClearAllChatMsgAsync(string deviceUuid, int flag = 0, string weChatId = "")
        {
            if (await DenyIfHighRiskDeviceOperationAsync(
                    deviceUuid,
                    nameof(ClearAllChatMsgAsync),
                    Permissions.MessageOperation.ClearWechat,
                    accountId: weChatId,
                    destructive: true,
                    metadata: new Dictionary<string, object?> { ["flag"] = flag }) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.ClearAllChatMsgAsync(deviceUuid, flag, weChatId);
        }

        /// <summary>
        /// 请求客户端查询红包详情。
        /// </summary>
        public async Task<TaskResult> QueryHbDetailAsync(string deviceUuid, string hbUrl, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(QueryHbDetailAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.QueryHbDetailAsync(deviceUuid, hbUrl, weChatId);
        }

        /// <summary>
        /// 请求客户端查询红包状态。
        /// </summary>
        public async Task<TaskResult> QueryHbStatusAsync(string deviceUuid, string hbUrl, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(QueryHbStatusAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.QueryHbStatusAsync(deviceUuid, hbUrl, weChatId);
        }

        /// <summary>
        /// 请求客户端发送微信红包，金额单位为分。
        /// </summary>
        public async Task<TaskResult> SendLuckyMoneyAsync(string deviceUuid, string friendId, int money, int number, string passwd, string wish = "", string weChatId = "")
        {
            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(SendLuckyMoneyAsync),
                    "payment",
                    ContentFields(("wish", wish)),
                    weChatId) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.SendLuckyMoneyAsync(deviceUuid, friendId, money, number, passwd, wish, weChatId);
        }

        /// <summary>
        /// 请求客户端执行微信转账，金额单位为分。
        /// </summary>
        public async Task<TaskResult> RemittanceAsync(string deviceUuid, string friendId, int money, string passwd, string memo = "", string roomId = "", string weChatId = "")
        {
            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(RemittanceAsync),
                    "payment",
                    ContentFields(("memo", memo)),
                    weChatId) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.RemittanceAsync(deviceUuid, friendId, money, passwd, memo, roomId, weChatId);
        }

        /// <summary>
        /// 请求客户端执行微信账号登出。
        /// </summary>
        public async Task<TaskResult> WechatLogoutAsync(string deviceUuid, string weChatId = "")
        {
            if (await DenyIfHighRiskDeviceOperationAsync(
                    deviceUuid,
                    nameof(WechatLogoutAsync),
                    Permissions.WechatOperation.Logout,
                    accountId: weChatId,
                    destructive: true) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.WechatLogoutAsync(deviceUuid, weChatId);
        }

        /// <summary>
        /// 请求客户端下载微信 CDN 媒体文件。
        /// </summary>
        public async Task<TaskResult> DownloadCdnFileAsync(
            string deviceUuid,
            string cdnUrl,
            string cdnKey,
            int fileType,
            string fileId = "",
            string fileFmt = "",
            int fileSize = 0,
            long msgSvrId = 0,
            string weChatId = "")
        {
            var user = await GetCurrentUserAsync();
            if (!await _accountAccessGuard.CanAccessDeviceAsync(user, deviceUuid))
            {
                _logger.LogWarning("CrmService DownloadCdnFileAsync 拒绝越权 CDN 下载请求。DeviceUuid={DeviceUuid}, WeChatId={WeChatId}, MsgSvrId={MsgSvrId}", deviceUuid, weChatId, msgSvrId);
                return TaskResult.Fail("无权访问该设备");
            }

            var profile = await _sensitiveMaskingService.BuildProfileAsync(user);
            if (!profile.CanViewMessageRaw)
            {
                _logger.LogWarning("CrmService DownloadCdnFileAsync 拒绝无原始媒体权限的 CDN 下载请求。DeviceUuid={DeviceUuid}, WeChatId={WeChatId}, MsgSvrId={MsgSvrId}", deviceUuid, weChatId, msgSvrId);
                return TaskResult.Fail("无权下载原始媒体文件");
            }

            if (!string.IsNullOrWhiteSpace(weChatId)
                && !await _accountAccessGuard.CanAccessAccountAsync(user, weChatId))
            {
                _logger.LogWarning("CrmService DownloadCdnFileAsync 拒绝越权账号 CDN 下载请求。DeviceUuid={DeviceUuid}, WeChatId={WeChatId}, MsgSvrId={MsgSvrId}", deviceUuid, weChatId, msgSvrId);
                return TaskResult.Fail("无权访问该微信账号");
            }

            return await _deviceCommandService.DownloadCdnFileAsync(deviceUuid, cdnUrl, cdnKey, fileType, fileId, fileFmt, fileSize, msgSvrId, weChatId);
        }

        /// <summary>
        /// 启动好友检测/清粉任务。
        /// </summary>
        public async Task<TaskResult> StartFriendDetectAsync(string deviceUuid, string message, bool onlyCheck = true, int skipHour = 24, int mode = 0, int max = 0, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(StartFriendDetectAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.StartFriendDetectAsync(deviceUuid, message, onlyCheck, skipHour, mode, max, weChatId);
        }

        /// <summary>
        /// 停止好友检测/清粉任务。
        /// </summary>
        public async Task<TaskResult> StopFriendDetectAsync(string deviceUuid, long taskId = 0, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(StopFriendDetectAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.StopFriendDetectAsync(deviceUuid, taskId, weChatId);
        }

        /// <summary>
        /// 拉取好友检测/清粉最终结果。
        /// </summary>
        public async Task<TaskResult> GetFriendDetectResultAsync(string deviceUuid, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(GetFriendDetectResultAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.GetFriendDetectResultAsync(deviceUuid, weChatId);
        }

        /// <summary>
        /// 请求客户端执行 GetA8Key。
        /// </summary>
        public async Task<TaskResult> GetA8KeyAsync(string deviceUuid, int type, string url, string userName = "", string msgSvrId = "", int reason = 0, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(GetA8KeyAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.GetA8KeyAsync(deviceUuid, type, url, userName, msgSvrId, reason, weChatId);
        }

        /// <summary>
        /// 修改微信资料或隐私设置。
        /// </summary>
        public async Task<TaskResult> UpdateWechatSettingAsync(string deviceUuid, int action, string content = "", int intParam = 0, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(UpdateWechatSettingAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.UpdateWechatSettingAsync(deviceUuid, action, content, intParam, weChatId);
        }

        /// <summary>
        /// 请求客户端回传当前本地配置快照。
        /// </summary>
        public async Task<TaskResult> TriggerConfigPushAsync(string deviceUuid)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(TriggerConfigPushAsync)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.TriggerConfigPushAsync(deviceUuid);
        }

        /// <summary>
        /// 下发 Android 设备级配置。
        /// <para>该配置直接对应 SmRun/62203 的 SetConfigTask(1382)，不写入账号设置 JSON。</para>
        /// </summary>
        public async Task<TaskResult> SetDeviceConfigAsync(string deviceUuid, DeviceConfigDto config)
        {
            if (await DenyIfHighRiskDeviceOperationAsync(
                    deviceUuid,
                    nameof(SetDeviceConfigAsync),
                    Permissions.DeviceTask.ConfigManage,
                    destructive: false) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.SetDeviceConfigAsync(deviceUuid, config);
        }

        /// <summary>
        /// 下发微信违禁词列表。
        /// </summary>
        public async Task<TaskResult> SetForbiddenWordAsync(string deviceUuid, IEnumerable<string>? words, string weChatId = "")
        {
            var normalizedDeviceUuid = deviceUuid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedDeviceUuid))
            {
                return TaskResult.Fail("设备ID为空");
            }

            var user = await GetCurrentUserAsync();
            if (!await _accountAccessGuard.CanAccessDeviceAsync(user, normalizedDeviceUuid))
            {
                _logger.LogWarning("CrmService SetForbiddenWordAsync 拒绝越权设备下发。DeviceUuid={DeviceUuid}", normalizedDeviceUuid);
                return TaskResult.Fail("无权访问该设备");
            }

            if (await DenyIfHighRiskDeviceOperationAsync(
                    normalizedDeviceUuid,
                    nameof(SetForbiddenWordAsync),
                    Permissions.DeviceTask.SensitiveConfigManage,
                    accountId: weChatId,
                    destructive: false) is { } denied)
            {
                return denied;
            }

            var normalizedWords = SensitiveWordPolicyService.NormalizeWords(words).ToArray();
            await _sensitiveWordPolicyService.SaveDeviceBlockWordsAsync(normalizedDeviceUuid, weChatId, normalizedWords);
            return await _deviceCommandService.SetForbiddenWordAsync(normalizedDeviceUuid, normalizedWords, weChatId);
        }

        public async Task<bool> ApproveChatRoomInviteAsync(string deviceUuid, long msgSvrId, string roomId = "", string msgContent = "", long msgId = 0)

        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(ApproveChatRoomInviteAsync)) is not null)
             {
                 return false;
             }


            return await _deviceCommandService.ApproveChatRoomInviteAsync(deviceUuid, msgSvrId, roomId, msgContent, msgId);

        }

        public async Task<TaskResult> SendJielongAsync(string deviceUuid, string chatRoomId, string content, string title = "", string sample = "", string memo = "", long msgSvrId = 0)

        {

            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(SendJielongAsync),
                    "chatroom",
                    ContentFields(("content", content), ("title", title), ("sample", sample), ("memo", memo))) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.SendJielongAsync(deviceUuid, chatRoomId, content, title, sample, memo, msgSvrId);

        }

        public async Task<TaskResult> AddFriendWithSceneAsync(string deviceUuid, string friendWxid, string message, string remark = "", string label = "", int scene = 3, int permission = 0, string verificationImagePath = "")

        {

            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(AddFriendWithSceneAsync),
                    "friend",
                    ContentFields(("message", message), ("remark", remark), ("label", label))) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.AddFriendWithSceneAsync(deviceUuid, friendWxid, message, remark, label, scene, permission, verificationImagePath);

        }



        public async Task<TaskResult> AddFriendsByPhoneAsync(string deviceUuid, IEnumerable<string> phones, string message, string remark = "", string label = "", int permission = 0)

        {

            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(AddFriendsByPhoneAsync),
                    "phone-friends",
                    ContentFields(("message", message), ("remark", remark), ("label", label))) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.AddFriendsByPhoneAsync(deviceUuid, phones, message, remark, label, permission);

        }



        public async Task<TaskResult> AddFriendFromPhonebookAsync(string deviceUuid, string message, int count = 1, int index = 0, bool reset = false)

        {

            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(AddFriendFromPhonebookAsync),
                    "phonebook",
                    ContentFields(("message", message))) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.AddFriendFromPhonebookAsync(deviceUuid, message, count, index, reset);

        }



        public async Task<TaskResult> AddFriendNameCardAsync(string deviceUuid, long msgSvrId, string message, string remark = "")

        {

            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(AddFriendNameCardAsync),
                    "name-card",
                    ContentFields(("message", message), ("remark", remark))) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.AddFriendNameCardAsync(deviceUuid, msgSvrId, message, remark);

        }



        public async Task<TaskResult> SendFriendVerifyAsync(string deviceUuid, string friendId, string message)

        {

            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(SendFriendVerifyAsync),
                    "friend-verify",
                    ContentFields(("message", message))) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.SendFriendVerifyAsync(deviceUuid, friendId, message);

        }



        public async Task<TaskResult> ModifyFriendMemoAsync(string deviceUuid, string friendId, string memo, string desc = "", string phone = "", int delFlag = 0)

        {

            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(ModifyFriendMemoAsync),
                    "friend-memo",
                    ContentFields(("memo", memo), ("desc", desc), ("phone", phone))) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.ModifyFriendMemoAsync(deviceUuid, friendId, memo, desc, phone, delFlag);

        }



        public async Task<TaskResult> SetFriendPermissionAsync(string deviceUuid, string friendId, int permissionMask)

        {
            if (await DenyIfHighRiskDeviceOperationAsync(
                    deviceUuid,
                    nameof(SetFriendPermissionAsync),
                    Permissions.ContactOperation.PermissionSet,
                    friendId,
                    destructive: true,
                    metadata: new Dictionary<string, object?> { ["permissionMask"] = permissionMask }) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.SetFriendPermissionAsync(deviceUuid, friendId, permissionMask);

        }



        public async Task<TaskResult> DeleteFriendAsync(string deviceUuid, string friendId)

        {
            if (await DenyIfHighRiskDeviceOperationAsync(
                    deviceUuid,
                    nameof(DeleteFriendAsync),
                    Permissions.ContactOperation.DeleteWechat,
                    friendId,
                    destructive: true) is { } denied)
            {
                return denied;
            }

            return await _deviceCommandService.DeleteFriendAsync(deviceUuid, friendId);

        }



        public async Task<bool> SyncChatRoomsAsync(string deviceUuid, int flag = 0, string weChatId = "")

        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncChatRoomsAsync)) is not null)
             {
                 return false;
             }


            return await _deviceCommandService.SyncChatRoomsAsync(deviceUuid, flag, weChatId);

        }





        public async Task<bool> GetChatRoomInviteListAsync(string deviceUuid)

        {
            var user = await GetCurrentUserAsync();
            if (!await _accountAccessGuard.CanAccessDeviceAsync(user, deviceUuid))
            {
                _logger.LogWarning("CrmService GetChatRoomInviteListAsync 拒绝越权设备下发。DeviceUuid={DeviceUuid}", deviceUuid);
                return false;
            }

            return await _deviceCommandService.GetChatRoomInviteListAsync(deviceUuid);

        }



        public async Task<TaskResult> SyncMomentsAsync(string deviceUuid, long startTime = 0, IEnumerable<long>? circleIds = null, string weChatId = "")

        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncMomentsAsync)) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.SyncMomentsAsync(deviceUuid, startTime, circleIds, weChatId);

        }

        public async Task<TaskResult> OneKeyLikeMomentsAsync(string deviceUuid, int rate = 100, int num = 0, int endTime = 0, int timeOut = 0)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(OneKeyLikeMomentsAsync),
                     Permissions.MomentOperation.Interact,
                     metadata: new Dictionary<string, object?> { ["rate"] = rate, ["num"] = num, ["endTime"] = endTime, ["timeOut"] = timeOut }) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.OneKeyLikeMomentsAsync(deviceUuid, rate, num, endTime, timeOut);
        }



        public async Task<bool> AgreeJoinGroupAsync(string deviceUuid, string talker, long msgSvrId, string content)

        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(AgreeJoinGroupAsync)) is not null)
             {
                 return false;
             }


            return await _deviceCommandService.AgreeJoinGroupAsync(deviceUuid, talker, msgSvrId, content);

        }



        public Task<TaskResult> PostMomentAsync(string deviceUuid, string content, List<string> imageUrls)

        {

            return this.PostMomentAdvancedAsync(deviceUuid, MomentPostRequestDto.FromLegacy(content, imageUrls));

        }



        public async Task<TaskResult> PostMomentAdvancedAsync(string deviceUuid, MomentPostRequestDto request)

        {

            var normalizedRequest = NormalizeMomentPostRequestForService(request);
            var requestedWeChatId = normalizedRequest.weChatId;
            var hasExplicitWeChatId = !string.IsNullOrWhiteSpace(requestedWeChatId);
            var currentWeChatId = await GetPrimaryWechatIdAsync(deviceUuid);
            var validation = MomentPostRequestValidator.BindAndValidate(normalizedRequest, currentWeChatId);
            if (!validation.IsValid)
            {
                return TaskResult.Fail(validation.ErrorMessage);
            }

            normalizedRequest = validation.Request;
            var momentPostAuditMetadata = MomentPostAuditMetadata(normalizedRequest);
            EnrichMomentPostAuditMetadata(
                momentPostAuditMetadata,
                requestedWeChatId,
                currentWeChatId,
                normalizedRequest.weChatId,
                hasExplicitWeChatId,
                validation.Warnings.Count);

            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(PostMomentAdvancedAsync),
                    "moment",
                    MomentPostContentFields(normalizedRequest)) is { } denied)
            {
                return denied;
            }

            if (await DenyIfHighRiskDeviceOperationAsync(
                    deviceUuid,
                    nameof(PostMomentAdvancedAsync),
                    Permissions.MomentOperation.Post,
                    metadata: momentPostAuditMetadata) is { } permissionDenied)
            {
                return permissionDenied;
            }

            return await _deviceCommandService.PostMomentAsync(deviceUuid, normalizedRequest);

        }



        public async Task<TaskResult> DeleteMomentAsync(string deviceUuid, long circleId)

        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(DeleteMomentAsync),
                     Permissions.MomentOperation.Delete,
                     targetId: circleId.ToString(),
                     destructive: true,
                     metadata: new Dictionary<string, object?> { ["circleId"] = circleId }) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.DeleteMomentAsync(deviceUuid, circleId);

        }



        public async Task<TaskResult> LikeMomentAsync(string deviceUuid, long circleId, bool isCancel = false)

        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(LikeMomentAsync),
                     Permissions.MomentOperation.Interact,
                     targetId: circleId.ToString(),
                     metadata: new Dictionary<string, object?> { ["circleId"] = circleId, ["isCancel"] = isCancel }) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.LikeMomentAsync(deviceUuid, circleId, isCancel);

        }



        public async Task<TaskResult> DeleteMomentCommentAsync(string deviceUuid, long circleId, long commentId, long publishTime)

        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(DeleteMomentCommentAsync),
                     Permissions.MomentOperation.Delete,
                     targetId: commentId.ToString(),
                     destructive: true,
                     metadata: new Dictionary<string, object?> { ["circleId"] = circleId, ["commentId"] = commentId, ["publishTime"] = publishTime }) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.DeleteMomentCommentAsync(deviceUuid, circleId, commentId, publishTime);

        }



        public async Task<TaskResult> ReplyMomentCommentAsync(string deviceUuid, long circleId, string toWeChatId, string content, long replyCommentId, bool isResend = false)

        {

            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(ReplyMomentCommentAsync),
                    "moment-comment",
                    ContentFields(("content", content))) is { } denied)
            {
                return denied;
            }

            if (await DenyIfHighRiskDeviceOperationAsync(
                    deviceUuid,
                    nameof(ReplyMomentCommentAsync),
                    Permissions.MomentOperation.Interact,
                    targetId: circleId.ToString(),
                    metadata: new Dictionary<string, object?> { ["circleId"] = circleId, ["replyCommentId"] = replyCommentId, ["isResend"] = isResend }) is { } permissionDenied)
            {
                return permissionDenied;
            }

            return await _deviceCommandService.ReplyMomentCommentAsync(deviceUuid, circleId, toWeChatId, content, replyCommentId, isResend);

        }



        public async Task<TaskResult> PullFriendMomentsAsync(string deviceUuid, string friendId, long refSnsId = 0, int count = 20, long startTime = 0, long refTime = 0)

        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullFriendMomentsAsync)) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.PullFriendMomentsAsync(deviceUuid, friendId, refSnsId, count, startTime, refTime);

        }



        public async Task<TaskResult> PullMomentDetailAsync(string deviceUuid, long circleId, bool getBigMap = false)

        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullMomentDetailAsync)) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.PullMomentDetailAsync(deviceUuid, circleId, getBigMap);

        }



        public async Task<TaskResult> SyncMomentMessagesAsync(string deviceUuid, bool onlyComment = false, bool getAll = true)

        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncMomentMessagesAsync)) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.SyncMomentMessagesAsync(deviceUuid, onlyComment, getAll);

        }



        public async Task<TaskResult> MarkMomentMessageReadAsync(string deviceUuid, long circleId, int commentId = 0)

        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(MarkMomentMessageReadAsync),
                     Permissions.MomentOperation.Interact,
                     targetId: commentId > 0 ? commentId.ToString() : circleId.ToString(),
                     metadata: new Dictionary<string, object?> { ["circleId"] = circleId, ["commentId"] = commentId }) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.MarkMomentMessageReadAsync(deviceUuid, circleId, commentId);

        }



        public async Task<TaskResult> ClearMomentMessageAsync(string deviceUuid, long circleId, int commentId = 0, bool isRead = true)

        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(ClearMomentMessageAsync),
                     Permissions.MomentOperation.Delete,
                     targetId: commentId > 0 ? commentId.ToString() : circleId.ToString(),
                     destructive: true,
                     metadata: new Dictionary<string, object?> { ["circleId"] = circleId, ["commentId"] = commentId, ["isRead"] = isRead }) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.ClearMomentMessageAsync(deviceUuid, circleId, commentId, isRead);

        }



        public async Task<bool> SendMultiPictureAsync(string deviceUuid, string friendWxid, List<string> imageUrls)

        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SendMultiPictureAsync)) is not null)
             {
                 return false;
             }


            return await _deviceCommandService.SendMultiPictureAsync(deviceUuid, friendWxid, imageUrls);

        }





        public async Task<TaskResult> SphGetMentionAsync(string deviceUuid, long lastLikeId = 0, long lastCommentId = 0, long lastFollowId = 0)

        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SphGetMentionAsync),
                     Permissions.FinderOperation.Read,
                     metadata: new Dictionary<string, object?> { ["lastLikeId"] = lastLikeId, ["lastCommentId"] = lastCommentId, ["lastFollowId"] = lastFollowId }) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.SphGetMentionAsync(deviceUuid, lastLikeId, lastCommentId, lastFollowId);

        }



        public async Task<TaskResult> SphGetCommentAsync(string deviceUuid, long feedId, string nonceId, string feedAuth, long refCommentId = 0, long replyCommentId = 0, int sortType = 0)

        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SphGetCommentAsync),
                     Permissions.FinderOperation.Read,
                     targetId: feedId.ToString(),
                     metadata: new Dictionary<string, object?>
                     {
                         ["feedId"] = feedId,
                         ["nonceEmpty"] = string.IsNullOrWhiteSpace(nonceId),
                         ["feedAuthEmpty"] = string.IsNullOrWhiteSpace(feedAuth),
                         ["refCommentId"] = refCommentId,
                         ["replyCommentId"] = replyCommentId,
                         ["sortType"] = sortType
                     }) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.SphGetCommentAsync(deviceUuid, feedId, nonceId, feedAuth, refCommentId, replyCommentId, sortType);

        }



        public async Task<TaskResult> SphUserPageAsync(string deviceUuid, string sphUserName)

        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SphUserPageAsync),
                     Permissions.FinderOperation.Read,
                     metadata: new Dictionary<string, object?> { ["sphUserNameEmpty"] = string.IsNullOrWhiteSpace(sphUserName) }) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.SphUserPageAsync(deviceUuid, sphUserName);

        }



        public async Task<TaskResult> SphPostAsync(string deviceUuid, string content, List<string> medias, int mediaType = 0, string cover = "")

        {

            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(SphPostAsync),
                    "finder-post",
                    ContentFields(("content", content))) is { } denied)
            {
                return denied;
            }

            if (await DenyIfHighRiskDeviceOperationAsync(
                    deviceUuid,
                    nameof(SphPostAsync),
                    Permissions.FinderOperation.Interact,
                    metadata: new Dictionary<string, object?> { ["mediaCount"] = medias?.Count ?? 0, ["mediaType"] = mediaType, ["hasCover"] = !string.IsNullOrWhiteSpace(cover) }) is { } permissionDenied)
            {
                return permissionDenied;
            }

            return await _deviceCommandService.SphPostAsync(deviceUuid, content, medias ?? new List<string>(), mediaType, cover);

        }



        public async Task<TaskResult> SphCommentAsync(string deviceUuid, long feedId, string nonceId, string feedAuth, int type, string content, string media = "", long replyCommentId = 0, string replyUsername = "")

        {

            if (await DenyIfDeviceOrSensitiveContentAsync(
                    deviceUuid,
                    nameof(SphCommentAsync),
                    "finder-comment",
                    ContentFields(("content", content), ("media", media))) is { } denied)
            {
                return denied;
            }

            if (await DenyIfHighRiskDeviceOperationAsync(
                    deviceUuid,
                    nameof(SphCommentAsync),
                    Permissions.FinderOperation.Interact,
                    targetId: feedId.ToString(),
                    metadata: new Dictionary<string, object?>
                    {
                        ["feedId"] = feedId,
                        ["nonceEmpty"] = string.IsNullOrWhiteSpace(nonceId),
                        ["feedAuthEmpty"] = string.IsNullOrWhiteSpace(feedAuth),
                        ["type"] = type,
                        ["hasMedia"] = !string.IsNullOrWhiteSpace(media),
                        ["replyCommentId"] = replyCommentId,
                        ["replyUsernameEmpty"] = string.IsNullOrWhiteSpace(replyUsername)
                    }) is { } permissionDenied)
            {
                return permissionDenied;
            }

            return await _deviceCommandService.SphCommentAsync(deviceUuid, feedId, nonceId, feedAuth, type, content, media, replyCommentId, replyUsername);

        }



        public async Task<TaskResult> SphLikeAsync(string deviceUuid, long feedId, int type = 1, bool isCancel = false)

        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SphLikeAsync),
                     Permissions.FinderOperation.Interact,
                     targetId: feedId.ToString(),
                     metadata: new Dictionary<string, object?> { ["feedId"] = feedId, ["type"] = type, ["isCancel"] = isCancel }) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.SphLikeAsync(deviceUuid, feedId, type, isCancel);

        }



        public async Task<TaskResult> SphDelCommentAsync(string deviceUuid, long feedId, long commentId)

        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SphDelCommentAsync),
                     Permissions.FinderOperation.DeleteComment,
                     targetId: commentId.ToString(),
                     destructive: true,
                     metadata: new Dictionary<string, object?> { ["feedId"] = feedId, ["commentId"] = commentId }) is { } denied)
             {
                 return denied;
             }


            return await _deviceCommandService.SphDelCommentAsync(deviceUuid, feedId, commentId);

        }



        public async Task<List<MomentsTimeline>> GetMomentsAsync(string deviceUuid, int count = 50, string weChatId = "")
        {
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return new List<MomentsTimeline>();
            }

            var user = await GetCurrentUserAsync();
            if (!await _accountAccessGuard.CanAccessDeviceAsync(user, deviceUuid))
            {
                _logger.LogWarning("CrmService GetMomentsAsync 拒绝越权设备读取。DeviceUuid={DeviceUuid}", deviceUuid);
                return new List<MomentsTimeline>();
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var normalizedWeChatId = weChatId?.Trim() ?? string.Empty;
            WechatAccount? account = null;
            if (!string.IsNullOrWhiteSpace(normalizedWeChatId))
            {
                if (!await _accountAccessGuard.CanAccessAccountAsync(user, normalizedWeChatId))
                {
                    _logger.LogWarning("CrmService GetMomentsAsync 拒绝越权账号读取。AccountId={AccountId}", normalizedWeChatId);
                    return new List<MomentsTimeline>();
                }

                // 同一设备可能历史登录过多个微信号；朋友圈数据按 ownerWxid 落库，
                // 因此前端已选中账号时必须优先按 wxid 查询，不能只取该设备下第一条账号。
                account = await db.WechatAccounts
                    .AsNoTracking()
                    .Where(u => u.wxid == normalizedWeChatId && !u.isDeleted)
                    .OrderByDescending(u => u.clientUuid == deviceUuid)
                    .FirstOrDefaultAsync();
            }

            account ??= await db.WechatAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.clientUuid == deviceUuid && !u.isDeleted);
            if (account == null)
            {
                return new List<MomentsTimeline>();
            }

            if (!await _accountAccessGuard.CanAccessAccountAsync(user, account.wxid))
            {
                _logger.LogWarning("CrmService GetMomentsAsync 拒绝越权账号读取。AccountId={AccountId}", account.wxid);
                return new List<MomentsTimeline>();
            }

            count = Math.Clamp(count, 1, 200);
            var moments = await db.MomentsTimelines
                .AsNoTracking()
                .Where(m => m.ownerWxid == account.wxid)
                .OrderByDescending(m => m.createTime)
                .Take(count)
                .ToListAsync();

            await BackfillMomentDisplayNamesAsync(db, account.wxid, moments);
            return await _sensitiveMaskingService.MaskMomentsAsync(user, moments);
        }

        /// <summary>
        /// 按联系人表回填朋友圈作者、评论者、点赞者昵称。
        /// <para>Contacts 的持久化仍由 DbHelper.SaveContacts 维护，这里只做查询结果展示补名，不写库。</para>
        /// </summary>
        private static async Task BackfillMomentDisplayNamesAsync(ApplicationDbContext db, string ownerWxid, List<MomentsTimeline> moments)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || moments == null || moments.Count == 0)
            {
                return;
            }

            var displayNames = await db.Contacts
                .AsNoTracking()
                .Where(c => c.ownerWxid == ownerWxid && !c.isDeleted)
                .Select(c => new { c.wxid, c.remarks, c.nickname, c.friendNo })
                .ToListAsync();

            var map = displayNames
                .Where(c => !string.IsNullOrWhiteSpace(c.wxid))
                .GroupBy(c => c.wxid)
                .ToDictionary(
                    g => g.Key,
                    g => ResolveContactDisplayName(g.First().remarks, g.First().nickname, g.Key),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var contact in displayNames.Where(c => !string.IsNullOrWhiteSpace(c.friendNo)))
            {
                map.TryAdd(contact.friendNo, ResolveContactDisplayName(contact.remarks, contact.nickname, contact.wxid));
            }

            var accounts = await db.WechatAccounts
                .AsNoTracking()
                .Where(a => !a.isDeleted && !string.IsNullOrWhiteSpace(a.wxid))
                .Select(a => new { a.wxid, a.nickname })
                .ToListAsync();
            foreach (var account in accounts)
            {
                if (!string.IsNullOrWhiteSpace(account.nickname))
                {
                    map.TryAdd(account.wxid, account.nickname);
                }
            }

            map[ownerWxid] = ResolveContactDisplayName(accountRemarks: string.Empty, accountNickname: null, fallback: ownerWxid);
            var selfName = await db.WechatAccounts
                .AsNoTracking()
                .Where(a => a.wxid == ownerWxid)
                .Select(a => a.nickname)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(selfName))
            {
                map[ownerWxid] = selfName!;
            }

            foreach (var moment in moments)
            {
                ApplyMomentDisplayNames(moment, map);
            }
        }

        private static void ApplyMomentDisplayNames(MomentsTimeline moment, Dictionary<string, string> displayNames)
        {
            if (!string.IsNullOrWhiteSpace(moment.userName) && displayNames.TryGetValue(moment.userName, out var authorName))
            {
                moment.nickName = authorName;
            }
            else if (LooksLikeRawWxid(moment.nickName))
            {
                moment.nickName = string.Empty;
            }

            var comments = DeserializeMomentComments(moment.commentsJson);
            foreach (var comment in comments)
            {
                if (!string.IsNullOrWhiteSpace(comment.userName) && displayNames.TryGetValue(comment.userName, out var commentName))
                {
                    comment.nickName = commentName;
                }
                else if (LooksLikeRawWxid(comment.nickName))
                {
                    comment.nickName = string.Empty;
                }
                if (!string.IsNullOrWhiteSpace(comment.replyUserName) && displayNames.TryGetValue(comment.replyUserName, out var replyName))
                {
                    comment.replyNickName = replyName;
                }
                else if (LooksLikeRawWxid(comment.replyNickName))
                {
                    comment.replyNickName = string.Empty;
                }
            }
            moment.commentsJson = JsonSerializer.Serialize(comments);

            var likes = DeserializeMomentLikes(moment.likesJson);
            foreach (var like in likes)
            {
                if (!string.IsNullOrWhiteSpace(like.userName) && displayNames.TryGetValue(like.userName, out var likeName))
                {
                    like.nickName = likeName;
                }
                else if (LooksLikeRawWxid(like.nickName))
                {
                    like.nickName = string.Empty;
                }
            }
            moment.likesJson = JsonSerializer.Serialize(likes);
        }

        private static string ResolveContactDisplayName(string? accountRemarks, string? accountNickname, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(accountRemarks)) return accountRemarks;
            if (!string.IsNullOrWhiteSpace(accountNickname)) return accountNickname;
            return fallback;
        }

        /// <summary>
        /// 判断展示名是否只是原始微信标识。
        /// </summary>
        private static bool LooksLikeRawWxid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var text = value.Trim();
            return text.StartsWith("wxid_", StringComparison.OrdinalIgnoreCase)
                || text.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase)
                || (text.StartsWith("v3_", StringComparison.OrdinalIgnoreCase) && text.EndsWith("@stranger", StringComparison.OrdinalIgnoreCase));
        }

        private static List<MomentCommentDto> DeserializeMomentComments(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<MomentCommentDto>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<MomentCommentDto>>(json) ?? new List<MomentCommentDto>();
            }
            catch
            {
                return new List<MomentCommentDto>();
            }
        }

        private static List<MomentLikeDto> DeserializeMomentLikes(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<MomentLikeDto>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<MomentLikeDto>>(json) ?? new List<MomentLikeDto>();
            }
            catch
            {
                return new List<MomentLikeDto>();
            }
        }




        public async Task<List<FinderMentionNoticeDto>> GetFinderMentionHistoryAsync(string deviceUuid, int count = 10, bool? success = null, long? taskId = null, DateTimeOffset? receivedFrom = null, DateTimeOffset? receivedTo = null)

        {

            var user = await GetCurrentUserAsync();
            if (!await _sensitiveMaskingService.CanReadFinderAsync(user))
            {
                _logger.LogWarning("CrmService GetFinderMentionHistoryAsync 拒绝无视频号读取权限用户。DeviceUuid={DeviceUuid}", deviceUuid);
                return new List<FinderMentionNoticeDto>();
            }

            var raw = await GetFinderHistoryAsync(user, deviceUuid, FinderResultTypeMention, count, success, taskId, receivedFrom, receivedTo, json =>

                JsonSerializer.Deserialize<FinderMentionNoticeDto>(json));

            return await _sensitiveMaskingService.MaskFinderMentionsAsync(user, raw);

        }



        public async Task<List<FinderUserPageDto>> GetFinderUserPageHistoryAsync(string deviceUuid, int count = 10, bool? success = null, long? taskId = null, DateTimeOffset? receivedFrom = null, DateTimeOffset? receivedTo = null)

        {

            var user = await GetCurrentUserAsync();
            if (!await _sensitiveMaskingService.CanReadFinderAsync(user))
            {
                _logger.LogWarning("CrmService GetFinderUserPageHistoryAsync 拒绝无视频号读取权限用户。DeviceUuid={DeviceUuid}", deviceUuid);
                return new List<FinderUserPageDto>();
            }

            var raw = await GetFinderHistoryAsync(user, deviceUuid, FinderResultTypeUserPage, count, success, taskId, receivedFrom, receivedTo, json =>

                JsonSerializer.Deserialize<FinderUserPageDto>(json));

            return await _sensitiveMaskingService.MaskFinderUserPagesAsync(user, raw);

        }



        public async Task<List<FinderCommentListDto>> GetFinderCommentHistoryAsync(string deviceUuid, int count = 10, bool? success = null, long? taskId = null, DateTimeOffset? receivedFrom = null, DateTimeOffset? receivedTo = null)

        {

            var user = await GetCurrentUserAsync();
            if (!await _sensitiveMaskingService.CanReadFinderAsync(user))
            {
                _logger.LogWarning("CrmService GetFinderCommentHistoryAsync 拒绝无视频号读取权限用户。DeviceUuid={DeviceUuid}", deviceUuid);
                return new List<FinderCommentListDto>();
            }

            var raw = await GetFinderHistoryAsync(user, deviceUuid, FinderResultTypeComment, count, success, taskId, receivedFrom, receivedTo, json =>

                JsonSerializer.Deserialize<FinderCommentListDto>(json));

            return await _sensitiveMaskingService.MaskFinderCommentsAsync(user, raw);

        }



        public async Task<FinderHistoryExportDto> ExportFinderHistoryAsync(string deviceUuid, int count = 10, bool? success = null, long? taskId = null, DateTimeOffset? receivedFrom = null, DateTimeOffset? receivedTo = null)

        {

            var normalizedDeviceUuid = deviceUuid?.Trim() ?? string.Empty;
            var normalizedCount = NormalizeFinderHistoryCount(count);
            var export = new FinderHistoryExportDto
            {
                success = false,
                message = string.Empty,
                deviceUuid = normalizedDeviceUuid,
                countPerType = normalizedCount,
                exportedAt = DateTimeOffset.UtcNow,
                exportPermission = Permissions.FinderOperation.Export,
                fieldPolicyVersion = "finder-export-v1",
                masked = true,
                rawPayloadIncluded = false,
                successFilter = success,
                taskIdFilter = taskId,
                receivedFrom = receivedFrom,
                receivedTo = receivedTo
            };

            var user = await GetCurrentUserAsync();
            if (string.IsNullOrWhiteSpace(normalizedDeviceUuid))
            {
                export.message = "deviceUuid 不能为空。";
                await _sensitiveDataAccessAuditService.LogFinderHistoryExportDeniedAsync(
                    user,
                    normalizedDeviceUuid,
                    "empty_device",
                    normalizedCount,
                    success,
                    taskId,
                    receivedFrom,
                    receivedTo,
                    $"CrmService.{nameof(ExportFinderHistoryAsync)}");
                return export;
            }

            if (!await _sensitiveMaskingService.CanExportFinderAsync(user))
            {
                export.message = "缺少视频号导出权限。";
                await _sensitiveDataAccessAuditService.LogFinderHistoryExportDeniedAsync(
                    user,
                    normalizedDeviceUuid,
                    "missing_finder_export_permission",
                    normalizedCount,
                    success,
                    taskId,
                    receivedFrom,
                    receivedTo,
                    $"CrmService.{nameof(ExportFinderHistoryAsync)}");
                return export;
            }

            if (!await _accountAccessGuard.CanAccessDeviceAsync(user, normalizedDeviceUuid))
            {
                export.message = "无权访问该设备。";
                await _sensitiveDataAccessAuditService.LogFinderHistoryExportDeniedAsync(
                    user,
                    normalizedDeviceUuid,
                    "device_access_denied",
                    normalizedCount,
                    success,
                    taskId,
                    receivedFrom,
                    receivedTo,
                    $"CrmService.{nameof(ExportFinderHistoryAsync)}");
                return export;
            }

            await using (var db = await _dbContextFactory.CreateDbContextAsync())
            {
                var account = await db.WechatAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.clientUuid == normalizedDeviceUuid && !item.isDeleted);
                if (account != null && !await _accountAccessGuard.CanAccessAccountAsync(user, account.wxid))
                {
                    export.message = "无权访问该微信账号。";
                    await _sensitiveDataAccessAuditService.LogFinderHistoryExportDeniedAsync(
                        user,
                        normalizedDeviceUuid,
                        "account_access_denied",
                        normalizedCount,
                        success,
                        taskId,
                        receivedFrom,
                        receivedTo,
                        $"CrmService.{nameof(ExportFinderHistoryAsync)}");
                    return export;
                }
            }

            var mentionRaw = await GetFinderHistoryAsync(
                user,
                normalizedDeviceUuid,
                FinderResultTypeMention,
                normalizedCount,
                success,
                taskId,
                receivedFrom,
                receivedTo,
                json => JsonSerializer.Deserialize<FinderMentionNoticeDto>(json));
            var userPageRaw = await GetFinderHistoryAsync(
                user,
                normalizedDeviceUuid,
                FinderResultTypeUserPage,
                normalizedCount,
                success,
                taskId,
                receivedFrom,
                receivedTo,
                json => JsonSerializer.Deserialize<FinderUserPageDto>(json));
            var commentRaw = await GetFinderHistoryAsync(
                user,
                normalizedDeviceUuid,
                FinderResultTypeComment,
                normalizedCount,
                success,
                taskId,
                receivedFrom,
                receivedTo,
                json => JsonSerializer.Deserialize<FinderCommentListDto>(json));

            export.mentionHistory = await _sensitiveMaskingService.MaskFinderMentionsAsync(user, mentionRaw, forExport: true);
            export.userPageHistory = await _sensitiveMaskingService.MaskFinderUserPagesAsync(user, userPageRaw, forExport: true);
            export.commentHistory = await _sensitiveMaskingService.MaskFinderCommentsAsync(user, commentRaw, forExport: true);
            export.success = true;
            export.message = $"视频号历史导出完成：提及 {export.mentionHistory.Count} 条，用户页 {export.userPageHistory.Count} 条，评论 {export.commentHistory.Count} 条。";

            await _sensitiveDataAccessAuditService.LogFinderHistoryExportedAsync(
                user,
                normalizedDeviceUuid,
                normalizedCount,
                export.mentionHistory.Count,
                export.userPageHistory.Count,
                export.commentHistory.Count,
                success,
                taskId,
                receivedFrom,
                receivedTo,
                $"CrmService.{nameof(ExportFinderHistoryAsync)}");

            return export;

        }



        private async Task<List<TDto>> GetFinderHistoryAsync<TDto>(

            ClaimsPrincipal? user,

            string deviceUuid,

            string resultType,

            int count,

            bool? success,

            long? taskId,

            DateTimeOffset? receivedFrom,

            DateTimeOffset? receivedTo,

            Func<string, TDto?> deserialize)

            where TDto : class

        {

            if (string.IsNullOrWhiteSpace(deviceUuid))

            {

                return new List<TDto>();

            }


            if (!await _accountAccessGuard.CanAccessDeviceAsync(user, deviceUuid))
            {
                _logger.LogWarning("CrmService GetFinderHistoryAsync 拒绝越权设备读取。DeviceUuid={DeviceUuid}, ResultType={ResultType}", deviceUuid, resultType);
                return new List<TDto>();
            }



            var finderHistoryLockKey = $"finder-history:{deviceUuid}";

            return await AsyncLockManager.ExecuteWithLockAsync(finderHistoryLockKey, async () =>

            {

                await using var db = await _dbContextFactory.CreateDbContextAsync();

                var account = await db.WechatAccounts

                    .AsNoTracking()

                    .FirstOrDefaultAsync(u => u.clientUuid == deviceUuid && !u.isDeleted);

                if (account != null && !await _accountAccessGuard.CanAccessAccountAsync(user, account.wxid))
                {
                    _logger.LogWarning("CrmService GetFinderHistoryAsync 拒绝越权账号读取。AccountId={AccountId}, ResultType={ResultType}", account.wxid, resultType);
                    return new List<TDto>();
                }

                var ownerKey = BuildFinderOwnerKey(deviceUuid, account?.wxid);

                if (string.IsNullOrWhiteSpace(ownerKey))

                {

                    return new List<TDto>();

                }



                var normalizedCount = count <= 0 ? 10 : Math.Min(count, 100);



                var query = db.FinderResultHistories

                    .AsNoTracking()

                    .Where(item => item.ownerKey == ownerKey && item.resultType == resultType);



                if (success.HasValue)

                {

                    query = query.Where(item => item.success == success.Value);

                }



                if (taskId.HasValue && taskId.Value > 0)

                {

                    query = query.Where(item => item.taskId == taskId.Value);

                }



                if (receivedFrom.HasValue)

                {

                    query = query.Where(item => item.receivedAt >= receivedFrom.Value.UtcDateTime);

                }



                if (receivedTo.HasValue)

                {

                    query = query.Where(item => item.receivedAt <= receivedTo.Value.UtcDateTime);

                }



                var rows = await query

                    .OrderByDescending(item => item.receivedAt)

                    .ThenByDescending(item => item.id)

                    .Take(normalizedCount)

                    .ToListAsync();



                var result = new List<TDto>();

                foreach (var row in rows)

                {

                    if (string.IsNullOrWhiteSpace(row.payloadJson))

                    {

                        continue;

                    }



                    try

                    {

                        var dto = deserialize(row.payloadJson);

                        if (dto != null)

                        {

                            result.Add(dto);

                        }

                    }

                    catch

                    {

                        // 历史记录单条反序列化失败时跳过，避免影响整批结果读取。

                    }

                }



                return result;

            });

        }



        private static string BuildFinderOwnerKey(string? deviceUuid, string? weChatId)

        {

            if (!string.IsNullOrWhiteSpace(deviceUuid))

            {

                return $"device:{deviceUuid}";

            }



            if (!string.IsNullOrWhiteSpace(weChatId))

            {

                return $"wx:{weChatId}";

            }



            return string.Empty;

        }



        private static int NormalizeFinderHistoryCount(int count)

        {

            if (count <= 0)

            {

                return 10;

            }



            return Math.Min(count, 100);

        }

    }

}
