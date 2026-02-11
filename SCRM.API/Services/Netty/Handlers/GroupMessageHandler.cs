using DotNetty.Transport.Channels;
using Google.Protobuf;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Services.Netty.Handlers.Abstractions;
using SCRM.Services.Data;
using SCRM.Services.Events;
using SCRM.API.Services.Core;
using System;
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
                    case EnumMsgType.ChatRoomMembersNotice: // 2034
                        await HandleChatRoomMembers(message.Content.Unpack<ChatRoomMembersNoticeMessage>(), context);
                        break;
                    case EnumMsgType.ChatRoomAddNotice:
                        // Pending Implementation
                        _logger.LogInformation("Received ChatRoomAddNotice. Pending implementation.");
                        break;
                    case EnumMsgType.ChatRoomDelNotice:
                        // Pending Implementation
                        _logger.LogInformation("Received ChatRoomDelNotice. Pending implementation.");
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

        private async Task HandleChatRoomMembers(ChatRoomMembersNoticeMessage msg, IChannelHandlerContext context)
        {
             if (string.IsNullOrEmpty(msg.WeChatId)) return;
             
             // Check Account
             var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
             if (account == null) return;

             // ChatRoom Members List? usually msg.Members
             // Proto: ChatRoomMembersNoticeMessage has WeChatId (which is the RoomID here) and Members.
             
             var chatRoomId = msg.WeChatId; // [Fix] Use WeChatId as RoomID per Proto definition
             if (string.IsNullOrEmpty(chatRoomId)) return;

             int count = msg.Members != null ? msg.Members.Count : 0;
             _logger.LogInformation("Received ChatRoom Members for {RoomId}: {Count} members", chatRoomId, count);

             // Logic to update DB:
             // 1. Find ChatRoom entity? (If exists)
             // 2. Iterate Members and sync to ChatRoomMembers table.
             // For MVP/Silence Error Logs: Just verify structure and log.
             
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
    }
}
