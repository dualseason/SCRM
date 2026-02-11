using DotNetty.Transport.Channels;
using Google.Protobuf;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models.Events;
using SCRM.API.Services.Core;
using SCRM.API.Services.Netty.Handlers.Abstractions;
using SCRM.Services.Data;
using SCRM.Services.Events;
using System;
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

        public ChatMessageHandler(ILogger<ChatMessageHandler> logger, ApplicationDbContext db, ConnectionManager connManager, IEventBus eventBus) : base(logger)
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
                    case EnumMsgType.FriendTalkNotice:
                        await HandleFriendTalk(message.Content.Unpack<FriendTalkNoticeMessage>(), context);
                        break;
                    case EnumMsgType.WeChatTalkToFriendNotice:
                        await HandleWeChatTalkToFriend(message.Content.Unpack<WeChatTalkToFriendNoticeMessage>(), context);
                        break;
                    case EnumMsgType.ConvDelNotice: // [Fix] 1055
                        await HandleConvDel(message.Content.Unpack<ConvDelNoticeMessage>(), context);
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

        // [Fix] Handle Conversation Deletion
        private async Task HandleConvDel(ConvDelNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrEmpty(msg.WeChatId)) return;
            // Depending on Proto: msg.FriendId might be the deleted chat target
            // Logic: Mark messages or conversation as deleted?
            // For now, logging + Ack is sufficient to stop errors.
            var target = msg.FriendId ?? "Unknown";
            _logger.LogInformation("Conversation Deleted: {Wxid} deleted chat with {Target}", msg.WeChatId, target);
            
            // Future: await _db.Conversations.FirstOrDefaultAsync(...).Delete();
            await Task.CompletedTask;
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

            // Create Message Entity (Direction: Receive = 2)
            var message = new Message
            {
                accountId = account.wxid,
                msgSvrId = msg.MsgSvrId,
                senderWxid = msg.FriendId,
                receiverWxid = msg.WeChatId,
                chatType = 1, // Single Chat
                messageType = (short)msg.ContentType, 
                content = msg.Content?.ToStringUtf8() ?? "",
                direction = 2, // Receive
                sendStatus = 3, // Delivered/Received
                readStatus = 0, // Unread
                sentAt = DateTime.UtcNow,
                receivedAt = DateTime.UtcNow,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow,
                isDeleted = false,
                isRevoked = false
            };

            // Link Sender/Receiver accounts if they exist in system?
            // Optional optimization: message.senderId = ...
            
            _db.Messages.Add(message);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Received Chat from {Friend} to {Me}, Content: {Content}", msg.FriendId, msg.WeChatId, message.content);

            // Publish Event
            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connManager.GetConnectionAsync(connId);
            if (connInfo != null)
            {
                await _eventBus.PublishAsync(new MessageReceivedEvent(connInfo.deviceUuid, message, connInfo.userId));
            }
        }

        private async Task HandleWeChatTalkToFriend(WeChatTalkToFriendNoticeMessage msg, IChannelHandlerContext context)
        {
             if (string.IsNullOrEmpty(msg.WeChatId)) return;

             var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
             if (account == null) return;

             // Create Message Entity (Direction: Send = 1)
             // This notifies us that the phone sent a message successfully (or is trying to)
             // Since it's a Notice from client, it usually means "I sent this"
             var message = new Message
             {
                 accountId = account.wxid,
                 msgSvrId = msg.MsgSvrId,
                 senderWxid = msg.WeChatId,
                 receiverWxid = msg.FriendId,
                 chatType = 1, // Single Chat
                 messageType = (short)msg.ContentType,
                 content = msg.Content?.ToStringUtf8() ?? "",
                 direction = 1, // Send
                 sendStatus = 2, // Sent
                 readStatus = 1, // Read by self obviously
                 sentAt = DateTime.UtcNow,
                 createdAt = DateTime.UtcNow,
                 updatedAt = DateTime.UtcNow,
                 isDeleted = false,
                 isRevoked = false
             };

             _db.Messages.Add(message);
             await _db.SaveChangesAsync();
             
             _logger.LogInformation("Synced Sent Chat from {Me} to {Friend}", msg.WeChatId, msg.FriendId);

             // Optionally notify UI to append self-message if not already done by optimistic UI
             // _eventBus.PublishAsync(new MessageSentEvent(...)); 
             // Reuse MessageReceivedEvent to force UI refresh? or just let UI pull?
             // Usually for self-sent, UI might already have it if sent via UI. 
             // But if sent via Phone, UI needs to know.
             var connId = context.Channel.Id.AsLongText();
             var connInfo = await _connManager.GetConnectionAsync(connId);
             if (connInfo != null)
             {
                 // We reuse MessageReceivedEvent or create a new sync event. 
                 // For now, assume UI handles "message" object regardless of direction
                 await _eventBus.PublishAsync(new MessageReceivedEvent(connInfo.deviceUuid, message, connInfo.userId));
             }
        }
    }
}
