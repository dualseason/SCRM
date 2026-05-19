using System;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models.Events;

namespace SCRM.Shared.Interfaces
{
    /// <summary>
    /// CRM 事件订阅接口 (消费者契约)
    /// 统一使用 IDisposable Subscribe Pattern
    /// </summary>
    public interface ICrmEvents
    {
        // --- 试点模块: 设备状态 ---
        
        /// <summary>
        /// 订阅设备状态变更 (上线/下线)
        /// Action 参数: (deviceId, isOnline)
        /// </summary>
        IDisposable SubscribeToDeviceStatus(Action<string, bool> handler);

        /// <summary>
        /// 订阅联系人列表更新
        /// Action 参数: (accountId)
        /// </summary>
        IDisposable SubscribeToContactsReceived(Action<string> handler);

        /// <summary>
        /// 订阅会话列表更新
        /// Action 参数: (accountId)
        /// </summary>
        IDisposable SubscribeToConversationsUpdated(Action<string> handler);

        /// <summary>
        /// 订阅好友请求列表更新。
        /// Action 参数: (accountId)
        /// </summary>
        IDisposable SubscribeToFriendRequestsUpdated(Action<string> handler);

        /// <summary>
        /// 订阅群邀请列表更新。
        /// Action 参数: (accountId)
        /// </summary>
        IDisposable SubscribeToGroupInvitationsUpdated(Action<string> handler);

        /// <summary>
        /// 订阅联系人标签列表更新。
        /// Action 参数: (accountId)
        /// </summary>
        IDisposable SubscribeToContactLabelsUpdated(Action<string> handler);

        /// <summary>
        /// 订阅手机短信记录更新。
        /// Action 参数: (accountId)
        /// </summary>
        IDisposable SubscribeToSmsRecordsUpdated(Action<string> handler);

        /// <summary>
        /// 订阅手机通话记录更新。
        /// Action 参数: (accountId)
        /// </summary>
        IDisposable SubscribeToCallLogRecordsUpdated(Action<string> handler);

        /// <summary>
        /// 订阅异步任务结果。
        /// </summary>
        IDisposable SubscribeToTaskResults(Action<TaskResultReceivedEvent> handler);

        // --- 铺开模块 ---
        /// <summary>
        /// 订阅消息到达事件
        /// Action 参数: (Message)
        /// </summary>
        IDisposable SubscribeToMessages(Action<Message> handler);

        /// <summary>
        /// 通用事件订阅
        /// </summary>
        IDisposable SubscribeToEvent<T>(string eventName, Action<T> handler);
    }
}
