using Microsoft.AspNetCore.Mvc;
using SCRM.Services;
using System;
using System.Threading.Tasks;

namespace SCRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NettyTestController : ControllerBase
    {
        private readonly INettyMessageService _nettyMessageService;
        private readonly ISignalRMessageService _signalRMessageService;
        private readonly IRocketMQProducerService _rocketMQProducer;

        public NettyTestController(
            INettyMessageService nettyMessageService,
            ISignalRMessageService signalRMessageService,
            IRocketMQProducerService rocketMQProducer)
        {
            _nettyMessageService = nettyMessageService;
            _signalRMessageService = signalRMessageService;
            _rocketMQProducer = rocketMQProducer;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var nettyRunning = await _nettyMessageService.IsNettyServerRunningAsync();
                var clientCount = await _nettyMessageService.GetConnectedClientsCountAsync();

                return Ok(new
                {
                    Service = "Netty Test API",
                    Status = "Running",
                    NettyServerRunning = nettyRunning,
                    NettyPort = ((INettyService)_nettyMessageService).Port,
                    ConnectedClients = clientCount,
                    Timestamp = DateTime.UtcNow,
                    AvailableEndpoints = new
                    {
                        GetStatus = "GET /api/nettytest/status",
                        SendMessage = "POST /api/nettytest/send-message",
                        SendToSignalR = "POST /api/nettytest/send-to-signalr",
                        SendToRocketMQ = "POST /api/nettytest/send-to-rocketmq",
                        TestIntegration = "POST /api/nettytest/test-integration"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("send-message")]
        public async Task<IActionResult> SendMessageToNetty([FromBody] NettySendRequest request)
        {
            try
            {
                var success = await _nettyMessageService.SendMessageToNettyAsync(
                    request.Message,
                    request.MessageType,
                    request.TargetId,
                    request.TargetType,
                    request.Topic,
                    request.Tag);

                return Ok(new
                {
                    Success = success,
                    Message = success ? "消息已通过 Netty 发送" : "Netty 消息发送失败",
                    MessageType = request.MessageType,
                    TargetId = request.TargetId,
                    TargetType = request.TargetType,
                    Topic = request.Topic,
                    Tag = request.Tag,
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("send-to-signalr")]
        public async Task<IActionResult> SendToSignalR([FromBody] NettySignalRRequest request)
        {
            try
            {
                // 首先通过 Netty 发送消息
                var nettySuccess = await _nettyMessageService.SendMessageToNettyAsync(
                    request.Message,
                    "signalr",
                    request.TargetId,
                    request.TargetType);

                // 然后直接通过 SignalR 发送（双重发送确保可靠）
                switch (request.TargetType?.ToLower())
                {
                    case "all":
                        await _signalRMessageService.SendMessageToAllAsync(request.Message, request.MessageType ?? "NettySignalR");
                        break;
                    case "user":
                        if (!string.IsNullOrEmpty(request.TargetId))
                        {
                            await _signalRMessageService.SendMessageToUserAsync(request.TargetId, request.Message, request.MessageType ?? "NettySignalR");
                        }
                        break;
                    case "device":
                        if (!string.IsNullOrEmpty(request.TargetId))
                        {
                            await _signalRMessageService.SendMessageToDeviceTypeAsync(request.TargetId, request.Message, request.MessageType ?? "NettySignalR");
                        }
                        break;
                    case "room":
                        if (!string.IsNullOrEmpty(request.TargetId))
                        {
                            await _signalRMessageService.SendMessageToRoomAsync(request.TargetId, request.Message, request.MessageType ?? "NettySignalR");
                        }
                        break;
                }

                return Ok(new
                {
                    Success = true,
                    Message = "消息已通过 Netty 和 SignalR 双重发送",
                    MessageType = request.MessageType,
                    TargetId = request.TargetId,
                    TargetType = request.TargetType,
                    NettySuccess = nettySuccess,
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("send-to-rocketmq")]
        public async Task<IActionResult> SendToRocketMQ([FromBody] NettyRocketMQRequest request)
        {
            try
            {
                // 通过 Netty 发送消息到 RocketMQ
                var nettySuccess = await _nettyMessageService.SendMessageToNettyAsync(
                    request.Message,
                    "rocketmq",
                    "",
                    "",
                    request.Topic,
                    request.Tag);

                // 也直接通过 RocketMQ 发送（双重发送）
                bool rocketMQSuccess;
                if (request.DelayLevel.HasValue)
                {
                    rocketMQSuccess = await _rocketMQProducer.SendDelayedMessageAsync(
                        request.Topic,
                        request.Message?.ToString() ?? "",
                        request.DelayLevel.Value);
                }
                else if (!string.IsNullOrEmpty(request.HashKey))
                {
                    rocketMQSuccess = await _rocketMQProducer.SendOrderlyMessageAsync(
                        request.Topic,
                        request.Message?.ToString() ?? "",
                        request.HashKey);
                }
                else
                {
                    rocketMQSuccess = await _rocketMQProducer.SendMessageAsync(
                        request.Topic,
                        request.Message?.ToString() ?? "",
                        request.Tag);
                }

                return Ok(new
                {
                    Success = nettySuccess && rocketMQSuccess,
                    Message = "消息已通过 Netty 和 RocketMQ 双重发送",
                    Topic = request.Topic,
                    Tag = request.Tag,
                    MessageType = request.MessageType,
                    DelayLevel = request.DelayLevel,
                    HashKey = request.HashKey,
                    NettySuccess = nettySuccess,
                    RocketMQSuccess = rocketMQSuccess,
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("test-integration")]
        public async Task<IActionResult> TestIntegration()
        {
            try
            {
                var results = new List<object>();

                // 1. 测试 Netty 到 SignalR
                var nettyToSignalRMsg = new { Test = "Netty to SignalR", From = "API", Timestamp = DateTime.UtcNow };
                var nettyToSignalRSuccess = await _nettyMessageService.SendMessageToNettyAsync(
                    nettyToSignalRMsg,
                    "signalr",
                    "",
                    "all");

                results.Add(new
                {
                    Test = "Netty to SignalR",
                    Success = nettyToSignalRSuccess,
                    Message = nettyToSignalRMsg
                });

                // 2. 测试 Netty 到 RocketMQ
                var nettyToRocketMQMsg = new { Test = "Netty to RocketMQ", From = "API", Timestamp = DateTime.UtcNow };
                var nettyToRocketMQSuccess = await _nettyMessageService.SendMessageToNettyAsync(
                    nettyToRocketMQMsg,
                    "rocketmq",
                    "",
                    "",
                    "scrm_integration_test",
                    "test");

                results.Add(new
                {
                    Test = "Netty to RocketMQ",
                    Success = nettyToRocketMQSuccess,
                    Message = nettyToRocketMQMsg
                });

                // 3. 测试双重发送到所有系统
                var broadcastMsg = new { Test = "Triple Broadcast", From = "API", Timestamp = DateTime.UtcNow };
                Task broadcastToSignalRTask = _signalRMessageService.SendMessageToAllAsync(broadcastMsg, "TripleBroadcast");
                Task<bool> broadcastToRocketMQTask = _rocketMQProducer.SendMessageAsync("scrm_integration_test", System.Text.Json.JsonSerializer.Serialize(broadcastMsg), "triple_broadcast");
                Task<bool> broadcastToNettyTask = _nettyMessageService.SendMessageToNettyAsync(broadcastMsg, "broadcast");

                await Task.WhenAll(broadcastToSignalRTask, broadcastToRocketMQTask, broadcastToNettyTask);

                var broadcastToSignalRSuccess = broadcastToSignalRTask.IsCompletedSuccessfully;
                var broadcastToRocketMQSuccess = broadcastToRocketMQTask.Result;
                var broadcastToNettySuccess = broadcastToNettyTask.Result;

                results.Add(new
                {
                    Test = "Triple Broadcast (All Systems)",
                    Success = broadcastToSignalRSuccess && broadcastToRocketMQSuccess && broadcastToNettySuccess,
                    SignalRSuccess = broadcastToSignalRSuccess,
                    RocketMQSuccess = broadcastToRocketMQSuccess,
                    NettySuccess = broadcastToNettySuccess,
                    Message = broadcastMsg
                });

                var totalTests = results.Count;
                var successCount = results.Count(r => (bool)((dynamic)r).Success);

                return Ok(new
                {
                    Message = "Netty 集成测试完成",
                    TotalTests = totalTests,
                    SuccessCount = successCount,
                    Results = results,
                    SuccessRate = totalTests > 0 ? (double)successCount / totalTests * 100 : 0,
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("test-tcp-client")]
        public async Task<IActionResult> TestTcpClientConnection([FromBody] TcpClientRequest request)
        {
            try
            {
                using var tcpClient = new System.Net.Sockets.TcpClient();
                var port = request.Port.HasValue ? request.Port.Value : 8081;
                await tcpClient.ConnectAsync(request.Host ?? "localhost", port);

                var message = new
                {
                    Type = "tcp_test",
                    Content = request.Message ?? "Hello from TCP client",
                    Timestamp = DateTime.UtcNow
                };

                string jsonMessage = System.Text.Json.JsonSerializer.Serialize(message);
                byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(jsonMessage);

                var stream = tcpClient.GetStream();
                await stream.WriteAsync(messageBytes, 0, messageBytes.Length);

                // 读取响应
                var buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string response = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);

                tcpClient.Close();

                return Ok(new
                {
                    Success = true,
                    Host = request.Host,
                    Port = request.Port,
                    RequestMessage = jsonMessage,
                    Response = response,
                    ConnectedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("server-stats")]
        public async Task<IActionResult> GetServerStats()
        {
            try
            {
                var nettyRunning = await _nettyMessageService.IsNettyServerRunningAsync();
                var nettyPort = ((INettyService)_nettyMessageService).Port;
                var clientCount = await _nettyMessageService.GetConnectedClientsCountAsync();

                return Ok(new
                {
                    NettyServer = new
                    {
                        Running = nettyRunning,
                        Port = nettyPort,
                        ConnectedClients = clientCount
                    },
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
    }

    // 请求模型类
    public class NettySendRequest
    {
        public object Message { get; set; } = new { };
        public string MessageType { get; set; } = "";
        public string TargetId { get; set; } = "";
        public string TargetType { get; set; } = "";
        public string Topic { get; set; } = "";
        public string? Tag { get; set; }
    }

    public class NettySignalRRequest
    {
        public object Message { get; set; } = new { };
        public string MessageType { get; set; } = "";
        public string TargetId { get; set; } = "";
        public string TargetType { get; set; } = "";
    }

    public class NettyRocketMQRequest
    {
        public object Message { get; set; } = new { };
        public string Topic { get; set; } = "";
        public string? Tag { get; set; }
        public string MessageType { get; set; } = "normal";
        public int? DelayLevel { get; set; }
        public string? HashKey { get; set; }
    }

    public class TcpClientRequest
    {
        public string? Host { get; set; }
        public int? Port { get; set; } = 8081;
        public string? Message { get; set; }
    }
}