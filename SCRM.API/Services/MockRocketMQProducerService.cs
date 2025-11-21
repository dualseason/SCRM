using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SCRM.Configurations;
using System;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public class MockRocketMQProducerService : IRocketMQProducerService, IDisposable
    {
        private readonly ILogger<MockRocketMQProducerService> _logger;
        private readonly RocketMQSettings _settings;
        private bool _isStarted = false;
        private readonly List<SentMessageInfo> _sentMessages = new List<SentMessageInfo>();

        public MockRocketMQProducerService(
            ILogger<MockRocketMQProducerService> logger,
            IOptions<RocketMQSettings> settings)
        {
            _logger = logger;
            _settings = settings.Value;
        }

        public async Task StartAsync()
        {
            if (!_isStarted)
            {
                _logger.LogInformation("Mock RocketMQ 生产者启动成功，生产者组: {ProducerGroup}, NameServer: {NameServer}",
                    _settings.ProducerGroup, _settings.NameServer);
                _isStarted = true;
            }
            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (_isStarted)
            {
                _logger.LogInformation("Mock RocketMQ 生产者已停止");
                _isStarted = false;
            }
            await Task.CompletedTask;
        }

        public async Task<bool> SendMessageAsync(string topic, string message)
        {
            return await SendMessageAsync(topic, message, null);
        }

        public async Task<bool> SendMessageAsync(string topic, string message, string? tag)
        {
            try
            {
                if (!_isStarted)
                {
                    await StartAsync();
                }

                // 模拟消息发送
                var messageId = Guid.NewGuid().ToString("N")[..16];

                _sentMessages.Add(new SentMessageInfo
                {
                    Topic = topic,
                    Tag = tag,
                    Message = message,
                    MessageId = messageId,
                    SentAt = DateTime.UtcNow,
                    MessageType = "Normal"
                });

                _logger.LogInformation("模拟消息发送成功 - 主题: {Topic}, 标签: {Tag}, 消息ID: {MessageId}",
                    topic, tag, messageId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送消息失败 - 主题: {Topic}, 标签: {Tag}, 消息: {Message}", topic, tag, message);
                return false;
            }
        }

        public async Task<bool> SendDelayedMessageAsync(string topic, string message, int delayLevel)
        {
            try
            {
                if (!_isStarted)
                {
                    await StartAsync();
                }

                // 模拟延迟消息发送
                var messageId = Guid.NewGuid().ToString("N")[..16];

                _sentMessages.Add(new SentMessageInfo
                {
                    Topic = topic,
                    Message = message,
                    MessageId = messageId,
                    SentAt = DateTime.UtcNow,
                    MessageType = "Delayed",
                    DelayLevel = delayLevel
                });

                _logger.LogInformation("模拟延迟消息发送成功 - 主题: {Topic}, 延迟级别: {DelayLevel}, 消息ID: {MessageId}",
                    topic, delayLevel, messageId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送延迟消息失败 - 主题: {Topic}, 消息: {Message}, 延迟级别: {DelayLevel}",
                    topic, message, delayLevel);
                return false;
            }
        }

        public async Task<bool> SendOrderlyMessageAsync(string topic, string message, string hashKey)
        {
            try
            {
                if (!_isStarted)
                {
                    await StartAsync();
                }

                // 模拟顺序消息发送
                var messageId = Guid.NewGuid().ToString("N")[..16];

                _sentMessages.Add(new SentMessageInfo
                {
                    Topic = topic,
                    Message = message,
                    MessageId = messageId,
                    SentAt = DateTime.UtcNow,
                    MessageType = "Orderly",
                    HashKey = hashKey
                });

                _logger.LogInformation("模拟顺序消息发送成功 - 主题: {Topic}, 哈希键: {HashKey}, 消息ID: {MessageId}",
                    topic, hashKey, messageId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送顺序消息失败 - 主题: {Topic}, 消息: {Message}, 哈希键: {HashKey}",
                    topic, message, hashKey);
                return false;
            }
        }

        public List<SentMessageInfo> GetSentMessages()
        {
            return _sentMessages.ToList();
        }

        public void ClearSentMessages()
        {
            _sentMessages.Clear();
        }

        public void Dispose()
        {
            if (_isStarted)
            {
                StopAsync().GetAwaiter().GetResult();
            }
        }
    }

    public class SentMessageInfo
    {
        public string Topic { get; set; } = string.Empty;
        public string? Tag { get; set; }
        public string Message { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public string MessageType { get; set; } = string.Empty;
        public int? DelayLevel { get; set; }
        public string? HashKey { get; set; }
    }
}