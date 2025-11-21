using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using SCRM.Services;
using System.Text;
using System.Threading.Tasks;

namespace SCRM.Netty
{
    public class NettyMessageHandler : ChannelHandlerAdapter
    {
        private readonly ILogger<NettyMessageHandler> _logger;
        private readonly ISignalRMessageService _signalRMessageService;
        private readonly IRocketMQProducerService _rocketMQProducer;

        public NettyMessageHandler(
            ILogger<NettyMessageHandler> logger,
            ISignalRMessageService signalRMessageService,
            IRocketMQProducerService rocketMQProducer)
        {
            _logger = logger;
            _signalRMessageService = signalRMessageService;
            _rocketMQProducer = rocketMQProducer;
        }

        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            _logger.LogInformation("Client connected: {RemoteAddress}", ctx.Channel.RemoteAddress);
            base.ChannelActive(ctx);
        }

        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            _logger.LogInformation("Client disconnected: {RemoteAddress}", ctx.Channel.RemoteAddress);
            base.ChannelInactive(ctx);
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object message)
        {
            try
            {
                if (message is IByteBuffer buffer)
                {
                    string receivedMessage = buffer.ToString(Encoding.UTF8);
                    _logger.LogInformation("Received message: {Message} from {RemoteAddress}",
                        receivedMessage, ctx.Channel.RemoteAddress);

                    // 处理接收到的消息
                    _ = Task.Run(() => ProcessReceivedMessage(receivedMessage, ctx.Channel.RemoteAddress?.ToString()));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from {RemoteAddress}", ctx.Channel.RemoteAddress);
            }

            // DotNetty automatically handles reference counting for simple messages
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            _logger.LogError(cause, "Exception in Netty handler from {RemoteAddress}", ctx.Channel.RemoteAddress);
            ctx.CloseAsync();
        }

        private async Task ProcessReceivedMessage(string message, string? remoteAddress)
        {
            try
            {
                // 解析消息类型
                var messageData = System.Text.Json.JsonSerializer.Deserialize<NettyMessage>(message);

                if (messageData == null)
                {
                    _logger.LogWarning("Failed to parse message: {Message}", message);
                    return;
                }

                switch (messageData.Type.ToLower())
                {
                    case "signalr":
                        await HandleSignalRMessage(messageData);
                        break;
                    case "rocketmq":
                        await HandleRocketMQMessage(messageData);
                        break;
                    case "broadcast":
                        await HandleBroadcastMessage(messageData);
                        break;
                    default:
                        _logger.LogWarning("Unknown message type: {Type}", messageData.Type);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message: {Message} from {RemoteAddress}", message, remoteAddress);
            }
        }

        private async Task HandleSignalRMessage(NettyMessage messageData)
        {
            try
            {
                switch (messageData.TargetType?.ToLower())
                {
                    case "all":
                        await _signalRMessageService.SendMessageToAllAsync(messageData.Content, "NettyBroadcast");
                        break;
                    case "user":
                        if (!string.IsNullOrEmpty(messageData.TargetId))
                        {
                            await _signalRMessageService.SendMessageToUserAsync(messageData.TargetId, messageData.Content, "NettyMessage");
                        }
                        break;
                    case "device":
                        if (!string.IsNullOrEmpty(messageData.TargetId))
                        {
                            await _signalRMessageService.SendMessageToDeviceTypeAsync(messageData.TargetId, messageData.Content, "NettyMessage");
                        }
                        break;
                    case "room":
                        if (!string.IsNullOrEmpty(messageData.TargetId))
                        {
                            await _signalRMessageService.SendMessageToRoomAsync(messageData.TargetId, messageData.Content, "NettyMessage");
                        }
                        break;
                }

                _logger.LogInformation("SignalR message forwarded: {Type} to {Target}",
                    messageData.Type, messageData.TargetType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling SignalR message");
            }
        }

        private async Task HandleRocketMQMessage(NettyMessage messageData)
        {
            try
            {
                if (!string.IsNullOrEmpty(messageData.Topic))
                {
                    bool success = await _rocketMQProducer.SendMessageAsync(
                        messageData.Topic,
                        messageData.Content?.ToString() ?? "",
                        messageData.Tag);

                    if (success)
                    {
                        _logger.LogInformation("RocketMQ message sent: {Topic} with tag: {Tag}",
                            messageData.Topic, messageData.Tag);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to send RocketMQ message: {Topic}", messageData.Topic);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling RocketMQ message");
            }
        }

        private async Task HandleBroadcastMessage(NettyMessage messageData)
        {
            try
            {
                // 同时发送到 SignalR 和 RocketMQ
                if (!string.IsNullOrEmpty(messageData.Topic))
                {
                    // 发送到 RocketMQ
                    await _rocketMQProducer.SendMessageAsync(
                        messageData.Topic,
                        messageData.Content?.ToString() ?? "",
                        messageData.Tag);
                }

                // 发送到 SignalR
                await _signalRMessageService.SendMessageToAllAsync(
                    messageData.Content ?? new { Message = "Netty broadcast" },
                    "NettyBroadcast");

                _logger.LogInformation("Broadcast message sent via both SignalR and RocketMQ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling broadcast message");
            }
        }
    }

    public class NettyMessage
    {
        public string Type { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public object Content { get; set; } = new { };
        public string? Tag { get; set; }
        public string? Topic { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Source { get; set; }
    }
}