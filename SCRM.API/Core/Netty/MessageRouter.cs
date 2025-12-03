using SCRM.SHARED.Proto;
using System;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Google.Protobuf;
using SCRM.Shared.Core;
using SCRM.Services;
using Google.Protobuf.WellKnownTypes;

namespace SCRM.Core.Netty
{
    public class MessageRouter
    {
        private readonly Serilog.ILogger _logger = Utility.logger;
        private readonly ConnectionManager _connectionManager;

        public MessageRouter(ConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        public async Task RouteMessage(TransportMessage message, IChannelHandlerContext context)
        {
            _logger.Information("Routing message: Id={Id}, Type={MsgType}, Token={Token}", 
                message.Id, message.MsgType, message.AccessToken);

            try
            {
                switch (message.MsgType)
                {
                    case EnumMsgType.HeartBeatReq:
                        await HandleHeartBeat(message, context);
                        break;
                    case EnumMsgType.DeviceAuthReq:
                        await HandleDeviceAuth(message, context);
                        break;
                    case EnumMsgType.TalkToFriendTaskResultNotice:
                        await HandleTalkToFriendTaskResult(message, context);
                        break;
                    case EnumMsgType.WeChatOnlineNotice:
                        await HandleWeChatOnline(message, context);
                        break;
                    case EnumMsgType.WeChatOfflineNotice:
                        await HandleWeChatOffline(message, context);
                        break;
                    case EnumMsgType.FriendTalkNotice:
                        await HandleFriendTalkNotice(message, context);
                        break;
                    case EnumMsgType.WeChatTalkToFriendNotice:
                        await HandleWeChatTalkToFriendNotice(message, context);
                        break;
                    case EnumMsgType.FriendAddNotice:
                        await HandleFriendAddNotice(message, context);
                        break;
                    case EnumMsgType.ChatroomPushNotice:
                        await HandleChatRoomPushNotice(message, context);
                        break;
                    case EnumMsgType.CircleDetailNotice:
                        await HandleCircleDetailNotice(message, context);
                        break;
                    case EnumMsgType.PostSnsnewsTaskResultNotice:
                        await HandlePostSNSNewsTaskResultNotice(message, context);
                        break;
                    case EnumMsgType.TaskResultNotice:
                        await HandleTaskResultNotice(message, context);
                        break;
                    case EnumMsgType.ScreenShotTaskResultNotice:
                        await HandleScreenShotTaskResultNotice(message, context);
                        break;
                    default:
                        _logger.Warning("Unhandled message type: {MsgType}", message.MsgType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error routing message type: {MsgType}", message.MsgType);
            }
        }

        private async Task HandleHeartBeat(TransportMessage message, IChannelHandlerContext context)
        {
            _logger.Debug("HeartBeat received from {RemoteAddress}", context.Channel.RemoteAddress);
            
            // 更新连接活跃时间
            await _connectionManager.UpdateConnectionActivityAsync(context.Channel.Id.AsLongText());

            var response = new TransportMessage
            {
                Id = message.Id,
                MsgType = EnumMsgType.MsgReceivedAck,
                RefMessageId = message.Id
            };
            
            await context.WriteAndFlushAsync(response);
        }

        private async Task HandleDeviceAuth(TransportMessage message, IChannelHandlerContext context)
        {
            _logger.Information("Device auth request received");
            
            var authReq = message.Content.Unpack<DeviceAuthReqMessage>();
            // TODO: 进行实际的认证逻辑，这里暂时允许所有
            
            // 注册连接信息
            string userId = "admin"; // 示例用户ID
            string deviceType = "Android"; // 示例设备类型
            string deviceInfo = authReq.Credential;
            
            await _connectionManager.AddConnectionAsync(userId, context.Channel.Id.AsLongText(), deviceType, deviceInfo);

            var responseContent = new DeviceAuthRspMessage
            {
                AccessToken = Guid.NewGuid().ToString("N"),
                Extra = new DeviceAuthRspMessage.Types.ExtraMessage
                {
                    SupplierId = 1,
                    UnionId = 1,
                    AccountType = EnumAccountType.Main,
                    SupplierName = "SCRM",
                    NickName = "Admin",
                    Token = "Token123"
                }
            };

            var response = new TransportMessage
            {
                Id = message.Id,
                MsgType = EnumMsgType.DeviceAuthRsp,
                RefMessageId = message.Id,
                Content = Any.Pack(responseContent)
            };

            await context.WriteAndFlushAsync(response);
        }

        private Task HandleTalkToFriendTaskResult(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<TalkToFriendTaskResultNoticeMessage>();
            _logger.Information("TalkToFriend task result: Success={Success}, Code={Code}, Msg={ErrMsg}", 
                result.Success, result.Code, result.ErrMsg);
            return Task.CompletedTask;
        }

        private Task HandleWeChatOnline(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatOnlineNoticeMessage>();
            _logger.Information("WeChat Online: {WeChatId} ({WeChatNick})", notice.WeChatId, notice.WeChatNick);
            return Task.CompletedTask;
        }

        private Task HandleWeChatOffline(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatOfflineNoticeMessage>();
            _logger.Information("WeChat Offline: {WeChatId}, Reason={Reason}", notice.WeChatId, notice.Reason);
            return Task.CompletedTask;
        }

        private Task HandleFriendTalkNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendTalkNoticeMessage>();
            _logger.Information("Friend Talk Notice: {WeChatId} received message from {FriendId}: {Content}", 
                notice.WeChatId, notice.FriendId, notice.Content.ToStringUtf8());
            return Task.CompletedTask;
        }

        private Task HandleWeChatTalkToFriendNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatTalkToFriendNoticeMessage>();
            _logger.Information("WeChat Talk To Friend Notice: {WeChatId} sent message to {FriendId}: {Content}", 
                notice.WeChatId, notice.FriendId, notice.Content.ToStringUtf8());
            return Task.CompletedTask;
        }

        private Task HandleFriendAddNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendAddNoticeMessage>();
            if (notice.Friend != null)
            {
                _logger.Information("Friend Add Notice: {WeChatId} added friend {FriendNick}", 
                    notice.WeChatId, notice.Friend.FriendNick);
            }
            return Task.CompletedTask;
        }

        private Task HandleChatRoomPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ChatRoomPushNoticeMessage>();
            _logger.Information("Chat Room Push Notice: {WeChatId} pushed {Count} chat rooms", notice.WeChatId, notice.ChatRooms.Count);
            return Task.CompletedTask;
        }

        private Task HandleCircleDetailNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<CircleDetailNoticeMessage>();
            _logger.Information("Circle Detail Notice: {WeChatId} - {CircleId}", notice.WeChatId, notice.Circle.CircleId);
            return Task.CompletedTask;
        }

        private Task HandlePostSNSNewsTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<PostSNSNewsTaskResultNoticeMessage>();
            _logger.Information("Post SNS News Result: Success={Success}, TaskId={TaskId}", result.Success, result.TaskId);
            return Task.CompletedTask;
        }

        private Task HandleTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<TaskResultNoticeMessage>();
            _logger.Information("Task Result: Success={Success}, TaskId={TaskId}, Msg={ErrMsg}", result.Success, result.TaskId, result.ErrMsg);
            return Task.CompletedTask;
        }

        private Task HandleScreenShotTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<ScreenShotTaskResultNoticeMessage>();
            _logger.Information("Screen Shot Result: Success={Success}, Url={Url}", result.Success, result.Url);
            return Task.CompletedTask;
        }
    }
}
