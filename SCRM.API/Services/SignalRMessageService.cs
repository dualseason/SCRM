using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SCRM.Hubs;
using System;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public class SignalRMessageService : ISignalRMessageService
    {
        private readonly IHubContext<SCRMHub> _hubContext;
        private readonly IConnectionManager _connectionManager;
        private readonly ILogger<SignalRMessageService> _logger;

        public SignalRMessageService(
            IHubContext<SCRMHub> hubContext,
            IConnectionManager connectionManager,
            ILogger<SignalRMessageService> logger)
        {
            _hubContext = hubContext;
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public async Task SendMessageToUserAsync(string userId, object message, string messageType = "")
        {
            try
            {
                await _hubContext.Clients.Group($"User_{userId}").SendAsync("ReceiveMessage", new
                {
                    FromUserId = "Server",
                    ToUserId = userId,
                    Message = message,
                    MessageType = messageType ?? "ServerMessage",
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("消息已发送到用户 - 用户ID: {UserId}, 消息类型: {MessageType}", userId, messageType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送消息到用户失败 - 用户ID: {UserId}, 消息类型: {MessageType}", userId, messageType);
            }
        }

        public async Task SendMessageToDeviceTypeAsync(string deviceType, object message, string messageType = "")
        {
            try
            {
                await _hubContext.Clients.Group($"Device_{deviceType}").SendAsync("ReceiveMessage", new
                {
                    FromUserId = "Server",
                    DeviceType = deviceType,
                    Message = message,
                    MessageType = messageType ?? "BroadcastToDeviceType",
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("消息已发送到设备类型 - 设备类型: {DeviceType}, 消息类型: {MessageType}", deviceType, messageType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送消息到设备类型失败 - 设备类型: {DeviceType}, 消息类型: {MessageType}", deviceType, messageType);
            }
        }

        public async Task SendMessageToAllAsync(object message, string messageType = "")
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", new
                {
                    FromUserId = "Server",
                    Message = message,
                    MessageType = messageType ?? "BroadcastToAll",
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("广播消息已发送 - 消息类型: {MessageType}", messageType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送广播消息失败 - 消息类型: {MessageType}", messageType);
            }
        }

        public async Task SendMessageToRoomAsync(string roomId, object message, string messageType = "")
        {
            try
            {
                await _hubContext.Clients.Group($"Room_{roomId}").SendAsync("ReceiveRoomMessage", new
                {
                    FromUserId = "Server",
                    RoomId = roomId,
                    Message = message,
                    MessageType = messageType ?? "RoomMessage",
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("房间消息已发送 - 房间ID: {RoomId}, 消息类型: {MessageType}", roomId, messageType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送房间消息失败 - 房间ID: {RoomId}, 消息类型: {MessageType}", roomId, messageType);
            }
        }

        public async Task NotifyUserOnlineAsync(string userId, string deviceType)
        {
            try
            {
                // 简化实现：发送到整个设备类型组，让客户端自己过滤
                await _hubContext.Clients.Group($"Device_{deviceType}").SendAsync("UserOnline", new
                {
                    UserId = userId,
                    DeviceType = deviceType,
                    ConnectionTime = DateTime.UtcNow,
                    ExcludeSelf = true // 标识让客户端忽略自己的通知
                });

                _logger.LogInformation("用户上线通知已发送 - 用户ID: {UserId}, 设备类型: {DeviceType}", userId, deviceType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送用户上线通知失败 - 用户ID: {UserId}, 设备类型: {DeviceType}", userId, deviceType);
            }
        }

        public async Task NotifyUserOfflineAsync(string userId, string deviceType)
        {
            try
            {
                // 简化实现：发送到整个设备类型组，让客户端自己过滤
                await _hubContext.Clients.Group($"Device_{deviceType}").SendAsync("UserOffline", new
                {
                    UserId = userId,
                    DeviceType = deviceType,
                    DisconnectTime = DateTime.UtcNow,
                    ExcludeSelf = true // 标识让客户端忽略自己的通知
                });

                _logger.LogInformation("用户离线通知已发送 - 用户ID: {UserId}, 设备类型: {DeviceType}", userId, deviceType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送用户离线通知失败 - 用户ID: {UserId}, 设备类型: {DeviceType}", userId, deviceType);
            }
        }

        public async Task SendHeartbeatResponseAsync(string userId)
        {
            try
            {
                var userConnections = await _connectionManager.GetConnectionsByUserAsync(userId);

                foreach (var connection in userConnections)
                {
                    await _hubContext.Clients.Client(connection.ConnectionId).SendAsync("HeartbeatResponse", new
                    {
                        UserId = userId,
                        ServerTime = DateTime.UtcNow,
                        ConnectionId = connection.ConnectionId,
                        IsOnline = true
                    });
                }

                _logger.LogInformation("心跳响应已发送 - 用户ID: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送心跳响应失败 - 用户ID: {UserId}", userId);
            }
        }
    }
}