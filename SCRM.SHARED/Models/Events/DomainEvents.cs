using System;

namespace SCRM.SHARED.Models.Events
{
    // --- Domain Events (领域事件) ---

    // 微信已上线事件 (业务逻辑层)
    public class WeChatOnlineEvent
    {
        public string deviceUuid { get; set; }
        public string weChatId { get; set; }
        public string nickName { get; set; }
        public string accountId { get; set; } // Changed to string (WxId)
        public string ownerId { get; set; }
        public DateTime timestamp { get; set; }

        public WeChatOnlineEvent() { }

        public WeChatOnlineEvent(string deviceUuid, string weChatId, string nickName, string accountId, string ownerId)
        {
            this.deviceUuid = deviceUuid;
            this.weChatId = weChatId;
            this.nickName = nickName;
            this.accountId = accountId;
            this.ownerId = ownerId;
            this.timestamp = DateTime.UtcNow;
        }
    }

    // 设备已连接事件
    public class DeviceConnectedEvent
    {
        public string userId { get; set; } // 设备所属的账号ID
        public string connectionId { get; set; } // TCP连接ID (Netty ChannelId.AsLongText)
        public string deviceType { get; set; } // 设备类型 (如 Android, iOS)
        public string ownerId { get; set; } // 该设备的管理员ID (用于通知目标)
        public DateTime timestamp { get; set; } // 事件发生时间

        public DeviceConnectedEvent(string userId, string connectionId, string deviceType, string ownerId)
        {
            this.userId = userId;
            this.connectionId = connectionId;
            this.deviceType = deviceType;
            this.ownerId = ownerId;
            this.timestamp = DateTime.UtcNow;
        }
    }

    // 消息已接收事件
    // 用于解耦 Netty 接收层和 SignalR 推送层
    public class MessageReceivedEvent
    {
        public string deviceUuid { get; set; } // 接收消息的设备唯一标识 (UUID)
        public object message { get; set; } // 消息内容载体 (这里传输的是 DTO 对象)
        public string ownerId { get; set; } // 设备所属的管理员ID (用于权限控制/定向推送)
        public DateTime timestamp { get; set; }

        public MessageReceivedEvent(string deviceUuid, object message, string ownerId)
        {
            this.deviceUuid = deviceUuid;
            this.message = message;
            this.ownerId = ownerId;
            this.timestamp = DateTime.UtcNow;
        }
    }

    // 接收联系人列表事件
    public class ContactsReceivedEvent
    {
        public string deviceUuid { get; set; }
        public string accountId { get; set; } // Changed to string
        public string ownerId { get; set; }

        public ContactsReceivedEvent(string deviceUuid, string accountId, string ownerId)
        {
            this.deviceUuid = deviceUuid;
            this.accountId = accountId;
            this.ownerId = ownerId;
        }
    }

    // 好友添加请求事件
    public class FriendRequestEvent
    {
        public string weChatId { get; set; } // 我们的账号
        public string friendId { get; set; } // 对方的Wxid
        public string friendNick { get; set; }
        public string reason { get; set; }
        public string connectionId { get; set; } // 当前连接ID 用于回发任务
        public string accountId { get; set; }      // 我们的数据库DB ID (WxId)

        public FriendRequestEvent(string weChatId, string friendId, string friendNick, string reason, string connectionId, string accountId)
        {
            this.weChatId = weChatId;
            this.friendId = friendId;
            this.friendNick = friendNick;
            this.reason = reason;
            this.connectionId = connectionId;
            this.accountId = accountId;
        }
    }

    // 朋友圈新发布事件
    public class CircleNewPublishEvent
    {
        public string weChatId { get; set; } // 我们的账号
        public string authorId { get; set; } // 发布者Wxid
        public long circleId { get; set; }   // 朋友圈ID
        public string content { get; set; }
        public string connectionId { get; set; }
        public string accountId { get; set; } // Changed to string

        public CircleNewPublishEvent(string weChatId, string authorId, long circleId, string content, string connectionId, string accountId)
        {
            this.weChatId = weChatId;
            this.authorId = authorId;
            this.circleId = circleId;
            this.content = content;
            this.connectionId = connectionId;
            this.accountId = accountId;
        }
    }

    // 自动化消息处理事件 (用于自动回复/抢红包)
    public class AutomationMessageEvent
    {
        public string accountId { get; set; } // Changed to string
        public string connectionId { get; set; }
        public string weChatId { get; set; }
        public string friendId { get; set; }
        public string contentXml { get; set; }
        public int contentType { get; set; }

        public AutomationMessageEvent(string accountId, string connectionId, string weChatId, string friendId, string contentXml, int contentType)
        {
            this.accountId = accountId;
            this.connectionId = connectionId;
            this.weChatId = weChatId;
            this.friendId = friendId;
            this.contentXml = contentXml;
            this.contentType = contentType;
        }
    }

    // 任务结果接收事件 (New)
    public class TaskResultReceivedEvent
    {
        public long taskId { get; set; }
        public bool success { get; set; }
        public string message { get; set; } // 可以是截屏URL或错误信息
        public string connectionId { get; set; }
        public string deviceUuid { get; set; } // 方便前端过滤
        public DateTime timestamp { get; set; }

        public TaskResultReceivedEvent(long taskId, bool success, string message, string connectionId, string deviceUuid)
        {
            this.taskId = taskId;
            this.success = success;
            this.message = message;
            this.connectionId = connectionId;
            this.deviceUuid = deviceUuid;
            this.timestamp = DateTime.UtcNow;
        }
    }
    
    // 截屏上传完成事件
    public class ScreenShotUploadedEvent
    {
        public string Url { get; set; }
        public string DeviceUuid { get; set; }

        public ScreenShotUploadedEvent(string url, string deviceUuid = "")
        {
            Url = url;
            DeviceUuid = deviceUuid;
        }
    }
    
    // 设备状态变更事件 (已上线/已下线)
    public class DeviceStatusChangedEvent
    {
        public string DeviceUuid { get; set; }
        public bool IsOnline { get; set; }
        
        public DeviceStatusChangedEvent(string deviceUuid, bool isOnline)
        {
            DeviceUuid = deviceUuid;
            IsOnline = isOnline;
        }
    }
}
