using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SCRM.Hubs;
using SCRM.Services;
using System;
using System.Threading.Tasks;

namespace SCRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SignalRTestController : ControllerBase
    {
        private readonly ISignalRMessageService _signalRMessageService;
        private readonly IConnectionManager _connectionManager;
        private readonly IRocketMQProducerService _rocketMQProducer;
        private readonly IHubContext<SCRMHub> _hubContext;

        public SignalRTestController(
            ISignalRMessageService signalRMessageService,
            IConnectionManager connectionManager,
            IRocketMQProducerService rocketMQProducer,
            IHubContext<SCRMHub> hubContext)
        {
            _signalRMessageService = signalRMessageService;
            _connectionManager = connectionManager;
            _rocketMQProducer = rocketMQProducer;
            _hubContext = hubContext;
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                Service = "SignalR Test API",
                Status = "Running",
                Timestamp = DateTime.UtcNow,
                AvailableEndpoints = new
                {
                    GetStatus = "GET /api/signalrtest/status",
                    GetConnections = "GET /api/signalrtest/connections",
                    SendMessageToAll = "POST /api/signalrtest/send-all",
                    SendMessageToUser = "POST /api/signalrtest/send-user",
                    SendRocketMQMessage = "POST /api/signalrtest/send-rocketmq",
                    GetStatistics = "GET /api/signalrtest/statistics"
                },
                SignalRHub = "/scrmhub"
            });
        }

        [HttpGet("connections")]
        public async Task<IActionResult> GetConnections()
        {
            try
            {
                var connections = await _connectionManager.GetAllConnectionsAsync();
                var onlineCount = await _connectionManager.GetOnlineUserCountAsync();

                return Ok(new
                {
                    TotalConnections = connections.Count(),
                    OnlineUsers = onlineCount,
                    Connections = connections.Select(c => new
                    {
                        c.UserId,
                        c.ConnectionId,
                        c.DeviceType,
                        c.DeviceInfo,
                        c.ConnectedAt,
                        c.LastActivityAt,
                        c.IsOnline
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("send-all")]
        public async Task<IActionResult> SendMessageToAll([FromBody] SignalRSendMessageRequest request)
        {
            try
            {
                await _signalRMessageService.SendMessageToAllAsync(request.Message, request.MessageType);

                return Ok(new
                {
                    Success = true,
                    Message = "消息已发送到所有客户端",
                    MessageType = request.MessageType,
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("send-user")]
        public async Task<IActionResult> SendMessageToUser([FromBody] SignalRSendUserMessageRequest request)
        {
            try
            {
                await _signalRMessageService.SendMessageToUserAsync(request.UserId, request.Message, request.MessageType);

                return Ok(new
                {
                    Success = true,
                    Message = $"消息已发送到用户 {request.UserId}",
                    UserId = request.UserId,
                    MessageType = request.MessageType,
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("send-device")]
        public async Task<IActionResult> SendMessageToDeviceType([FromBody] SignalRSendDeviceMessageRequest request)
        {
            try
            {
                await _signalRMessageService.SendMessageToDeviceTypeAsync(request.DeviceType, request.Message, request.MessageType);

                return Ok(new
                {
                    Success = true,
                    Message = $"消息已发送到设备类型 {request.DeviceType}",
                    DeviceType = request.DeviceType,
                    MessageType = request.MessageType,
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("send-rocketmq")]
        public async Task<IActionResult> SendRocketMQMessage([FromBody] SignalRSendRocketMQRequest request)
        {
            try
            {
                bool success;

                switch (request.MessageType.ToLower())
                {
                    case "delayed":
                        success = await _rocketMQProducer.SendDelayedMessageAsync(request.Topic, request.Message, request.DelayLevel ?? 3);
                        break;
                    case "orderly":
                        success = await _rocketMQProducer.SendOrderlyMessageAsync(request.Topic, request.Message, request.HashKey ?? "default");
                        break;
                    default:
                        success = await _rocketMQProducer.SendMessageAsync(request.Topic, request.Message, request.Tag);
                        break;
                }

                if (success)
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = "RocketMQ 消息发送成功，将通过 SignalR 转发",
                        Topic = request.Topic,
                        Tag = request.Tag,
                        MessageType = request.MessageType,
                        SentAt = DateTime.UtcNow
                    });
                }
                else
                {
                    return BadRequest(new { Success = false, Message = "RocketMQ 消息发送失败" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("send-test-events")]
        public async Task<IActionResult> SendTestEvents()
        {
            try
            {
                var results = new List<object>();

                // 发送用户事件
                var userEventMessage = "{\"userId\":\"123\",\"action\":\"login\",\"timestamp\":\"" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\"}";
                var userResult = await _rocketMQProducer.SendMessageAsync("scrm_user_events", userEventMessage, "login");
                results.Add(new { Type = "UserEvent", Topic = "scrm_user_events", Success = userResult });

                // 发送订单事件
                var orderEventMessage = "{\"orderId\":\"ORD-999\",\"amount\":199.99,\"status\":\"paid\",\"timestamp\":\"" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\"}";
                var orderResult = await _rocketMQProducer.SendMessageAsync("scrm_order_events", orderEventMessage, "paid");
                results.Add(new { Type = "OrderEvent", Topic = "scrm_order_events", Success = orderResult });

                // 发送通知事件
                var notificationEventMessage = "{\"userId\":\"123\",\"type\":\"push\",\"title\":\"Order Confirmation\",\"content\":\"Your order has been confirmed!\",\"timestamp\":\"" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\"}";
                var notificationResult = await _rocketMQProducer.SendMessageAsync("scrm_notification_events", notificationEventMessage, "push");
                results.Add(new { Type = "NotificationEvent", Topic = "scrm_notification_events", Success = notificationResult });

                // 发送延迟通知
                var delayedNotificationMessage = "{\"type\":\"reminder\",\"title\":\"Follow Up\",\"content\":\"Don't forget to check your order status\",\"timestamp\":\"" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\"}";
                var delayedResult = await _rocketMQProducer.SendDelayedMessageAsync("scrm_notification_events", delayedNotificationMessage, 3);
                results.Add(new { Type = "DelayedNotification", Topic = "scrm_notification_events", Success = delayedResult });

                var totalSuccess = results.Count(r => ((dynamic)r).Success);

                return Ok(new
                {
                    Message = "测试事件发送完成",
                    TotalEvents = results.Count,
                    SuccessCount = totalSuccess,
                    Results = results,
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var allConnections = await _connectionManager.GetAllConnectionsAsync();
                var onlineUsers = await _connectionManager.GetOnlineUserCountAsync();

                // 获取连接管理器统计信息（如果实现了）
                ConnectionStatistics? stats = null;
                if (_connectionManager is ConnectionManager connManager)
                {
                    stats = connManager.GetStatistics();
                }

                var deviceTypeStats = allConnections
                    .GroupBy(c => c.DeviceType)
                    .ToDictionary(g => g.Key, g => g.Count());

                return Ok(new
                {
                    TotalConnections = allConnections.Count(),
                    OnlineUsers = onlineUsers,
                    DeviceTypeStatistics = deviceTypeStats,
                    DetailedStatistics = stats,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("send-heartbeat/{userId}")]
        public async Task<IActionResult> SendHeartbeat(string userId)
        {
            try
            {
                await _signalRMessageService.SendHeartbeatResponseAsync(userId);

                return Ok(new
                {
                    Success = true,
                    UserId = userId,
                    Message = "心跳响应已发送",
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
    }

    public class SignalRSendMessageRequest
    {
        public object Message { get; set; } = new { };
        public string MessageType { get; set; } = "";
    }

    public class SignalRSendUserMessageRequest : SignalRSendMessageRequest
    {
        public string UserId { get; set; } = string.Empty;
    }

    public class SignalRSendDeviceMessageRequest : SignalRSendMessageRequest
    {
        public string DeviceType { get; set; } = string.Empty;
    }

    public class SignalRSendRocketMQRequest
    {
        public string Topic { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Tag { get; set; }
        public string MessageType { get; set; } = "normal"; // normal, delayed, orderly
        public int? DelayLevel { get; set; }
        public string? HashKey { get; set; }
    }
}