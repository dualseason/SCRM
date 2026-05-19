using DotNetty.Transport.Channels;
using Google.Protobuf;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Data;
using SCRM.SHARED.Models.Events;
using SCRM.API.Services.Core;
using SCRM.API.Services.Netty.Handlers.Abstractions;
using SCRM.API.Services.Netty.Parsing;
using SCRM.Services.Data;
using SCRM.Services.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 聊天消息处理器
    /// <para>处理私聊、群聊等聊天相关消息的上报。</para>
    /// </summary>
    public class ChatMessageHandler : MessageHandlerBase
    {
        private readonly ILogger<ChatMessageHandler> _logger;
        private readonly ApplicationDbContext _db;
        private readonly ConnectionManager _connManager;
        private readonly IEventBus _eventBus;
        private readonly ClientTaskService _clientTaskService;

        public ChatMessageHandler(
            ILogger<ChatMessageHandler> logger,
            ApplicationDbContext db,
            ConnectionManager connManager,
            IEventBus eventBus,
            ClientTaskService clientTaskService) : base(logger)
        {
            _logger = logger;
            _db = db;
            _connManager = connManager;
            _eventBus = eventBus;
            _clientTaskService = clientTaskService;
        }

        public async Task HandleMessage(TransportMessage message, IChannelHandlerContext context)
        {
            await SendAckAsync(message, context);

            try
            {
                switch (message.MsgType)
                {
                    case EnumMsgType.FriendTalkNotice:
                        await HandleFriendTalk(message.Content.Unpack<FriendTalkNoticeMessage>(), context);
                        break;
                    case EnumMsgType.WeChatTalkToFriendNotice:
                        await HandleWeChatTalkToFriend(message.Content.Unpack<WeChatTalkToFriendNoticeMessage>(), context);
                        break;
                    case EnumMsgType.PostMessageReadNotice:
                        await HandlePostMessageRead(message.Content.Unpack<PostMessageReadNoticeMessage>(), context);
                        break;
                    case EnumMsgType.HistoryMsgPushNotice:
                        await HandleHistoryMsgPush(message.Content.Unpack<HistoryMsgPushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.ConversationPushNotice:
                        await HandleConversationPush(message.Content.Unpack<ConversationPushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.ChatMsgIdsPushNotice:
                        await HandleChatMsgIdsPush(message.Content.Unpack<ChatMsgIdsPushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.ChatMsgFilePushNotice:
                        await HandleChatMsgFilePush(message.Content.Unpack<ChatMsgFilePushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.CdndownloadResultNotice: // proto 原名 CDNDownloadResultNotice
                        await HandleCdnDownloadResult(message.Content.Unpack<CDNDownloadResultNoticeMessage>(), context);
                        break;
                    case EnumMsgType.MsgDelNotice:
                        await HandleMsgDel(message.Content.Unpack<MsgDelNoticeMessage>(), context);
                        break;
                    case EnumMsgType.ConvDelNotice: // [Fix] 1055
                        await HandleConvDel(message.Content.Unpack<ConvDelNoticeMessage>(), context);
                        break;
                    case EnumMsgType.RequestTalkMsgTaskResultNotice:
                        await HandleRequestTalkMsgTaskResult(message.Content.Unpack<RequestTalkMsgTaskResultNoticeMessage>(), context);
                        break;
                    case EnumMsgType.RequestTalkContentTaskResultNotice:
                        await HandleRequestTalkContentTaskResult(message.Content.Unpack<RequestTalkContentTaskResultNoticeMessage>(), context);
                        break;
                    case EnumMsgType.RequestTalkDetailTaskResultNotice:
                        await HandleRequestTalkDetailTaskResult(message.Content.Unpack<RequestTalkDetailTaskResultNoticeMessage>(), context);
                        break;
                    case EnumMsgType.UnreadListPushNotice:
                        await HandleUnreadListPush(message.Content.Unpack<UnreadListPushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.BizConversPushNotice:
                        await HandleBizConversPush(message.Content.Unpack<BizConversPushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.QwConversPushNotice:
                        await HandleQwConversPush(message.Content.Unpack<QwConversPushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.GroupSendHistoryPushNotice:
                        await HandleGroupSendHistoryPush(message.Content.Unpack<GroupSendHistoryPushNoticeMessage>(), context);
                        break;
                    default:
                        // Other chat types (history, etc.) can be added here
                         _logger.LogInformation("Unhandled Chat Message Type: {Type} ({Id})", message.MsgType, (int)message.MsgType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Chat Message");
            }
        }

        /// <summary>
        /// 处理客户端按时间段回传的消息 MsgSvrId 快照。
        /// <para>
        /// 该通知来自 TriggerChatMsgIdsPushTask(1251) -> ChatMsgIdsPushNotice(1050)。
        /// 当前只记录并向前端推送摘要，不做删除对账；是否按快照软删除缺失消息，需要先确认 Android message.createTime 与服务端消息时间字段口径。
        /// </para>
        /// </summary>
        private async Task HandleChatMsgIdsPush(ChatMsgIdsPushNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning(
                    "ChatMsgIdsPushNotice ignored because WeChatId is empty. Start={Start}, End={End}, Count={Count}",
                    msg.StartTime,
                    msg.EndTime,
                    msg.Ids.Count);
                return;
            }

            var firstId = msg.Ids.Count > 0 ? msg.Ids[0].ToString() : string.Empty;
            var lastId = msg.Ids.Count > 0 ? msg.Ids[msg.Ids.Count - 1].ToString() : string.Empty;
            _logger.LogInformation(
                "ChatMsgIdsPushNotice handled: WeChatId={WeChatId}, Start={Start}, End={End}, Count={Count}, First={First}, Last={Last}",
                msg.WeChatId,
                msg.StartTime,
                msg.EndTime,
                msg.Ids.Count,
                firstId,
                lastId);

            await PublishChatMsgIdsSnapshotResultAsync(context, msg, firstId, lastId);
        }

        /// <summary>
        /// 处理聊天媒体文件上传结果。
        /// <para>
        /// 安卓端把图片、语音、视频或文件上传到服务端后，通过 ChatMsgFilePushNotice(1051)
        /// 回填 MsgSvrId 与 URL。这里只更新已有消息，避免凭 MsgSvrId 猜测会话创建脏消息。
        /// </para>
        /// </summary>
        private async Task HandleChatMsgFilePush(ChatMsgFilePushNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId) || msg.MsgSvrId == 0 || string.IsNullOrWhiteSpace(msg.Url))
            {
                _logger.LogWarning(
                    "ChatMsgFilePushNotice ignored because required fields are empty. WeChatId={WeChatId}, MsgSvrId={MsgSvrId}, UrlEmpty={UrlEmpty}",
                    msg.WeChatId,
                    msg.MsgSvrId,
                    string.IsNullOrWhiteSpace(msg.Url));
                return;
            }

            var updated = await _db.UpdateMessageMediaUrlByMsgSvrId(
                msg.WeChatId,
                msg.MsgSvrId,
                msg.Url,
                msg.MsgType > 0 ? (short)msg.MsgType : null,
                msg.FileSize,
                msg.SubType,
                "ChatMsgFilePushNotice");

            _logger.LogInformation(
                "ChatMsgFilePushNotice handled: WeChatId={WeChatId}, MsgSvrId={MsgSvrId}, MsgType={MsgType}, SubType={SubType}, FileSize={FileSize}, Updated={Updated}",
                msg.WeChatId,
                msg.MsgSvrId,
                msg.MsgType,
                msg.SubType,
                msg.FileSize,
                updated != null);

            if (updated != null)
            {
                await PublishMessageReceivedAsync(context, updated);
            }
        }

        /// <summary>
        /// 处理微信 CDN 文件下载结果。
        /// <para>
        /// CDNDownloadResultNotice(1271) 不携带 FriendId，成功时只按 MsgSvrId 回填 URL 与结构化媒体；
        /// 失败时推送一条无 TaskId 通知，让网页能看到失败原因。
        /// </para>
        /// </summary>
        private async Task HandleCdnDownloadResult(CDNDownloadResultNoticeMessage msg, IChannelHandlerContext context)
        {
            if (!msg.Success)
            {
                _logger.LogWarning(
                    "CDNDownloadResultNotice failed: WeChatId={WeChatId}, FileId={FileId}, MsgSvrId={MsgSvrId}, ErrMsg={ErrMsg}",
                    msg.WeChatId,
                    msg.FileId,
                    msg.MsgSvrId,
                    msg.ErrMsg);
                await PublishTaskNotificationAsync(
                    context,
                    false,
                    $"CDN下载失败：WeChatId={msg.WeChatId}，MsgSvrId={msg.MsgSvrId}，FileId={msg.FileId}，ErrMsg={msg.ErrMsg}");
                return;
            }

            if (string.IsNullOrWhiteSpace(msg.WeChatId) || msg.MsgSvrId == 0 || string.IsNullOrWhiteSpace(msg.Url))
            {
                _logger.LogWarning(
                    "CDNDownloadResultNotice ignored because required fields are empty. WeChatId={WeChatId}, FileId={FileId}, MsgSvrId={MsgSvrId}, UrlEmpty={UrlEmpty}",
                    msg.WeChatId,
                    msg.FileId,
                    msg.MsgSvrId,
                    string.IsNullOrWhiteSpace(msg.Url));
                return;
            }

            var updated = await _db.UpdateMessageMediaUrlByMsgSvrId(
                msg.WeChatId,
                msg.MsgSvrId,
                msg.Url,
                sourceNotice: "CDNDownloadResultNotice",
                fileId: msg.FileId);

            _logger.LogInformation(
                "CDNDownloadResultNotice handled: WeChatId={WeChatId}, FileId={FileId}, MsgSvrId={MsgSvrId}, Updated={Updated}",
                msg.WeChatId,
                msg.FileId,
                msg.MsgSvrId,
                updated != null);

            if (updated != null)
            {
                await PublishMessageReceivedAsync(context, updated);
            }
            else
            {
                await PublishTaskNotificationAsync(
                    context,
                    false,
                    $"CDN下载成功但未命中本地消息：WeChatId={msg.WeChatId}，MsgSvrId={msg.MsgSvrId}，FileId={msg.FileId}");
            }
        }

        /// <summary>
        /// 处理手机微信侧单条消息删除通知。
        /// <para>MsgDelNotice(1054) 与 ConvDelNotice(1055) 不同：这里只软删除一条消息，不删除整个会话。</para>
        /// </summary>
        private async Task HandleMsgDel(MsgDelNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning("MsgDelNotice ignored because WeChatId is empty. FriendId={FriendId}, MsgId={MsgId}, MsgSvrId={MsgSvrId}",
                    msg.FriendId,
                    msg.MsgId,
                    msg.MsgSvrId);
                return;
            }

            var originalContent = DecodeContent(msg.Content);
            var deletedMessage = await _db.MarkMessageDeleted(
                msg.WeChatId,
                msg.MsgSvrId,
                msg.MsgId,
                msg.FriendId ?? string.Empty,
                originalContent);

            _logger.LogInformation(
                "MsgDelNotice handled: WeChatId={WeChatId}, FriendId={FriendId}, MsgId={MsgId}, MsgSvrId={MsgSvrId}, Deleted={Deleted}",
                msg.WeChatId,
                msg.FriendId,
                msg.MsgId,
                msg.MsgSvrId,
                deletedMessage != null);

            // 当前消息查询统一过滤 isDeleted；通知会话/聊天页刷新即可从页面移除该消息。
            await PublishConversationsUpdatedAsync(context, msg.WeChatId);
        }

        // [Fix] Handle Conversation Deletion
        private async Task HandleConvDel(ConvDelNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrEmpty(msg.WeChatId)) return;
            var target = msg.FriendId ?? "Unknown";
            var deleted = await _db.MarkConversationDeleted(msg.WeChatId, target);
            _logger.LogInformation(
                "Conversation Deleted: {Wxid} deleted chat with {Target}, Marked={Deleted}",
                msg.WeChatId,
                target,
                deleted);

            // 会话删除不会产生新的 ReceiveMessage；主动通知前端刷新会话列表和当前聊天区。
            if (deleted)
            {
                await PublishConversationsUpdatedAsync(context, msg.WeChatId);
            }
        }

        /// <summary>
        /// 处理客户端上报的会话已读通知。
        /// <para>
        /// 安卓端在用户进入聊天页后会上报 PostMessageReadNotice；
        /// 服务端同步清理该会话的未读消息与会话未读计数，避免日志持续出现“Unhandled Chat Message Type”。
        /// </para>
        /// </summary>
        private async Task HandlePostMessageRead(PostMessageReadNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId) || string.IsNullOrWhiteSpace(msg.FriendId))
            {
                _logger.LogDebug("PostMessageReadNotice ignored because WeChatId/FriendId is empty.");
                return;
            }

            var updatedMessages = await _db.MarkConversationMessagesRead(msg.WeChatId, msg.FriendId);
            _logger.LogInformation(
                "PostMessageReadNotice handled: WeChatId={WeChatId}, FriendId={FriendId}, UpdatedMessages={UpdatedMessages}",
                msg.WeChatId,
                msg.FriendId,
                updatedMessages);

            // 已读通知同时会清 Conversations.unreadCount，主动推送会话刷新，避免前端红点停留到下一次全量刷新。
            await PublishConversationsUpdatedAsync(context, msg.WeChatId);
        }

        private async Task HandleFriendTalk(FriendTalkNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrEmpty(msg.WeChatId)) return;

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for WeChatId: {WeChatId}", msg.WeChatId);
                return;
            }

            var message = BuildMessageEntity(
                account.wxid,
                msg.FriendId,
                msg.ContentType,
                msg.Content,
                msg.MsgId,
                msg.MsgSvrId,
                isSend: false,
                createTime: msg.CreateTime,
                status: 0);
            var savedMessages = await _db.SaveMessages(account.wxid, new List<Message> { message });
            var savedMessage = savedMessages.FirstOrDefault() ?? message;
            await SaveAdvancedMessageContentMetadataAsync(savedMessage, msg.Ext, "FriendTalkNotice");
            await UpsertConversationForRealtimeMessageAsync(context, account.wxid, savedMessage, msg.NickName);

            _logger.LogInformation("Received Chat from {Friend} to {Me}, Content: {Content}", msg.FriendId, msg.WeChatId, savedMessage.content);

            // Publish Event
            await PublishMessageReceivedAsync(context, savedMessage);
            await PublishContactsRefreshWhenFriendVerifiedAsync(
                context,
                msg.WeChatId,
                msg.FriendId,
                msg.NickName,
                savedMessage.content,
                DecodeContent(msg.Content));
        }

        private async Task HandleWeChatTalkToFriend(WeChatTalkToFriendNoticeMessage msg, IChannelHandlerContext context)
        {
             if (string.IsNullOrEmpty(msg.WeChatId)) return;

             var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
             if (account == null) return;

             // 发送方向实时 Notice 同时带 MsgId 与 TaskId。为了与 1028 pending 消息合并，
             // 本地消息 ID 优先使用服务端下发时的 TaskId；微信本地 MsgId 另存到 clientMsgId 便于排查。
             var localMessageId = msg.TaskId > 0 ? msg.TaskId : msg.MsgId;
             var message = BuildMessageEntity(
                 account.wxid,
                 msg.FriendId,
                 msg.ContentType,
                 msg.Content,
                 localMessageId,
                 msg.MsgSvrId,
                 isSend: true,
                 createTime: msg.CreateTime,
                 status: 0);
             if (msg.TaskId > 0 && msg.MsgId > 0 && msg.TaskId != msg.MsgId)
             {
                 message.clientMsgId = msg.MsgId.ToString(System.Globalization.CultureInfo.InvariantCulture);
             }
              var savedMessages = await _db.SaveMessages(account.wxid, new List<Message> { message });
              var savedMessage = savedMessages.FirstOrDefault() ?? message;
              await SaveAdvancedMessageContentMetadataAsync(savedMessage, msg.Ext, "WeChatTalkToFriendNotice");
              await UpsertConversationForRealtimeMessageAsync(context, account.wxid, savedMessage, null);
              
             _logger.LogInformation("Synced Sent Chat from {Me} to {Friend}", msg.WeChatId, msg.FriendId);

             // 复用 MessageReceivedEvent 推给 UI；前端按 direction 区分收发。
             await PublishMessageReceivedAsync(context, savedMessage);
         }

        /// <summary>
        /// 处理历史消息分页推送。
        /// <para>
        /// 旧代码把 HistoryMsgPushNotice 路由到了 ChatMessageHandler 却没有处理，导致日志显示 Unhandled。
        /// 这里统一转成 Message 实体并通过 DbHelper.SaveMessages 幂等入库。
        /// </para>
        /// </summary>
        private async Task HandleHistoryMsgPush(HistoryMsgPushNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId) || msg.Messages == null || msg.Messages.Count == 0)
            {
                return;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for HistoryMsgPushNotice WeChatId: {WeChatId}", msg.WeChatId);
                return;
            }

            var messages = msg.Messages
                .Where(item => !string.IsNullOrWhiteSpace(item.FriendId))
                .Select(item => BuildMessageEntity(
                    account.wxid,
                    item.FriendId,
                    item.ContentType,
                    item.Content,
                    item.MsgId,
                    item.MsgSvrId,
                    item.IsSend,
                    item.CreateTime,
                    item.Status))
                .ToList();

            var savedMessages = await _db.SaveMessages(account.wxid, messages);
            foreach (var savedMessage in savedMessages)
            {
                await UpsertConversationForRealtimeMessageAsync(context, account.wxid, savedMessage, null);
            }

            _logger.LogInformation(
                "HistoryMsgPushNotice handled: WeChatId={WeChatId}, Page={Page}, Size={Size}, Saved={Saved}",
                msg.WeChatId,
                msg.Page,
                msg.Size,
                savedMessages.Count);

            // 历史补偿可能一次推送大量消息，只把最后 20 条同步给当前页面，避免 SignalR/Blazor 刷屏。
            foreach (var message in savedMessages.TakeLast(20))
            {
                await PublishMessageReceivedAsync(context, message);
            }
        }

        /// <summary>
        /// 处理会话列表推送。
        /// </summary>
        private async Task HandleConversationPush(ConversationPushNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning("ConversationPushNotice ignored because WeChatId is empty. TaskId={TaskId}", msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, "会话同步失败：WeChatId 为空");
                return;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for ConversationPushNotice WeChatId: {WeChatId}, TaskId={TaskId}", msg.WeChatId, msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, $"会话同步失败：账号不存在 {msg.WeChatId}");
                return;
            }

            if (msg.Convers == null || msg.Convers.Count == 0)
            {
                _logger.LogInformation(
                    "ConversationPushNotice handled with empty result: WeChatId={WeChatId}, TaskId={TaskId}, Page={Page}, Count={Count}, Offset={Offset}, NextOffset={NextOffset}",
                    msg.WeChatId,
                    msg.TaskId,
                    msg.Page,
                    msg.Count,
                    msg.Offset,
                    msg.NextOffset);
                await PublishTaskResultAsync(context, msg.TaskId, true, $"会话同步完成：0 条，Count={msg.Count}，Page={msg.Page}，Offset={msg.Offset}，NextOffset={msg.NextOffset}");
                return;
            }

            var conversations = msg.Convers
                .Where(item => !string.IsNullOrWhiteSpace(item.UserName))
                .Select(item => BuildConversationEntity(account.wxid, item))
                .ToList();

            var savedConversations = await _db.SaveConversations(account.wxid, conversations);
            _logger.LogInformation(
                "ConversationPushNotice handled: WeChatId={WeChatId}, TaskId={TaskId}, Page={Page}, Size={Size}, Count={Count}, Offset={Offset}, NextOffset={NextOffset}, Saved={Saved}, ChatRooms={ChatRooms}",
                msg.WeChatId,
                msg.TaskId,
                msg.Page,
                msg.Size,
                msg.Count,
                msg.Offset,
                msg.NextOffset,
                savedConversations.Count,
                savedConversations.Count(c => IsChatRoom(c.conversationWxid)));

            await PublishConversationsUpdatedAsync(context, account.wxid);
            await PublishTaskResultAsync(context, msg.TaskId, true, $"会话同步完成：保存 {savedConversations.Count}/{msg.Convers.Count} 条，Count={msg.Count}，Page={msg.Page}，Offset={msg.Offset}，NextOffset={msg.NextOffset}");
        }

        /// <summary>
        /// 处理按消息 ID 拉取到的单条聊天消息。
        /// <para>
        /// 62203/SmRun 的 RequestTalkMsgTaskResultNotice 常用于大图、视频、语音或历史单条补偿；
        /// 服务端按 HistoryMsgPushNotice 的同一口径落库，避免收到 1253 后只打印未处理日志。
        /// </para>
        /// </summary>
        private async Task HandleRequestTalkMsgTaskResult(RequestTalkMsgTaskResultNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId) || string.IsNullOrWhiteSpace(msg.FriendId))
            {
                _logger.LogDebug(
                    "RequestTalkMsgTaskResultNotice ignored because WeChatId/FriendId is empty. WeChatId={WeChatId}, FriendId={FriendId}, MsgSvrId={MsgSvrId}",
                    msg.WeChatId,
                    msg.FriendId,
                    msg.MsgSvrId);
                return;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for RequestTalkMsgTaskResultNotice WeChatId: {WeChatId}", msg.WeChatId);
                return;
            }

            var message = BuildMessageEntity(
                account.wxid,
                msg.FriendId,
                msg.ContentType,
                msg.Content,
                // 1253 只携带 MsgSvrId，不携带微信本地 MsgId；不能把 MsgSvrId 写入 localMessageId。
                0,
                msg.MsgSvrId,
                msg.IsSend,
                msg.CreateTime,
                msg.Status);
            var savedMessages = await _db.SaveMessages(account.wxid, new List<Message> { message });
            var savedMessage = savedMessages.FirstOrDefault() ?? message;
            await SaveAdvancedMessageContentMetadataAsync(savedMessage, null, "RequestTalkMsgTaskResultNotice");
            await UpsertConversationForRealtimeMessageAsync(context, account.wxid, savedMessage, null);

            _logger.LogInformation(
                "RequestTalkMsgTaskResultNotice handled: WeChatId={WeChatId}, FriendId={FriendId}, MsgSvrId={MsgSvrId}, ContentType={ContentType}",
                msg.WeChatId,
                msg.FriendId,
                msg.MsgSvrId,
                msg.ContentType);

            await PublishMessageReceivedAsync(context, savedMessage);
        }

        /// <summary>
        /// 处理原始消息内容回包。
        /// <para>该通知没有 FriendId，无法可靠新建消息；按 MsgSvrId 更新已有消息正文或 XML。</para>
        /// </summary>
        private async Task HandleRequestTalkContentTaskResult(RequestTalkContentTaskResultNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId) || msg.MsgSvrId == 0)
            {
                _logger.LogDebug(
                    "RequestTalkContentTaskResultNotice ignored because WeChatId/MsgSvrId is empty. WeChatId={WeChatId}, MsgSvrId={MsgSvrId}",
                    msg.WeChatId,
                    msg.MsgSvrId);
                return;
            }

            if (msg.MsgType == 0 && string.IsNullOrWhiteSpace(msg.Content))
            {
                _logger.LogWarning(
                    "RequestTalkContentTaskResultNotice ignored because it looks like an empty not-found response. WeChatId={WeChatId}, MsgSvrId={MsgSvrId}, MsgType={MsgType}",
                    msg.WeChatId,
                    msg.MsgSvrId,
                    msg.MsgType);
                return;
            }

            var updated = await UpdateMessageContentByMsgSvrIdAsync(
                msg.WeChatId,
                msg.MsgSvrId,
                NormalizeOriginalContentType(msg.MsgType),
                msg.Content ?? string.Empty,
                preferXml: LooksLikeXml(msg.Content ?? string.Empty));

            _logger.LogInformation(
                "RequestTalkContentTaskResultNotice handled: WeChatId={WeChatId}, MsgSvrId={MsgSvrId}, MsgType={MsgType}, Updated={Updated}",
                msg.WeChatId,
                msg.MsgSvrId,
                msg.MsgType,
                updated != null);

            if (updated != null)
            {
                await SaveAdvancedMessageContentMetadataAsync(updated, null, "RequestTalkContentTaskResultNotice", msg.MsgType);
                await PublishMessageReceivedAsync(context, updated);
            }
        }

        /// <summary>
        /// 处理聊天详情回包。
        /// <para>详情回包包含 FriendId，可在本地没有消息时创建一条补偿记录；若已有 MsgId 则按本地消息号幂等覆盖。</para>
        /// </summary>
        private async Task HandleRequestTalkDetailTaskResult(RequestTalkDetailTaskResultNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogDebug("RequestTalkDetailTaskResultNotice ignored because WeChatId is empty. MsgId={MsgId}", msg.MsgId);
                return;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for RequestTalkDetailTaskResultNotice WeChatId: {WeChatId}", msg.WeChatId);
                return;
            }

            var contentText = DecodeContent(msg.Content);
            var localMessageId = msg.MsgId == 0 ? string.Empty : msg.MsgId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _clientTaskService.TryTakeRequestTalkDetailTaskContext(
                account.wxid,
                msg.MsgId,
                msg.FriendId,
                out var memoryPendingContext);
            var persistedPendingContext = await _db.FindRequestTalkDetailPendingContext(account.wxid, msg.MsgId, msg.FriendId);
            var pendingMsgSvrId = memoryPendingContext?.MsgSvrId
                ?? persistedPendingContext?.MsgSvrId
                ?? 0;
            var effectiveFriendId = !string.IsNullOrWhiteSpace(msg.FriendId)
                ? msg.FriendId.Trim()
                : memoryPendingContext?.FriendId?.Trim()
                    ?? persistedPendingContext?.FriendId?.Trim()
                    ?? string.Empty;

            var existing = !string.IsNullOrWhiteSpace(localMessageId)
                ? await _db.Messages.FirstOrDefaultAsync(m => m.accountId == account.wxid
                    && m.localMessageId == localMessageId
                    && !m.isDeleted)
                : null;

            if (existing == null && pendingMsgSvrId > 0)
            {
                existing = await FindMessageByMsgSvrIdForRequestDetailAsync(account.wxid, pendingMsgSvrId, effectiveFriendId);
            }

            if (existing != null)
            {
                if (pendingMsgSvrId > 0 && existing.msgSvrId.GetValueOrDefault() == 0)
                {
                    existing.msgSvrId = pendingMsgSvrId;
                }
                if (msg.MsgId > 0 && string.IsNullOrWhiteSpace(existing.localMessageId))
                {
                    existing.localMessageId = localMessageId;
                }

                ApplyRequestTalkDetailContent(existing, contentText);
                existing.updatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await SaveAdvancedMessageContentMetadataAsync(existing, null, "RequestTalkDetailTaskResultNotice");
                await PublishMessageReceivedAsync(context, existing);
                _logger.LogInformation(
                    "RequestTalkDetailTaskResultNotice updated existing message: WeChatId={WeChatId}, FriendId={FriendId}, MsgId={MsgId}, MsgSvrId={MsgSvrId}, IsOriginal={IsOriginal}",
                    msg.WeChatId,
                    effectiveFriendId,
                    msg.MsgId,
                    existing.msgSvrId,
                    msg.IsOriginal);
                return;
            }

            if (string.IsNullOrWhiteSpace(effectiveFriendId))
            {
                _logger.LogInformation(
                    "RequestTalkDetailTaskResultNotice received but no existing message and FriendId empty: WeChatId={WeChatId}, MsgId={MsgId}",
                    msg.WeChatId,
                    msg.MsgId);
                return;
            }

            var message = BuildMessageEntity(
                account.wxid,
                effectiveFriendId,
                NormalizeRealtimeContentType(EnumContentType.Text, contentText),
                ByteString.CopyFromUtf8(contentText),
                msg.MsgId,
                pendingMsgSvrId,
                isSend: false,
                createTime: 0,
                status: 0);
            var savedMessages = await _db.SaveMessages(account.wxid, new List<Message> { message });
            var savedMessage = savedMessages.FirstOrDefault() ?? message;
            await SaveAdvancedMessageContentMetadataAsync(savedMessage, null, "RequestTalkDetailTaskResultNotice");
            await UpsertConversationForRealtimeMessageAsync(context, account.wxid, savedMessage, null);
            await PublishMessageReceivedAsync(context, savedMessage);

            _logger.LogInformation(
                "RequestTalkDetailTaskResultNotice inserted fallback message: WeChatId={WeChatId}, FriendId={FriendId}, MsgId={MsgId}, MsgSvrId={MsgSvrId}, IsOriginal={IsOriginal}",
                msg.WeChatId,
                effectiveFriendId,
                msg.MsgId,
                savedMessage.msgSvrId,
                msg.IsOriginal);
        }

        /// <summary>
        /// 按 MsgSvrId 查找消息详情补偿目标消息。
        /// </summary>
        private async Task<Message?> FindMessageByMsgSvrIdForRequestDetailAsync(string ownerWxid, long msgSvrId, string? friendId)
        {
            var query = _db.Messages
                .Where(m => m.accountId == ownerWxid
                    && m.msgSvrId == msgSvrId
                    && !m.isDeleted);

            var normalizedFriendId = friendId?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedFriendId))
            {
                var byFriend = await query
                    .Where(m => m.senderWxid == normalizedFriendId || m.receiverWxid == normalizedFriendId)
                    .OrderByDescending(m => m.updatedAt)
                    .FirstOrDefaultAsync();

                if (byFriend != null)
                {
                    return byFriend;
                }
            }

            return await query
                .OrderByDescending(m => m.updatedAt)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// 应用消息详情补偿内容。
        /// <para>空内容不覆盖已有正文；XML 优先写 contentXml，避免破坏已经回填到 content 的媒体 URL。</para>
        /// </summary>
        private static void ApplyRequestTalkDetailContent(Message message, string contentText)
        {
            if (string.IsNullOrWhiteSpace(contentText))
            {
                return;
            }

            if (LooksLikeXml(contentText))
            {
                message.contentXml = contentText;
                if (string.IsNullOrWhiteSpace(message.content) || LooksLikeXml(message.content))
                {
                    message.content = contentText;
                }
                return;
            }

            message.content = contentText;
        }

        /// <summary>
        /// 处理未读数列表推送。
        /// <para>该协议只携带会话、未读数和免打扰状态，不携带昵称/头像；这里只更新已有会话，缺失时创建最小会话。</para>
        /// </summary>
        private async Task HandleUnreadListPush(UnreadListPushNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning("UnreadListPushNotice ignored because WeChatId is empty. TaskId={TaskId}", msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, "未读列表同步失败：WeChatId 为空");
                return;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for UnreadListPushNotice WeChatId: {WeChatId}, TaskId={TaskId}", msg.WeChatId, msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, $"未读列表同步失败：账号不存在 {msg.WeChatId}");
                return;
            }

            var now = DateTime.UtcNow;
            var rawCount = msg.Convers?.Count ?? 0;
            var conversations = msg.Convers == null
                ? new List<Conversation>()
                : msg.Convers
                    .Where(item => !string.IsNullOrWhiteSpace(item.UserName))
                    .Select(item => new Conversation
                    {
                        wechatAccountId = msg.WeChatId,
                        conversationWxid = item.UserName ?? string.Empty,
                        conversationType = IsChatRoom(item.UserName ?? string.Empty) ? 2 : 1,
                        displayName = item.UserName ?? string.Empty,
                        displayAvatar = string.Empty,
                        unreadCount = Math.Max(0, item.UnreadCnt),
                        messageCount = 0,
                        isPinned = 0,
                        isMuted = item.IsSilent ? 1 : 0,
                        lastMessageContent = string.Empty,
                        lastMessageTime = NormalizeWeChatTimestamp(item.UpdateTime) ?? now,
                        createdAt = now,
                        updatedAt = now,
                        isDeleted = false
                    })
                    .ToList();

            var (savedConversations, clearedCount) = await _db.SyncUnreadConversationSnapshot(msg.WeChatId, conversations);
            _logger.LogInformation(
                "UnreadListPushNotice handled: WeChatId={WeChatId}, TaskId={TaskId}, Raw={Raw}, Valid={Valid}, Saved={Saved}, Cleared={Cleared}",
                msg.WeChatId,
                msg.TaskId,
                rawCount,
                conversations.Count,
                savedConversations.Count,
                clearedCount);

            await PublishConversationsUpdatedAsync(context, msg.WeChatId);
            await PublishTaskResultAsync(context, msg.TaskId, true, $"未读列表同步完成：保存 {savedConversations.Count}/{conversations.Count} 条，清零 {clearedCount} 条，原始 {rawCount} 条");
        }

        /// <summary>
        /// 处理公众号/业务号会话推送。
        /// <para>当前 UI 没有独立公众号页，先落到 Conversations，避免 2041 被判定为未知消息。</para>
        /// </summary>
        private async Task HandleBizConversPush(BizConversPushNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning("BizConversPushNotice ignored because WeChatId is empty. TaskId={TaskId}", msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, "公众号会话同步失败：WeChatId 为空");
                return;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for BizConversPushNotice WeChatId: {WeChatId}, TaskId={TaskId}", msg.WeChatId, msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, $"公众号会话同步失败：账号不存在 {msg.WeChatId}");
                return;
            }

            if (msg.Convers == null || msg.Convers.Count == 0)
            {
                _logger.LogInformation("BizConversPushNotice handled with empty result: WeChatId={WeChatId}, TaskId={TaskId}, Page={Page}, Count={Count}", msg.WeChatId, msg.TaskId, msg.Page, msg.Count);
                await PublishTaskResultAsync(context, msg.TaskId, true, $"公众号会话同步完成：0 条，Count={msg.Count}，Page={msg.Page}");
                return;
            }

            var conversations = msg.Convers
                .Where(item => !string.IsNullOrWhiteSpace(item.UserName))
                .Select(item => BuildBizConversationEntity(account.wxid, item))
                .ToList();

            var savedConversations = await _db.SaveConversations(account.wxid, conversations);
            _logger.LogInformation(
                "BizConversPushNotice handled: WeChatId={WeChatId}, TaskId={TaskId}, Page={Page}, Size={Size}, Count={Count}, Saved={Saved}",
                msg.WeChatId,
                msg.TaskId,
                msg.Page,
                msg.Size,
                msg.Count,
                savedConversations.Count);
            await PublishConversationsUpdatedAsync(context, account.wxid);
            await PublishTaskResultAsync(context, msg.TaskId, true, $"公众号会话同步完成：保存 {savedConversations.Count}/{msg.Convers.Count} 条，Count={msg.Count}，Page={msg.Page}");
        }

        /// <summary>
        /// 处理企微会话推送。
        /// <para>企微会话优先使用 RoomFlag 判断群/个人，避免企微群 ID 不带 @chatroom 时被落成个人会话。</para>
        /// </summary>
        private async Task HandleQwConversPush(QwConversPushNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning("QwConversPushNotice ignored because WeChatId is empty. TaskId={TaskId}", msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, "企微会话同步失败：WeChatId 为空");
                return;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for QwConversPushNotice WeChatId: {WeChatId}, TaskId={TaskId}", msg.WeChatId, msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, $"企微会话同步失败：账号不存在 {msg.WeChatId}");
                return;
            }

            if (msg.Convers == null || msg.Convers.Count == 0)
            {
                _logger.LogInformation("QwConversPushNotice handled with empty result: WeChatId={WeChatId}, TaskId={TaskId}, Page={Page}, Count={Count}", msg.WeChatId, msg.TaskId, msg.Page, msg.Count);
                await PublishTaskResultAsync(context, msg.TaskId, true, $"企微会话同步完成：0 条，Count={msg.Count}，Page={msg.Page}");
                return;
            }

            var conversations = msg.Convers
                .Where(item => !string.IsNullOrWhiteSpace(item.UserName))
                .Select(item => BuildQwConversationEntity(account.wxid, item))
                .ToList();

            var savedConversations = await _db.SaveConversations(account.wxid, conversations);
            _logger.LogInformation(
                "QwConversPushNotice handled: WeChatId={WeChatId}, TaskId={TaskId}, Page={Page}, Size={Size}, Count={Count}, Saved={Saved}",
                msg.WeChatId,
                msg.TaskId,
                msg.Page,
                msg.Size,
                msg.Count,
                savedConversations.Count);
            await PublishConversationsUpdatedAsync(context, account.wxid);
            await PublishTaskResultAsync(context, msg.TaskId, true, $"企微会话同步完成：保存 {savedConversations.Count}/{msg.Convers.Count} 条，Count={msg.Count}，Page={msg.Page}");
        }

        /// <summary>
        /// 处理群发助手历史记录推送。
        /// <para>Android 侧从微信 massendinfo 表读取后通过 1056 上报；这里落到 MassMessages/MassMessageDetails，避免历史结果丢失。</para>
        /// </summary>
        private async Task HandleGroupSendHistoryPush(GroupSendHistoryPushNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning("GroupSendHistoryPushNotice ignored because WeChatId is empty. TaskId={TaskId}", msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, "群发历史同步失败：WeChatId 为空");
                return;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for GroupSendHistoryPushNotice WeChatId: {WeChatId}, TaskId={TaskId}", msg.WeChatId, msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, $"群发历史同步失败：账号不存在 {msg.WeChatId}");
                return;
            }

            if (msg.Messages == null || msg.Messages.Count == 0)
            {
                _logger.LogInformation("GroupSendHistoryPushNotice handled with empty result: WeChatId={WeChatId}, TaskId={TaskId}", msg.WeChatId, msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, true, "群发历史同步完成：0 条");
                return;
            }

            var converted = BuildMassSendHistoryPayload(account.wxid, msg.Messages);
            var saved = await _db.SaveMassSendHistory(account.wxid, converted.Messages, converted.DetailsByFingerprint);

            _logger.LogInformation(
                "GroupSendHistoryPushNotice handled: WeChatId={WeChatId}, TaskId={TaskId}, Count={Count}, Saved={Saved}",
                msg.WeChatId,
                msg.TaskId,
                msg.Messages.Count,
                saved.Count);

            await PublishTaskResultAsync(
                context,
                msg.TaskId,
                true,
                $"群发历史已同步：{saved.Count}/{msg.Messages.Count}");
        }

        private Message BuildMessageEntity(
            string ownerWxid,
            string friendId,
            EnumContentType contentType,
            ByteString content,
            long msgId,
            long msgSvrId,
            bool isSend,
            long createTime,
            int status)
        {
            var now = DateTime.UtcNow;
            var messageTime = NormalizeWeChatTimestamp(createTime) ?? now;
            var contentText = DecodeContent(content);
            contentText = NormalizeRealtimeContent(friendId, contentText, isSend);
            var normalizedContentType = NormalizeRealtimeContentType(contentType, contentText);

            return new Message
            {
                accountId = ownerWxid,
                msgSvrId = msgSvrId == 0 ? null : msgSvrId,
                localMessageId = msgId == 0 ? null : msgId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                senderWxid = isSend ? ownerWxid : friendId,
                receiverWxid = isSend ? friendId : ownerWxid,
                chatType = IsChatRoom(friendId) ? (short)2 : (short)1,
                messageType = (short)normalizedContentType,
                content = contentText,
                contentXml = LooksLikeXml(contentText) ? contentText : null,
                direction = isSend ? (short)1 : (short)2,
                sendStatus = isSend ? NormalizeSendStatus(status) : (short)3,
                readStatus = isSend ? (short)1 : (short)0,
                sentAt = messageTime,
                receivedAt = isSend ? null : messageTime,
                createdAt = now,
                updatedAt = now,
                isDeleted = false,
                isRevoked = false
            };
        }

        /// <summary>
        /// 归一化实时聊天正文。
        /// <para>
        /// 旧版安卓上报单聊 FriendTalkNotice 时可能把 friendId 作为 clientMsgId 前缀拼到 bytes Content，
        /// 形如“wxid_xxx正文”。服务端保存前兜底剥离，避免网页出现 wxid 与正文粘连。
        /// 群聊正文仍保留“成员wxid:正文”格式，供前端解析群成员昵称。
        /// </para>
        /// </summary>
        private static string NormalizeRealtimeContent(string? friendId, string contentText, bool isSend)
        {
            if (isSend || string.IsNullOrWhiteSpace(friendId) || string.IsNullOrEmpty(contentText) || IsChatRoom(friendId))
            {
                return contentText;
            }

            return StripPrivateChatPrefix(contentText, friendId);
        }

        /// <summary>
        /// 剥离单聊正文前误拼接的 wxid/clientMsgId。
        /// <para>62203 迁移期间发现安卓端有两种形态：
        /// “wxid_xxx:正文”和“wxid_xxx正文”。后者没有分隔符，只能按当前 friendId 精确前缀剥离。</para>
        /// </summary>
        private static string StripPrivateChatPrefix(string? contentText, string? friendId)
        {
            if (string.IsNullOrEmpty(contentText) || string.IsNullOrWhiteSpace(friendId))
            {
                return contentText ?? string.Empty;
            }

            var prefix = friendId.Trim();
            var text = contentText;
            var changed = false;
            while (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = text.Substring(prefix.Length);
                var cleanedRemainder = remainder.TrimStart(':', '：', ' ', (char)9, (char)13, (char)10);
                // 这里的 prefix 来自 FriendTalkNotice.FriendId，是当前会话对方 wxid 的精确值。
                // 62203 实测安卓端可能把该精确 friendId 直接拼到正文前，形如“wxid_xxx12”。
                // 因此只要命中完整 friendId 前缀就剥离，不再按后续首字符是否为字母数字阻断。
                text = cleanedRemainder;
                changed = true;
            }

            text = changed ? text : StripLooseWxidPrefix(contentText);
            return TrimAccidentalCoordinateSuffix(text);
        }

        /// <summary>
        /// 服务端保存前兜底剥离疑似 wxid 前缀。
        /// <para>仅处理“wxid_ + 常见长度 + 中文/标点正文”的形态，避免误截真实 wxid。</para>
        /// </summary>
        private static string StripLooseWxidPrefix(string contentText)
        {
            if (string.IsNullOrWhiteSpace(contentText) || !contentText.StartsWith("wxid_", StringComparison.OrdinalIgnoreCase))
            {
                return contentText;
            }

            var match = System.Text.RegularExpressions.Regex.Match(
                contentText,
                @"^(?<wxid>wxid_[A-Za-z0-9_\-]{14,22})(?<rest>[\s\S]*)$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return contentText;
            }

            var rest = match.Groups["rest"].Value;
            if (string.IsNullOrWhiteSpace(rest))
            {
                return contentText;
            }

            var first = rest[0];
            if (char.IsWhiteSpace(first) || first == ':' || first == '：' || IsLikelyMessageStartChar(first))
            {
                return rest.TrimStart(':', '：', (char)13, (char)10, ' ', (char)9);
            }

            return contentText;
        }

        private static bool IsLikelyMessageStartChar(char ch)
        {
            return (ch >= '\u4e00' && ch <= '\u9fff')
                || char.IsPunctuation(ch)
                || char.IsSymbol(ch);
        }

        /// <summary>
        /// 剥离实时单聊文本尾部误拼接的坐标片段。
        /// <para>安卓 UI Hook 迁移 62203 期间偶发把点击坐标如“720.640”拼到正文尾部；这里仅对文本保存前做兜底清洗。</para>
        /// </summary>
        private static string TrimAccidentalCoordinateSuffix(string contentText)
        {
            if (string.IsNullOrWhiteSpace(contentText))
            {
                return contentText;
            }

            return System.Text.RegularExpressions.Regex.Replace(
                contentText,
                @"\s+\d{2,4}\.\d{2,4}\s*$",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }
        /// <summary>
        /// 归一化实时消息类型。
        /// <para>
        /// 微信普通文本表情（例如“[微笑]”）经常仍按 Text 上报；保持文本展示即可。
        /// 但网页下发的图片/视频/文件有时会先以 Text 形式回推远端 URL，如果不纠正，
        /// 聊天页只能看到一串链接，用户会误判为媒体发送失败。这里只根据明确 URL 后缀做展示层类型修正。
        /// </para>
        /// </summary>
        private static EnumContentType NormalizeRealtimeContentType(EnumContentType contentType, string contentText)
        {
            if (contentType != EnumContentType.Text || string.IsNullOrWhiteSpace(contentText))
            {
                return contentType;
            }

            var lower = contentText.Split('?', '#')[0].ToLowerInvariant();
            if (lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".png")
                || lower.EndsWith(".gif") || lower.EndsWith(".webp") || lower.EndsWith(".bmp"))
            {
                return EnumContentType.Picture;
            }

            if (lower.EndsWith(".mp4") || lower.EndsWith(".mov") || lower.EndsWith(".m4v")
                || lower.EndsWith(".3gp") || lower.EndsWith(".avi")
                || lower.EndsWith(".mkv") || lower.EndsWith(".webm"))
            {
                return EnumContentType.Video;
            }

            return contentType;
        }

        private static Conversation BuildConversationEntity(string ownerWxid, ConversMessage item)
        {
            var now = DateTime.UtcNow;
            var conversationWxid = item.UserName ?? string.Empty;
            var displayName = !string.IsNullOrWhiteSpace(item.Remark)
                ? item.Remark
                : (!string.IsNullOrWhiteSpace(item.ShowName) ? item.ShowName : conversationWxid);

            return new Conversation
            {
                wechatAccountId = ownerWxid,
                conversationWxid = conversationWxid,
                conversationType = IsChatRoom(conversationWxid) ? 2 : 1,
                displayName = displayName ?? string.Empty,
                displayAvatar = item.Avatar ?? string.Empty,
                unreadCount = item.UnreadCnt,
                messageCount = item.MsgCnt,
                isPinned = item.IsTop ? 1 : 0,
                isMuted = item.IsSilent ? 1 : 0,
                lastMessageContent = item.Digest ?? string.Empty,
                lastMessageTime = NormalizeWeChatTimestamp(item.UpdateTime) ?? now,
                createdAt = now,
                updatedAt = now,
                isDeleted = false
            };
        }

        /// <summary>
        /// 构建公众号/业务号会话实体。
        /// </summary>
        private static Conversation BuildBizConversationEntity(string ownerWxid, BizConversMessage item)
        {
            var now = DateTime.UtcNow;
            var conversationWxid = item.UserName ?? string.Empty;
            var displayName = !string.IsNullOrWhiteSpace(item.ShowName) ? item.ShowName : conversationWxid;

            return new Conversation
            {
                wechatAccountId = ownerWxid,
                conversationWxid = conversationWxid,
                conversationType = IsChatRoom(conversationWxid) ? 2 : 1,
                displayName = displayName ?? string.Empty,
                displayAvatar = item.Avatar ?? string.Empty,
                unreadCount = item.UnreadCnt,
                messageCount = item.MsgCnt,
                isPinned = 0,
                isMuted = 0,
                lastMessageContent = item.Digest ?? string.Empty,
                lastMessageTime = NormalizeWeChatTimestamp(item.UpdateTime) ?? now,
                createdAt = now,
                updatedAt = now,
                isDeleted = false
            };
        }

        /// <summary>
        /// 构建企微会话实体。
        /// </summary>
        private static Conversation BuildQwConversationEntity(string ownerWxid, QwConversMessage item)
        {
            var now = DateTime.UtcNow;
            var conversationWxid = item.UserName ?? string.Empty;
            var displayName = !string.IsNullOrWhiteSpace(item.ShowName)
                ? item.ShowName
                : (!string.IsNullOrWhiteSpace(item.ParentName) ? item.ParentName : conversationWxid);

            var isGroupConversation = item.RoomFlag != 0 || IsChatRoom(conversationWxid);

            return new Conversation
            {
                wechatAccountId = ownerWxid,
                conversationWxid = conversationWxid,
                conversationType = isGroupConversation ? 2 : 1,
                displayName = displayName ?? string.Empty,
                displayAvatar = item.Avatar ?? string.Empty,
                unreadCount = item.UnreadCnt,
                messageCount = item.MsgCnt,
                isPinned = item.IsTop ? 1 : 0,
                isMuted = item.IsSilent ? 1 : 0,
                lastMessageContent = item.Digest ?? string.Empty,
                lastMessageTime = NormalizeWeChatTimestamp(item.UpdateTime) ?? now,
                createdAt = now,
                updatedAt = now,
                isDeleted = false
            };
        }

        /// <summary>
        /// 构建群发助手历史落库负载。
        /// </summary>
        private static (List<SCRM.API.Models.Entities.MassMessage> Messages, Dictionary<string, List<MassMessageDetail>> DetailsByFingerprint)
            BuildMassSendHistoryPayload(string ownerWxid, IEnumerable<Jubo.JuLiao.IM.Wx.Proto.MassMessage> items)
        {
            var messages = new List<SCRM.API.Models.Entities.MassMessage>();
            var detailsByFingerprint = new Dictionary<string, List<MassMessageDetail>>();
            var accountKey = BuildStableNumericKey(ownerWxid);

            foreach (var item in items.Where(item => item != null))
            {
                var sentTime = NormalizeWeChatTimestamp(item.CreateTime) ?? DateTime.UtcNow;
                var toList = item.ToList ?? string.Empty;
                var recipients = SplitMassSendRecipients(toList);
                var totalRecipients = item.ToCount > 0 ? item.ToCount : recipients.Count;
                var status = item.Status;
                var content = item.Content ?? string.Empty;

                var message = new SCRM.API.Models.Entities.MassMessage
                {
                    wechatAccountId = accountKey,
                    messageTitle = toList,
                    messageContent = content,
                    messageType = (int)item.ContentType,
                    targetType = 1,
                    totalRecipients = totalRecipients,
                    successSentCount = IsMassSendSuccessStatus(status) ? totalRecipients : 0,
                    failedSentCount = IsMassSendFailureStatus(status) ? totalRecipients : 0,
                    sendStatus = status,
                    scheduledTime = sentTime,
                    sentTime = sentTime,
                    createdAt = sentTime,
                    updatedAt = DateTime.UtcNow
                };

                messages.Add(message);

                if (recipients.Count > 0)
                {
                    var fingerprint = DbHelper.BuildMassSendHistoryFingerprint(message);
                    detailsByFingerprint[fingerprint] = recipients.Select(recipient => new MassMessageDetail
                    {
                        recipientWxid = recipient,
                        sendStatus = status,
                        errorMessage = IsMassSendFailureStatus(status) ? $"群发助手状态：{status}" : string.Empty,
                        retryCount = 0,
                        sentTime = sentTime,
                        createdAt = sentTime,
                        updatedAt = DateTime.UtcNow
                    }).ToList();
                }
            }

            return (messages, detailsByFingerprint);
        }

        /// <summary>
        /// 好友通过验证后，微信通常先上报 FriendTalkNotice 系统文本，再异步写入联系人表。
        /// <para>这里不直接写联系人库，只通知前端刷新并等待 FriendPushNotice/DbHelper.SaveContacts 落库。</para>
        /// </summary>
        private async Task PublishContactsRefreshWhenFriendVerifiedAsync(
            IChannelHandlerContext context,
            string accountId,
            string? friendId,
            string? friendNick,
            string? content,
            string? rawContent = null)
        {
            if (!LooksLikeFriendVerificationAccepted(content, friendId)
                && !LooksLikeFriendVerificationAccepted(rawContent, friendId))
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
                "Friend verification accepted message detected, publish contacts refresh: Device={DeviceUuid}, Account={AccountId}, Friend={FriendId}, Nick={Nick}, Content={Content}",
                connInfo.deviceUuid,
                accountId,
                friendId,
                friendNick,
                NormalizeFriendVerificationContent(content ?? rawContent, friendId));

            // 好友通过验证的系统消息说明对方已经接受请求，先关闭 FriendRequests 待处理状态。
            // 联系人资料仍按 SaveContacts 最小占位 + 后续全量同步覆盖，避免绕过 DbHelper。
            var markedAccepted = await _db.MarkFriendRequestAccepted(accountId, friendId ?? string.Empty, "好友验证消息已确认通过");
            await UpsertVerifiedFriendStubAsync(accountId, friendId, friendNick);
            await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, accountId, connInfo.userId));
            if (markedAccepted)
            {
                await _eventBus.PublishAsync(new FriendRequestsUpdatedEvent(connInfo.deviceUuid, accountId, connInfo.userId));
            }

            // 好友验证通过时，实测安卓端可能只先上报 FriendTalkNotice，短时间内 FriendPushNotice 仍未包含新好友。
            // 这里不绕过 DbHelper：先按系统消息中的 friendId/nick 做最小联系人占位，再补发多轮全量同步，后续 FriendPushNotice 会覆盖完整资料。
            _clientTaskService.ScheduleContactListRefreshAfterMutation(
                connId,
                DateTime.UtcNow.Ticks,
                "FriendTalkNoticeVerified");

            if (!string.IsNullOrWhiteSpace(friendId))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                        var taskId = DateTime.UtcNow.Ticks;
                        var sent = await _clientTaskService.SendGetContactInfoTaskAsync(connId, friendId.Trim(), taskId: taskId).ConfigureAwait(false);
                        _logger.LogInformation(
                            "好友验证通过后补拉单个联系人资料: Friend={FriendId}, TaskId={TaskId}, Sent={Sent}",
                            friendId,
                            taskId,
                            sent);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "好友验证通过后补拉单个联系人资料异常: Friend={FriendId}", friendId);
                    }
                });
            }
        }

        /// <summary>
        /// 通过实时聊天消息补齐会话和最小群资料。
        /// <para>
        /// 62203 实测群消息会先以 FriendTalkNotice 到达；如果没有主动触发 ConversationPushNotice/ChatroomPushNotice，
        /// 仅保存消息会导致 Web 端收到事件但 IM 群聊列表为空。这里按 DbHelper 统一口径补最小会话。
        /// </para>
        /// </summary>
        private async Task UpsertConversationForRealtimeMessageAsync(IChannelHandlerContext context, string ownerWxid, Message message, string? noticeNickName)
        {
            var conversationWxid = ResolveConversationWxid(ownerWxid, message);
            if (string.IsNullOrWhiteSpace(conversationWxid))
            {
                _logger.LogDebug(
                    "Realtime conversation skipped: Owner={OwnerWxid}, Sender={Sender}, Receiver={Receiver}, MessageId={MessageId}",
                    ownerWxid,
                    message.senderWxid,
                    message.receiverWxid,
                    message.messageId);
                return;
            }

            var isChatRoom = IsChatRoom(conversationWxid);
            string? displayName = null;
            string? displayAvatar = null;

            if (isChatRoom)
            {
                var group = await _db.Groups
                    .AsNoTracking()
                    .Where(g => g.groupWxid == conversationWxid && !g.isDeleted)
                    .OrderByDescending(g => g.updatedAt)
                    .FirstOrDefaultAsync();

                displayName = !string.IsNullOrWhiteSpace(group?.groupName)
                    ? group.groupName
                    : conversationWxid;
                displayAvatar = group?.groupAvatar ?? string.Empty;

                await UpsertMinimalChatRoomFromMessageAsync(ownerWxid, conversationWxid, displayName, displayAvatar);
            }
            else
            {
                displayName = !string.IsNullOrWhiteSpace(noticeNickName)
                    ? noticeNickName
                    : await ResolveContactDisplayNameAsync(ownerWxid, conversationWxid);
            }

            var savedConversation = await _db.SaveConversationFromMessage(ownerWxid, message, displayName, displayAvatar);
            if (savedConversation != null && message.conversationId == null)
            {
                message.conversationId = savedConversation.id;
            }

            _logger.LogInformation(
                "Realtime conversation upsert: Owner={OwnerWxid}, Conversation={ConversationWxid}, IsChatRoom={IsChatRoom}, Saved={Saved}, ConversationId={ConversationId}, DisplayName={DisplayName}",
                ownerWxid,
                conversationWxid,
                isChatRoom,
                savedConversation != null,
                savedConversation?.id,
                savedConversation?.displayName ?? displayName);

            if (savedConversation != null && isChatRoom)
            {
                await PublishConversationsUpdatedAsync(context, ownerWxid);
            }
        }

        /// <summary>
        /// 群聊未同步到 Groups 表时，收到任意群消息先创建最小群资料。
        /// <para>后续 ChatroomPushNotice 到达后会通过 DbHelper.SaveChatRooms 覆盖完整群名、头像和成员。</para>
        /// </summary>
        private async Task UpsertMinimalChatRoomFromMessageAsync(string ownerWxid, string roomId, string displayName, string? displayAvatar)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            var group = new Group
            {
                groupWxid = roomId,
                groupName = string.IsNullOrWhiteSpace(displayName) ? roomId : displayName,
                groupAvatar = displayAvatar ?? string.Empty,
                groupNotice = string.Empty,
                groupDescription = string.Empty,
                ownerWxid = string.Empty,
                memberCount = 0,
                isMuted = 0,
                isPinned = 0,
                groupStatus = 1,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow,
                isDeleted = false
            };

            await _db.SaveChatRooms(ownerWxid, new List<Group> { group });
        }

        /// <summary>
        /// 按 MsgSvrId 更新已有消息内容。
        /// <para>RequestTalkContentTaskResultNotice 不携带 FriendId，只能更新已经入库的消息，避免猜测会话关系。</para>
        /// </summary>
        private async Task<Message?> UpdateMessageContentByMsgSvrIdAsync(
            string ownerWxid,
            long msgSvrId,
            EnumContentType contentType,
            string content,
            bool preferXml)
        {
            var normalizedContentType = NormalizeRealtimeContentType(contentType, content);
            return await _db.UpdateMessageContentByMsgSvrId(
                ownerWxid,
                msgSvrId,
                (short)normalizedContentType,
                content,
                preferXml);
        }

        /// <summary>
        /// 查找好友显示名。
        /// </summary>
        private async Task<string> ResolveContactDisplayNameAsync(string ownerWxid, string friendWxid)
        {
            var contact = await _db.Contacts
                .AsNoTracking()
                .Where(c => c.ownerWxid == ownerWxid
                    && c.wxid == friendWxid
                    && !c.isDeleted)
                .OrderByDescending(c => c.updatedAt)
                .FirstOrDefaultAsync();

            if (contact == null)
            {
                return friendWxid;
            }

            if (!string.IsNullOrWhiteSpace(contact.remarks))
            {
                return contact.remarks;
            }

            return string.IsNullOrWhiteSpace(contact.nickname) ? friendWxid : contact.nickname;
        }

        /// <summary>
        /// 从消息中解析会话 ID，群聊优先取 @chatroom。
        /// </summary>
        private static string ResolveConversationWxid(string ownerWxid, Message message)
        {
            var sender = message.senderWxid ?? string.Empty;
            var receiver = message.receiverWxid ?? string.Empty;

            if (IsChatRoom(sender))
            {
                return sender;
            }

            if (IsChatRoom(receiver))
            {
                return receiver;
            }

            if (!string.Equals(sender, ownerWxid, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(sender))
            {
                return sender;
            }

            if (!string.Equals(receiver, ownerWxid, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(receiver))
            {
                return receiver;
            }

            return string.Empty;
        }

        /// <summary>
        /// 通过好友验证系统消息补一个最小联系人记录。
        /// <para>持久化必须走 DbHelper.SaveContacts，避免绕过联系人软删除恢复和 ownerWxid+wxid 幂等口径。</para>
        /// </summary>
        private async Task UpsertVerifiedFriendStubAsync(string accountId, string? friendId, string? friendNick)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(friendId))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var contact = new Contact
            {
                ownerWxid = accountId,
                wxid = friendId.Trim(),
                nickname = string.IsNullOrWhiteSpace(friendNick) ? friendId.Trim() : friendNick.Trim(),
                remarks = string.Empty,
                isFriend = 1,
                isDeleted = false,
                createdAt = now,
                updatedAt = now
            };

            await _db.SaveContacts(accountId, new List<Contact> { contact });
        }

        private static bool LooksLikeFriendVerificationAccepted(string? content, string? friendId = null)
        {
            var normalized = NormalizeFriendVerificationContent(content, friendId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            // 安卓端 62203 的 FriendTalkNotice 可能把 friendId 拼到文本前面，
            // 例如“wxid_xxx我通过了你的朋友验证请求...”。识别时先去掉前缀，再按关键短语兜底。
            return normalized.Contains("通过了你的朋友验证请求", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("现在我们可以开始聊天了", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("我们可以开始聊天", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 归一化好友通过验证系统消息。
        /// <para>部分安卓上报内容会把 friendId 拼在正文前，导致日志看起来像“wxid_xxx我通过了...”。</para>
        /// </summary>
        private static string NormalizeFriendVerificationContent(string? content, string? friendId)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            return StripPrivateChatPrefix(content.Trim(), friendId);
        }

        /// <summary>
        /// 解析并保存高级消息展示元数据。
        /// <para>解析失败不影响原消息保存和推送，只记录警告，保证聊天主链路稳定。</para>
        /// </summary>
        private async Task SaveAdvancedMessageContentMetadataAsync(
            Message message,
            string? ext,
            string sourceNotice,
            int? originalMsgType = null)
        {
            try
            {
                var advanced = AdvancedMessageContentParser.Parse(
                    message.messageType,
                    message.content,
                    message.contentXml,
                    ext,
                    sourceNotice,
                    originalMsgType,
                    message.msgSvrId,
                    TryParseNullableLong(message.localMessageId));

                var saved = await _db.SaveAdvancedMessageContentMetadata(message, advanced);
                if (saved)
                {
                    _logger.LogDebug(
                        "高级消息结构化元数据已保存。MessageId={MessageId}, MsgSvrId={MsgSvrId}, SourceNotice={SourceNotice}, SemanticKind={SemanticKind}",
                        message.messageId,
                        message.msgSvrId,
                        sourceNotice,
                        advanced?.SemanticKind);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "高级消息结构化解析/落库失败，继续推送基础消息。MessageId={MessageId}, MsgSvrId={MsgSvrId}, SourceNotice={SourceNotice}",
                    message.messageId,
                    message.msgSvrId,
                    sourceNotice);
            }
        }

        private async Task PublishMessageReceivedAsync(IChannelHandlerContext context, Message message)
        {
            try
            {
                await _db.EnrichMessagesWithMediaMetadataAsync(new List<Message> { message });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "补齐消息媒体展示元数据失败，继续推送基础消息。MessageId={MessageId}, MsgSvrId={MsgSvrId}", message.messageId, message.msgSvrId);
            }

            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connManager.GetConnectionAsync(connId);
            if (connInfo != null)
            {
                await _eventBus.PublishAsync(new MessageReceivedEvent(connInfo.deviceUuid, message, connInfo.userId));
            }
        }

        /// <summary>
        /// 发布任务结果事件。
        /// <para>用于非 TaskMessageHandler 管辖的异步数据推送，例如群发历史同步完成。</para>
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

        /// <summary>
        /// 发布没有 TaskId 的异步通知。
        /// <para>CDNDownloadResultNotice 当前协议不携带 TaskId，但失败原因仍应透出到网页。</para>
        /// </summary>
        private async Task PublishTaskNotificationAsync(IChannelHandlerContext context, bool success, string message)
        {
            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connManager.GetConnectionAsync(connId);
            if (connInfo == null)
            {
                return;
            }

            await _eventBus.PublishAsync(new TaskResultReceivedEvent(0, success, message, connId, connInfo.deviceUuid));
        }

        /// <summary>
        /// 发布聊天 MsgSvrId 快照摘要。
        /// <para>
        /// ChatMsgIdsPushNotice(1050) 没有 TaskId 字段，因此这里使用 TaskId=0 作为纯通知，
        /// 只让前端看到本次快照数量与首尾 ID；不据此做删除、未读或会话状态对账。
        /// </para>
        /// </summary>
        private async Task PublishChatMsgIdsSnapshotResultAsync(
            IChannelHandlerContext context,
            ChatMsgIdsPushNoticeMessage msg,
            string firstId,
            string lastId)
        {
            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connManager.GetConnectionAsync(connId);
            if (connInfo == null)
            {
                return;
            }

            var summary = $"聊天消息 ID 快照同步完成：本次返回 {msg.Ids.Count} 条，WeChatId={msg.WeChatId}，Start={msg.StartTime}，End={msg.EndTime}，First={firstId}，Last={lastId}";
            await _eventBus.PublishAsync(new TaskResultReceivedEvent(0, true, summary, connId, connInfo.deviceUuid));
        }

        /// <summary>
        /// 实时群消息补齐会话后通知前端刷新群聊列表。
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
                "Publish ConversationsUpdatedEvent: Device={DeviceUuid}, Account={AccountId}, User={UserId}",
                connInfo.deviceUuid,
                accountId,
                connInfo.userId);
            await _eventBus.PublishAsync(new ConversationsUpdatedEvent(connInfo.deviceUuid, accountId, connInfo.userId));
        }

        private static string DecodeContent(ByteString content)
        {
            if (content == null || content.IsEmpty)
            {
                return string.Empty;
            }

            try
            {
                return content.ToStringUtf8();
            }
            catch
            {
                return Convert.ToBase64String(content.ToByteArray());
            }
        }

        private static long? TryParseNullableLong(string? value)
        {
            if (long.TryParse(value?.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static bool LooksLikeXml(string content)
        {
            return !string.IsNullOrWhiteSpace(content) && content.TrimStart().StartsWith("<", StringComparison.Ordinal);
        }

        /// <summary>
        /// 将微信原始 msgType 粗略映射为网页展示内容类型。
        /// <para>这里只处理确定的微信常见类型，未知类型保留文本，避免误改消息语义。</para>
        /// </summary>
        private static EnumContentType NormalizeOriginalContentType(int msgType)
        {
            return msgType switch
            {
                1 => EnumContentType.Text,
                3 => EnumContentType.Picture,
                34 => EnumContentType.Voice,
                42 => EnumContentType.NameCard,
                43 or 62 => EnumContentType.Video,
                47 => EnumContentType.Emoji,
                48 => EnumContentType.Location,
                49 => EnumContentType.Link,
                66 => EnumContentType.QiyeNameCard,
                67 => EnumContentType.KefuNameCard,
                1048625 => EnumContentType.Emoji,
                754974769 => EnumContentType.ShiPinHao,
                822083633 => EnumContentType.QuoteMsg,
                855638065 => EnumContentType.RoomLiving,
                973078577 => EnumContentType.FinderLive,
                64 or 10000 or 10002 or 268445456 or 268445458 => EnumContentType.System,
                1107296305 => EnumContentType.RoomManage,
                _ => EnumContentType.Text
            };
        }

        private static bool IsChatRoom(string wxid)
        {
            return !string.IsNullOrWhiteSpace(wxid) && wxid.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 拆分群发目标列表。
        /// </summary>
        private static List<string> SplitMassSendRecipients(string toList)
        {
            if (string.IsNullOrWhiteSpace(toList))
            {
                return new List<string>();
            }

            return toList
                .Split(new[] { ',', ';', '，', '；', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// 群发助手状态是否可视为成功。
        /// </summary>
        private static bool IsMassSendSuccessStatus(int status)
        {
            return status == 2 || status == 3 || status == 4;
        }

        /// <summary>
        /// 群发助手状态是否可视为失败。
        /// </summary>
        private static bool IsMassSendFailureStatus(int status)
        {
            return status < 0 || status == 5;
        }

        /// <summary>
        /// 生成与 DbHelper 相同口径的微信账号数值键。
        /// <para>MassMessages 历史表仍使用 long 类型账号键，这里保持与 DbHelper 内部 FNV-1a 口径一致。</para>
        /// </summary>
        private static long BuildStableNumericKey(string ownerWxid)
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

        private static short NormalizeSendStatus(int status)
        {
            // 微信本地 status=2/3/4 等都表示已进入发送链；服务端统一映射到“已发送”。
            return status >= 0 ? (short)2 : (short)5;
        }

        private static DateTime? NormalizeWeChatTimestamp(long timestamp)
        {
            if (timestamp <= 0)
            {
                return null;
            }

            try
            {
                // 微信库里常见两种格式：10 位秒级时间戳，或 13 位毫秒级时间戳。
                if (timestamp > 100000000000L)
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
                }

                return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
            }
            catch
            {
                return null;
            }
        }
    }
}

