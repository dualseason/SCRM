using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;
using SCRM.SHARED.Models.Dtos;
using SCRM.Services;
using SCRM.API.Services.Core;
using SCRM.API.Services.Security;
using SCRM.Services.Data;
using SCRM.API.Services.Data;
using SCRM.SHARED.Utils;
using SCRM.Models.Constants;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace SCRM.API.Hubs
{
    [Authorize]
    public class ClientHub : Hub
    {
        private readonly SCRM.Services.Data.ApplicationDbContext _context;
        private readonly ConnectionManager _connectionManager;
        private readonly ClientTaskService _clientTaskService;
        private readonly ServerDeviceCommandService _deviceCommandService;
        private readonly AuthService _authService;
        private readonly AccountAccessGuard _accountAccessGuard;
        private readonly SensitiveMaskingService _sensitiveMaskingService;
        private readonly SensitiveDataAccessAuditService _sensitiveDataAccessAuditService;
        private readonly SensitiveWordPolicyService _sensitiveWordPolicyService;
        private readonly SensitiveContentGuard _sensitiveContentGuard;
        private readonly DeviceOperationGuard _deviceOperationGuard;
        private readonly ILogger<ClientHub> _logger;

        public ClientHub(
            SCRM.Services.Data.ApplicationDbContext context, 
            ConnectionManager connectionManager, 
            ClientTaskService clientTaskService, 
            ServerDeviceCommandService deviceCommandService,
            AuthService authService,
            AccountAccessGuard accountAccessGuard,
            SensitiveMaskingService sensitiveMaskingService,
            SensitiveDataAccessAuditService sensitiveDataAccessAuditService,
            SensitiveWordPolicyService sensitiveWordPolicyService,
            SensitiveContentGuard sensitiveContentGuard,
            DeviceOperationGuard deviceOperationGuard,
            ILogger<ClientHub> logger)
        {
            _context = context;
            _connectionManager = connectionManager;
            _clientTaskService = clientTaskService;
            _deviceCommandService = deviceCommandService;
            _authService = authService;
            _accountAccessGuard = accountAccessGuard;
            _sensitiveMaskingService = sensitiveMaskingService;
            _sensitiveDataAccessAuditService = sensitiveDataAccessAuditService;
            _sensitiveWordPolicyService = sensitiveWordPolicyService;
            _sensitiveContentGuard = sensitiveContentGuard;
            _deviceOperationGuard = deviceOperationGuard;
            _logger = logger;
        }

        /// <summary>
        /// 校验当前 Hub 用户是否可访问指定微信账号，并记录拒绝日志。
        /// </summary>
        private async Task<bool> CanAccessAccountOrLogAsync(string? accountId, string operation)
        {
            var allowed = await _accountAccessGuard.CanAccessAccountAsync(Context.User, accountId);
            if (!allowed)
            {
                _logger.LogWarning(
                    "Hub {Operation} denied: user cannot access account. User={User}, AccountId={AccountId}",
                    operation,
                    Context.UserIdentifier ?? Context.User?.Identity?.Name,
                    accountId);
            }

            return allowed;
        }

        /// <summary>
        /// 校验当前 Hub 用户是否可访问指定设备，并记录拒绝日志。
        /// </summary>
        private async Task<bool> CanAccessDeviceOrLogAsync(string? deviceUuid, string operation)
        {
            var allowed = await _accountAccessGuard.CanAccessDeviceAsync(Context.User, deviceUuid);
            if (!allowed)
            {
                _logger.LogWarning(
                    "Hub {Operation} denied: user cannot access device. User={User}, DeviceUuid={DeviceUuid}",
                    operation,
                    Context.UserIdentifier ?? Context.User?.Identity?.Name,
                    deviceUuid);
            }

            return allowed;
        }

        /// <summary>
        /// Hub 设备任务下发前的统一边界：先校验设备归属，再做服务端敏感词风控。
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

            if (!await CanAccessDeviceOrLogAsync(normalizedDeviceUuid, scene))
            {
                return TaskResult.Fail("无权访问该设备");
            }

            var normalizedAccountId = accountId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedAccountId))
            {
                normalizedAccountId = await GetPrimaryWechatIdAsync(normalizedDeviceUuid);
            }

            var check = await _sensitiveContentGuard.CheckAsync(
                Context.User,
                scene,
                normalizedDeviceUuid,
                normalizedAccountId,
                targetId,
                contents,
                $"ClientHub.{scene}",
                Context.GetHttpContext());

            return check.Allowed ? null : TaskResult.Fail(check.Message);
        }

        /// <summary>
        /// Hub 设备任务下发前的统一设备归属校验。
        /// <para>用于无文字内容的设备任务；发送类任务继续走 DenyIfDeviceOrSensitiveContentAsync。</para>
        /// </summary>
        private async Task<TaskResult?> DenyIfNoDeviceAccessAsync(string? deviceUuid, string operation)
        {
            var normalizedDeviceUuid = deviceUuid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedDeviceUuid))
            {
                return TaskResult.Fail("设备ID为空");
            }

            if (!await CanAccessDeviceOrLogAsync(normalizedDeviceUuid, operation))
            {
                return TaskResult.Fail("无权访问该设备");
            }

            return null;
        }

        /// <summary>
        /// Hub 高危设备操作下发前的统一授权与审计。
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
            var result = await _deviceOperationGuard.CheckAsync(
                Context.User,
                deviceUuid,
                operation,
                requiredPermission,
                $"ClientHub.{operation}",
                accountId,
                targetId,
                destructive,
                metadata,
                Context.GetHttpContext());

            return result.Allowed ? null : result.Denied;
        }

        /// <summary>
        /// 构造待风控字段字典；审计只记录字段名和长度，不记录字段原文。
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
        /// 标准化高级发圈请求，避免 SignalR/JSON 传入 null 集合导致任务构造异常。
        /// </summary>
        private static MomentPostRequestDto NormalizeMomentPostRequestForHub(MomentPostRequestDto? request)
        {
            return MomentPostRequestValidator.Normalize(request);
        }

        /// <summary>
        /// 构造高级发圈的文本风控字段。
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
        /// 构造高级发圈审计 metadata；只记录类型、数量和布尔状态，不记录原文。
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

            var normalizedTalker = talker?.Trim() ?? string.Empty;
            var query = _context.Messages
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

        public async Task JoinGroup(string deviceUuid)
        {
            // Validate that the user owns the device with this UUID
            var userId = Context.UserIdentifier;
            var isAdmin = Context.User?.IsInRole("SuperAdmin") == true || Context.User?.IsInRole("Admin") == true;

            if (string.IsNullOrEmpty(userId)) 
            {
                // This should not happen with [Authorize]
                throw new HubException("Unauthorized: User Identifier is missing.");
            }

            // ATOMIC CACHE: Use GetSrClient extension
            var client = await _context.GetSrClient(deviceUuid);

            if (client == null) throw new HubException("Device not found");

            // Allow Admin or Owner
            // Note: client.ownerId check depends on whether it's populated. 
            // Existing GetDevices uses: c.ownerId == userId || c.ownerId == null
            if (isAdmin || client.ownerId == userId || client.ownerId == null)
            {
                // Join the Group named after the Device UUID
                await Groups.AddToGroupAsync(Context.ConnectionId, deviceUuid);
            }
            else
            {
                throw new HubException("Forbidden: You do not own this device.");
            }
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task<IEnumerable<SrClient>> GetDevices()
        {
            var userId = Context.UserIdentifier;
            var userName = Context.User?.Identity?.Name;
            
            var isAdmin = Context.User?.IsInRole("SuperAdmin") == true || Context.User?.IsInRole("Admin") == true;

            Console.WriteLine($"[ClientHub] GetDevices for User: {userId} ({userName}), IsAdmin: {isAdmin}");

            var result = await _authService.GetDevicesForUserAsync(userId ?? string.Empty, isAdmin);

            Console.WriteLine($"[ClientHub] GetDevices found {result.Count} devices.");
            return await _sensitiveMaskingService.MaskDevicesAsync(Context.User, result);
        }

        public async Task<IEnumerable<Contact>> GetContacts(string accountId)
        {
            if (!await CanAccessAccountOrLogAsync(accountId, nameof(GetContacts)))
            {
                return Enumerable.Empty<Contact>();
            }

            // Use Atomic Get
            var contacts = await _context.GetContacts(accountId);
            return await _sensitiveMaskingService.MaskContactsAsync(Context.User, contacts);
        }

        public async Task<IEnumerable<Message>> GetChatHistory(string accountId, string friendWxId)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(friendWxId))
            {
                return Enumerable.Empty<Message>();
            }

            accountId = accountId.Trim();
            if (!await CanAccessAccountOrLogAsync(accountId, nameof(GetChatHistory)))
            {
                return Enumerable.Empty<Message>();
            }

            var target = friendWxId.Trim();
            var isChatRoom = IsChatRoomWxid(target);
            var query = _context.Messages
                .AsNoTracking()
                .Where(m => m.accountId == accountId && !m.isDeleted);

            // 群聊消息可能以 senderWxid=roomId 或 receiverWxid=roomId 入库；
            // 私聊也保留双向匹配，保持与 DbHelper.SaveConversationFromMessage 的会话解析一致。
            query = isChatRoom
                ? query.Where(m => m.chatType == 2 && (m.senderWxid == target || m.receiverWxid == target))
                : query.Where(m => m.senderWxid == target || m.receiverWxid == target);

            var messages = await query
                .OrderByDescending(m => m.sentAt ?? m.receivedAt ?? m.createdAt)
                .Take(50)
                .OrderBy(m => m.sentAt ?? m.receivedAt ?? m.createdAt)
                .ToListAsync();

            await _context.EnrichMessagesWithMediaMetadataAsync(messages);
            return await _sensitiveMaskingService.MaskMessagesAsync(Context.User, messages);
        }

        /// <summary>
        /// 获取指定群聊成员。
        /// <para>用于 Web 聊天页把群消息中的“wxid_xxx:\n正文”展示成昵称；只读查询，不修改持久化状态。</para>
        /// </summary>
        public async Task<IEnumerable<GroupMemberDto>> GetGroupMembers(string accountId, string chatRoomId)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(chatRoomId))
            {
                return Enumerable.Empty<GroupMemberDto>();
            }

            accountId = accountId.Trim();
            if (!await CanAccessAccountOrLogAsync(accountId, nameof(GetGroupMembers)))
            {
                return Enumerable.Empty<GroupMemberDto>();
            }

            var accountKey = BuildWechatAccountNumericKey(accountId);
            var roomId = chatRoomId.Trim();
            var group = await _context.Set<Group>()
                .AsNoTracking()
                .Where(g => g.groupWxid == roomId
                    && !g.isDeleted
                    && (g.wechatAccountId == accountKey || g.wechatAccountId == 0))
                .OrderByDescending(g => g.wechatAccountId == accountKey)
                .ThenByDescending(g => g.updatedAt)
                .FirstOrDefaultAsync();

            if (group == null)
            {
                return Enumerable.Empty<GroupMemberDto>();
            }

            return await _context.Set<GroupMember>()
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
        }

        /// <summary>
        /// 请求手机端按 3056 异步同步当前微信好友列表。
        /// <para>Hub 只负责下发任务；联系人最终仍由 FriendPushNotice 进入 DbHelper.SaveContacts 持久化。</para>
        /// </summary>
        public async Task<TaskResult> SyncContacts(string deviceUuid, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncContacts)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.SyncFriendListAsync(deviceUuid, weChatId);
        }

        /// <summary>
        /// 请求手机端按 3050 回传当前微信账号快照。
        /// <para>用于服务端刚启动或页面显示“未登录”时主动校准设备微信状态。</para>
        /// </summary>
        public async Task<TaskResult> RefreshWeChatAccounts(string deviceUuid)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(RefreshWeChatAccounts)) is { } denied)
             {
                 return denied;
             }

            return await _deviceCommandService.RefreshWeChatAccountsAsync(deviceUuid);
        }

        public async Task<TaskResult> SendMessage(string deviceUuid, string friendWxId, string content, int type = 1, string atIds = "")
        {
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(SendMessage),
                     "conversation",
                     ContentFields(("content", content), ("atIds", atIds))) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
            // Delegate deeply to ClientTaskService
            var normalizedType = NormalizeTalkToFriendContentType(type, content);
            return await _clientTaskService.SendTalkToFriendTaskAsync(connectionId, friendWxId, content, (EnumContentType)normalizedType, atIds);
        }

        /// <summary>
        /// 归一化聊天内容类型，兜底修正旧页面/旧脚本按文本提交的媒体 URL。
        /// </summary>
        private static int NormalizeTalkToFriendContentType(int type, string? content)
        {
            if (type != (int)EnumContentType.UnknownContent && type != (int)EnumContentType.Text)
            {
                return type;
            }

            var text = content?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return type;
            }

            var lower = text.Split('?', '#')[0].ToLowerInvariant();
            if (lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".png")
                || lower.EndsWith(".gif") || lower.EndsWith(".webp") || lower.EndsWith(".bmp"))
            {
                return (int)EnumContentType.Picture;
            }

            if (lower.EndsWith(".mp4") || lower.EndsWith(".mov") || lower.EndsWith(".m4v")
                || lower.EndsWith(".3gp") || lower.EndsWith(".avi")
                || lower.EndsWith(".mkv") || lower.EndsWith(".webm"))
            {
                return (int)EnumContentType.Video;
            }

            if (text.StartsWith("{", StringComparison.Ordinal) && text.Contains("\"url\"", StringComparison.OrdinalIgnoreCase))
            {
                return (int)EnumContentType.File;
            }

            if (type == 8)
            {
                return (int)EnumContentType.File;
            }

            return type == (int)EnumContentType.UnknownContent ? (int)EnumContentType.Text : type;
        }

        public async Task<bool> SyncChatRooms(string deviceUuid, int flag = 0, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncChatRooms)) is not null)
             {
                 return false;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return false;

             var roomTaskId = DateTime.UtcNow.Ticks;
             var conversationTaskId = roomTaskId + 1;
             var roomSent = await _clientTaskService.SendTriggerChatRoomPushTaskAsync(connectionId, roomTaskId, flag, weChatId);
             var conversationSent = await _clientTaskService.SendTriggerConversationPushTaskAsync(
                 connectionId,
                 withName: true,
                 limit: 100,
                 taskId: conversationTaskId);

             _logger.LogInformation(
                 "Hub SyncChatRooms dispatched: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, RoomTaskId={RoomTaskId}, RoomSent={RoomSent}, ConversationTaskId={ConversationTaskId}, ConversationSent={ConversationSent}",
                 deviceUuid,
                 connectionId,
                 roomTaskId,
                 roomSent,
                 conversationTaskId,
                 conversationSent);

             return roomSent || conversationSent;
        }


        /// <summary>
        /// 拉取群邀请列表。
        /// </summary>
        public async Task<bool> GetChatRoomInviteList(string deviceUuid)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(GetChatRoomInviteList)) is not null)
             {
                 return false;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return false;
             return await _clientTaskService.SendGetChatRoomInviteListTaskAsync(connectionId, string.Empty, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 查询已落库的群邀请审批列表。
        /// <para>该接口不触发设备任务，只读取 ChatRoomInvitePush/List Notice 的持久化结果。</para>
        /// </summary>
        public async Task<List<GroupInvitationDto>> GetGroupInvitations(string accountId, string chatRoomId = "", int count = 100, bool pendingOnly = false)
        {
             if (string.IsNullOrWhiteSpace(accountId))
             {
                 return new List<GroupInvitationDto>();
             }

             accountId = accountId.Trim();
             if (!await CanAccessAccountOrLogAsync(accountId, nameof(GetGroupInvitations)))
             {
                 return new List<GroupInvitationDto>();
             }

             return await _context.GetGroupInvitations(accountId, chatRoomId, count, pendingOnly);
        }

        /// <summary>
        /// 二维码入群。
        /// </summary>
        public async Task<TaskResult> JoinGroupByQr(string deviceUuid, string qrUrl = "", string qrContent = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(JoinGroupByQr)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
             return await _clientTaskService.SendJoinGroupByQrTaskAsync(connectionId, qrUrl, qrContent, string.Empty, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 拉取群二维码，成功时 message 中返回二维码地址。
        /// </summary>
        public async Task<TaskResult> PullChatRoomQrCode(string deviceUuid, string chatRoomId)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullChatRoomQrCode)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             return await _clientTaskService.SendPullChatRoomQrCodeTaskAsync(connectionId, chatRoomId, string.Empty, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 拉取个人微信二维码。
        /// </summary>
        public async Task<TaskResult> PullWeChatQrCode(string deviceUuid)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullWeChatQrCode)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendPullWeChatQrCodeTaskAsync(connectionId, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 拉取附近 POI 列表。
        /// </summary>
        public async Task<TaskResult> GetPoiList(string deviceUuid, double lat, double lng, string keyword = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(GetPoiList)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendGetPoiListTaskAsync(connectionId, lat, lng, keyword, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 拉取微信表情详情。
        /// </summary>
        public async Task<TaskResult> PullEmojiInfo(string deviceUuid, string md5)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullEmojiInfo)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendPullEmojiInfoTaskAsync(connectionId, md5, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 对指定聊天表情消息执行消息级补图。
        /// </summary>
        public async Task<TaskResult> PullEmojiInfoForMessage(string deviceUuid, string md5, long msgSvrId, string friendId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullEmojiInfoForMessage)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendPullEmojiInfoForMessageTaskAsync(connectionId, weChatId, md5, msgSvrId, friendId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 搜索微信联系人。
        /// </summary>
        public async Task<TaskResult> FindContact(string deviceUuid, string content)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(FindContact)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendFindContactTaskAsync(connectionId, content, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 查询微信定位。
        /// </summary>
        public async Task<TaskResult> GetWeChatLocation(string deviceUuid, bool noCache = false)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(GetWeChatLocation),
                     Permissions.WechatOperation.LocationQuery,
                     destructive: false,
                     metadata: new Dictionary<string, object?> { ["noCache"] = noCache }) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendWeChatLocationTaskAsync(connectionId, noCache, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 查询微信零钱和银行卡摘要。
        /// </summary>
        public async Task<TaskResult> GetWalletBalance(string deviceUuid, int flag = 0)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(GetWalletBalance),
                     Permissions.WechatOperation.WalletQuery,
                     destructive: false,
                     metadata: new Dictionary<string, object?> { ["flag"] = flag }) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendWalletBalanceTaskAsync(connectionId, flag, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 查询手机状态。
        /// </summary>
        public async Task<TaskResult> GetPhoneState(string deviceUuid, string imei = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(GetPhoneState)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendPhoneStateTaskAsync(connectionId, imei, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 发送手机短信。
        /// </summary>
        public async Task<TaskResult> SendSms(string deviceUuid, string number, string content)
        {
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(SendSms),
                     "sms-recipient",
                     ContentFields(("number", number), ("content", content))) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             var imei = await GetDeviceImeiAsync(deviceUuid);
             return await _clientTaskService.SendSmsTaskAsync(connectionId, weChatId, imei, number, content, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 拉取手机短信历史。
        /// </summary>
        public async Task<TaskResult> PullSms(string deviceUuid, long startTime, long endTime)
        {
             if (!await CanAccessDeviceOrLogAsync(deviceUuid, nameof(PullSms)))
             {
                 return TaskResult.Fail("无权访问该设备");
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             var imei = await GetDeviceImeiAsync(deviceUuid);
             return await _clientTaskService.SendPullSmsTaskAsync(connectionId, weChatId, imei, startTime, endTime, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 拉取手机通话记录。
        /// </summary>
        public async Task<TaskResult> PullCallLogs(string deviceUuid, long startTime, long endTime)
        {
             if (!await CanAccessDeviceOrLogAsync(deviceUuid, nameof(PullCallLogs)))
             {
                 return TaskResult.Fail("无权访问该设备");
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             var imei = await GetDeviceImeiAsync(deviceUuid);
             return await _clientTaskService.SendPullCallLogTaskAsync(connectionId, weChatId, imei, startTime, endTime, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 读取已落库短信记录。
        /// </summary>
        public async Task<List<SmsRecordDto>> GetSmsRecords(string accountId, string imei = "", int count = 200)
        {
             if (!await CanAccessAccountOrLogAsync(accountId, nameof(GetSmsRecords)))
             {
                 return new List<SmsRecordDto>();
             }

             var records = await _context.GetSmsRecords(accountId, imei, count);
             var profile = await _sensitiveMaskingService.BuildProfileAsync(Context.User);
             await _sensitiveDataAccessAuditService.LogSensitiveFieldsReturnedAsync(
                 Context.User,
                 "SmsRecords",
                 accountId,
                 BuildSmsSensitiveFields(records, profile),
                 records.Count,
                 nameof(GetSmsRecords),
                 imei: imei,
                 detail: $"count={Math.Clamp(count, 1, 500)}",
                 httpContext: Context.GetHttpContext());
             return records.Select(record => SensitiveMaskingService.MaskSmsRecord(record, profile)).ToList();
        }

        /// <summary>
        /// 读取已落库通话记录。
        /// </summary>
        public async Task<List<CallLogRecordDto>> GetCallLogRecords(string accountId, string imei = "", int count = 200)
        {
             if (!await CanAccessAccountOrLogAsync(accountId, nameof(GetCallLogRecords)))
             {
                 return new List<CallLogRecordDto>();
             }

             var records = await _context.GetCallLogRecords(accountId, imei, count);
             var profile = await _sensitiveMaskingService.BuildProfileAsync(Context.User);
             await _sensitiveDataAccessAuditService.LogSensitiveFieldsReturnedAsync(
                 Context.User,
                 "CallLogRecords",
                 accountId,
                 BuildCallLogSensitiveFields(records, profile),
                 records.Count,
                 nameof(GetCallLogRecords),
                 imei: imei,
                 detail: $"count={Math.Clamp(count, 1, 500)}",
                 httpContext: Context.GetHttpContext());
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
        /// 同步企微用户列表。
        /// </summary>
        public async Task<TaskResult> SyncQwUsers(string deviceUuid)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncQwUsers)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendTriggerQwUserPushTaskAsync(connectionId, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 同步聊天消息 MsgSvrId 快照。
        /// </summary>
        public async Task<TaskResult> SyncChatMsgIds(string deviceUuid, long startTime, long endTime)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncChatMsgIds)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendTriggerChatMsgIdsPushTaskAsync(connectionId, startTime, endTime, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 同步历史聊天消息。
        /// </summary>
        public async Task<TaskResult> SyncHistoryMessages(string deviceUuid, string friendId = "", long startTime = 0, long endTime = 0, int flag = 0, int count = 50)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncHistoryMessages)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendTriggerHistoryMsgPushTaskAsync(connectionId, weChatId, friendId, startTime, endTime, flag, count, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 同步指定会话已读状态。
        /// </summary>
        public async Task<TaskResult> SyncMessageRead(string deviceUuid, string friendId)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncMessageRead)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendTriggerMessageReadTaskAsync(connectionId, weChatId, friendId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 同步未读会话列表。
        /// </summary>
        public async Task<TaskResult> SyncUnreadList(string deviceUuid)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncUnreadList)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendTriggerUnreadPushTaskAsync(connectionId, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 同步单个会话未读状态。
        /// </summary>
        public async Task<TaskResult> SyncConversationUnread(string deviceUuid, string friendId)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncConversationUnread)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendTriggerUnReadTaskAsync(connectionId, weChatId, friendId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 同步业务联系人列表。
        /// </summary>
        public async Task<TaskResult> SyncBizContacts(string deviceUuid)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncBizContacts)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendTriggerBizContactPushTaskAsync(connectionId, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 同步企微会话列表。
        /// </summary>
        public async Task<TaskResult> SyncQwConversations(string deviceUuid, long startTime = 0, long endTime = 0, int limit = 100, int offset = 0)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncQwConversations)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendTriggerQwConvPushTaskAsync(connectionId, weChatId, startTime, endTime, limit, offset, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 同步联系人标签列表。
        /// </summary>
        public async Task<TaskResult> SyncContactLabels(string deviceUuid)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncContactLabels)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendTriggerLabelPushTaskAsync(connectionId, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 查询已落库的联系人标签字典。
        /// <para>该方法只读 ContactTags；真正同步由 SyncContactLabels 下发 TriggerLabelPushTask 后等待异步 Notice。</para>
        /// </summary>
        public async Task<List<ContactLabelDto>> GetContactLabels(string accountId, bool includeDeleted = false)
        {
             if (string.IsNullOrWhiteSpace(accountId))
             {
                 return new List<ContactLabelDto>();
             }

             accountId = accountId.Trim();
             if (!await CanAccessAccountOrLogAsync(accountId, nameof(GetContactLabels)))
             {
                 return new List<ContactLabelDto>();
             }

             return await _context.GetContactLabels(accountId, includeDeleted);
        }

        /// <summary>
        /// 创建或重命名联系人标签，也可通过 AddList/DelList 调整标签成员。
        /// </summary>
        public async Task<TaskResult> SaveContactLabel(string deviceUuid, string labelName, int labelId = 0, string addList = "", string delList = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SaveContactLabel)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendContactLabelTaskAsync(connectionId, weChatId, labelName, labelId, addList, delList, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 删除联系人标签。
        /// </summary>
        public async Task<TaskResult> DeleteContactLabel(string deviceUuid, int labelId)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(DeleteContactLabel)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendContactLabelDeleteTaskAsync(connectionId, weChatId, labelId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 设置单个好友的完整标签 ID 集合。
        /// </summary>
        public async Task<TaskResult> SetContactLabels(string deviceUuid, string friendId, int[] labelIds)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SetContactLabels)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendContactSetLabelTaskAsync(connectionId, weChatId, friendId, labelIds, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端回传当前配置快照。
        /// </summary>
        public async Task<TaskResult> TriggerConfigPush(string deviceUuid)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(TriggerConfigPush)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var taskId = DateTime.UtcNow.Ticks;
             var sent = await _clientTaskService.SendTriggerConfigPushTaskAsync(connectionId, taskId);
             return sent
                 ? TaskResult.Ok(taskId, "配置同步指令已下发，等待客户端上报配置快照")
                 : TaskResult.Fail(taskId, "配置同步指令下发失败");
        }

        /// <summary>
        /// 下发 Android 设备级配置。
        /// <para>只允许 62203 已知 SetConfigTask 键，避免账号设置与设备配置混用。</para>
        /// </summary>
        public async Task<TaskResult> SetDeviceConfig(string deviceUuid, DeviceConfigDto config)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SetDeviceConfig),
                     Permissions.DeviceTask.ConfigManage,
                     destructive: false) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             if (config == null || config.IsEmpty) return TaskResult.Fail("没有选择任何需要下发的配置项");

             var knownBoolKeys = new HashSet<string>(StringComparer.Ordinal)
             {
                 "fastSend", "silentFunc", "silentAccept", "autoPic", "autoLogin", "addInWw",
                 "lightscn", "forceRun", "disturb", "moreLog", "wx_show_alias", "wx_can_delete",
                 "wx_can_block", "wx_can_exitGroup", "wx_del_conv", "wx_can_logout",
                 "wx_can_changeAcnt", "wx_can_acntInfo", "wx_show_toast", "wx_can_sendcard"
             };
             var knownIntKeys = new HashSet<string>(StringComparer.Ordinal) { "keepWake", "port" };
             var knownStrKeys = new HashSet<string>(StringComparer.Ordinal) { "host", "fileUpUrl" };

             var boolConfs = NormalizeDeviceConfigMap(config.BoolConfs, knownBoolKeys);
             var intConfs = NormalizeDeviceConfigMap(config.IntConfs, knownIntKeys);
             var strConfs = NormalizeDeviceConfigMap(config.StrConfs, knownStrKeys);
             if (boolConfs.Count == 0 && intConfs.Count == 0 && strConfs.Count == 0)
             {
                 return TaskResult.Fail("没有有效的 62203 配置键可下发");
             }

             if (intConfs.TryGetValue("port", out var port) && (port <= 0 || port > 65535))
             {
                 return TaskResult.Fail("port 必须在 1 到 65535 之间");
             }

             if (intConfs.TryGetValue("keepWake", out var keepWake) && keepWake < 0)
             {
                 return TaskResult.Fail("keepWake 不能小于 0");
             }

             var sent = await _clientTaskService.SendSetConfigTaskAsync(connectionId, boolConfs, intConfs, strConfs);
             var count = boolConfs.Count + intConfs.Count + strConfs.Count;
             return sent
                 ? TaskResult.Ok(0, $"设备配置已下发：{count} 项")
                 : TaskResult.Fail("设备配置下发失败");
        }

        /// <summary>
        /// 下发微信违禁词列表。
        /// <para>SetForbiddenWord 没有任务回执，返回值仅表示消息是否成功写入 Netty 通道。</para>
        /// </summary>
        public async Task<TaskResult> SetForbiddenWord(string deviceUuid, string[] words)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SetForbiddenWord),
                     Permissions.DeviceTask.SensitiveConfigManage,
                     destructive: false) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             if (string.IsNullOrWhiteSpace(weChatId)) return TaskResult.Fail("WeChat account not found");

             var normalizedWords = SensitiveWordPolicyService.NormalizeWords(words).ToArray();
             await _sensitiveWordPolicyService.SaveDeviceBlockWordsAsync(deviceUuid, weChatId, normalizedWords);
             var sent = await _clientTaskService.SendSetForbiddenWordAsync(connectionId, weChatId, normalizedWords);
             return sent
                 ? TaskResult.Ok(0, $"违禁词列表已下发：Count={normalizedWords.Length}")
                 : TaskResult.Fail("违禁词列表下发失败");
        }

        private static Dictionary<string, TValue> NormalizeDeviceConfigMap<TValue>(
            IDictionary<string, TValue>? source,
            ISet<string> allowList)
        {
             var result = new Dictionary<string, TValue>(StringComparer.Ordinal);
             if (source == null) return result;

             foreach (var item in source)
             {
                 var key = item.Key?.Trim() ?? string.Empty;
                 if (string.IsNullOrWhiteSpace(key) || !allowList.Contains(key))
                 {
                     continue;
                 }

                 result[key] = item.Value;
             }

             return result;
        }

        /// <summary>
        /// 拉取微信好友申请历史/补偿列表。
        /// <para>
        /// Hub 只负责把 PullFriendAddReqListTask(1234) 下发到当前在线设备；
        /// 结果由 FriendAddReqListNotice(2036) 异步回传并刷新好友请求页面。
        /// </para>
        /// </summary>
        public async Task<TaskResult> PullFriendAddReqList(string deviceUuid, long startTime = 0, bool onlyNew = true, bool getAll = false)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullFriendAddReqList)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendPullFriendAddReqListTaskAsync(connectionId, startTime, onlyNew, getAll, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 按 MsgSvrId 补偿单条聊天消息。
        /// <para>结果由 RequestTalkMsgTaskResultNotice 异步回传并刷新会话。</para>
        /// </summary>
        public async Task<TaskResult> RequestTalkMsg(string deviceUuid, long msgSvrId)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(RequestTalkMsg)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendRequestTalkMsgTaskAsync(connectionId, weChatId, msgSvrId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 按 MsgSvrId 补偿原始聊天正文/XML。
        /// </summary>
        public async Task<TaskResult> RequestTalkContent(string deviceUuid, long msgSvrId)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(RequestTalkContent)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendRequestTalkContentTaskAsync(connectionId, weChatId, msgSvrId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 补偿聊天消息详情。
        /// </summary>
        public async Task<TaskResult> RequestTalkDetail(string deviceUuid, string friendId, long msgId, string msgSvrId = "", string md5 = "", bool getOriginal = false)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(RequestTalkDetail)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendRequestTalkDetailTaskAsync(connectionId, weChatId, friendId, msgId, msgSvrId, md5, getOriginal, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端对指定语音消息执行语音转文字。
        /// <para>Android 通过通用 TaskResultNotice 回传识别结果，服务端按 TaskId 上下文落库。</para>
        /// </summary>
        public async Task<TaskResult> VoiceTransText(string deviceUuid, string friendId, long msgSvrId)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(VoiceTransText)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendVoiceTransTextTaskAsync(connectionId, weChatId, friendId, msgSvrId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端执行 GetA8Key。
        /// </summary>
        public async Task<TaskResult> GetA8Key(string deviceUuid, int type, string url, string userName = "", string msgSvrId = "", int reason = 0)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(GetA8Key)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             if (string.IsNullOrWhiteSpace(url)) return TaskResult.Fail("Url 不能为空");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendGetA8KeyTaskAsync(connectionId, weChatId, type, url, userName, msgSvrId, reason, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 修改微信资料或隐私设置。
        /// </summary>
        public async Task<TaskResult> UpdateWechatSetting(string deviceUuid, int action, string content = "", int intParam = 0)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(UpdateWechatSetting)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             if (!System.Enum.IsDefined(typeof(EnumSettings), action)) return TaskResult.Fail($"不支持的微信设置动作：{action}");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             if (string.IsNullOrWhiteSpace(weChatId)) return TaskResult.Fail("WeChat account not found");
             return await _clientTaskService.SendWechatSettingTaskAsync(connectionId, weChatId, (EnumSettings)action, content, intParam, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端撤回指定聊天消息。
        /// </summary>
        public async Task<TaskResult> RevokeMessage(string deviceUuid, string friendId, long msgSvrId)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(RevokeMessage),
                     Permissions.MessageOperation.Revoke,
                     friendId,
                     destructive: true,
                     metadata: new Dictionary<string, object?> { ["msgSvrId"] = msgSvrId }) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendRevokeMessageTaskAsync(connectionId, weChatId, friendId, msgSvrId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端转发一条已有聊天消息。
        /// </summary>
        public async Task<TaskResult> ForwardMessage(string deviceUuid, string talker, long msgSvrId, string friendIds, string extMsg = "")
        {
             var riskFields = await BuildForwardMessageContentFieldsAsync(deviceUuid, null, talker, new[] { msgSvrId }, extMsg);
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(ForwardMessage),
                     "forward-targets",
                     riskFields) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendForwardMessageTaskAsync(connectionId, weChatId, talker, msgSvrId, friendIds, extMsg, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端转发多条已有聊天消息。
        /// </summary>
        public async Task<TaskResult> ForwardMultiMessage(string deviceUuid, string talker, long[] msgIds, string friendIds, string extMsg = "", bool sendRecord = false)
        {
             var riskFields = await BuildForwardMessageContentFieldsAsync(deviceUuid, null, talker, msgIds, extMsg);
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(ForwardMultiMessage),
                     "forward-targets",
                     riskFields) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendForwardMultiMessageTaskAsync(connectionId, weChatId, talker, msgIds, friendIds, extMsg, sendRecord, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端按原始内容转发消息。
        /// </summary>
        public async Task<TaskResult> ForwardMessageByContent(string deviceUuid, string friendIds, long msgSvrId, int msgType, string content, string thumb = "", string extMsg = "")
        {
             var riskFields = await BuildForwardMessageContentFieldsAsync(deviceUuid, null, null, new[] { msgSvrId }, extMsg, content);
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(ForwardMessageByContent),
                     "forward-content-targets",
                     riskFields) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendForwardMessageByContentTaskAsync(connectionId, weChatId, friendIds, msgSvrId, msgType, content, thumb, extMsg, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端清空微信端聊天记录。
        /// </summary>
        public async Task<TaskResult> ClearAllChatMsg(string deviceUuid, int flag = 0)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(ClearAllChatMsg),
                     Permissions.MessageOperation.ClearWechat,
                     destructive: true,
                     metadata: new Dictionary<string, object?> { ["flag"] = flag }) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendClearAllChatMsgTaskAsync(connectionId, weChatId, flag, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端查询红包详情；结果由 QueryHbDetailTaskResultNotice 异步推送。
        /// </summary>
        public async Task<TaskResult> QueryHbDetail(string deviceUuid, string hbUrl)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(QueryHbDetail)) is { } denied)
             {
                 return denied;
             }

             if (string.IsNullOrWhiteSpace(hbUrl)) return TaskResult.Fail("红包链接为空");
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             var queued = await _clientTaskService.SendQueryHbDetailTaskAsync(connectionId, weChatId, hbUrl.Trim());
             return queued ? TaskResult.Ok(0, "红包详情查询任务已下发，等待客户端异步结果") : TaskResult.Fail("红包详情查询任务下发失败");
        }

        /// <summary>
        /// 请求客户端查询红包状态；结果由 QueryHbStatusTaskResultNotice 异步推送。
        /// </summary>
        public async Task<TaskResult> QueryHbStatus(string deviceUuid, string hbUrl)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(QueryHbStatus)) is { } denied)
             {
                 return denied;
             }

             if (string.IsNullOrWhiteSpace(hbUrl)) return TaskResult.Fail("红包链接为空");
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             var queued = await _clientTaskService.SendQueryHbStatusTaskAsync(connectionId, weChatId, hbUrl.Trim());
             return queued ? TaskResult.Ok(0, "红包状态查询任务已下发，等待客户端异步结果") : TaskResult.Fail("红包状态查询任务下发失败");
        }

        /// <summary>
        /// 请求客户端发送微信红包，金额单位为分。
        /// <para>支付密码只随本次任务传输，不在服务端日志中输出。</para>
        /// </summary>
        public async Task<TaskResult> SendLuckyMoney(string deviceUuid, string friendId, int money, int number, string passwd, string wish = "")
        {
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(SendLuckyMoney),
                     "payment",
                     ContentFields(("wish", wish))) is { } denied)
             {
                 return denied;
             }

             if (string.IsNullOrWhiteSpace(friendId)) return TaskResult.Fail("红包接收人为空");
             if (money < 1 || money > 20000) return TaskResult.Fail("红包金额必须在 1 到 20000 分之间");
             if (number < 1 || number > 100) return TaskResult.Fail("红包个数必须在 1 到 100 之间");
             if (!IsValidPaymentPassword(passwd)) return TaskResult.Fail("支付密码必须为 6 位数字");

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendLuckyMoneyTaskAsync(connectionId, weChatId, friendId.Trim(), money, number, passwd.Trim(), wish?.Trim() ?? string.Empty, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端执行微信转账，金额单位为分。
        /// </summary>
        public async Task<TaskResult> Remittance(string deviceUuid, string friendId, int money, string passwd, string memo = "", string roomId = "")
        {
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(Remittance),
                     "payment",
                     ContentFields(("memo", memo))) is { } denied)
             {
                 return denied;
             }

             if (string.IsNullOrWhiteSpace(friendId)) return TaskResult.Fail("转账收款人为空");
             if (money < 1) return TaskResult.Fail("转账金额必须大于 0 分");
             if (!IsValidPaymentPassword(passwd)) return TaskResult.Fail("支付密码必须为 6 位数字");

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendRemittanceTaskAsync(connectionId, weChatId, friendId.Trim(), money, passwd.Trim(), memo?.Trim() ?? string.Empty, DateTime.UtcNow.Ticks, roomId?.Trim() ?? string.Empty);
        }

        /// <summary>
        /// 请求客户端执行微信账号登出。
        /// </summary>
        public async Task<TaskResult> WechatLogout(string deviceUuid)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(WechatLogout),
                     Permissions.WechatOperation.Logout,
                     destructive: true) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             var queued = await _clientTaskService.SendWechatLogoutTaskAsync(connectionId, weChatId);
             return queued ? TaskResult.Ok(0, "微信登出任务已下发，等待客户端上报账号状态") : TaskResult.Fail("微信登出任务下发失败");
        }

        /// <summary>
        /// 请求客户端下载微信 CDN 媒体文件。
        /// </summary>
        public async Task<TaskResult> DownloadCdnFile(
            string deviceUuid,
            string cdnUrl,
            string cdnKey,
            int fileType,
            string fileId = "",
            string fileFmt = "",
            int fileSize = 0,
            long msgSvrId = 0)
        {
             if (!await CanAccessDeviceOrLogAsync(deviceUuid, nameof(DownloadCdnFile)))
             {
                 return TaskResult.Fail("无权访问该设备");
             }

             var profile = await _sensitiveMaskingService.BuildProfileAsync(Context.User);
             if (!profile.CanViewMessageRaw)
             {
                 _logger.LogWarning(
                     "Hub {Operation} denied: user cannot download raw media. User={User}, DeviceUuid={DeviceUuid}, MsgSvrId={MsgSvrId}",
                     nameof(DownloadCdnFile),
                     Context.UserIdentifier ?? Context.User?.Identity?.Name,
                     deviceUuid,
                     msgSvrId);
                 return TaskResult.Fail("无权下载原始媒体文件");
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             if (!string.IsNullOrWhiteSpace(weChatId)
                 && !await CanAccessAccountOrLogAsync(weChatId, nameof(DownloadCdnFile)))
             {
                 return TaskResult.Fail("无权访问该微信账号");
             }
             var normalizedFileType = System.Enum.IsDefined(typeof(CDNFileType), fileType)
                 ? (CDNFileType)fileType
                 : CDNFileType.ChatMsgFile;
             return await _clientTaskService.SendCDNDownloadFileTaskAsync(
                 connectionId,
                 weChatId,
                 cdnUrl,
                 cdnKey,
                 normalizedFileType,
                 fileId,
                 fileFmt,
                 fileSize,
                 msgSvrId,
                 DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 启动好友检测/清粉任务。
        /// </summary>
        public async Task<TaskResult> StartFriendDetect(string deviceUuid, string message, bool onlyCheck = true, int skipHour = 24, int mode = 0, int max = 0)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(StartFriendDetect)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendPostFriendDetectTaskAsync(
                 connectionId,
                 weChatId,
                 message,
                 onlyCheck,
                 skipHour,
                 mode,
                 max,
                 DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 停止好友检测/清粉任务。
        /// </summary>
        public async Task<TaskResult> StopFriendDetect(string deviceUuid, long taskId = 0)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(StopFriendDetect)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendPostStopFriendDetectTaskAsync(connectionId, weChatId, taskId == 0 ? DateTime.UtcNow.Ticks : taskId);
        }

        /// <summary>
        /// 拉取好友检测/清粉最终结果。
        /// </summary>
        public async Task<TaskResult> GetFriendDetectResult(string deviceUuid)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(GetFriendDetectResult)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             return await _clientTaskService.SendGetFriendDetectResultTaskAsync(connectionId, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 确认群邀请。
        /// </summary>
        public async Task<bool> ApproveChatRoomInvite(string deviceUuid, long msgSvrId, string roomId = "", string msgContent = "", long msgId = 0)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(ApproveChatRoomInvite)) is not null)
             {
                 return false;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return false;
             var finalMsgId = msgId != 0 ? msgId : msgSvrId;
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             var result = await _clientTaskService.SendChatRoomInviteApproveTaskAsync(connectionId, msgSvrId, roomId, msgContent, weChatId, finalMsgId, DateTime.UtcNow.Ticks);
             return result.success;
        }

        /// <summary>
        /// 下发群接龙。
        /// </summary>
        public async Task<TaskResult> SendJielong(string deviceUuid, string chatRoomId, string content, string title = "", string sample = "", string memo = "", long msgSvrId = 0)
        {
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(SendJielong),
                     "chatroom",
                     ContentFields(("content", content), ("title", title), ("sample", sample), ("memo", memo))) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
             return await _clientTaskService.SendJielongTaskAsync(connectionId, chatRoomId, content, title, sample, memo, msgSvrId, string.Empty, DateTime.UtcNow.Ticks);
        }

        public async Task<TaskResult> SyncMoments(string deviceUuid, long startTime = 0, long[]? circleIds = null, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncMoments)) is { } denied)
             {
                 return denied;
             }

             _logger.LogInformation("Hub SyncMoments request: DeviceUuid={DeviceUuid}, StartTime={StartTime}, CircleIdCount={CircleIdCount}, WeChatId={WeChatId}",
                 deviceUuid, startTime, circleIds?.Length ?? 0, weChatId);
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             if (string.IsNullOrWhiteSpace(weChatId))
             {
                 weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             }
             var taskId = DateTime.UtcNow.Ticks;
             var success = await _clientTaskService.SendTriggerCirclePushTaskAsync(connectionId, taskId, weChatId, startTime, circleIds);
             var result = success
                 ? new TaskResult { taskId = taskId, success = true, message = "朋友圈同步指令已下发，等待客户端回执" }
                 : new TaskResult { taskId = taskId, success = false, message = "朋友圈同步任务下发失败" };
             _logger.LogInformation("Hub SyncMoments result: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, Success={Success}, Message={Message}",
                 deviceUuid, connectionId, result.success, result.message);
             return result;
        }

        public async Task<TaskResult> DeleteMoment(string deviceUuid, long circleId)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(DeleteMoment),
                     Permissions.MomentOperation.Delete,
                     targetId: circleId.ToString(),
                     destructive: true,
                     metadata: new Dictionary<string, object?> { ["circleId"] = circleId }) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             if (string.IsNullOrEmpty(weChatId)) return TaskResult.Fail("WeChat account not found");
             return await _clientTaskService.SendDeleteSNSNewsTaskAsync(connectionId, weChatId, circleId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 普通朋友圈点赞/取消点赞。
        /// </summary>
        public async Task<TaskResult> LikeMoment(string deviceUuid, long circleId, bool isCancel = false)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(LikeMoment),
                     Permissions.MomentOperation.Interact,
                     targetId: circleId.ToString(),
                     metadata: new Dictionary<string, object?> { ["circleId"] = circleId, ["isCancel"] = isCancel }) is { } denied)
             {
                 return denied;
             }

             _logger.LogInformation("Hub LikeMoment request: DeviceUuid={DeviceUuid}, CircleId={CircleId}, IsCancel={IsCancel}", deviceUuid, circleId, isCancel);
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             if (string.IsNullOrEmpty(weChatId)) return TaskResult.Fail("WeChat account not found");
             if (circleId == 0) return TaskResult.Fail("朋友圈ID无效");
             return await _clientTaskService.SendCircleLikeTaskAsync(connectionId, weChatId, circleId, isCancel, DateTime.UtcNow.Ticks);
        }

        public async Task<TaskResult> DeleteMomentComment(string deviceUuid, long circleId, long commentId, long publishTime)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(DeleteMomentComment),
                     Permissions.MomentOperation.Delete,
                     targetId: commentId.ToString(),
                     destructive: true,
                     metadata: new Dictionary<string, object?> { ["circleId"] = circleId, ["commentId"] = commentId, ["publishTime"] = publishTime }) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             if (string.IsNullOrEmpty(weChatId)) return TaskResult.Fail("WeChat account not found");
             return await _clientTaskService.SendCircleCommentDeleteTaskAsync(connectionId, weChatId, circleId, commentId, publishTime, DateTime.UtcNow.Ticks);
        }

        public async Task<TaskResult> ReplyMomentComment(string deviceUuid, long circleId, string toWeChatId, string content, long replyCommentId, bool isResend = false)
        {
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(ReplyMomentComment),
                     "moment-comment",
                     ContentFields(("content", content))) is { } denied)
             {
                 return denied;
             }

             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(ReplyMomentComment),
                     Permissions.MomentOperation.Interact,
                     targetId: circleId.ToString(),
                     metadata: new Dictionary<string, object?> { ["circleId"] = circleId, ["replyCommentId"] = replyCommentId, ["isResend"] = isResend }) is { } permissionDenied)
             {
                 return permissionDenied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             if (string.IsNullOrEmpty(weChatId)) return TaskResult.Fail("WeChat account not found");
             return await _clientTaskService.SendCircleCommentReplyTaskAsync(connectionId, weChatId, circleId, toWeChatId, content, replyCommentId, isResend, DateTime.UtcNow.Ticks);
        }

        public async Task<TaskResult> PullFriendMoments(string deviceUuid, string friendId, long refSnsId = 0, int count = 20, long startTime = 0, long refTime = 0)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullFriendMoments)) is { } denied)
             {
                 return denied;
             }

             _logger.LogInformation("Hub PullFriendMoments request: DeviceUuid={DeviceUuid}, FriendIdEmpty={FriendIdEmpty}, RefSnsId={RefSnsId}, Count={Count}, StartTime={StartTime}, RefTime={RefTime}",
                 deviceUuid, string.IsNullOrWhiteSpace(friendId), refSnsId, count, startTime, refTime);
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             if (string.IsNullOrEmpty(weChatId)) return TaskResult.Fail("WeChat account not found");
             if (string.IsNullOrWhiteSpace(friendId)) return TaskResult.Fail("好友微信ID不能为空");
             var success = await _clientTaskService.SendPullFriendCircleTaskAsync(connectionId, weChatId, friendId, refSnsId, count, startTime, refTime, DateTime.UtcNow.Ticks);
             var result = success ? TaskResult.Ok() : TaskResult.Fail("好友朋友圈拉取任务下发失败");
             _logger.LogInformation("Hub PullFriendMoments result: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, Success={Success}, Message={Message}",
                 deviceUuid, connectionId, result.success, result.message);
             return result;
        }

        public async Task<TaskResult> PullMomentDetail(string deviceUuid, long circleId, bool getBigMap = false)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(PullMomentDetail)) is { } denied)
             {
                 return denied;
             }

             _logger.LogInformation("Hub PullMomentDetail request: DeviceUuid={DeviceUuid}, CircleId={CircleId}, GetBigMap={GetBigMap}",
                 deviceUuid, circleId, getBigMap);
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             if (string.IsNullOrEmpty(weChatId)) return TaskResult.Fail("WeChat account not found");
             // 微信 snsId 可能为负数，0 才表示未传有效 ID。
             if (circleId == 0) return TaskResult.Fail("朋友圈ID无效");
             var success = await _clientTaskService.SendPullCircleDetailTaskAsync(connectionId, weChatId, circleId, getBigMap);
             var result = success ? TaskResult.Ok() : TaskResult.Fail("朋友圈详情拉取任务下发失败");
             _logger.LogInformation("Hub PullMomentDetail result: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, Success={Success}, Message={Message}",
                 deviceUuid, connectionId, result.success, result.message);
             return result;
        }

        public async Task<TaskResult> SyncMomentMessages(string deviceUuid, bool onlyComment = false, bool getAll = true)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncMomentMessages)) is { } denied)
             {
                 return denied;
             }

             _logger.LogInformation("Hub SyncMomentMessages request: DeviceUuid={DeviceUuid}, OnlyComment={OnlyComment}, GetAll={GetAll}",
                 deviceUuid, onlyComment, getAll);
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             if (string.IsNullOrEmpty(weChatId)) return TaskResult.Fail("WeChat account not found");
             var taskId = DateTime.UtcNow.Ticks;
             var success = await _clientTaskService.SendTriggerCircleMsgPushTaskAsync(connectionId, weChatId, onlyComment, getAll, taskId);
             var result = success
                 ? TaskResult.Ok(taskId, "朋友圈互动消息同步指令已下发，等待客户端回执")
                 : TaskResult.Fail(taskId, "朋友圈互动消息同步任务下发失败");
             _logger.LogInformation("Hub SyncMomentMessages result: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, Success={Success}, Message={Message}",
                 deviceUuid, connectionId, result.success, result.message);
             return result;
        }

        public async Task<TaskResult> MarkMomentMessageRead(string deviceUuid, long circleId, int commentId = 0)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(MarkMomentMessageRead),
                     Permissions.MomentOperation.Interact,
                     targetId: commentId > 0 ? commentId.ToString() : circleId.ToString(),
                     metadata: new Dictionary<string, object?> { ["circleId"] = circleId, ["commentId"] = commentId }) is { } denied)
             {
                 return denied;
             }

             _logger.LogInformation("Hub MarkMomentMessageRead request: DeviceUuid={DeviceUuid}, CircleId={CircleId}, CommentId={CommentId}",
                 deviceUuid, circleId, commentId);
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             if (string.IsNullOrEmpty(weChatId)) return TaskResult.Fail("WeChat account not found");
             // 微信 snsId 可能为负数，0 才表示未传有效 ID。
             if (circleId == 0) return TaskResult.Fail("朋友圈ID无效");
             var success = await _clientTaskService.SendCircleMsgReadTaskAsync(connectionId, weChatId, circleId, commentId);
             var result = success ? TaskResult.Ok() : TaskResult.Fail("朋友圈互动消息已读任务下发失败");
             _logger.LogInformation("Hub MarkMomentMessageRead result: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, Success={Success}, Message={Message}",
                 deviceUuid, connectionId, result.success, result.message);
             return result;
        }

        public async Task<TaskResult> ClearMomentMessage(string deviceUuid, long circleId, int commentId = 0, bool isRead = true)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(ClearMomentMessage),
                     Permissions.MomentOperation.Delete,
                     targetId: commentId > 0 ? commentId.ToString() : circleId.ToString(),
                     destructive: true,
                     metadata: new Dictionary<string, object?> { ["circleId"] = circleId, ["commentId"] = commentId, ["isRead"] = isRead }) is { } denied)
             {
                 return denied;
             }

             _logger.LogInformation("Hub ClearMomentMessage request: DeviceUuid={DeviceUuid}, CircleId={CircleId}, CommentId={CommentId}, IsRead={IsRead}",
                 deviceUuid, circleId, commentId, isRead);
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             if (string.IsNullOrEmpty(weChatId)) return TaskResult.Fail("WeChat account not found");
             var success = await _clientTaskService.SendCircleMsgClearTaskAsync(connectionId, weChatId, circleId, commentId, isRead);
             var result = success ? TaskResult.Ok() : TaskResult.Fail("朋友圈互动消息清理任务下发失败");
             _logger.LogInformation("Hub ClearMomentMessage result: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, Success={Success}, Message={Message}",
                 deviceUuid, connectionId, result.success, result.message);
             return result;
        }

        /// <summary>
        /// 朋友圈一键点赞。
        /// <para>62203 会读取 Rate/Num/EndTime/TimeOut；这里保留默认值，同时允许高级调用覆盖。</para>
        /// </summary>
        public async Task<TaskResult> OneKeyLikeMoments(string deviceUuid, int rate = 100, int num = 0, int endTime = 0, int timeOut = 0)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(OneKeyLikeMoments),
                     Permissions.MomentOperation.Interact,
                     metadata: new Dictionary<string, object?> { ["rate"] = rate, ["num"] = num, ["endTime"] = endTime, ["timeOut"] = timeOut }) is { } denied)
             {
                 return denied;
             }

             _logger.LogInformation("Hub OneKeyLikeMoments request: DeviceUuid={DeviceUuid}, Rate={Rate}, Num={Num}, EndTime={EndTime}, TimeOut={TimeOut}",
                 deviceUuid, rate, num, endTime, timeOut);
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var weChatId = await GetPrimaryWechatIdAsync(deviceUuid);
             if (string.IsNullOrEmpty(weChatId)) return TaskResult.Fail("WeChat account not found");
             var taskId = DateTime.UtcNow.Ticks;
             return await _clientTaskService.SendOneKeyLikeTaskAsync(connectionId, taskId, weChatId, rate, num, endTime, timeOut);
        }

        /// <summary>
        /// 执行群聊操作（踢人、拉人、修改群名等）
        /// </summary>
        /// <param name="connectionId">设备连接ID</param>
        /// <param name="chatRoomId">群聊ID (ChatRoomId)</param>
        /// <param name="action">操作类型 (0=改名, 2=拉人, 3=踢人)</param>
        /// <param name="content">操作内容 (如被操作人的wxid或新群名)</param>
        /// <param name="intValue">附加参数</param>

        public async Task<TaskResult> SendGroupMessage(string deviceUuid, List<string> friendIds, string content, int contentType = 0, int duration = 0, bool original = false)
        {
             var friendCount = friendIds?.Count ?? 0;
             _logger.LogInformation("Hub SendGroupMessage request: DeviceUuid={DeviceUuid}, FriendCount={FriendCount}, ContentType={ContentType}, Duration={Duration}, Original={Original}, ContentEmpty={ContentEmpty}",
                 deviceUuid, friendCount, contentType, duration, original, string.IsNullOrWhiteSpace(content));
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(SendGroupMessage),
                     "mass-send",
                     ContentFields(("content", content))) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId))
             {
                 _logger.LogWarning("Hub SendGroupMessage failed: device offline or connection not found. DeviceUuid={DeviceUuid}, FriendCount={FriendCount}",
                     deviceUuid, friendCount);
                 return TaskResult.Fail("设备未在线或连接不存在");
             }
             var normalizedFriendIds = friendIds ?? new List<string>();
             var result = await _clientTaskService.SendWeChatGroupSendTaskAsync(connectionId, normalizedFriendIds, content, contentType, duration, original);
             _logger.LogInformation("Hub SendGroupMessage result: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, Success={Success}, Message={Message}",
                  deviceUuid, connectionId, result.success, result.message);
             return result;
        }

        /// <summary>
        /// 请求设备同步微信“群发助手”历史。
        /// <para>下发成功后等待 Android 通过 GroupSendHistoryPushNotice 上报，Web 页面可随后读取 MassMessages 历史表。</para>
        /// </summary>
        public async Task<TaskResult> SyncMassSendHistory(string deviceUuid, long endTime = 0, string weChatId = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SyncMassSendHistory)) is { } denied)
             {
                 return denied;
             }

             _logger.LogInformation("Hub SyncMassSendHistory request: DeviceUuid={DeviceUuid}, EndTime={EndTime}, WeChatId={WeChatId}", deviceUuid, endTime, weChatId);
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
             var taskId = DateTime.UtcNow.Ticks;
             var queued = await _clientTaskService.SendGetGroupSendHistoryTaskAsync(connectionId, taskId, endTime, weChatId);
             var result = queued
                 ? TaskResult.Ok(taskId, "群发历史同步指令已下发，等待客户端上报历史")
                 : TaskResult.Fail(taskId, "群发历史同步指令下发失败");
             _logger.LogInformation("Hub SyncMassSendHistory result: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, TaskId={TaskId}, Success={Success}",
                 deviceUuid, connectionId, taskId, result.success);
             return result;
        }

        /// <summary>
        /// 读取指定微信账号的群发助手历史。
        /// <para>只读查询，持久化写入仍由 DbHelper.SaveMassSendHistory 负责。</para>
        /// </summary>
        public async Task<IEnumerable<MassSendHistoryDto>> GetMassSendHistory(string accountId, int count = 50)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return Enumerable.Empty<MassSendHistoryDto>();
            }

            var ownerWxid = accountId.Trim();
            if (!await CanAccessAccountOrLogAsync(ownerWxid, nameof(GetMassSendHistory)))
            {
                return Enumerable.Empty<MassSendHistoryDto>();
            }

            var accountKey = BuildWechatAccountNumericKey(ownerWxid);
            var normalizedCount = count <= 0 ? 50 : Math.Min(count, 200);

            var histories = await _context.Set<SCRM.API.Models.Entities.MassMessage>()
                .AsNoTracking()
                .Where(m => m.wechatAccountId == accountKey)
                .OrderByDescending(m => m.sentTime == default ? m.updatedAt : m.sentTime)
                .ThenByDescending(m => m.id)
                .Take(normalizedCount)
                .ToListAsync();

            if (!histories.Any())
            {
                return Enumerable.Empty<MassSendHistoryDto>();
            }

            var historyIds = histories.Select(m => m.id).ToList();
            var details = await _context.Set<MassMessageDetail>()
                .AsNoTracking()
                .Where(d => historyIds.Contains(d.massMessageId))
                .OrderBy(d => d.id)
                .ToListAsync();

            var recipientIds = details
                .Select(d => d.recipientWxid)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var displayNames = await BuildContactDisplayNameMapAsync(ownerWxid, recipientIds);
            var detailsByHistory = details
                .GroupBy(d => d.massMessageId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return histories.Select(message => new MassSendHistoryDto
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
                        recipientDisplayName = ResolveDisplayName(displayNames, detail.recipientWxid, detail.recipientWxid),
                        sendStatus = detail.sendStatus,
                        errorMessage = detail.errorMessage ?? string.Empty,
                        retryCount = detail.retryCount,
                        sentTime = detail.sentTime
                    }).ToList()
                    : new List<MassSendHistoryDetailDto>()
            }).ToList();
        }

        public async Task<bool> ExecuteGroupAction(string deviceUuid, string chatRoomId, int action, string content, int intValue)
        {
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(ExecuteGroupAction),
                     "chatroom-action",
                     ContentFields(("content", content))) is not null)
             {
                 return false;
             }

             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(ExecuteGroupAction),
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

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return false;
             var result = await _clientTaskService.SendChatRoomActionTaskAsync(connectionId, chatRoomId, (EnumChatRoomAction)action, content, intValue, DateTime.UtcNow.Ticks);
             return result.success;
        }

        /// <summary>
        /// 同意加入群聊
        /// </summary>
        /// <param name="connectionId">设备连接ID</param>
        /// <param name="talker">邀请人ID</param>
        /// <param name="msgSvrId">消息服务器ID (MsgSvrId)</param>
        /// <param name="content">消息内容</param>
        public async Task<bool> AgreeJoinGroup(string deviceUuid, string talker, long msgSvrId, string content)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(AgreeJoinGroup)) is not null)
             {
                 return false;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return false;
             var result = await _clientTaskService.SendAgreeJoinChatRoomTaskAsync(connectionId, talker, msgSvrId, content, DateTime.UtcNow.Ticks);
             return result.success;
        }
        /// <summary>
        /// 删除好友
        /// </summary>
        /// <param name="connectionId">设备连接ID</param>
        /// <param name="friendId">要删除的好友wxid</param>
        public async Task<TaskResult> DeleteFriend(string deviceUuid, string friendId)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(DeleteFriend),
                     Permissions.ContactOperation.DeleteWechat,
                     friendId,
                     destructive: true) is { } denied)
             {
                 return denied;
             }

             _logger.LogInformation("Hub DeleteFriend request: DeviceUuid={DeviceUuid}, FriendId={FriendId}", deviceUuid, friendId);
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
             var result = await _clientTaskService.SendDeleteFriendTaskAsync(connectionId, friendId, DateTime.UtcNow.Ticks);
             _logger.LogInformation("Hub DeleteFriend result: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, Success={Success}, Message={Message}",
                 deviceUuid, connectionId, result.success, result.message);
             return result;
        }

        /// <summary>
        /// 接受好友添加请求
        /// </summary>
        /// <param name="connectionId">设备连接ID</param>
        /// <param name="friendId">请求者的wxid</param>
        /// <param name="friendNick">请求者的昵称</param>
        public async Task<TaskResult> AcceptFriendRequest(
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
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(AcceptFriendRequest)) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
             var normalizedOperation = System.Enum.IsDefined(typeof(AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation), operation)
                 ? (AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation)operation
                 : AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation.Accept;
             return await _clientTaskService.SendAcceptFriendAddRequestTaskAsync(
                 connectionId,
                 friendId,
                 friendNick,
                 DateTime.UtcNow.Ticks,
                 normalizedOperation,
                 remark,
                 replyMsg,
                 addWithWW,
                 onlyWW,
                 permission);
        }

        public async Task<TaskResult> AddFriendInChatRoom(string deviceUuid, string chatRoomId, string friendId, string message, string remark = "", int permission = 0)
        {
             _logger.LogInformation("Hub AddFriendInChatRoom request: DeviceUuid={DeviceUuid}, ChatRoom={ChatRoomId}, FriendId={FriendId}, Permission={Permission}",
                 deviceUuid, chatRoomId, friendId, permission);
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(AddFriendInChatRoom),
                     "chatroom-friend",
                     ContentFields(("message", message), ("remark", remark))) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
             var result = await _clientTaskService.SendAddFriendInChatRoomTaskAsync(connectionId, chatRoomId, friendId, message, remark, permission, DateTime.UtcNow.Ticks);
             _logger.LogInformation("Hub AddFriendInChatRoom result: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, Success={Success}, Message={Message}",
                 deviceUuid, connectionId, result.success, result.message);
             return result;
        }

        public async Task<TaskResult> AddFriendWithScene(string deviceUuid, string friendWxid, string message, string remark = "", string label = "", int scene = 3, int permission = 0, string verificationImagePath = "")
        {
             _logger.LogInformation(
                 "Hub AddFriendWithScene request: DeviceUuid={DeviceUuid}, Friend={FriendWxid}, Scene={Scene}, Permission={Permission}, Remark={Remark}, Label={Label}, HasImage={HasImage}",
                 deviceUuid,
                 friendWxid,
                 scene,
                 permission,
                 remark,
                 label,
                 !string.IsNullOrWhiteSpace(verificationImagePath));
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(AddFriendWithScene),
                     "friend",
                     ContentFields(("message", message), ("remark", remark), ("label", label))) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
             var result = await _clientTaskService.SendAddFriendWithSceneTaskAsync(connectionId, friendWxid, message, remark, label, scene, permission, verificationImagePath);
             _logger.LogInformation("Hub AddFriendWithScene result: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, Success={Success}, Message={Message}",
                 deviceUuid, connectionId, result.success, result.message);
             return result;
        }

        public async Task<TaskResult> ModifyFriendMemo(string deviceUuid, string friendId, string memo, string desc = "", string phone = "", int delFlag = 0)
        {
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(ModifyFriendMemo),
                     "friend-memo",
                     ContentFields(("memo", memo), ("desc", desc), ("phone", phone))) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
             return await _clientTaskService.SendModifyFriendMemoTaskAsync(connectionId, friendId, memo, desc, phone, delFlag, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 设置好友权限。
        /// permissionMask：8=仅聊天，2=不让他看我朋友圈，1=不看他朋友圈。
        /// </summary>
        public async Task<TaskResult> SetFriendPermission(string deviceUuid, string friendId, int permissionMask)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SetFriendPermission),
                     Permissions.ContactOperation.PermissionSet,
                     friendId,
                     destructive: true,
                     metadata: new Dictionary<string, object?> { ["permissionMask"] = permissionMask }) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
             return await _clientTaskService.SendSetFriendPermissionTaskAsync(connectionId, friendId, permissionMask, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 通过手机号添加好友，对齐 62203 AddFriendsTask(1072)。
        /// </summary>
        public async Task<TaskResult> AddFriendsByPhone(string deviceUuid, string[] phones, string message, string remark = "", string label = "", int permission = 0)
        {
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(AddFriendsByPhone),
                     "phone-friends",
                     ContentFields(("message", message), ("remark", remark), ("label", label))) is { } denied)
             {
                 return denied;
             }

             return await _deviceCommandService.AddFriendsByPhoneAsync(deviceUuid, phones, message, remark, label, permission);
        }

        /// <summary>
        /// 从通讯录批量添加好友，对齐 62203 AddFriendFromPhonebookTask(1215)。
        /// </summary>
        public async Task<TaskResult> AddFriendFromPhonebook(string deviceUuid, string message, int count = 1, int index = 0, bool reset = false)
        {
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(AddFriendFromPhonebook),
                     "phonebook",
                     ContentFields(("message", message))) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
             return await _clientTaskService.SendAddFriendFromPhonebookTaskAsync(connectionId, message, count, index, DateTime.UtcNow.Ticks, reset);
        }

        /// <summary>
        /// 通过聊天中的名片消息添加好友，对齐 62203 AddFriendNameCardTask(1236)。
        /// </summary>
        public async Task<TaskResult> AddFriendNameCard(string deviceUuid, long msgSvrId, string message, string remark = "")
        {
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(AddFriendNameCard),
                     "name-card",
                     ContentFields(("message", message), ("remark", remark))) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
             return await _clientTaskService.SendAddFriendNameCardTaskAsync(connectionId, msgSvrId, message, remark, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 重新发送好友验证，对齐 62203 SendFriendVerifyTask(1231)。
        /// </summary>
        public async Task<TaskResult> SendFriendVerify(string deviceUuid, string friendId, string message)
        {
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(SendFriendVerify),
                     "friend-verify",
                     ContentFields(("message", message))) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
             return await _clientTaskService.SendFriendVerifyTaskAsync(connectionId, friendId, message, DateTime.UtcNow.Ticks);
        }

        public async Task<bool> SendMultiPicture(string deviceUuid, string friendWxid, List<string> imageUrls)
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(SendMultiPicture)) is not null)
             {
                 return false;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return false;
             var normalizedUrls = imageUrls?
                 .Where(url => !string.IsNullOrWhiteSpace(url))
                 .Select(url => url.Trim())
                 .Distinct(StringComparer.OrdinalIgnoreCase)
                 .ToList() ?? new List<string>();
             if (normalizedUrls.Count == 0) return false;

             var successCount = 0;
             foreach (var imageUrl in normalizedUrls)
             {
                 var result = await _clientTaskService.SendTalkToFriendTaskAsync(
                     connectionId,
                     friendWxid,
                     imageUrl,
                     EnumContentType.Picture);
                 if (result.success)
                 {
                     successCount++;
                     await Task.Delay(350);
                 }
             }

             return successCount == normalizedUrls.Count;
        }

        /// <summary>
        /// 请求手机截屏
        /// </summary>
        /// <param name="connectionId">设备连接ID</param>
        public async Task<TaskResult> RequestScreenShot(string deviceUuid)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(RequestScreenShot),
                     Permissions.DeviceTask.Screenshot,
                     destructive: false) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId))
             {
                 return TaskResult.Fail("设备未在线或连接不存在");
             }

             var result = await _clientTaskService.SendScreenShotTaskAsync(connectionId, DateTime.UtcNow.Ticks);
             if (!result.success && string.IsNullOrWhiteSpace(result.message))
             {
                 result.message = "截图失败";
             }

             return result;
        }

        /// <summary>
        /// 执行手机系统操作（重启、清理缓存等）
        /// </summary>
        /// <param name="connectionId">设备连接ID</param>
        /// <param name="action">操作类型 (1=重启, 4=清App缓存, 5=清微信缓存, 9=重启微信)</param>
        public async Task<bool> ExecutePhoneAction(
            string deviceUuid,
            int action,
            string strParam = "",
            int intParam = 0,
            string weChatId = "",
            string imei = "")
        {
             if (await DenyIfNoDeviceAccessAsync(deviceUuid, nameof(ExecutePhoneAction)) is not null)
             {
                 return false;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return false;
             var result = await _clientTaskService.SendPhoneActionTaskAsync(
                 connectionId,
                 (EnumPhoneAction)action,
                 DateTime.UtcNow.Ticks,
                 strParam,
                 intParam,
                 weChatId,
                 imei);
             return result.success;
        }
        /// <summary>
        /// 获取账号配置
        /// </summary>
        public async Task<WechatAccountSettings> GetAccountSettings(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return new WechatAccountSettings();
            }

            accountId = accountId.Trim();
            if (!await CanAccessAccountOrLogAsync(accountId, nameof(GetAccountSettings)))
            {
                return new WechatAccountSettings();
            }

            var account = await _context.WechatAccounts.FindAsync(accountId);
            if (account == null || string.IsNullOrEmpty(account.settings))
                return new WechatAccountSettings();

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<WechatAccountSettings>(account.settings) ?? new WechatAccountSettings();
            }
            catch
            {
                return new WechatAccountSettings();
            }
        }

        /// <summary>
        /// 更新账号配置
        /// </summary>
        public async Task<bool> UpdateAccountSettings(string accountId, WechatAccountSettings settings)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return false;
            }

            accountId = accountId.Trim();
            if (!await CanAccessAccountOrLogAsync(accountId, nameof(UpdateAccountSettings)))
            {
                return false;
            }

            var account = await _context.WechatAccounts.FindAsync(accountId);
            if (account == null) return false;

            account.settings = System.Text.Json.JsonSerializer.Serialize(settings);
            
            // Using standard SaveChangesAsync as the entity is tracked by the context.
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// 发送朋友圈
        /// </summary>

        public async Task<TaskResult> SphGetMention(string deviceUuid, long lastLikeId = 0, long lastCommentId = 0, long lastFollowId = 0)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SphGetMention),
                     Permissions.FinderOperation.Read,
                     metadata: new Dictionary<string, object?> { ["lastLikeId"] = lastLikeId, ["lastCommentId"] = lastCommentId, ["lastFollowId"] = lastFollowId }) is { } denied)
             {
                 return denied;
             }

             _logger.LogInformation("Hub SphGetMention request: DeviceUuid={DeviceUuid}, LastLikeId={LastLikeId}, LastCommentId={LastCommentId}, LastFollowId={LastFollowId}",
                 deviceUuid, lastLikeId, lastCommentId, lastFollowId);
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var success = await _clientTaskService.SendSphGetMentionTaskAsync(connectionId, lastLikeId, lastCommentId, lastFollowId, DateTime.UtcNow.Ticks);
             var result = success ? TaskResult.Ok() : TaskResult.Fail("视频号提及拉取任务下发失败");
             _logger.LogInformation("Hub SphGetMention result: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, Success={Success}, Message={Message}",
                 deviceUuid, connectionId, result.success, result.message);
             return result;
        }

        public async Task<TaskResult> SphGetComment(string deviceUuid, long feedId, string nonceId, string feedAuth, long refCommentId = 0, long replyCommentId = 0, int sortType = 0)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SphGetComment),
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

             _logger.LogInformation("Hub SphGetComment request: DeviceUuid={DeviceUuid}, FeedId={FeedId}, NonceEmpty={NonceEmpty}, FeedAuthEmpty={FeedAuthEmpty}, RefCommentId={RefCommentId}, ReplyCommentId={ReplyCommentId}, SortType={SortType}",
                 deviceUuid, feedId, string.IsNullOrWhiteSpace(nonceId), string.IsNullOrWhiteSpace(feedAuth), refCommentId, replyCommentId, sortType);
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             var success = await _clientTaskService.SendSphGetCommentTaskAsync(connectionId, feedId, nonceId, feedAuth, refCommentId, replyCommentId, sortType, DateTime.UtcNow.Ticks);
             var result = success ? TaskResult.Ok() : TaskResult.Fail("视频号评论列表拉取任务下发失败");
             _logger.LogInformation("Hub SphGetComment result: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, Success={Success}, Message={Message}",
                 deviceUuid, connectionId, result.success, result.message);
             return result;
        }

        public async Task<TaskResult> SphUserPage(string deviceUuid, string sphUserName)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SphUserPage),
                     Permissions.FinderOperation.Read,
                     metadata: new Dictionary<string, object?> { ["sphUserNameEmpty"] = string.IsNullOrWhiteSpace(sphUserName) }) is { } denied)
             {
                 return denied;
             }

             _logger.LogInformation("Hub SphUserPage request: DeviceUuid={DeviceUuid}, SphUserNameEmpty={SphUserNameEmpty}",
                 deviceUuid, string.IsNullOrWhiteSpace(sphUserName));
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             if (string.IsNullOrWhiteSpace(sphUserName)) return TaskResult.Fail("视频号用户名不能为空");
             var success = await _clientTaskService.SendSphUserPageTaskAsync(connectionId, sphUserName, DateTime.UtcNow.Ticks);
             var result = success ? TaskResult.Ok() : TaskResult.Fail("视频号用户页拉取任务下发失败");
             _logger.LogInformation("Hub SphUserPage result: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}, Success={Success}, Message={Message}",
                 deviceUuid, connectionId, result.success, result.message);
             return result;
        }

        public async Task<TaskResult> SphPost(string deviceUuid, string content, List<string> medias, int mediaType = 0, string cover = "")
        {
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(SphPost),
                     "finder-post",
                     ContentFields(("content", content))) is { } denied)
             {
                 return denied;
             }

             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SphPost),
                     Permissions.FinderOperation.Interact,
                     metadata: new Dictionary<string, object?> { ["mediaCount"] = medias?.Count ?? 0, ["mediaType"] = mediaType, ["hasCover"] = !string.IsNullOrWhiteSpace(cover) }) is { } permissionDenied)
             {
                 return permissionDenied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             return await _clientTaskService.SendSphPostTaskAsync(connectionId, content, medias ?? new List<string>(), mediaType, cover, DateTime.UtcNow.Ticks);
        }

        public async Task<TaskResult> SphComment(string deviceUuid, long feedId, string nonceId, string feedAuth, int type, string content, string media = "", long replyCommentId = 0, string replyUsername = "")
        {
             if (await DenyIfDeviceOrSensitiveContentAsync(
                     deviceUuid,
                     nameof(SphComment),
                     "finder-comment",
                     ContentFields(("content", content), ("media", media))) is { } denied)
             {
                 return denied;
             }

             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SphComment),
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

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             return await _clientTaskService.SendSphCommentTaskAsync(connectionId, feedId, nonceId, feedAuth, type, content, media, replyCommentId, replyUsername, DateTime.UtcNow.Ticks);
        }

        public async Task<TaskResult> SphLike(string deviceUuid, long feedId, int type = 1, bool isCancel = false)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SphLike),
                     Permissions.FinderOperation.Interact,
                     targetId: feedId.ToString(),
                     metadata: new Dictionary<string, object?> { ["feedId"] = feedId, ["type"] = type, ["isCancel"] = isCancel }) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             return await _clientTaskService.SendSphLikeTaskAsync(connectionId, feedId, type, isCancel, DateTime.UtcNow.Ticks);
        }

        public async Task<TaskResult> SphDelComment(string deviceUuid, long feedId, long commentId)
        {
             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(SphDelComment),
                     Permissions.FinderOperation.DeleteComment,
                     targetId: commentId.ToString(),
                     destructive: true,
                     metadata: new Dictionary<string, object?> { ["feedId"] = feedId, ["commentId"] = commentId }) is { } denied)
             {
                 return denied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             return await _clientTaskService.SendSphDelCommentTaskAsync(connectionId, feedId, commentId, DateTime.UtcNow.Ticks);
        }

        public Task<TaskResult> PostMoment(string deviceUuid, string content, List<string> imageUrls)
        {
             return this.PostMomentAdvanced(deviceUuid, MomentPostRequestDto.FromLegacy(content, imageUrls));
        }

        public async Task<TaskResult> PostMomentAdvanced(string deviceUuid, MomentPostRequestDto request)
        {
             var normalizedRequest = NormalizeMomentPostRequestForHub(request);
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
                     nameof(PostMomentAdvanced),
                     "moment",
                     MomentPostContentFields(normalizedRequest)) is { } denied)
             {
                 return denied;
             }

             if (await DenyIfHighRiskDeviceOperationAsync(
                     deviceUuid,
                     nameof(PostMomentAdvanced),
                     Permissions.MomentOperation.Post,
                     metadata: momentPostAuditMetadata) is { } permissionDenied)
             {
                 return permissionDenied;
             }

             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("Device offline");
             
             // Generate TaskId
             var taskId = DateTime.UtcNow.Ticks;
             return await _clientTaskService.SendPostSNSNewsTaskAsync(connectionId, normalizedRequest, taskId);
        }

        private async Task<string> GetPrimaryWechatIdAsync(string deviceUuid)
        {
            // 只把当前在线账号作为任务默认 wxid。
            // 离线历史账号和 Android 3051 lastKnown 快照只能用于展示/诊断，不能污染实际任务下发目标。
            var account = await _context.WechatAccounts
                .AsNoTracking()
                .OrderByDescending(u => u.lastOnlineAt)
                .ThenByDescending(u => u.updatedAt)
                .FirstOrDefaultAsync(u => u.clientUuid == deviceUuid && !u.isDeleted && u.accountStatus == 1);
            return account?.wxid ?? string.Empty;
        }

        /// <summary>
        /// 获取设备 IMEI，缺失时用设备 UUID 兜底。
        /// </summary>
        private async Task<string> GetDeviceImeiAsync(string deviceUuid)
        {
            var device = await _context.GetSrClient(deviceUuid);
            return string.IsNullOrWhiteSpace(device?.device?.IMEI)
                ? deviceUuid
                : device.device.IMEI;
        }

        public async Task<IEnumerable<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>> GetMomentsTimeline(string deviceUuid, int page = 1, string weChatId = "")
        {
            if (string.IsNullOrWhiteSpace(deviceUuid)
                || !await CanAccessDeviceOrLogAsync(deviceUuid, nameof(GetMomentsTimeline)))
            {
                return Enumerable.Empty<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>();
            }

            int pageSize = 10;
            page = Math.Max(1, page);
            var normalizedWeChatId = weChatId?.Trim() ?? string.Empty;
            WechatAccount? account = null;
            if (!string.IsNullOrWhiteSpace(normalizedWeChatId))
            {
                if (!await CanAccessAccountOrLogAsync(normalizedWeChatId, nameof(GetMomentsTimeline)))
                {
                    return Enumerable.Empty<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>();
                }

                account = await _context.WechatAccounts
                    .AsNoTracking()
                    .Where(u => u.wxid == normalizedWeChatId && !u.isDeleted)
                    .OrderByDescending(u => u.clientUuid == deviceUuid)
                    .FirstOrDefaultAsync();
            }

            account ??= await _context.WechatAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.clientUuid == deviceUuid && !u.isDeleted);
            if (account == null) return Enumerable.Empty<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>();

            if (!await CanAccessAccountOrLogAsync(account.wxid, nameof(GetMomentsTimeline)))
            {
                return Enumerable.Empty<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>();
            }

            var list = await _context.MomentsTimelines
                .AsNoTracking()
                .Where(m => m.ownerWxid == account.wxid)
                .OrderByDescending(m => m.createTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var displayNames = await BuildContactDisplayNameMapAsync(account.wxid);
            list = await _sensitiveMaskingService.MaskMomentsAsync(Context.User, list);
            return list.Select(m =>
            {
                var comments = DeserializeMomentComments(m.commentsJson);
                var likes = DeserializeMomentLikes(m.likesJson);
                ApplyDisplayNames(comments, likes, displayNames);
                return new SCRM.SHARED.Models.Dtos.MomentsTimelineDto
                {
                    snsId = m.snsId,
                    userName = m.userName,
                    nickName = ResolveDisplayName(displayNames, m.userName, m.nickName),
                    content = MomentContentExtractor.FirstNonEmpty(
                        m.content,
                        MomentContentExtractor.ExtractTextFromXml(m.xmlContent)),
                    xmlContent = m.xmlContent ?? string.Empty,
                    createTime = m.createTime,
                    stringTime = m.createTime.ToString(),
                    images = !string.IsNullOrEmpty(m.imagesJson)
                        ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(m.imagesJson) ?? new List<string>()
                        : new List<string>(),
                    videoUrl = m.videoUrl ?? string.Empty,
                    link = !string.IsNullOrEmpty(m.linkInfoJson)
                        ? System.Text.Json.JsonSerializer.Deserialize<SCRM.SHARED.Models.Dtos.MomentLinkDto>(m.linkInfoJson) ?? new SCRM.SHARED.Models.Dtos.MomentLinkDto()
                        : new SCRM.SHARED.Models.Dtos.MomentLinkDto(),
                    comments = comments,
                    likes = likes
                };
            });
        }

        /// <summary>
        /// 为 SignalR 朋友圈查询回填联系人昵称。
        /// <para>只读 Contacts 表，不改变 DbHelper 负责的联系人持久化口径。</para>
        /// </summary>
        private async Task<Dictionary<string, string>> BuildContactDisplayNameMapAsync(string ownerWxid)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(ownerWxid))
            {
                return map;
            }

            var contacts = await _context.Contacts
                .AsNoTracking()
                .Where(c => c.ownerWxid == ownerWxid && !c.isDeleted)
                .Select(c => new { c.wxid, c.remarks, c.nickname, c.friendNo })
                .ToListAsync();

            foreach (var contact in contacts)
            {
                if (!string.IsNullOrWhiteSpace(contact.wxid))
                {
                    map[contact.wxid] = ResolveDisplayName(contact.remarks, contact.nickname, contact.wxid);
                }
                if (!string.IsNullOrWhiteSpace(contact.friendNo))
                {
                    map.TryAdd(contact.friendNo, ResolveDisplayName(contact.remarks, contact.nickname, contact.wxid));
                }
            }

            var selfName = await _context.WechatAccounts
                .AsNoTracking()
                .Where(a => a.wxid == ownerWxid && !a.isDeleted)
                .Select(a => a.nickname)
                .FirstOrDefaultAsync();
            map[ownerWxid] = string.IsNullOrWhiteSpace(selfName) ? ownerWxid : selfName!;
            return map;
        }

        /// <summary>
        /// 为指定接收人集合回填联系人展示名。
        /// </summary>
        private async Task<Dictionary<string, string>> BuildContactDisplayNameMapAsync(string ownerWxid, IReadOnlyCollection<string> wxids)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(ownerWxid) || wxids == null || wxids.Count == 0)
            {
                return map;
            }

            var contacts = await _context.Contacts
                .AsNoTracking()
                .Where(c => c.ownerWxid == ownerWxid && !c.isDeleted && wxids.Contains(c.wxid))
                .Select(c => new { c.wxid, c.remarks, c.nickname, c.friendNo })
                .ToListAsync();

            foreach (var contact in contacts)
            {
                var displayName = ResolveDisplayName(contact.remarks, contact.nickname, contact.wxid);
                if (!string.IsNullOrWhiteSpace(contact.wxid))
                {
                    map[contact.wxid] = displayName;
                }
                if (!string.IsNullOrWhiteSpace(contact.friendNo))
                {
                    map.TryAdd(contact.friendNo, displayName);
                }
            }

            return map;
        }

        private static void ApplyDisplayNames(
            List<SCRM.SHARED.Models.Dtos.MomentCommentDto> comments,
            List<SCRM.SHARED.Models.Dtos.MomentLikeDto> likes,
            Dictionary<string, string> displayNames)
        {
            foreach (var comment in comments)
            {
                comment.nickName = ResolveDisplayName(displayNames, comment.userName, comment.nickName);
                comment.replyNickName = ResolveDisplayName(displayNames, comment.replyUserName, comment.replyNickName);
            }

            foreach (var like in likes)
            {
                like.nickName = ResolveDisplayName(displayNames, like.userName, like.nickName);
            }
        }

        private static string ResolveDisplayName(Dictionary<string, string> displayNames, string? wxid, string? currentName)
        {
            if (!string.IsNullOrWhiteSpace(wxid) && displayNames.TryGetValue(wxid, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            if (!string.IsNullOrWhiteSpace(currentName) && !LooksLikeRawWxid(currentName)) return currentName!;
            return wxid ?? string.Empty;
        }

        private static string ResolveDisplayName(string? remarks, string? nickname, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(remarks)) return remarks;
            if (!string.IsNullOrWhiteSpace(nickname)) return nickname;
            return fallback;
        }

        /// <summary>
        /// 判断目标会话是否为群聊。
        /// </summary>
        private static bool IsChatRoomWxid(string? wxid)
        {
            return !string.IsNullOrWhiteSpace(wxid)
                && wxid.Trim().EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase);
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

        /// <summary>
        /// 判断展示名是否只是原始微信标识，避免朋友圈作者/评论者显示 wxid。
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

        private static List<SCRM.SHARED.Models.Dtos.MomentCommentDto> DeserializeMomentComments(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<SCRM.SHARED.Models.Dtos.MomentCommentDto>();
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<SCRM.SHARED.Models.Dtos.MomentCommentDto>>(json) ?? new List<SCRM.SHARED.Models.Dtos.MomentCommentDto>();
            }
            catch
            {
                return new List<SCRM.SHARED.Models.Dtos.MomentCommentDto>();
            }
        }

        private static List<SCRM.SHARED.Models.Dtos.MomentLikeDto> DeserializeMomentLikes(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<SCRM.SHARED.Models.Dtos.MomentLikeDto>();
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<SCRM.SHARED.Models.Dtos.MomentLikeDto>>(json) ?? new List<SCRM.SHARED.Models.Dtos.MomentLikeDto>();
            }
            catch
            {
                return new List<SCRM.SHARED.Models.Dtos.MomentLikeDto>();
            }
        }

        private static bool IsValidPaymentPassword(string? passwd)
        {
            var value = passwd?.Trim() ?? string.Empty;
            return value.Length == 6 && value.All(char.IsDigit);
        }

        public async Task<IEnumerable<Conversation>> GetConversations(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return Enumerable.Empty<Conversation>();
            }

            accountId = accountId.Trim();

            if (!await CanAccessAccountOrLogAsync(accountId, nameof(GetConversations)))
            {
                return Enumerable.Empty<Conversation>();
            }

            var conversations = await _context.Conversations
                .AsNoTracking()
                .Where(c => c.wechatAccountId == accountId && !c.isDeleted)
                .OrderByDescending(c => c.lastMessageTime == default ? c.updatedAt : c.lastMessageTime)
                .Take(100) // Limit to 100 recent conversations
                .ToListAsync();

            _logger.LogInformation(
                "Hub GetConversations: AccountId={AccountId}, Count={Count}, ChatRooms={ChatRooms}",
                accountId,
                conversations.Count,
                conversations.Count(c => c.conversationType == 2 || IsChatRoomWxid(c.conversationWxid)));

            return await _sensitiveMaskingService.MaskConversationsAsync(Context.User, conversations);
        }
    }
}
