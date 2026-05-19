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
                    case EnumMsgType.PhoneStateWarningNotice:
                        await sp.GetRequiredService<AuthMessageHandler>().HandlePhoneStateWarning(message, context);
                        break;
                    case EnumMsgType.PostDeviceInfoNotice:
                        await sp.GetRequiredService<AuthMessageHandler>().HandlePostDeviceInfo(message, context);
                        break;

                    // === 任务结果 (TaskMessageHandler) ===
                    case EnumMsgType.TaskResultNotice:
                    case EnumMsgType.TalkToFriendTaskResultNotice:
                    case EnumMsgType.ScreenShotTaskResultNotice: // 1283
                    case (EnumMsgType)1073: // PostSNSNewsTaskResultNotice
                    case EnumMsgType.OneKeyLikeTaskResultNotice:
                    case EnumMsgType.CircleCommentDeleteTaskResultNotice:
                    case EnumMsgType.CircleCommentReplyTaskResultNotice:
                    case EnumMsgType.PullChatRoomQrCodeTaskResultNotice:
                    case EnumMsgType.PullWeChatQrCodeTaskResultNotice:
                    case EnumMsgType.GetPoiListTaskResultNotice:
                    case EnumMsgType.PullEmojiInfoTaskResultNotice:
                    case EnumMsgType.TakeMoneyTaskResultNotice:
                    case EnumMsgType.FindContactTaskResult:
                    case EnumMsgType.WeChatLocationTaskResultNotice:
                    case EnumMsgType.WalletBalanceTaskResultNotice:
                    case EnumMsgType.PhoneStateTaskResultNotice:
                    case EnumMsgType.QueryHbDetailTaskResultNotice: // Phase 4 addition
                    case EnumMsgType.QueryHbStatusTaskResultNotice: // Phase 4 addition
                    case EnumMsgType.SphPostTaskResultNotice:
                    case EnumMsgType.ContactLabelInfoNotice: // 2032，标签列表异步推送
                    case EnumMsgType.ContactLabelAddNotice: // 1038，标签新增/重命名通知
                    case EnumMsgType.ContactLabelDelNotice: // 1044，标签删除通知
                        await sp.GetRequiredService<TaskMessageHandler>().HandleTaskResult(message, context);
                        break;

                    // === 系统消息 (SystemMessageHandler) ===
                    case EnumMsgType.WeChatOnlineNotice:
                    case EnumMsgType.WeChatLoginNotice:
                    case EnumMsgType.WeChatOfflineNotice:
                    case EnumMsgType.AccountLogoutNotice:
                    case EnumMsgType.GetWeChatsRsp:
                    case EnumMsgType.ConfigPushNotice: // 62203 标准配置快照上报
                    case EnumMsgType.SetConfigTask: // 客户端上报配置信息的通道
                    // case EnumMsgType.PostDeviceInfoNotice: // Moved to AuthMessageHandler
                    case EnumMsgType.FriendDetectResultNotice:
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
                    case EnumMsgType.ChatMsgIdsPushNotice:
                    case EnumMsgType.ChatMsgFilePushNotice:
                    case EnumMsgType.CdndownloadResultNotice: // 1271，proto 原名 CDNDownloadResultNotice，C# 生成名会规整为 CdndownloadResultNotice
                    case EnumMsgType.MsgDelNotice:
                    case EnumMsgType.ConvDelNotice: // [Fix] 1055
                    case EnumMsgType.RequestTalkMsgTaskResultNotice:
                    case EnumMsgType.RequestTalkContentTaskResultNotice:
                    case EnumMsgType.RequestTalkDetailTaskResultNotice:
                    case EnumMsgType.UnreadListPushNotice:
                    case EnumMsgType.BizConversPushNotice:
                    case EnumMsgType.QwConversPushNotice:
                    case EnumMsgType.GroupSendHistoryPushNotice:
                        var chatHandler = sp.GetService<ChatMessageHandler>();
                         if (chatHandler != null)await chatHandler.HandleMessage(message, context);
                         else _logger.LogWarning("Handler for {MsgType} not registered.", msgType);
                        break;

                    // === 联系人消息 (ContactMessageHandler) ===
                    case EnumMsgType.FriendAddNotice:
                    case EnumMsgType.AddFriendNotice: // 1264，手机端主动向别人发起加好友请求，不代表已成为好友
                    case EnumMsgType.FriendAddReqeustNotice: // 1027，好友添加请求通知（proto 原拼写为 Reqeust）
                    case EnumMsgType.FriendAddReqListNotice:
                    case EnumMsgType.FriendDelNotice:
                    case EnumMsgType.FriendChangeNotice: // 1017
                    case EnumMsgType.FriendPushNotice: // 2026，好友列表分页/全量推送
                    case EnumMsgType.SyncFriendListAsyncRsp: // 3057，好友同步请求 ACK；主数据仍走 FriendPushNotice
                    case EnumMsgType.ContactInfoNotice: // 1278，单个联系人资料回包
                    // case EnumMsgType.ContactPushNotice: // Missing in Proto
                    case EnumMsgType.BizContactPushNotice: // 2071
                    case EnumMsgType.BizContactAddNotice: // [Fix] 2038
                    case EnumMsgType.QwUserPushNotice: // 1286，proto 原名为 QwUserPUshNotice，C# 生成名会规整为 QwUserPushNotice
                        var contactHandler = sp.GetService<ContactMessageHandler>();
                        if (contactHandler != null) await contactHandler.HandleMessage(message, context);
                         else _logger.LogWarning("Handler for {MsgType} not registered.", msgType);
                        break;

                    // === 群组消息 (GroupMessageHandler) ===
                    case EnumMsgType.ChatroomPushNotice:
                    // case (EnumMsgType)1025: // ChatRoomMembersNotice (Potential Duplicate with ChatRoomChangeNotice or AddNotice)
                    case EnumMsgType.ChatRoomAddNotice:
                    case EnumMsgType.ChatRoomDelNotice:
                    case EnumMsgType.ChatRoomChangedNotice:
                    case EnumMsgType.ChatRoomMembersNotice: // [Fix] 2034
                    case EnumMsgType.ChatRoomInvitePushNotice:
                    case EnumMsgType.ChatRoomInviteListNotice:
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
                    case EnumMsgType.CircleMsgPushNotice:
                    case EnumMsgType.CircleLikeNotice:
                    case EnumMsgType.CircleCommentNotice:
                    case EnumMsgType.CircleDelNotice:
                    case EnumMsgType.SphMentionListNotice:
                    case EnumMsgType.SphUserPagePushNotice:
                    case EnumMsgType.SphCommentListNotice:
                        var momentsHandler = sp.GetService<MomentsMessageHandler>();
                         if (momentsHandler != null) await momentsHandler.HandleMessage(message, context);
                         else _logger.LogWarning("Handler for {MsgType} not registered.", msgType);
                        break;

                    // === 手机短信与通话记录 (PhoneMessageHandler) ===
                    case EnumMsgType.CallLogPushNotice:
                    case EnumMsgType.SmsPushNotice:
                    case EnumMsgType.SmsReadNotice:
                    case EnumMsgType.SmsSentNotice:
                    case EnumMsgType.PullSmsTaskResultNotice:
                    case EnumMsgType.PullCallLogTaskResultNotice:
                        var phoneHandler = sp.GetService<PhoneMessageHandler>();
                        if (phoneHandler != null) await phoneHandler.HandleMessage(message, context);
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
