using DotNetty.Transport.Channels;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SCRM.API.Services.Netty.Handlers;
using SCRM.Shared.Core;
using SCRM.SHARED.Models;
using System;
using System.Threading.Tasks;
using Jubo.JuLiao.IM.Wx.Proto;

namespace SCRM.Services.Netty
{
    /// <summary>
    /// Netty 消息路由服务
    /// 负责将不同类型的 TransportMessage 分发到对应的 Handler
    /// </summary>
    public class MessageRouter
    {
        private readonly ILogger<MessageRouter> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public MessageRouter(ILogger<MessageRouter> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task RouteMessage(TransportMessage message, IChannelHandlerContext context)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var sp = scope.ServiceProvider;
                var msgType = message.MsgType;

                switch (msgType)
                {
                    // === 鉴权与心跳 (AuthMessageHandler) ===
                    case EnumMsgType.DeviceAuthReq:
                        await sp.GetRequiredService<AuthMessageHandler>().HandleDeviceAuth(message, context);
                        break;
                    case EnumMsgType.HeartBeatReq:
                        await sp.GetRequiredService<AuthMessageHandler>().HandleHeartBeat(message, context);
                        break;

                    // === 任务结果 (TaskMessageHandler) ===
                    case EnumMsgType.TaskResultNotice:
                    case EnumMsgType.TalkToFriendTaskResultNotice:
                    case EnumMsgType.ScreenShotTaskResultNotice: // 1282
                    case (EnumMsgType)1073: // PostSNSNewsTaskResultNotice
                    case EnumMsgType.OneKeyLikeTaskResultNotice:
                    case EnumMsgType.QueryHbDetailTaskResultNotice: // Phase 4 addition
                    case EnumMsgType.QueryHbStatusTaskResultNotice: // Phase 4 addition
                        await sp.GetRequiredService<TaskMessageHandler>().HandleTaskResult(message, context);
                        break;

                    // === 系统消息 (SystemMessageHandler) ===
                    case EnumMsgType.WeChatOnlineNotice:
                    case EnumMsgType.WeChatOfflineNotice:
                    case EnumMsgType.ConfigPushNotice:
                    case EnumMsgType.PostDeviceInfoNotice:
                    case EnumMsgType.PostFriendDetectCountNotice:
                        var systemHandler = sp.GetService<SystemMessageHandler>();
                        if (systemHandler != null) 
                            await systemHandler.HandleMessage(message, context);
                        else 
                            _logger.LogWarning("Handler for {MsgType} not registered.", msgType);
                        break;

                    // === 聊天消息 (ChatMessageHandler) ===
                    case EnumMsgType.FriendTalkNotice:
                    case EnumMsgType.WeChatTalkToFriendNotice:
                    case EnumMsgType.PostMessageReadNotice:
                    case EnumMsgType.HistoryMsgPushNotice:
                    case EnumMsgType.ConversationPushNotice:
                        var chatHandler = sp.GetService<ChatMessageHandler>();
                         if (chatHandler != null)await chatHandler.HandleMessage(message, context);
                         else _logger.LogWarning("Handler for {MsgType} not registered.", msgType);
                        break;

                    // === 联系人消息 (ContactMessageHandler) ===
                    case EnumMsgType.FriendAddNotice:
                    case EnumMsgType.FriendDelNotice:
                    case EnumMsgType.FriendChangeNotice: // 1017
                    // case EnumMsgType.ContactPushNotice: // Missing in Proto
                    case EnumMsgType.BizContactPushNotice: // 2071
                        var contactHandler = sp.GetService<ContactMessageHandler>();
                        if (contactHandler != null) await contactHandler.HandleMessage(message, context);
                         else _logger.LogWarning("Handler for {MsgType} not registered.", msgType);
                        break;

                    // === 群组消息 (GroupMessageHandler) ===
                    // case EnumMsgType.ChatRoomPushNotice: // Missing in Proto
                    // case (EnumMsgType)1025: // ChatRoomMembersNotice (Potential Duplicate with ChatRoomChangeNotice or AddNotice)
                    case EnumMsgType.ChatRoomAddNotice:
                    case EnumMsgType.ChatRoomDelNotice:
                    // case EnumMsgType.ChatRoomChangeNotice: // Error: Definition missing
                        var groupHandler = sp.GetService<GroupMessageHandler>();
                         if (groupHandler != null) await groupHandler.HandleMessage(message, context);
                         else _logger.LogWarning("Handler for {MsgType} not registered.", msgType);
                        break;
                    
                    /*
                    case (EnumMsgType)1025: // Duplicate
                         groupHandler = sp.GetService<GroupMessageHandler>();
                         if (groupHandler != null) await groupHandler.HandleMessage(message, context);
                         break;
                    */

                    // === 朋友圈消息 (MomentsMessageHandler) ===
                    case EnumMsgType.CirclePushNotice: // 1070
                    case EnumMsgType.CircleDetailNotice: // 1071
                    case EnumMsgType.CircleNewPublishNotice: // 1076
                        var momentsHandler = sp.GetService<MomentsMessageHandler>();
                         if (momentsHandler != null) await momentsHandler.HandleMessage(message, context);
                         else _logger.LogWarning("Handler for {MsgType} not registered.", msgType);
                        break;

                    // === 其他/默认 ===
                    default:
                        _logger.LogWarning("Unknown or unhandled MsgType: {MsgType} ({Id})", msgType, (int)msgType);
                        break;
                }
            }
        }
    }
}
