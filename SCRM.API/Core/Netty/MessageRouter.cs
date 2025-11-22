using Jubo.JuLiao.IM.Wx.Proto;
using System;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Google.Protobuf;
using SCRM.Shared.Core;

namespace SCRM.Core.Netty
{
    public class MessageRouter
    {
        public MessageRouter()
        {
        }

        public async Task RouteMessage(TransportMessage message, IChannelHandlerContext context)
        {
            Utility.logger.Information("Routing message: Id={Id}, Type={MsgType}, Token={Token}", 
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

                    case EnumMsgType.TalkToFriendTask:
                        await HandleTalkToFriend(message, context);
                        break;

                    case EnumMsgType.WeChatOnlineNotice:
                    case EnumMsgType.WeChatOfflineNotice:
                        await HandleWeChatStatus(message, context);
                        break;

                    default:
                        Utility.logger.Warning("Unhandled message type: {MsgType}", message.MsgType);
                        break;
                }
            }
            catch (Exception ex)
            {
                Utility.logger.Error(ex, "Error routing message type: {MsgType}", message.MsgType);
            }
        }

        private Task HandleHeartBeat(TransportMessage message, IChannelHandlerContext context)
        {
            Utility.logger.Debug("HeartBeat received from {RemoteAddress}", context.Channel.RemoteAddress);
            
            var response = new TransportMessage
            {
                Id = message.Id,
                MsgType = EnumMsgType.MsgReceivedAck,
                RefMessageId = message.Id
            };
            
            return context.WriteAndFlushAsync(response);
        }

        private Task HandleDeviceAuth(TransportMessage message, IChannelHandlerContext context)
        {
            Utility.logger.Information("Device auth request received");
            return Task.CompletedTask;
        }

        private Task HandleTalkToFriend(TransportMessage message, IChannelHandlerContext context)
        {
            Utility.logger.Information("TalkToFriend task received");
            return Task.CompletedTask;
        }

        private Task HandleWeChatStatus(TransportMessage message, IChannelHandlerContext context)
        {
            Utility.logger.Information("WeChat status change: {MsgType}", message.MsgType);
            return Task.CompletedTask;
        }
    }
}
