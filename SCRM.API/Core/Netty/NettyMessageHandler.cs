using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using System;
using System.Threading.Tasks;
using SCRM.Shared.Core;

namespace SCRM.Core.Netty
{
    public class NettyMessageHandler : ChannelHandlerAdapter
    {
        private readonly MessageRouter _messageRouter;

        public NettyMessageHandler(MessageRouter messageRouter)
        {
            _messageRouter = messageRouter;
        }

        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            Utility.logger.Information("Client connected: {RemoteAddress}", ctx.Channel.RemoteAddress);
            base.ChannelActive(ctx);
        }

        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            Utility.logger.Information("Client disconnected: {RemoteAddress}", ctx.Channel.RemoteAddress);
            base.ChannelInactive(ctx);
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object message)
        {
            try
            {
                if (message is TransportMessage transportMessage)
                {
                    Utility.logger.Information("Received TransportMessage: Id={Id}, Type={MsgType} from {RemoteAddress}",
                        transportMessage.Id, transportMessage.MsgType, ctx.Channel.RemoteAddress);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _messageRouter.RouteMessage(transportMessage, ctx);
                        }
                        catch (Exception ex)
                        {
                            Utility.logger.Error(ex, "Error processing TransportMessage: {MsgType}", transportMessage.MsgType);
                        }
                    });
                }
                else
                {
                    Utility.logger.Warning("Received unknown message type: {MessageType}", message?.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                Utility.logger.Error(ex, "Error in ChannelRead from {RemoteAddress}", ctx.Channel.RemoteAddress);
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            Utility.logger.Error(cause, "Exception in Netty handler from {RemoteAddress}", ctx.Channel.RemoteAddress);
            ctx.CloseAsync();
        }
    }
}