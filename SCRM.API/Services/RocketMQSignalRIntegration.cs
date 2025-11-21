using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
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

                // 如果使用的是 MockRocketMQConsumer，这里可以触发消息处理
                if (_rocketMQConsumer is MockRocketMQConsumerService mockConsumer)
                {
                    var receivedMessages = mockConsumer.GetReceivedMessages();
                    foreach (var message in receivedMessages.Take(5)) // 每次处理最多5条消息
                    {
                        await ProcessRocketMQMessage(message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行定期任务时发生错误");
            }
        }

        private async Task ProcessRocketMQMessage(ReceivedMessageInfo message)
        {
            try
            {
                _logger.LogInformation("处理 RocketMQ 消息 - 主题: {Topic}, 标签: {Tag}, 消息内容: {Message}",
                    message.Topic, message.Tag, message.Message);

                switch (message.Topic)
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
                        _logger.LogWarning("未知的 RocketMQ 消息主题: {Topic}", message.Topic);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理 RocketMQ 消息时发生错误 - 消息ID: {MessageId}", message.MessageId);
            }
        }

        private async Task ProcessUserEvent(ReceivedMessageInfo message)
        {
            try
            {
                // 发送用户事件到所有连接的客户端
                await _signalRMessageService.SendMessageToAllAsync(new
                {
                    EventType = "UserEvent",
                    Action = message.Tag,
                    Message = message.Message,
                    MessageId = message.MessageId,
                    Timestamp = message.ReceivedAt
                }, "UserEvent");

                // 如果是特定用户事件，也可以只发送给特定用户
                // 这里需要从消息内容中解析出用户ID
                // 示例：await _signalRMessageService.SendMessageToUserAsync(userId, message, "UserEvent");

                _logger.LogInformation("用户事件已通过 SignalR 转发 - 标签: {Tag}, 消息ID: {MessageId}",
                    message.Tag, message.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理用户事件时发生错误");
            }
        }

        private async Task ProcessOrderEvent(ReceivedMessageInfo message)
        {
            try
            {
                // 发送订单事件到所有客户端
                await _signalRMessageService.SendMessageToAllAsync(new
                {
                    EventType = "OrderEvent",
                    Action = message.Tag,
                    Message = message.Message,
                    MessageId = message.MessageId,
                    Timestamp = message.ReceivedAt
                }, "OrderEvent");

                _logger.LogInformation("订单事件已通过 SignalR 转发 - 标签: {Tag}, 消息ID: {MessageId}",
                    message.Tag, message.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理订单事件时发生错误");
            }
        }

        private async Task ProcessNotificationEvent(ReceivedMessageInfo message)
        {
            try
            {
                // 发送通知事件到所有客户端
                await _signalRMessageService.SendMessageToAllAsync(new
                {
                    EventType = "NotificationEvent",
                    Type = message.Tag,
                    Message = message.Message,
                    MessageId = message.MessageId,
                    Timestamp = message.ReceivedAt
                }, "NotificationEvent");

                _logger.LogInformation("通知事件已通过 SignalR 转发 - 标签: {Tag}, 消息ID: {MessageId}",
                    message.Tag, message.MessageId);
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