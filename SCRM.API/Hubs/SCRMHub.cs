using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace SCRM.Hubs
{
    public class SCRMHub : Hub
    {
        private readonly ILogger<SCRMHub> _logger;

        public SCRMHub(ILogger<SCRMHub> logger)
        {
            _logger = logger;
        }

        // 客户端连接时调用
        public override async Task OnConnectedAsync()
        {
            var user = Context.GetHttpContext()?.Request.Query["userId"].ToString();
            var deviceType = Context.GetHttpContext()?.Request.Query["deviceType"].ToString();

            if (string.IsNullOrEmpty(user))
            {
                user = Context.ConnectionId;
            }

            _logger.LogInformation("客户端连接 - 连接ID: {ConnectionId}, 用户ID: {UserId}, 设备类型: {DeviceType}",
                Context.ConnectionId, user, deviceType);

            // 将用户添加到组
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{user}");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Device_{deviceType}");

            // 向连接用户发送欢迎消息
            await Clients.Caller.SendAsync("Welcome", new
            {
                Message = "欢迎使用 SCRM 实时通信服务",
                ConnectionId = Context.ConnectionId,
                UserId = user,
                DeviceType = deviceType,
                ServerTime = DateTime.UtcNow
            });

            // 通知其他用户该用户上线
            await Clients.OthersInGroup($"Device_{deviceType}").SendAsync("UserOnline", new
            {
                UserId = user,
                DeviceType = deviceType,
                ConnectionTime = DateTime.UtcNow
            });

            await base.OnConnectedAsync();
        }

        // 客户端断开连接时调用
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var user = Context.GetHttpContext()?.Request.Query["userId"].ToString();
            var deviceType = Context.GetHttpContext()?.Request.Query["deviceType"].ToString();

            if (string.IsNullOrEmpty(user))
            {
                user = Context.ConnectionId;
            }

            _logger.LogInformation("客户端断开连接 - 连接ID: {ConnectionId}, 用户ID: {UserId}, 设备类型: {DeviceType}",
                Context.ConnectionId, user, deviceType);

            // 从组中移除用户
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{user}");
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Device_{deviceType}");

            // 通知其他用户该用户离线
            await Clients.OthersInGroup($"Device_{deviceType}").SendAsync("UserOffline", new
            {
                UserId = user,
                DeviceType = deviceType,
                DisconnectTime = DateTime.UtcNow
            });

            await base.OnDisconnectedAsync(exception);
        }

        // 注册用户身份
        public async Task RegisterUser(string userId, string deviceType, string deviceInfo = "")
        {
            _logger.LogInformation("用户注册 - 用户ID: {UserId}, 设备类型: {DeviceType}, 设备信息: {DeviceInfo}, 连接ID: {ConnectionId}",
                userId, deviceType, deviceInfo, Context.ConnectionId);

            // 将用户添加到相关组
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Device_{deviceType}");
            await Groups.AddToGroupAsync(Context.ConnectionId, "AllUsers");

            // 发送注册成功确认
            await Clients.Caller.SendAsync("RegisterSuccess", new
            {
                UserId = userId,
                DeviceType = deviceType,
                ConnectionId = Context.ConnectionId,
                RegisteredAt = DateTime.UtcNow
            });
        }

        // 发送消息到特定用户
        public async Task SendMessageToUser(string targetUserId, object message)
        {
            var senderId = Context.GetHttpContext()?.Request.Query["userId"].ToString() ?? Context.ConnectionId;

            _logger.LogInformation("发送消息到用户 - 发送者: {SenderId}, 接收者: {TargetUserId}, 消息: {Message}",
                senderId, targetUserId, message);

            await Clients.Group($"User_{targetUserId}").SendAsync("ReceiveMessage", new
            {
                FromUserId = senderId,
                ToUserId = targetUserId,
                Message = message,
                Timestamp = DateTime.UtcNow,
                MessageType = "DirectMessage"
            });
        }

        // 发送消息到特定设备类型
        public async Task SendMessageToDeviceType(string deviceType, object message)
        {
            var senderId = Context.GetHttpContext()?.Request.Query["userId"].ToString() ?? Context.ConnectionId;

            _logger.LogInformation("发送消息到设备类型 - 发送者: {SenderId}, 设备类型: {DeviceType}, 消息: {Message}",
                senderId, deviceType, message);

            await Clients.Group($"Device_{deviceType}").SendAsync("ReceiveMessage", new
            {
                FromUserId = senderId,
                DeviceType = deviceType,
                Message = message,
                Timestamp = DateTime.UtcNow,
                MessageType = "BroadcastToDeviceType"
            });
        }

        // 发送消息到所有用户
        public async Task SendMessageToAll(object message)
        {
            var senderId = Context.GetHttpContext()?.Request.Query["userId"].ToString() ?? Context.ConnectionId;

            _logger.LogInformation("发送广播消息 - 发送者: {SenderId}, 消息: {Message}",
                senderId, message);

            await Clients.Group("AllUsers").SendAsync("ReceiveMessage", new
            {
                FromUserId = senderId,
                Message = message,
                Timestamp = DateTime.UtcNow,
                MessageType = "BroadcastToAll"
            });
        }

        // 加入房间/群组
        public async Task JoinRoom(string roomId)
        {
            var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString() ?? Context.ConnectionId;

            _logger.LogInformation("用户加入房间 - 用户ID: {UserId}, 房间ID: {RoomId}, 连接ID: {ConnectionId}",
                userId, roomId, Context.ConnectionId);

            await Groups.AddToGroupAsync(Context.ConnectionId, $"Room_{roomId}");

            // 通知房间内其他用户有新用户加入
            await Clients.OthersInGroup($"Room_{roomId}").SendAsync("UserJoinedRoom", new
            {
                UserId = userId,
                RoomId = roomId,
                JoinTime = DateTime.UtcNow
            });

            // 发送加入成功确认
            await Clients.Caller.SendAsync("JoinRoomSuccess", new
            {
                RoomId = roomId,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            });
        }

        // 离开房间/群组
        public async Task LeaveRoom(string roomId)
        {
            var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString() ?? Context.ConnectionId;

            _logger.LogInformation("用户离开房间 - 用户ID: {UserId}, 房间ID: {RoomId}, 连接ID: {ConnectionId}",
                userId, roomId, Context.ConnectionId);

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Room_{roomId}");

            // 通知房间内其他用户有用户离开
            await Clients.OthersInGroup($"Room_{roomId}").SendAsync("UserLeftRoom", new
            {
                UserId = userId,
                RoomId = roomId,
                LeaveTime = DateTime.UtcNow
            });
        }

        // 发送消息到房间
        public async Task SendMessageToRoom(string roomId, object message)
        {
            var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString() ?? Context.ConnectionId;

            _logger.LogInformation("发送房间消息 - 用户ID: {UserId}, 房间ID: {RoomId}, 消息: {Message}",
                userId, roomId, message);

            await Clients.Group($"Room_{roomId}").SendAsync("ReceiveRoomMessage", new
            {
                FromUserId = userId,
                RoomId = roomId,
                Message = message,
                Timestamp = DateTime.UtcNow,
                MessageType = "RoomMessage"
            });
        }

        // 发送心跳包
        public async Task SendHeartbeat()
        {
            var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString() ?? Context.ConnectionId;

            await Clients.Caller.SendAsync("HeartbeatResponse", new
            {
                UserId = userId,
                ServerTime = DateTime.UtcNow,
                ConnectionId = Context.ConnectionId
            });
        }

        // 客户端调用：获取在线用户列表
        public async Task GetOnlineUsers(string deviceType = "")
        {
            // 这里应该从连接管理服务获取实际的用户列表
            // 由于示例，我们发送一个模拟响应
            await Clients.Caller.SendAsync("OnlineUsersList", new
            {
                DeviceType = deviceType,
                Users = new object[0], // 实际应该返回真实的用户列表
                Timestamp = DateTime.UtcNow
            });
        }
    }
}