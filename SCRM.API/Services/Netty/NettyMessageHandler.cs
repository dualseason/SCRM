using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using System;
using System.Threading.Tasks;
using SCRM.Shared.Core;
using SCRM.API.Services.Core;

namespace SCRM.Services.Netty
{
    /// <summary>
    /// Netty 消息处理器
    /// 处理 Channel 生命周期事件（连接、断开）及消息接收
    /// </summary>
    public class NettyMessageHandler : ChannelHandlerAdapter
    {
        private readonly Microsoft.Extensions.Logging.ILogger<NettyMessageHandler> _logger;
        private readonly MessageRouter _messageRouter;
        private readonly ConnectionManager _connectionManager;

        public NettyMessageHandler(MessageRouter messageRouter, ConnectionManager connectionManager, Microsoft.Extensions.Logging.ILogger<NettyMessageHandler> logger)
        {
            _messageRouter = messageRouter;
            _connectionManager = connectionManager;
            _logger = logger;
        }

        /// <summary>
        /// 客户端建立连接时触发
        /// </summary>
        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            _logger.LogInformation("[TCP接入] 客户端连接成功: {RemoteAddress}, ChannelId: {ChannelId}", ctx.Channel.RemoteAddress, ctx.Channel.Id.AsLongText());
            _logger.LogInformation("Client connected: {RemoteAddress}", ctx.Channel.RemoteAddress);
            _connectionManager.RegisterChannel(ctx.Channel.Id.AsLongText(), ctx.Channel);
            base.ChannelActive(ctx);
        }

        /// <summary>
        /// 客户端断开连接时触发
        /// </summary>
        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            _logger.LogInformation("[TCP断开] 客户端连接断开: {RemoteAddress}, ChannelId: {ChannelId}", ctx.Channel.RemoteAddress, ctx.Channel.Id.AsLongText());
            _logger.LogInformation("Client disconnected: {RemoteAddress}", ctx.Channel.RemoteAddress);
            _connectionManager.RemoveChannel(ctx.Channel.Id.AsLongText());
            base.ChannelInactive(ctx);
        }

        /// <summary>
        /// 接收到消息时触发
        /// </summary>
        public override void ChannelRead(IChannelHandlerContext ctx, object message)
        {
            try
            {
                if (message is TransportMessage transportMessage)
                {
                    _logger.LogInformation("Received TransportMessage: Id={Id}, Type={MsgType} from {RemoteAddress}",
                        transportMessage.Id, transportMessage.MsgType, ctx.Channel.RemoteAddress);

                    // 异步处理业务逻辑，避免阻塞 IO 线程
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _messageRouter.RouteMessage(transportMessage, ctx);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing TransportMessage: {MsgType}", transportMessage.MsgType);
                        }
                    });
                }
                else
                {
                    _logger.LogWarning("Received unknown message type: {MessageType}", message?.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ChannelRead from {RemoteAddress}", ctx.Channel.RemoteAddress);
            }
        }

        /// <summary>
        /// 处理过程中发生异常时触发
        /// </summary>
        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            if (cause is System.Net.Sockets.SocketException socketEx && socketEx.ErrorCode == 10054)
            {
                // 远程主机强迫关闭了一个现有的连接 - 常见网络波动或强制关闭
                _logger.LogDebug("Connection reset by client {RemoteAddress} (10054)", ctx.Channel.RemoteAddress);
            }
            else
            {
                _logger.LogError(cause, "Exception in Netty handler from {RemoteAddress}", ctx.Channel.RemoteAddress);
            }
            // 发生异常时关闭连接
            ctx.CloseAsync();
        }
    }
}