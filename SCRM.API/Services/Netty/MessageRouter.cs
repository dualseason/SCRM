using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using DotNetty.Transport.Channels;
using Google.Protobuf;
using Jubo.JuLiao.IM.Wx.Proto;
using SCRM.SHARED.Models;
using SCRM.API.Services.Netty.Handlers;

namespace SCRM.Services.Netty
{
    /// <summary>
    /// 消息路由服务
    /// 负责接收从 NettyMessageHandler 传来的 TransportMessage，并将其分发到对应的 Handler 服务。
    /// 本类只负责路由，不包含具体业务逻辑。
    /// </summary>
    public class MessageRouter
    {
        private readonly ILogger<MessageRouter> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public MessageRouter(IServiceScopeFactory scopeFactory, ILogger<MessageRouter> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// 路由消息到具体的 Handler
        /// </summary>
        public async Task RouteMessage(TransportMessage message, IChannelHandlerContext context)
        {
            // 基础日志记录
            _logger.LogDebug("路由消息：Id={Id}, MsgType={MsgType} ({MsgTypeId}), Token={Token}", 
                message.Id, message.MsgType, (int)message.MsgType, message.AccessToken);

            if (message.Content != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("消息内容 TypeUrl: {TypeUrl}", message.Content.TypeUrl);
            }

            try
            {
                // 创建 Scoped 作用域，确保 Handler 中使用的 DbContext 等资源能正确释放
                using (var scope = _scopeFactory.CreateScope())
                {
                    var sp = scope.ServiceProvider;

                    // 根据消息类型分发到不同的 Handler
                    switch (message.MsgType)
                    {
                        // --- 认证与心跳 (AuthMessageHandler) ---
                        case EnumMsgType.HeartBeatReq: // 1001
                            await sp.GetRequiredService<AuthMessageHandler>().HandleHeartBeat(message, context);
                            break;
                        case EnumMsgType.DeviceAuthReq: // 1000
                            await sp.GetRequiredService<AuthMessageHandler>().HandleDeviceAuth(message, context);
                            break;

                        // --- 系统级消息 (SystemMessageHandler) ---
                        case EnumMsgType.WeChatOnlineNotice: // 300
                            await sp.GetRequiredService<SystemMessageHandler>().HandleWeChatOnline(message, context);
                            break;
                        case EnumMsgType.WeChatOfflineNotice: // 301
                            await sp.GetRequiredService<SystemMessageHandler>().HandleWeChatOffline(message, context);
                            break;
                        case EnumMsgType.PostDeviceInfoNotice: // 2027
                            await sp.GetRequiredService<SystemMessageHandler>().HandlePostDeviceInfoNotice(message, context);
                            break;
                        case EnumMsgType.ConfigPushNotice: // 推送配置
                             await sp.GetRequiredService<SystemMessageHandler>().HandleConfigPushNotice(message, context);
                            break;
                        case EnumMsgType.PostFriendDetectCountNotice: // 4.6
                             await sp.GetRequiredService<SystemMessageHandler>().HandlePostFriendDetectCountNotice(message, context);
                            break;

                        // --- 聊天消息 (ChatMessageHandler) ---
                        case EnumMsgType.FriendTalkNotice: // 3.5
                            await sp.GetRequiredService<ChatMessageHandler>().HandleFriendTalkNotice(message, context);
                            break;
                        case EnumMsgType.WeChatTalkToFriendNotice: // 3.6
                            await sp.GetRequiredService<ChatMessageHandler>().HandleWeChatTalkToFriendNotice(message, context);
                            break;
                        case EnumMsgType.PostMessageReadNotice: // 4.3
                            await sp.GetRequiredService<ChatMessageHandler>().HandlePostMessageReadNotice(message, context);
                            break;
                        case EnumMsgType.HistoryMsgPushNotice: // 4.20
                            await sp.GetRequiredService<ChatMessageHandler>().HandleHistoryMsgPushNotice(message, context);
                            break;
                        case EnumMsgType.ConversationPushNotice: // 4.47
                            await sp.GetRequiredService<ChatMessageHandler>().HandleConversationPushNotice(message, context);
                            break;

                        // --- 联系人 (ContactMessageHandler) ---
                        case EnumMsgType.FriendAddNotice: // 3.3
                            await sp.GetRequiredService<ContactMessageHandler>().HandleFriendAddNotice(message, context);
                            break;
                        case EnumMsgType.FriendChangeNotice: // 1052
                            await sp.GetRequiredService<ContactMessageHandler>().HandleFriendChangeNotice(message, context);
                            break;
                        case EnumMsgType.FriendDelNotice: // 3.4
                            await sp.GetRequiredService<ContactMessageHandler>().HandleFriendDelNotice(message, context);
                            break;
                        case EnumMsgType.FriendPushNotice: // 3.1
                            await sp.GetRequiredService<ContactMessageHandler>().HandleFriendPushNotice(message, context);
                            break;
                        case EnumMsgType.FriendAddReqeustNotice: // 3.7
                            await sp.GetRequiredService<ContactMessageHandler>().HandleFriendAddReqeustNotice(message, context);
                            break;
                        case EnumMsgType.FriendAddReqListNotice: // 4.49
                            await sp.GetRequiredService<ContactMessageHandler>().HandleFriendAddReqListNotice(message, context);
                            break;
                        case EnumMsgType.ContactLabelInfoNotice: // 3.16
                            await sp.GetRequiredService<ContactMessageHandler>().HandleContactLabelInfoNotice(message, context);
                            break;
                        case EnumMsgType.BizContactPushNotice: // 4.50
                            await sp.GetRequiredService<ContactMessageHandler>().HandleBizContactPushNotice(message, context);
                            break;
                        case EnumMsgType.BizContactAddNotice: // 3.18
                            await sp.GetRequiredService<ContactMessageHandler>().HandleBizContactAddNotice(message, context);
                            break;

                        // --- 群组 (GroupMessageHandler) ---
                        case EnumMsgType.ChatroomPushNotice: // 3.23
                            await sp.GetRequiredService<GroupMessageHandler>().HandleChatRoomPushNotice(message, context);
                            break;
                        case EnumMsgType.ChatRoomMembersNotice: // 3.9
                            await sp.GetRequiredService<GroupMessageHandler>().HandleChatRoomMembersNotice(message, context);
                            break;

                        // --- 朋友圈 (MomentsMessageHandler) ---
                        case EnumMsgType.CirclePushNotice: // 3.13
                            await sp.GetRequiredService<MomentsMessageHandler>().HandleCirclePushNotice(message, context);
                            break;
                        case EnumMsgType.CircleDetailNotice: // 3.26
                            await sp.GetRequiredService<MomentsMessageHandler>().HandleCircleDetailNotice(message, context);
                            break;
                        case EnumMsgType.CircleNewPublishNotice: // 3.13 监听
                            await sp.GetRequiredService<MomentsMessageHandler>().HandleCircleNewPublishNotice(message, context);
                            break;
                        case (EnumMsgType)1073: // PostSNSNewsTaskResultNotice
                            await sp.GetRequiredService<MomentsMessageHandler>().HandlePostSNSNewsTaskResultNotice(message, context);
                            break;
                        case EnumMsgType.OneKeyLikeTaskResultNotice: // 4.22
                            await sp.GetRequiredService<MomentsMessageHandler>().HandleOneKeyLikeTaskResultNotice(message, context);
                            break;

                        // --- 任务结果 (TaskMessageHandler) ---
                        case EnumMsgType.TalkToFriendTaskResultNotice: // 4.1
                            await sp.GetRequiredService<TaskMessageHandler>().HandleTalkToFriendTaskResult(message, context);
                            break;
                        case EnumMsgType.ScreenShotTaskResultNotice: // 4.8
                            await sp.GetRequiredService<TaskMessageHandler>().HandleScreenShotTaskResultNotice(message, context);
                            break;
                        case EnumMsgType.TaskResultNotice: // 4.9
                            await sp.GetRequiredService<TaskMessageHandler>().HandleTaskResultNotice(message, context);
                            break;
                        case EnumMsgType.QueryHbDetailTaskResultNotice: // 4.25
                            await sp.GetRequiredService<TaskMessageHandler>().HandleQueryHbDetailTaskResultNotice(message, context);
                            break;
                        case EnumMsgType.QueryHbStatusTaskResultNotice: // 4.25 Status
                            await sp.GetRequiredService<TaskMessageHandler>().HandleQueryHbStatusTaskResultNotice(message, context);
                            break;

                        case EnumMsgType.UnknownMsg:
                        default:
                            if (_logger.IsEnabled(LogLevel.Trace))
                            {
                                _logger.LogTrace("忽略未处理的消息类型: {MsgType}", message.MsgType);
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理消息异常，Id={Id}, Type={MsgType}", message.Id, message.MsgType);
            }
        }
    }
}