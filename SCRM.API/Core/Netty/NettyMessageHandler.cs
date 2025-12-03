using DotNetty.Transport.Channels;
using SCRM.SHARED.Proto;
using System;
using System.Threading.Tasks;
using SCRM.Shared.Core;
using SCRM.Services;

namespace SCRM.Core.Netty
{
    public class NettyMessageHandler : ChannelHandlerAdapter
    {
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;
        private readonly MessageRouter _messageRouter;
        private readonly ConnectionManager _connectionManager;

        public NettyMessageHandler(MessageRouter messageRouter, ConnectionManager connectionManager)
        {
            _messageRouter = messageRouter;
            _connectionManager = connectionManager;
        }

        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            _logger.Information("Client connected: {RemoteAddress}", ctx.Channel.RemoteAddress);
            _connectionManager.RegisterChannel(ctx.Channel.Id.AsLongText(), ctx.Channel);
            base.ChannelActive(ctx);
        }

        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            _logger.Information("Client disconnected: {RemoteAddress}", ctx.Channel.RemoteAddress);
            _connectionManager.RemoveChannel(ctx.Channel.Id.AsLongText());
            base.ChannelInactive(ctx);
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object message)
        {
            try
            {
                if (message is TransportMessage transportMessage)
                {
                    _logger.Information("Received TransportMessage: Id={Id}, Type={MsgType} from {RemoteAddress}",
                        transportMessage.Id, transportMessage.MsgType, ctx.Channel.RemoteAddress);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _messageRouter.RouteMessage(transportMessage, ctx);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Error processing TransportMessage: {MsgType}", transportMessage.MsgType);
                        }
                    });
                }
                else
                {
                    _logger.Warning("Received unknown message type: {MessageType}", message?.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in ChannelRead from {RemoteAddress}", ctx.Channel.RemoteAddress);
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            _logger.Error(cause, "Exception in Netty handler from {RemoteAddress}", ctx.Channel.RemoteAddress);
            ctx.CloseAsync();
        }
    }
}