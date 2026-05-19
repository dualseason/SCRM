using System;
using SCRM.SHARED.Models.Dtos;

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

    // 微信已离线事件
    public class WeChatOfflineEvent
    {
        public string deviceUuid { get; set; }
        public DateTime timestamp { get; set; }

        public WeChatOfflineEvent() { }

        public WeChatOfflineEvent(string deviceUuid)
        {
            this.deviceUuid = deviceUuid;
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

    /// <summary>
    /// 会话列表更新事件。
    /// <para>
    /// 群聊列表同步、群资料变更和实时群消息补会话后使用该事件通知前端重新读取 Conversations。
    /// </para>
    /// </summary>
    public class ConversationsUpdatedEvent
    {
        public string deviceUuid { get; set; }
        public string accountId { get; set; }
        public string ownerId { get; set; }
        public DateTime timestamp { get; set; }

        public ConversationsUpdatedEvent(string deviceUuid, string accountId, string ownerId)
        {
            this.deviceUuid = deviceUuid;
            this.accountId = accountId;
            this.ownerId = ownerId;
            this.timestamp = DateTime.UtcNow;
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

    /// <summary>
    /// 好友请求列表已更新事件。
    /// <para>用于通知 Web 端刷新 FriendRequests 页面；不同于 ContactsReceivedEvent，它只表示请求列表状态变化。</para>
    /// </summary>
    public class FriendRequestsUpdatedEvent
    {
        public string deviceUuid { get; set; }
        public string accountId { get; set; }
        public string ownerId { get; set; }
        public DateTime timestamp { get; set; }

        public FriendRequestsUpdatedEvent(string deviceUuid, string accountId, string ownerId)
        {
            this.deviceUuid = deviceUuid;
            this.accountId = accountId;
            this.ownerId = ownerId;
            this.timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 群邀请列表已更新事件。
    /// <para>ChatRoomInvitePushNotice / ChatRoomInviteListNotice 落库后使用该事件通知 Web 端刷新审批列表。</para>
    /// </summary>
    public class GroupInvitationsUpdatedEvent
    {
        public string deviceUuid { get; set; }
        public string accountId { get; set; }
        public string ownerId { get; set; }
        public DateTime timestamp { get; set; }

        public GroupInvitationsUpdatedEvent(string deviceUuid, string accountId, string ownerId)
        {
            this.deviceUuid = deviceUuid;
            this.accountId = accountId;
            this.ownerId = ownerId;
            this.timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 联系人标签列表已更新事件。
    /// <para>ContactLabelInfo/Add/Del Notice 或标签任务成功回执落库后使用该事件通知 Web 端刷新标签页面。</para>
    /// </summary>
    public class ContactLabelsUpdatedEvent
    {
        public string deviceUuid { get; set; }
        public string accountId { get; set; }
        public string ownerId { get; set; }
        public DateTime timestamp { get; set; }

        public ContactLabelsUpdatedEvent(string deviceUuid, string accountId, string ownerId)
        {
            this.deviceUuid = deviceUuid;
            this.accountId = accountId;
            this.ownerId = ownerId;
            this.timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 手机短信记录已更新事件。
    /// <para>SmsPushNotice、SmsReadNotice、SmsSentNotice 或 PullSmsTaskResultNotice 落库后通知 Web 端刷新短信列表。</para>
    /// </summary>
    public class SmsRecordsUpdatedEvent
    {
        public string deviceUuid { get; set; }
        public string accountId { get; set; }
        public string ownerId { get; set; }
        public DateTime timestamp { get; set; }

        public SmsRecordsUpdatedEvent(string deviceUuid, string accountId, string ownerId)
        {
            this.deviceUuid = deviceUuid;
            this.accountId = accountId;
            this.ownerId = ownerId;
            this.timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 手机通话记录已更新事件。
    /// <para>CallLogPushNotice 或 PullCallLogTaskResultNotice 落库后通知 Web 端刷新通话列表。</para>
    /// </summary>
    public class CallLogRecordsUpdatedEvent
    {
        public string deviceUuid { get; set; }
        public string accountId { get; set; }
        public string ownerId { get; set; }
        public DateTime timestamp { get; set; }

        public CallLogRecordsUpdatedEvent(string deviceUuid, string accountId, string ownerId)
        {
            this.deviceUuid = deviceUuid;
            this.accountId = accountId;
            this.ownerId = ownerId;
            this.timestamp = DateTime.UtcNow;
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

    /// <summary>
    /// 朋友圈时间线实时到达事件。
    /// <para>服务端 Netty handler 落库后通过该事件通知 Blazor Server 侧 Store，避免只推 SignalR 而页面本地状态收不到真实内容。</para>
    /// </summary>
    public class MomentTimelineReceivedEvent
    {
        public MomentsTimelineDto data { get; set; }
        public DateTime timestamp { get; set; }

        public MomentTimelineReceivedEvent(MomentsTimelineDto data)
        {
            this.data = data;
            timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 朋友圈时间线变更通知事件。
    /// <para>只携带刷新提示，不携带朋友圈正文、XML、媒体地址等原始敏感字段。</para>
    /// </summary>
    public class MomentTimelineChangedEvent
    {
        public RealtimeDataChangedNoticeDto data { get; set; }
        public DateTime timestamp { get; set; }

        public MomentTimelineChangedEvent(RealtimeDataChangedNoticeDto data)
        {
            this.data = data;
            timestamp = DateTime.UtcNow;
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
        public object? data { get; set; }
        public DateTime timestamp { get; set; }

        public TaskResultReceivedEvent(long taskId, bool success, string message, string connectionId, string deviceUuid, object? data = null)
        {
            this.taskId = taskId;
            this.success = success;
            this.message = message;
            this.connectionId = connectionId;
            this.deviceUuid = deviceUuid;
            this.data = data;
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

    /// <summary>
    /// 视频号提及结果事件。
    /// </summary>
    public class FinderMentionReceivedEvent
    {
        public FinderMentionNoticeDto data { get; set; }

        public FinderMentionReceivedEvent(FinderMentionNoticeDto data)
        {
            this.data = data;
        }
    }

    /// <summary>
    /// 视频号用户页结果事件。
    /// </summary>
    public class FinderUserPageReceivedEvent
    {
        public FinderUserPageDto data { get; set; }

        public FinderUserPageReceivedEvent(FinderUserPageDto data)
        {
            this.data = data;
        }
    }

    /// <summary>
    /// 视频号评论列表结果事件。
    /// </summary>
    public class FinderCommentListReceivedEvent
    {
        public FinderCommentListDto data { get; set; }

        public FinderCommentListReceivedEvent(FinderCommentListDto data)
        {
            this.data = data;
        }
    }

    /// <summary>
    /// 视频号结果变更通知事件。
    /// <para>只携带刷新提示，不携带评论正文、用户昵称、头像、封面、NonceId 等原始敏感字段。</para>
    /// </summary>
    public class FinderResultChangedEvent
    {
        public RealtimeDataChangedNoticeDto data { get; set; }
        public DateTime timestamp { get; set; }

        public FinderResultChangedEvent(RealtimeDataChangedNoticeDto data)
        {
            this.data = data;
            timestamp = DateTime.UtcNow;
        }
    }
}
