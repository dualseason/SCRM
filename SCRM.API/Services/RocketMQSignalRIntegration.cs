using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public class RocketMQSignalRIntegration : BackgroundService
    {
        private readonly IRocketMQConsumerService _rocketMQConsumer;
        private readonly ISignalRMessageService _signalRMessageService;
        private readonly IConnectionManager _connectionManager;
        private readonly ILogger<RocketMQSignalRIntegration> _logger;

        public RocketMQSignalRIntegration(
            IRocketMQConsumerService rocketMQConsumer,
            ISignalRMessageService signalRMessageService,
            IConnectionManager connectionManager,
            ILogger<RocketMQSignalRIntegration> logger)
        {
            _rocketMQConsumer = rocketMQConsumer;
            _signalRMessageService = signalRMessageService;
            _connectionManager = connectionManager;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RocketMQ 与 SignalR 集成服务启动");

            // 启动 RocketMQ 消费者
            await _rocketMQConsumer.StartAsync();

            // 订阅相关主题
            await SubscribeToRocketMQTopics();

            // 保持服务运行
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 执行定期任务，如检查连接状态等
                    await PerformPeriodicTasks();
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // 正常取消操作
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RocketMQ 与 SignalR 集成服务执行过程中发生错误");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task SubscribeToRocketMQTopics()
        {
            try
            {
                // 订阅各种事件主题
                await _rocketMQConsumer.SubscribeAsync("scrm_user_events");
                await _rocketMQConsumer.SubscribeAsync("scrm_order_events");
                await _rocketMQConsumer.SubscribeAsync("scrm_notification_events");

                _logger.LogInformation("已订阅所有 RocketMQ 主题");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订阅 RocketMQ 主题失败");
            }
        }

        private async Task PerformPeriodicTasks()
        {
            try
            {
                // 获取连接统计信息
                var connections = await _connectionManager.GetAllConnectionsAsync();
                var onlineCount = await _connectionManager.GetOnlineUserCountAsync();

                _logger.LogDebug("当前连接统计 - 总连接数: {TotalConnections}, 在线用户数: {OnlineUsers}",
                    connections.Count(), onlineCount);

                // Note: MockRocketMQConsumerService has been moved to TEST project
                // In production, this will integrate with actual RocketMQ implementation
                // For now, we'll just log the connection stats
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行定期任务时发生错误");
            }
        }

        private async Task ProcessRocketMQMessage(object message)
        {
            // Since ReceivedMessageInfo was part of MockRocketMQConsumerService, using object instead
            // or create a simple message wrapper if needed
            var topic = message?.GetType().GetProperty("Topic")?.GetValue(message)?.ToString() ?? "unknown";
            var tag = message?.GetType().GetProperty("Tag")?.GetValue(message)?.ToString() ?? "";
            var content = message?.ToString() ?? "";

            _logger.LogInformation("处理 RocketMQ 消息 - 主题: {Topic}, 标签: {Tag}, 消息内容: {Message}", topic, tag, content);

            try
            {
                _logger.LogInformation("处理 RocketMQ 消息 - 主题: {Topic}, 标���: {Tag}, 消息内容: {Message}", topic, tag, content);

                switch (topic)
                {
                    case "scrm_user_events":
                        await ProcessUserEvent(message);
                        break;
                    case "scrm_order_events":
                        await ProcessOrderEvent(message);
                        break;
                    case "scrm_notification_events":
                        await ProcessNotificationEvent(message);
                        break;
                    default:
                        _logger.LogWarning("未知的 RocketMQ 消息主题: {Topic}", topic);
                        break;
                }
            }
            catch (Exception ex)
            {
                var messageId = message?.GetType().GetProperty("MessageId")?.GetValue(message)?.ToString() ?? "unknown";
                _logger.LogError(ex, "处理 RocketMQ 消息时发生错误 - 消息ID: {MessageId}", messageId);
            }
        }

        private async Task ProcessUserEvent(object messageObj)
        {
            try
            {
                var tag = messageObj?.GetType().GetProperty("Tag")?.GetValue(messageObj)?.ToString() ?? "";
                var msgContent = messageObj?.ToString() ?? "";
                var messageId = messageObj?.GetType().GetProperty("MessageId")?.GetValue(messageObj)?.ToString() ?? "";
                var receivedAt = messageObj?.GetType().GetProperty("ReceivedAt")?.GetValue(messageObj) ?? DateTime.UtcNow;

                // 发送用户事件到所有连接的客户端
                await _signalRMessageService.SendMessageToAllAsync(new
                {
                    EventType = "UserEvent",
                    Action = tag,
                    Message = msgContent,
                    MessageId = messageId,
                    Timestamp = receivedAt
                }, "UserEvent");

                // 如果是特定用户事件，也可以只发送给特定用户
                // 这里需要从消息内容中解析出用户ID
                // 示例：await _signalRMessageService.SendMessageToUserAsync(userId, message, "UserEvent");

                _logger.LogInformation("用户事件已通过 SignalR 转发 - 标签: {Tag}, 消息ID: {MessageId}",
                    tag, messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理用户事件时发生错误");
            }
        }

        private async Task ProcessOrderEvent(object messageObj)
        {
            try
            {
                var tag = messageObj?.GetType().GetProperty("Tag")?.GetValue(messageObj)?.ToString() ?? "";
                var msgContent = messageObj?.ToString() ?? "";
                var messageId = messageObj?.GetType().GetProperty("MessageId")?.GetValue(messageObj)?.ToString() ?? "";
                var receivedAt = messageObj?.GetType().GetProperty("ReceivedAt")?.GetValue(messageObj) ?? DateTime.UtcNow;

                // 发送订单事件到所有客户端
                await _signalRMessageService.SendMessageToAllAsync(new
                {
                    EventType = "OrderEvent",
                    Action = tag,
                    Message = msgContent,
                    MessageId = messageId,
                    Timestamp = receivedAt
                }, "OrderEvent");

                _logger.LogInformation("订单事件已通过 SignalR 转发 - 标签: {Tag}, 消息ID: {MessageId}",
                    tag, messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理订单事件时发生错误");
            }
        }

        private async Task ProcessNotificationEvent(object messageObj)
        {
            try
            {
                var tag = messageObj?.GetType().GetProperty("Tag")?.GetValue(messageObj)?.ToString() ?? "";
                var msgContent = messageObj?.ToString() ?? "";
                var messageId = messageObj?.GetType().GetProperty("MessageId")?.GetValue(messageObj)?.ToString() ?? "";
                var receivedAt = messageObj?.GetType().GetProperty("ReceivedAt")?.GetValue(messageObj) ?? DateTime.UtcNow;

                // 发送通知事件到所有客户端
                await _signalRMessageService.SendMessageToAllAsync(new
                {
                    EventType = "NotificationEvent",
                    Type = tag,
                    Message = msgContent,
                    MessageId = messageId,
                    Timestamp = receivedAt
                }, "NotificationEvent");

                _logger.LogInformation("通知事件已通过 SignalR 转发 - 标签: {Tag}, 消息ID: {MessageId}",
                    tag, messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理通知事件时发生错误");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("RocketMQ 与 SignalR 集成服务停止");

            // 停止 RocketMQ 消费者
            if (_rocketMQConsumer != null)
            {
                await _rocketMQConsumer.StopAsync();
            }

            await base.StopAsync(cancellationToken);
        }
    }
}