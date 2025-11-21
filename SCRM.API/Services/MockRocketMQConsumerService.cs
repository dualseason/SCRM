using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SCRM.Configurations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public class MockRocketMQConsumerService : IRocketMQConsumerService, IDisposable
    {
        private readonly ILogger<MockRocketMQConsumerService> _logger;
        private readonly RocketMQSettings _settings;
        private bool _isStarted = false;
        private readonly List<SubscriptionInfo> _subscriptions = new List<SubscriptionInfo>();
        private readonly List<ReceivedMessageInfo> _receivedMessages = new List<ReceivedMessageInfo>();
        private Timer? _messageSimulatorTimer;

        public MockRocketMQConsumerService(
            ILogger<MockRocketMQConsumerService> logger,
            IOptions<RocketMQSettings> settings)
        {
            _logger = logger;
            _settings = settings.Value;
        }

        public async Task StartAsync()
        {
            if (!_isStarted)
            {
                _logger.LogInformation("Mock RocketMQ 消费者启动成功，消费者组: {ConsumerGroup}, NameServer: {NameServer}",
                    _settings.ConsumerGroup, _settings.NameServer);
                _isStarted = true;

                // 启动消息模拟器定时器
                _messageSimulatorTimer = new Timer(SimulateMessageReception, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
            }
            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (_isStarted)
            {
                _messageSimulatorTimer?.Dispose();
                _isStarted = false;
                _logger.LogInformation("Mock RocketMQ 消费者已停止");
            }
            await Task.CompletedTask;
        }

        public async Task<bool> SubscribeAsync(string topic)
        {
            return await SubscribeAsync(topic, "*");
        }

        public async Task<bool> SubscribeAsync(string topic, string? tag)
        {
            try
            {
                if (!_isStarted)
                {
                    await StartAsync();
                }

                var subscription = new SubscriptionInfo
                {
                    Topic = topic,
                    Tag = tag ?? "*",
                    SubscribedAt = DateTime.UtcNow
                };

                _subscriptions.Add(subscription);
                _logger.LogInformation("订阅主题成功 - 主题: {Topic}, 标签: {Tag}", topic, tag ?? "*");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订阅主题失败 - 主题: {Topic}, 标签: {Tag}", topic, tag);
                return false;
            }
        }

        private void SimulateMessageReception(object? state)
        {
            if (!_isStarted || _subscriptions.Count == 0)
                return;

            try
            {
                // 随机选择一个订阅
                var randomSubscription = _subscriptions.ElementAt(Random.Shared.Next(_subscriptions.Count));

                // 模拟接收消息
                var messageBody = GenerateMockMessage(randomSubscription.Topic, randomSubscription.Tag);
                var messageId = Guid.NewGuid().ToString("N")[..16];

                var receivedMessage = new ReceivedMessageInfo
                {
                    Topic = randomSubscription.Topic,
                    Tag = randomSubscription.Tag,
                    Message = messageBody,
                    MessageId = messageId,
                    ReceivedAt = DateTime.UtcNow
                };

                _receivedMessages.Add(receivedMessage);

                // 处理消息
                ProcessMessage(receivedMessage);

                // 保持最近100条消息
                if (_receivedMessages.Count > 100)
                {
                    _receivedMessages.RemoveAt(0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "模拟消息接收时发生错误");
            }
        }

        private string GenerateMockMessage(string topic, string tag)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

            switch (topic)
            {
                case "scrm_user_events":
                    return $@"{{""userId"":""{Random.Shared.Next(1000, 9999)}"",""action"":""{tag}"",""timestamp"":""{timestamp}""}}";
                case "scrm_order_events":
                    return $@"{{""orderId"":""ORD-{Random.Shared.Next(1000, 9999):D3}"",""amount"":{Random.Shared.Next(100, 1000):F2},""status"":""{tag}"",""timestamp"":""{timestamp}""}}";
                case "scrm_notification_events":
                    return $@"{{""userId"":""{Random.Shared.Next(1000, 9999)}"",""type"":""{tag}"",""title"":""Test Notification"",""content"":""This is a test message"",""timestamp"":""{timestamp}""}}";
                default:
                    return $@"{{""topic"":""{topic}"",""tag"":""{tag}"",""message"":""Test message"",""timestamp"":""{timestamp}""}}";
            }
        }

        private void ProcessMessage(ReceivedMessageInfo message)
        {
            try
            {
                _logger.LogInformation("接收到模拟消息 - 主题: {Topic}, 标签: {Tag}, 消息ID: {MessageId}, 内容: {Body}",
                    message.Topic, message.Tag, message.MessageId, message.Message);

                // 根据主题处理不同的消息类型
                switch (message.Topic)
                {
                    case "scrm_user_events":
                        ProcessUserEvent(message.Tag, message.Message, message.MessageId);
                        break;
                    case "scrm_order_events":
                        ProcessOrderEvent(message.Tag, message.Message, message.MessageId);
                        break;
                    case "scrm_notification_events":
                        ProcessNotificationEvent(message.Tag, message.Message, message.MessageId);
                        break;
                    default:
                        _logger.LogWarning("未知主题的消息: {Topic}", message.Topic);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理消息时发生错误 - 消息ID: {MessageId}", message.MessageId);
            }
        }

        private void ProcessUserEvent(string tag, string message, string messageId)
        {
            _logger.LogInformation("处理用户事件 - 标签: {Tag}, 消息: {Message}, 消息ID: {MessageId}", tag, message, messageId);
        }

        private void ProcessOrderEvent(string tag, string message, string messageId)
        {
            _logger.LogInformation("处理订单事件 - 标签: {Tag}, 消息: {Message}, 消息ID: {MessageId}", tag, message, messageId);
        }

        private void ProcessNotificationEvent(string tag, string message, string messageId)
        {
            _logger.LogInformation("处理通知事件 - 标签: {Tag}, 消息: {Message}, 消息ID: {MessageId}", tag, message, messageId);
        }

        public List<SubscriptionInfo> GetSubscriptions()
        {
            return _subscriptions.ToList();
        }

        public List<ReceivedMessageInfo> GetReceivedMessages()
        {
            return _receivedMessages.ToList();
        }

        public void ClearReceivedMessages()
        {
            _receivedMessages.Clear();
        }

        public void Dispose()
        {
            if (_isStarted)
            {
                StopAsync().GetAwaiter().GetResult();
            }
            _messageSimulatorTimer?.Dispose();
        }
    }

    public class SubscriptionInfo
    {
        public string Topic { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public DateTime SubscribedAt { get; set; }
    }

    public class ReceivedMessageInfo
    {
        public string Topic { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
        public bool Processed { get; set; } = true;
    }
}