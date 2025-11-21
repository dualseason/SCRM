using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SCRM.Services;
using System;
using System.Threading.Tasks;

namespace SCRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RocketMQTestController : ControllerBase
    {
        private readonly IRocketMQProducerService _producerService;
        private readonly IRocketMQConsumerService _consumerService;
        private readonly ILogger<RocketMQTestController> _logger;

        public RocketMQTestController(
            IRocketMQProducerService producerService,
            IRocketMQConsumerService consumerService,
            ILogger<RocketMQTestController> logger)
        {
            _producerService = producerService;
            _consumerService = consumerService;
            _logger = logger;
        }

        [HttpPost("send-message")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                _logger.LogInformation("准备发送消息到主题: {Topic}", request.Topic);

                var result = await _producerService.SendMessageAsync(request.Topic, request.Message, request.Tag);

                return Ok(new
                {
                    Success = result,
                    Message = result ? "消息发送成功" : "消息发送失败",
                    Topic = request.Topic,
                    Tag = request.Tag,
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送消息时发生错误");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"发送消息失败: {ex.Message}",
                    Error = ex.ToString()
                });
            }
        }

        [HttpPost("send-delayed-message")]
        public async Task<IActionResult> SendDelayedMessage([FromBody] SendDelayedMessageRequest request)
        {
            try
            {
                _logger.LogInformation("准备发送延迟消息到主题: {Topic}, 延迟级别: {DelayLevel}", request.Topic, request.DelayLevel);

                var result = await _producerService.SendDelayedMessageAsync(request.Topic, request.Message, request.DelayLevel);

                return Ok(new
                {
                    Success = result,
                    Message = result ? "延迟消息发送成功" : "延迟消息发送失败",
                    Topic = request.Topic,
                    DelayLevel = request.DelayLevel,
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送延迟消息时发生错误");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"发送延迟消息失败: {ex.Message}",
                    Error = ex.ToString()
                });
            }
        }

        [HttpPost("send-orderly-message")]
        public async Task<IActionResult> SendOrderlyMessage([FromBody] SendOrderlyMessageRequest request)
        {
            try
            {
                _logger.LogInformation("准备发送顺序消息到主题: {Topic}, 哈希键: {HashKey}", request.Topic, request.HashKey);

                var result = await _producerService.SendOrderlyMessageAsync(request.Topic, request.Message, request.HashKey);

                return Ok(new
                {
                    Success = result,
                    Message = result ? "顺序消息发送成功" : "顺序消息发送失败",
                    Topic = request.Topic,
                    HashKey = request.HashKey,
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送顺序消息时发生错误");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"发送顺序消息失败: {ex.Message}",
                    Error = ex.ToString()
                });
            }
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> SubscribeTopic([FromBody] SubscribeRequest request)
        {
            try
            {
                _logger.LogInformation("准备订阅主题: {Topic}, 标签: {Tag}", request.Topic, request.Tag);

                var result = await _consumerService.SubscribeAsync(request.Topic, request.Tag);

                return Ok(new
                {
                    Success = result,
                    Message = result ? "主题订阅成功" : "主题订阅失败",
                    Topic = request.Topic,
                    Tag = request.Tag,
                    SubscribedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订阅主题时发生错误");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"订阅主题失败: {ex.Message}",
                    Error = ex.ToString()
                });
            }
        }

        [HttpPost("send-test-messages")]
        public async Task<IActionResult> SendTestMessages()
        {
            try
            {
                var results = new List<object>();

                // 发送用户事件消息
                var userMessage = "{\"userId\":\"123\",\"action\":\"register\",\"timestamp\":\"" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\"}";
                var userResult = await _producerService.SendMessageAsync("scrm_user_events", userMessage, "register");
                results.Add(new { Type = "UserEvent", Topic = "scrm_user_events", Success = userResult });

                // 发送订单事件消息
                var orderMessage = "{\"orderId\":\"ORD-001\",\"amount\":299.99,\"status\":\"created\",\"timestamp\":\"" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\"}";
                var orderResult = await _producerService.SendMessageAsync("scrm_order_events", orderMessage, "created");
                results.Add(new { Type = "OrderEvent", Topic = "scrm_order_events", Success = orderResult });

                // 发送通知事件消息
                var notificationMessage = "{\"userId\":\"123\",\"type\":\"email\",\"title\":\"Welcome\",\"content\":\"Welcome to SCRM!\",\"timestamp\":\"" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\"}";
                var notificationResult = await _producerService.SendMessageAsync("scrm_notification_events", notificationMessage, "email");
                results.Add(new { Type = "NotificationEvent", Topic = "scrm_notification_events", Success = notificationResult });

                // 发送延迟消息
                var delayedMessage = "{\"message\":\"This is a delayed message\",\"timestamp\":\"" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\"}";
                var delayedResult = await _producerService.SendDelayedMessageAsync("scrm_notification_events", delayedMessage, 3);
                results.Add(new { Type = "DelayedMessage", Topic = "scrm_notification_events", Success = delayedResult });

                return Ok(new
                {
                    Message = "测试消息发送完成",
                    TotalMessages = results.Count,
                    SuccessCount = results.Count(r => ((dynamic)r).Success),
                    Results = results,
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送测试消息时发生错误");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"发送测试消息失败: {ex.Message}",
                    Error = ex.ToString()
                });
            }
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                Service = "RocketMQ Test API",
                Status = "Running",
                Timestamp = DateTime.UtcNow,
                AvailableTopics = new[] { "scrm_user_events", "scrm_order_events", "scrm_notification_events" },
                Endpoints = new
                {
                    SendMessage = "POST /api/rocketmqtest/send-message",
                    SendDelayedMessage = "POST /api/rocketmqtest/send-delayed-message",
                    SendOrderlyMessage = "POST /api/rocketmqtest/send-orderly-message",
                    SubscribeTopic = "POST /api/rocketmqtest/subscribe",
                    SendTestMessages = "POST /api/rocketmqtest/send-test-messages",
                    GetStatus = "GET /api/rocketmqtest/status"
                }
            });
        }
    }

    public class SendMessageRequest
    {
        public string Topic { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Tag { get; set; }
    }

    public class SendDelayedMessageRequest : SendMessageRequest
    {
        public int DelayLevel { get; set; } = 1;
    }

    public class SendOrderlyMessageRequest : SendMessageRequest
    {
        public string HashKey { get; set; } = string.Empty;
    }

    public class SubscribeRequest
    {
        public string Topic { get; set; } = string.Empty;
        public string? Tag { get; set; } = "*";
    }
}