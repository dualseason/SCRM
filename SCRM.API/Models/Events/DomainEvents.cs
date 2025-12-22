using System;

namespace SCRM.API.Models.Events
{
    // --- Domain Events (领域事件) ---

    // 设备已连接事件
    public class DeviceConnectedEvent
    {
        public string UserId { get; set; } // 设备所属的账号ID
        public string ConnectionId { get; set; } // TCP连接ID (Netty ChannelId.AsLongText)
        public string DeviceType { get; set; } // 设备类型 (如 Android, iOS)
        public string OwnerId { get; set; } // 该设备的管理员ID (用于通知目标)
        public DateTime Timestamp { get; set; } // 事件发生时间

        public DeviceConnectedEvent(string userId, string connectionId, string deviceType, string ownerId)
        {
            UserId = userId;
            ConnectionId = connectionId;
            DeviceType = deviceType;
            OwnerId = ownerId;
            Timestamp = DateTime.UtcNow;
        }
    }

    // 消息已接收事件
    // 用于解耦 Netty 接收层和 SignalR 推送层
    public class MessageReceivedEvent
    {
        public string DeviceUuid { get; set; } // 接收消息的设备唯一标识 (UUID)
        public object Message { get; set; } // 消息内容载体 (这里传输的是 DTO 对象)
        public string OwnerId { get; set; } // 设备所属的管理员ID (用于权限控制/定向推送)
        public DateTime Timestamp { get; set; }

        public MessageReceivedEvent(string deviceUuid, object message, string ownerId)
        {
            DeviceUuid = deviceUuid;
            Message = message;
            OwnerId = ownerId;
            Timestamp = DateTime.UtcNow;
        }
    }

    // 接收联系人列表事件
    public class ContactsReceivedEvent
    {
        public string DeviceUuid { get; set; }
        public long AccountId { get; set; }
        public string OwnerId { get; set; }

        public ContactsReceivedEvent(string deviceUuid, long accountId, string ownerId)
        {
            DeviceUuid = deviceUuid;
            AccountId = accountId;
            OwnerId = ownerId;
        }
    }

    // 好友添加请求事件
    public class FriendRequestEvent
    {
        public string WeChatId { get; set; } // 我们的账号
        public string FriendId { get; set; } // 对方的Wxid
        public string FriendNick { get; set; }
        public string Reason { get; set; }
        public string ConnectionId { get; set; } // 当前连接ID 用于回发任务
        public long AccountId { get; set; }      // 我们的数据库DB ID

        public FriendRequestEvent(string weChatId, string friendId, string friendNick, string reason, string connectionId, long accountId)
        {
            WeChatId = weChatId;
            FriendId = friendId;
            FriendNick = friendNick;
            Reason = reason;
            ConnectionId = connectionId;
            AccountId = accountId;
        }
    }

    // 朋友圈新发布事件
    public class CircleNewPublishEvent
    {
        public string WeChatId { get; set; } // 我们的账号
        public string AuthorId { get; set; } // 发布者Wxid
        public long CircleId { get; set; }   // 朋友圈ID
        public string Content { get; set; }
        public string ConnectionId { get; set; }
        public long AccountId { get; set; }

        public CircleNewPublishEvent(string weChatId, string authorId, long circleId, string content, string connectionId, long accountId)
        {
            WeChatId = weChatId;
            AuthorId = authorId;
            CircleId = circleId;
            Content = content;
            ConnectionId = connectionId;
            AccountId = accountId;
        }
    }

    // 自动化消息处理事件 (用于自动回复/抢红包)
    public class AutomationMessageEvent
    {
        public long AccountId { get; set; }
        public string ConnectionId { get; set; }
        public string WeChatId { get; set; }
        public string FriendId { get; set; }
        public string ContentXml { get; set; }
        public int ContentType { get; set; }

        public AutomationMessageEvent(long accountId, string connectionId, string weChatId, string friendId, string contentXml, int contentType)
        {
            AccountId = accountId;
            ConnectionId = connectionId;
            WeChatId = weChatId;
            FriendId = friendId;
            ContentXml = contentXml;
            ContentType = contentType;
        }
    }

    // 任务结果接收事件 (New)
    public class TaskResultReceivedEvent
    {
        public long TaskId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } // 可以是截屏URL或错误信息
        public string ConnectionId { get; set; }
        public string DeviceUuid { get; set; } // 方便前端过滤
        public DateTime Timestamp { get; set; }

        public TaskResultReceivedEvent(long taskId, bool success, string message, string connectionId, string deviceUuid)
        {
            TaskId = taskId;
            Success = success;
            Message = message;
            ConnectionId = connectionId;
            DeviceUuid = deviceUuid;
            Timestamp = DateTime.UtcNow;
        }
    }
}
