using DotNetty.Transport.Channels;
using Google.Protobuf;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Data;
using SCRM.API.Services.Netty.Handlers.Abstractions;
using SCRM.Services.Data;
using SCRM.Services.Events;
using SCRM.API.Services.Core;
using SCRM.SHARED.Models.Dtos;
using SCRM.SHARED.Models.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 群组消息处理器
    /// <para>处理群聊相关的非聊天类通知。</para>
    /// </summary>
    public class GroupMessageHandler : MessageHandlerBase
    {
        private readonly ILogger<GroupMessageHandler> _logger;
        private readonly ApplicationDbContext _db;
        private readonly ConnectionManager _connManager;
        private readonly IEventBus _eventBus;

        public GroupMessageHandler(
            ILogger<GroupMessageHandler> logger,
            ApplicationDbContext db,
            ConnectionManager connManager,
            IEventBus eventBus) : base(logger) 
        { 
            _logger = logger;
            _db = db;
            _connManager = connManager;
            _eventBus = eventBus;
        }

        public async Task HandleMessage(TransportMessage message, IChannelHandlerContext context)
        {
            await SendAckAsync(message, context);
            
            try
            {
                switch (message.MsgType)
                {
                    case EnumMsgType.ChatroomPushNotice: // 2031
                        await HandleChatRoomPush(message.Content.Unpack<ChatRoomPushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.ChatRoomMembersNotice: // 2034
                        await HandleChatRoomMembers(message.Content.Unpack<ChatRoomMembersNoticeMessage>(), context);
                        break;
                    case EnumMsgType.ChatRoomInvitePushNotice:
                        await HandleChatRoomInvitePush(message.Content.Unpack<ChatRoomInvitePushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.ChatRoomInviteListNotice:
                        await HandleChatRoomInviteList(message, context);
                        break;
                    case EnumMsgType.ChatRoomAddNotice:
                        await HandleChatRoomAdd(message.Content.Unpack<ChatRoomAddNoticeMessage>(), context);
                        break;
                    case EnumMsgType.ChatRoomDelNotice:
                        await HandleChatRoomDel(message.Content.Unpack<ChatRoomDelNoticeMessage>(), context);
                        break;
                    case EnumMsgType.ChatRoomChangedNotice:
                        await HandleChatRoomChanged(message.Content.Unpack<ChatRoomChangedNoticeMessage>(), context);
                        break;
                    default:
                        _logger.LogWarning("Unhandled Group Message Type: {Type} ({Id})", message.MsgType, (int)message.MsgType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Group Message");
            }
        }


        /// <summary>
        /// 处理群聊列表分页推送。
        /// <para>SmRun 侧 TriggerChatroomPushTask 回包使用 ChatroomPushNotice(2031)，之前路由缺失会导致网页群资料长期不同步。</para>
        /// </summary>
        private async Task HandleChatRoomPush(ChatRoomPushNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning("ChatroomPushNotice ignored because WeChatId is empty. TaskId={TaskId}", msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, "群资料同步失败：WeChatId 为空");
                return;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for ChatroomPushNotice WeChatId: {WeChatId}, TaskId={TaskId}", msg.WeChatId, msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, $"群资料同步失败：账号不存在 {msg.WeChatId}");
                return;
            }

            if (msg.ChatRooms == null || msg.ChatRooms.Count == 0)
            {
                _logger.LogInformation(
                    "ChatroomPushNotice handled with empty result: WeChatId={WeChatId}, TaskId={TaskId}, Page={Page}, Count={Count}",
                    msg.WeChatId,
                    msg.TaskId,
                    msg.Page,
                    msg.Count);
                await PublishTaskResultAsync(context, msg.TaskId, true, $"群资料同步完成：0 条，Count={msg.Count}，Page={msg.Page}");
                return;
            }

            var payload = BuildChatRoomSavePayload(msg.ChatRooms);
            var savedGroups = await _db.SaveChatRooms(account.wxid, payload.Groups, payload.MembersByRoom);
            var savedConversations = await _db.EnsureChatRoomConversations(account.wxid, savedGroups);
            await PublishConversationsUpdatedAsync(context, account.wxid);

            _logger.LogInformation(
                "ChatroomPushNotice handled: WeChatId={WeChatId}, TaskId={TaskId}, Page={Page}, Size={Size}, Count={Count}, SavedGroups={SavedGroups}, SavedConversations={SavedConversations}",
                msg.WeChatId,
                msg.TaskId,
                msg.Page,
                msg.Size,
                msg.Count,
                savedGroups.Count,
                savedConversations.Count);
            await PublishTaskResultAsync(context, msg.TaskId, true, $"群资料同步完成：群 {savedGroups.Count}/{msg.ChatRooms.Count}，会话 {savedConversations.Count}，Count={msg.Count}，Page={msg.Page}");
        }

        /// <summary>
        /// 处理新增群聊通知。
        /// </summary>
        private async Task HandleChatRoomAdd(ChatRoomAddNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId) || msg.ChatRoom == null || string.IsNullOrWhiteSpace(msg.ChatRoom.UserName))
            {
                _logger.LogDebug("ChatRoomAddNotice ignored because WeChatId/RoomId is empty.");
                return;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for ChatRoomAddNotice WeChatId: {WeChatId}", msg.WeChatId);
                return;
            }

            var payload = BuildChatRoomSavePayload(new[] { msg.ChatRoom });
            var savedGroups = await _db.SaveChatRooms(account.wxid, payload.Groups, payload.MembersByRoom);
            var savedConversations = await _db.EnsureChatRoomConversations(account.wxid, savedGroups);
            await PublishConversationsUpdatedAsync(context, account.wxid);
            _logger.LogInformation(
                "ChatRoomAddNotice handled: WeChatId={WeChatId}, RoomId={RoomId}, SavedGroups={SavedGroups}, SavedConversations={SavedConversations}",
                msg.WeChatId,
                msg.ChatRoom.UserName,
                savedGroups.Count,
                savedConversations.Count);
        }

        /// <summary>
        /// 处理群聊删除通知。
        /// </summary>
        private async Task HandleChatRoomDel(ChatRoomDelNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId) || string.IsNullOrWhiteSpace(msg.RoomId))
            {
                _logger.LogDebug("ChatRoomDelNotice ignored because WeChatId/RoomId is empty.");
                return;
            }

            var marked = await _db.MarkChatRoomDeleted(msg.WeChatId, msg.RoomId);
            var conversationMarked = await _db.MarkConversationDeleted(msg.WeChatId, msg.RoomId);
            if (marked || conversationMarked)
            {
                await PublishConversationsUpdatedAsync(context, msg.WeChatId);
            }
            _logger.LogInformation(
                "ChatRoomDelNotice handled: WeChatId={WeChatId}, RoomId={RoomId}, Marked={Marked}, ConversationMarked={ConversationMarked}",
                msg.WeChatId,
                msg.RoomId,
                marked,
                conversationMarked);
        }

        /// <summary>
        /// 处理群资料变更通知。
        /// </summary>
        private async Task HandleChatRoomChanged(ChatRoomChangedNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId) || string.IsNullOrWhiteSpace(msg.UserName))
            {
                _logger.LogDebug("ChatRoomChangedNotice ignored because WeChatId/UserName is empty.");
                return;
            }

            var changed = await _db.ApplyChatRoomChange(msg.WeChatId, msg.UserName, (int)msg.What, msg.Content);
            if (changed)
            {
                await SyncChangedChatRoomConversationAsync(msg.WeChatId, msg.UserName);
                await PublishConversationsUpdatedAsync(context, msg.WeChatId);
            }
            _logger.LogInformation(
                "ChatRoomChangedNotice handled: WeChatId={WeChatId}, RoomId={RoomId}, Change={Change}, Changed={Changed}",
                msg.WeChatId,
                msg.UserName,
                msg.What,
                changed);
        }

        /// <summary>
        /// 群资料变更后同步群聊会话的显示名、头像和删除状态。
        /// <para>
        /// ChatRoomChangedNotice 只更新 Groups 表；IM 群聊列表读取 Conversations，
        /// 因此必须复用 DbHelper.EnsureChatRoomConversations 把群资料同步到会话表。
        /// </para>
        /// </summary>
        private async Task SyncChangedChatRoomConversationAsync(string ownerWxid, string roomId)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            var groups = await _db.Groups
                .AsNoTracking()
                .Where(g => g.groupWxid == roomId && !g.isDeleted)
                .OrderByDescending(g => g.updatedAt)
                .ToListAsync();

            if (groups.Count == 0)
            {
                _logger.LogDebug("ChatRoomChangedNotice conversation sync skipped: Group not found. WeChatId={WeChatId}, RoomId={RoomId}",
                    ownerWxid,
                    roomId);
                return;
            }

            var savedConversations = await _db.EnsureChatRoomConversations(ownerWxid, groups);
            _logger.LogInformation(
                "ChatRoomChangedNotice conversation synced: WeChatId={WeChatId}, RoomId={RoomId}, Conversations={Count}",
                ownerWxid,
                roomId,
                savedConversations.Count);
        }

        /// <summary>
        /// 保存群邀请实时推送。
        /// </summary>
        private async Task HandleChatRoomInvitePush(ChatRoomInvitePushNoticeMessage msg, IChannelHandlerContext context, string source = "Push")
        {
            var invitedCount = msg.Invited != null ? msg.Invited.Count : 0;
            if (string.IsNullOrWhiteSpace(msg.WeChatId) || string.IsNullOrWhiteSpace(msg.ChatRoomId))
            {
                _logger.LogDebug(
                    "ChatRoomInvitePush ignored because WeChatId/ChatRoomId is empty. WeChatId={WeChatId}, ChatRoomId={ChatRoomId}",
                    msg.WeChatId,
                    msg.ChatRoomId);
                return;
            }

            var invitation = BuildGroupInvitationFromPush(msg, source);
            var saved = await _db.SaveGroupInvitations(msg.WeChatId, new[] { invitation });
            if (saved.Count > 0)
            {
                await PublishGroupInvitationsUpdatedAsync(context, msg.WeChatId);
            }

            _logger.LogInformation(
                "ChatRoomInvitePush handled: WeChatId={WeChatId}, ChatRoomId={ChatRoomId}, Inviter={Inviter}, InvitedCount={InvitedCount}, MsgId={MsgId}, Saved={SavedCount}, Source={Source}",
                msg.WeChatId,
                msg.ChatRoomId,
                msg.Inviter,
                invitedCount,
                msg.MsgId,
                saved.Count,
                source);
        }

        /// <summary>
        /// 保存群邀请列表结果。兼容 SmRun 侧历史实现：部分实时推送会使用 ChatRoomInviteListNotice 作为 MsgType，
        /// 但 Any 内实际装载 ChatRoomInvitePushNoticeMessage。
        /// </summary>
        private async Task HandleChatRoomInviteList(TransportMessage message, IChannelHandlerContext context)
        {
            try
            {
                var msg = message.Content.Unpack<ChatRoomInviteListNoticeMessage>();
                var inviteCount = msg.InviteMsg != null ? msg.InviteMsg.Count : 0;
                if (string.IsNullOrWhiteSpace(msg.WeChatId) || inviteCount == 0)
                {
                    _logger.LogDebug(
                        "ChatRoomInviteList ignored because WeChatId/InviteMsg is empty. WeChatId={WeChatId}, InviteCount={InviteCount}, TaskId={TaskId}",
                        msg.WeChatId,
                        inviteCount,
                        msg.TaskId);
                    return;
                }

                var inviteMessages = msg.InviteMsg ?? Enumerable.Empty<InviteMessage>();
                var invitations = inviteMessages
                    .Where(item => item != null)
                    .Select(item => BuildGroupInvitationFromList(msg.WeChatId, msg.TaskId, item))
                    .ToList();

                var saved = await _db.SaveGroupInvitations(msg.WeChatId, invitations);
                if (saved.Count > 0)
                {
                    await PublishGroupInvitationsUpdatedAsync(context, msg.WeChatId);
                }

                _logger.LogInformation(
                    "ChatRoomInviteList handled: WeChatId={WeChatId}, InviteCount={InviteCount}, TaskId={TaskId}, Saved={SavedCount}",
                    msg.WeChatId,
                    inviteCount,
                    msg.TaskId,
                    saved.Count);
                return;
            }
            catch (Exception listEx)
            {
                try
                {
                    var pushMsg = message.Content.Unpack<ChatRoomInvitePushNoticeMessage>();
                    await HandleChatRoomInvitePush(pushMsg, context, "CompatPush");
                    return;
                }
                catch (Exception pushEx)
                {
                    _logger.LogWarning(listEx, "ChatRoomInviteListNotice unpack as list failed.");
                    _logger.LogWarning(pushEx, "ChatRoomInviteListNotice unpack as push failed.");
                }
            }
        }

        /// <summary>
        /// 将群邀请实时推送转换为持久化实体。
        /// </summary>
        private static GroupInvitation BuildGroupInvitationFromPush(ChatRoomInvitePushNoticeMessage msg, string source)
        {
            return new GroupInvitation
            {
                weChatId = msg.WeChatId ?? string.Empty,
                chatRoomId = msg.ChatRoomId ?? string.Empty,
                inviterWxid = msg.Inviter ?? string.Empty,
                inviteName = msg.InviteName ?? string.Empty,
                reason = msg.Reason ?? string.Empty,
                invitationMessage = msg.Reason ?? string.Empty,
                msgId = msg.MsgId,
                updateTime = msg.UpdateTime,
                invitedJson = SerializeInvitedMembers(msg.Invited),
                source = source
            };
        }

        /// <summary>
        /// 将群邀请列表项转换为持久化实体。
        /// </summary>
        private static GroupInvitation BuildGroupInvitationFromList(string weChatId, long taskId, InviteMessage msg)
        {
            return new GroupInvitation
            {
                weChatId = weChatId ?? string.Empty,
                chatRoomId = msg.ChatRoomId ?? string.Empty,
                inviterWxid = msg.Inviter ?? string.Empty,
                inviteName = msg.InviteName ?? string.Empty,
                reason = msg.Reason ?? string.Empty,
                invitationMessage = msg.Reason ?? string.Empty,
                msgId = msg.MsgId,
                updateTime = msg.UpdateTime,
                taskId = taskId,
                invitedJson = SerializeInvitedMembers(msg.Invited),
                source = "List"
            };
        }

        /// <summary>
        /// 序列化被邀请成员，统一输出给 GroupInvitationDto 使用的小字段集。
        /// </summary>
        private static string SerializeInvitedMembers(IEnumerable<InvitedMemMessage>? invited)
        {
            var members = (invited ?? Enumerable.Empty<InvitedMemMessage>())
                .Where(item => item != null)
                .Select(item => new GroupInvitationMemberDto
                {
                    userName = item.UserName ?? string.Empty,
                    nickName = item.NickName ?? string.Empty,
                    avatar = item.Avatar ?? string.Empty
                })
                .ToList();

            return JsonSerializer.Serialize(members);
        }

        private async Task HandleChatRoomMembers(ChatRoomMembersNoticeMessage msg, IChannelHandlerContext context)
        {
             if (string.IsNullOrEmpty(msg.WeChatId)) return;
             
             // 62203 / SmRun 对位：
             // ChatRoomMembersNoticeMessage 里的 WeChatId 是“账号 wxid”，不是群 id。
             // 当前协议本身不携带 chatRoomId，因此服务端这里不能把 WeChatId 误判成 RoomId。
             var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
             if (account == null) return;

             int count = msg.Members != null ? msg.Members.Count : 0;
             _logger.LogInformation(
                 "Received ChatRoomMembersNotice: AccountWxId={WeChatId}, Members={Count}",
                 msg.WeChatId, count);

             // Logic to update DB:
             // 1. 当前协议缺少群 id，暂时不能直接落到具体 GroupMember 关系；
             // 2. 先保证协议解包与链路稳定，避免继续把账号 wxid 错认成群 id；
             // 3. 后续若 SmRun 侧补 chatRoomId，再补服务端持久化。
             
             /* 
             if (msg.Members != null) {
                 foreach (var member in msg.Members) {
                     // Sync logic...
                 }
             }
             */

             // Notify UI?
              // await _eventBus.PublishAsync(new GroupMembersUpdatedEvent(...));
        }

        private static (List<Group> Groups, Dictionary<string, List<GroupMember>> MembersByRoom) BuildChatRoomSavePayload(IEnumerable<ChatRoomMessage> chatRooms)
        {
            var now = DateTime.UtcNow;
            var groups = new List<Group>();
            var membersByRoom = new Dictionary<string, List<GroupMember>>();

            foreach (var room in chatRooms.Where(r => r != null && !string.IsNullOrWhiteSpace(r.UserName)))
            {
                var displayNames = room.ShowNameList
                    .Where(s => s != null && !string.IsNullOrWhiteSpace(s.UserName))
                    .GroupBy(s => s.UserName)
                    .ToDictionary(g => g.Key, g => g.First());

                var memberWxids = room.MemberList
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Distinct()
                    .ToList();

                // 少数增量通知可能只带 ShowNameList，不带 MemberList；此时用 ShowNameList 兜底，避免成员完全丢失。
                foreach (var wxid in displayNames.Keys)
                {
                    if (!memberWxids.Contains(wxid))
                    {
                        memberWxids.Add(wxid);
                    }
                }

                groups.Add(new Group
                {
                    groupWxid = room.UserName,
                    groupName = string.IsNullOrWhiteSpace(room.NickName) ? room.UserName : room.NickName,
                    groupNotice = room.Notice ?? string.Empty,
                    ownerWxid = room.Owner ?? string.Empty,
                    memberCount = memberWxids.Count,
                    groupAvatar = room.Avatar ?? string.Empty,
                    groupDescription = room.Remark ?? string.Empty,
                    isMuted = room.MsgSilent ? 1 : 0,
                    isPinned = 0,
                    groupStatus = room.IsUnusual ? 2 : 1,
                    createdAt = now,
                    updatedAt = now,
                    isDeleted = false
                });

                if (memberWxids.Count > 0)
                {
                    membersByRoom[room.UserName] = memberWxids.Select(wxid =>
                    {
                        displayNames.TryGetValue(wxid, out var display);
                        var showName = display?.ShowName ?? string.Empty;
                        return new GroupMember
                        {
                            memberWxid = wxid,
                            memberNickname = string.IsNullOrWhiteSpace(showName) ? wxid : showName,
                            alias = showName,
                            inviterWxid = display?.Inviter ?? string.Empty,
                            memberRole = string.Equals(wxid, room.Owner, StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                            joinSource = display?.Flag ?? 0,
                            joinTime = now,
                            memberRemarks = showName,
                            createdAt = now,
                            updatedAt = now,
                            isDeleted = false
                        };
                    }).ToList();
                }
            }

            return (groups, membersByRoom);
        }

        /// <summary>
        /// 通知前端重新读取会话列表。
        /// <para>群聊资料更新不会产生聊天消息事件，必须单独通知 IM 群聊页刷新。</para>
        /// </summary>
        private async Task PublishConversationsUpdatedAsync(IChannelHandlerContext context, string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return;
            }

            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connManager.GetConnectionAsync(connId);
            if (connInfo == null)
            {
                return;
            }

            _logger.LogInformation(
                "Publish ConversationsUpdatedEvent from group handler: Device={DeviceUuid}, Account={AccountId}, User={UserId}",
                connInfo.deviceUuid,
                accountId,
                connInfo.userId);
            await _eventBus.PublishAsync(new ConversationsUpdatedEvent(connInfo.deviceUuid, accountId, connInfo.userId));
        }

        /// <summary>
        /// 通知前端刷新群邀请审批列表。
        /// </summary>
        private async Task PublishGroupInvitationsUpdatedAsync(IChannelHandlerContext context, string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return;
            }

            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connManager.GetConnectionAsync(connId);
            if (connInfo == null)
            {
                return;
            }

            await _eventBus.PublishAsync(new GroupInvitationsUpdatedEvent(connInfo.deviceUuid, accountId, connInfo.userId));
        }

        /// <summary>
        /// 发布异步群任务结果。
        /// <para>ChatroomPushNotice 等数据型回包不会再额外发送 TaskResultNotice，因此这里直接补齐任务闭环。</para>
        /// </summary>
        private async Task PublishTaskResultAsync(IChannelHandlerContext context, long taskId, bool success, string message)
        {
            if (taskId <= 0)
            {
                return;
            }

            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connManager.GetConnectionAsync(connId);
            if (connInfo == null)
            {
                return;
            }

            await _eventBus.PublishAsync(new TaskResultReceivedEvent(taskId, success, message, connId, connInfo.deviceUuid));
        }
    }
}
