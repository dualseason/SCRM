using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;

namespace SCRM.API.Services.Netty.Handlers.Abstractions
{
    /// <summary>
    /// 消息处理器基类
    /// 提供通用的ACK发送和日志功能
    /// </summary>
    public abstract class MessageHandlerBase
    {
        protected readonly ILogger Logger;

        protected MessageHandlerBase(ILogger logger)
        {
            Logger = logger;
        }

        /// <summary>
        /// 发送消息确认(ACK)
        /// </summary>
        /// <param name="message">收到的原始消息</param>
        /// <param name="context">通道上下文</param>
        protected async Task SendAckAsync(TransportMessage message, IChannelHandlerContext context)
        {
            try
            {
                var response = new TransportMessage
                {
                    Id = 0,
                    MsgType = EnumMsgType.MsgReceivedAck,
                    RefMessageId = message.Id
                };

                await context.WriteAndFlushAsync(response);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send ACK for message {MsgId}", message.Id);
            }
        }
    }
}
