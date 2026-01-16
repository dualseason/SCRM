using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Google.Protobuf;
using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using SCRM.API.Hubs;
using SCRM.Services;
using SCRM.Services.Data;
using SCRM.API.Services.Data;
using SCRM.Services.Events;
using SCRM.API.Models.Events;
using SCRM.API.Models.Entities;
using System.Collections.Generic;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 聊天消息处理器
    /// 负责处理好友聊天、消息发送结果、历史记录同步等业务
    /// Scoped Service
    /// </summary>
    public class ChatMessageHandler
    {
        private readonly ILogger<ChatMessageHandler> _logger;
        private readonly ConnectionManager _connectionManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHubContext<ClientHub> _hubContext;
        private readonly IEventBus _eventBus;

        public ChatMessageHandler(
            ILogger<ChatMessageHandler> logger,
            ConnectionManager connectionManager,
            ApplicationDbContext dbContext,
            IHubContext<ClientHub> hubContext,
            IEventBus eventBus)
        {
            _logger = logger;
            _connectionManager = connectionManager;
            _dbContext = dbContext;
            _hubContext = hubContext;
            _eventBus = eventBus;
        }

        /// <summary>
        /// 处理好友发来聊天消息通知 (3.5)
        /// 1. 解析消息内容
        /// 2. 分发 MessageReceivedEvent，供前端显示
        /// 3. 分发 AutomationMessageEvent
        /// 4. 将消息持久化到数据库
        /// </summary>
        public async Task HandleFriendTalkNotice(TransportMessage message, IChannelHandlerContext context)
        {
            FriendTalkNoticeMessage notice = message.Content.Unpack<FriendTalkNoticeMessage>();
            string contentUtf8 = notice.Content.ToStringUtf8();
            
            _logger.LogInformation("好友聊天通知：{WeChatId} 收到来自 {FriendId} 的消息: {Content}", 
                notice.WeChatId, notice.FriendId, contentUtf8);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo == null) return;

            // 判定是否为“我”发送的消息 (同步消息)
            bool isSelf = notice.FriendId == notice.WeChatId || contentUtf8.Contains("<is_self>1</is_self>");
            
            // 判定是否为群聊
            bool isGroup = notice.FriendId.EndsWith("@chatroom");
            
            // 方向判定：1=发送, 2=接收
            int direction = isSelf ? 1 : 2;
            
            if (!long.TryParse(connectionInfo.userId, out long acctId)) return;

            // --- 消息去重 & 持久化 ---
            // 1. 基于 MsgSvrId 的幂等检查
            var existingMsg = await _dbContext.Messages.FirstOrDefaultAsync(m => m.msgSvrId == notice.MsgSvrId && m.accountId == acctId);
            
            if (existingMsg == null)
            {
                // 1.5 确保会话存在
                var conversation = await _dbContext.Conversations
                    .FirstOrDefaultAsync(c => c.wechatAccountId == acctId && c.conversationWxid == notice.FriendId);

                if (conversation == null)
                {
                    conversation = new Conversation
                    {
                        wechatAccountId = (int)acctId,
                        conversationWxid = notice.FriendId,
                        conversationType = isGroup ? 2 : 1,
                        displayName = notice.FriendId, 
                        displayAvatar = "",
                        unreadCount = 0,
                        messageCount = 0,
                        isPinned = 0,
                        isMuted = 0,
                        lastMessageTime = DateTime.UtcNow,
                        createdAt = DateTime.UtcNow,
                        updatedAt = DateTime.UtcNow,
                        isDeleted = false
                    };
                    _dbContext.Conversations.Add(conversation);
                    await _dbContext.SaveChangesAsync(); 
                }

                var msg = new Message
                {
                    conversationId = conversation.id,
                    accountId = (int)acctId,
                    senderWxid = isSelf ? notice.WeChatId : notice.FriendId,
                    receiverWxid = isSelf ? notice.FriendId : notice.WeChatId,
                    content = contentUtf8,
                    chatType = (short)(isGroup ? 2 : 1), 
                    messageType = (short)notice.ContentType,
                    direction = (short)direction,
                    sendStatus = 3, // 已送达
                    readStatus = isSelf ? (short)1 : (short)0,
                    msgSvrId = notice.MsgSvrId,
                    sentAt = DateTime.UtcNow,
                    receivedAt = DateTime.UtcNow,
                    createdAt = DateTime.UtcNow,
                    updatedAt = DateTime.UtcNow
                };
                _dbContext.Messages.Add(msg);
                await _dbContext.SaveChangesAsync();
            }
            else
            {
                    _logger.LogDebug("检测到重复消息，跳过保存。MsgSvrId: {MsgSvrId}", notice.MsgSvrId);
            }

            // --- 发送给 UI 前端 (SignalR) ---
            var deviceInfo = connectionInfo.deviceInfo ?? connectionId;
            var msgDto = new 
            { 
                FriendId = notice.FriendId, // 会话窗口 ID
                Content = contentUtf8, 
                IsSelf = isSelf,
                MsgSvrId = notice.MsgSvrId,
                IsGroup = isGroup,
                TaskId = notice.MsgId
            };
            
            await _hubContext.Clients.Group(deviceInfo).SendAsync("ReceiveMessage", msgDto);
            
            // --- 自动化逻辑转发 ---
            try 
            {
                    var automationEvent = new AutomationMessageEvent(
                        acctId, 
                        connectionId, 
                        notice.WeChatId, 
                        notice.FriendId, 
                        contentUtf8, 
                        (int)notice.ContentType
                    );
                    await _eventBus.PublishAsync(automationEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发布自动化事件出错");
            }

            // 发送 ACK
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理手机上回复好友的聊天消息通知 (3.6)
        /// 1. 推送到 SignalR 前端
        /// 2. 持久化到数据库 (Direction = 1 发送)
        /// </summary>
        public async Task HandleWeChatTalkToFriendNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatTalkToFriendNoticeMessage>();
            _logger.LogInformation("微信回复好友通知：{WeChatId} 发送消息给 {FriendId}: {Content}", 
                notice.WeChatId, notice.FriendId, notice.Content.ToStringUtf8());

            // 推送到 SignalR 组
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo != null)
            {
                await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("ReceiveMessage", notice.FriendId, notice.Content.ToStringUtf8(), true); 
            } 

            // 持久化到数据库
            if (connectionInfo != null && long.TryParse(connectionInfo.userId, out long accountId))
            {
                var msg = new Message
                {
                    accountId = (int)accountId,
                    senderWxid = notice.WeChatId, 
                    receiverWxid = notice.FriendId,
                    content = notice.Content.ToStringUtf8(),
                    chatType = 1, // 默认为单聊，如需严谨需查表
                    messageType = (short)notice.ContentType,
                    direction = 1, // 发送
                    sendStatus = 2, // 已发送
                    readStatus = 1, // 已读
                    msgSvrId = notice.MsgId,
                    sentAt = DateTime.UtcNow,
                    createdAt = DateTime.UtcNow,
                    updatedAt = DateTime.UtcNow
                };
                _dbContext.Messages.Add(msg);
                await _dbContext.SaveChangesAsync();
            }
            
            // 发送 ACK
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理消息已读通知 (4.3)
        /// </summary>
        public async Task HandlePostMessageReadNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<PostMessageReadNoticeMessage>();
            _logger.LogInformation("消息已读通知：{WeChatId} 在会话 {FriendId} 中已读", 
                notice.WeChatId, notice.FriendId);
            
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理历史聊天记录推送 (4.20 结果)
        /// </summary>
        public async Task HandleHistoryMsgPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<HistoryMsgPushNoticeMessage>();
            _logger.LogInformation("收到历史消息推送: WeChatId={WeChatId}, MsgCount={MsgCount}", notice.WeChatId, notice.Messages.Count);
            // 暂只记录日志，后续可实现批量入库
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理会话列表推送 (4.47 结果)
        /// </summary>
        public async Task HandleConversationPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ConversationPushNoticeMessage>();
            // 'Convers' aligned with proto
            _logger.LogInformation("收到会话列表推送: WeChatId={WeChatId}, ConversCount={ConversCount}", notice.WeChatId, notice.Convers.Count);
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 从 XML 中提取特定标签的值
        /// 辅助方法
        /// </summary>
        private string ExtractXmlValue(string xml, string tagName)
        {
            try {
                string startTag = $"<{tagName}>";
                string endTag = $"</{tagName}>";
                int start = xml.IndexOf(startTag);
                if (start == -1) 
                {
                    startTag = $"<{tagName}><![CDATA[";
                    endTag = $"]]></{tagName}>";
                    start = xml.IndexOf(startTag);
                }

                if (start != -1)
                {
                    start += startTag.Length;
                    int end = xml.IndexOf(endTag, start);
                    if (end != -1)
                    {
                        return xml.Substring(start, end - start);
                    }
                }
            } catch {}
            return string.Empty;
        }

        private async Task SendAckAsync(TransportMessage message, IChannelHandlerContext context)
        {
             var response = new TransportMessage
             {
                 Id = 0,
                 MsgType = EnumMsgType.MsgReceivedAck,
                 RefMessageId = message.Id
             };
             
             await context.WriteAndFlushAsync(response);
        }
    }
}
