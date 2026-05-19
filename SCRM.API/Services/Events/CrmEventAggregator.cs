using System;
using System.Threading.Tasks;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models.Dtos;
using SCRM.SHARED.Models.Events;
using SCRM.Services.Events;
using SCRM.Shared.Interfaces;

namespace SCRM.API.Services.Events
{
    /// <summary>
    /// Event Aggregator Bridge
    /// Bridges the gap between IEventBus (Task-based, Server-side) 
    /// and ICrmEvents (Action-based, UI-side)
    /// </summary>
    /// <summary>
    /// 事件聚合器适配器 (Bridge Pattern)
    /// <para>连接后端的 IEventBus (Task-based) 与前端 UI 的 ICrmEvents (Action-based)。</para>
    /// <para>主要职责：</para>
    /// <list type="bullet">
    /// <item>作为 Publisher: 将前端动作转换为后端 IEventBus 事件</item>
    /// <item>作为 Subscriber: 将后端 IEventBus 事件转发给前端 Action 回调</item>
    /// </list>
    /// </summary>
    public class CrmEventAggregator : ICrmEvents, ICrmEventPublisher
    {
        private readonly IEventBus _eventBus;
        
        // 空释放对象，避免返回 null
        private static readonly IDisposable _emptyDisposable = new EmptyDisposable();

        public CrmEventAggregator(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        #region Publisher Implementation

        /// <summary>
        /// 发布设备状态变更事件
        /// </summary>
        public void PublishDeviceStatus(string deviceId, bool isOnline)
        {
             _eventBus.PublishAsync(new DeviceStatusChangedEvent(deviceId, isOnline));
        }

        /// <summary>
        /// 发布通用事件 (Legacy Adapter)
        /// </summary>
        public void PublishEvent<T>(string eventName, T data)
        {
             switch (eventName)
             {
                 case "OnScreenShotUploaded" when data is string url:
                     _eventBus.PublishAsync(new ScreenShotUploadedEvent(url));
                     break;
                 case "OnScreenShotUploaded" when data is ScreenShotUploadedEvent screenshotEvent:
                     _eventBus.PublishAsync(screenshotEvent);
                     break;
                 case "FinderMentionListReceived" when data is FinderMentionNoticeDto mention:
                     _eventBus.PublishAsync(new FinderMentionReceivedEvent(mention));
                     break;
                 case "FinderUserPageReceived" when data is FinderUserPageDto userPage:
                     _eventBus.PublishAsync(new FinderUserPageReceivedEvent(userPage));
                     break;
                 case "FinderCommentListReceived" when data is FinderCommentListDto comments:
                     _eventBus.PublishAsync(new FinderCommentListReceivedEvent(comments));
                     break;
                 case "MomentTimelineReceived" when data is MomentsTimelineDto moment:
                     _eventBus.PublishAsync(new MomentTimelineReceivedEvent(moment));
                     break;
                 case "MomentTimelineChanged" when data is RealtimeDataChangedNoticeDto momentNotice:
                     _eventBus.PublishAsync(new MomentTimelineChangedEvent(momentNotice));
                     break;
                 case "FinderResultChanged" when data is RealtimeDataChangedNoticeDto finderNotice:
                     _eventBus.PublishAsync(new FinderResultChangedEvent(finderNotice));
                     break;
                  
                 // Future extensions can act here
                 default:
                     // Log warning or ignore? For now ignore to avoid noise.
                     break;
             }
        }

        #endregion

        #region Subscriber Implementation

        /// <summary>
        /// 订阅设备状态变更
        /// </summary>
        public IDisposable SubscribeToDeviceStatus(Action<string, bool> handler)
        {
            return _eventBus.Subscribe<DeviceStatusChangedEvent>(async e => 
            {
                handler?.Invoke(e.DeviceUuid, e.IsOnline);
                await Task.CompletedTask;
            });
        }

        /// <summary>
        /// 订阅通用事件 (Legacy Adapter)
        /// </summary>
        public IDisposable SubscribeToEvent<T>(string eventName, Action<T> handler)
        {
            if (string.IsNullOrEmpty(eventName) || handler == null) return _emptyDisposable;

            if (eventName == "OnScreenShotUploaded" && typeof(T) == typeof(string))
            {
                 return _eventBus.Subscribe<ScreenShotUploadedEvent>(async e => 
                 {
                     if (handler is Action<string> stringHandler)
                     {
                         stringHandler(e.Url);
                     }
                     await Task.CompletedTask;
                 });
            }

            if (eventName == "OnScreenShotUploaded" && typeof(T) == typeof(ScreenShotUploadedEvent))
            {
                 return _eventBus.Subscribe<ScreenShotUploadedEvent>(async e =>
                 {
                     if (handler is Action<ScreenShotUploadedEvent> typedHandler)
                     {
                         typedHandler(e);
                     }

                     await Task.CompletedTask;
                 });
            }

            if (eventName == "OnWeChatOnline" && typeof(T) == typeof(WeChatOnlineEvent))
            {
                 return _eventBus.Subscribe<WeChatOnlineEvent>(async e => 
                 {
                     if (handler is Action<WeChatOnlineEvent> typedHandler)
                     {
                         typedHandler(e);
                     }
                     await Task.CompletedTask;
                 });
            }

            if (eventName == "OnWeChatOffline" && typeof(T) == typeof(WeChatOfflineEvent))
            {
                 return _eventBus.Subscribe<WeChatOfflineEvent>(async e => 
                 {
                     if (handler is Action<WeChatOfflineEvent> typedHandler)
                     {
                         typedHandler(e);
                     }
                     await Task.CompletedTask;
                 });
            }

            if (eventName == "OnMessageReceived" && typeof(T) == typeof(Message))
            {
                 if (handler is Action<Message> msgHandler)
                 {
                     return SubscribeToMessages(msgHandler);
                 }
                 return _emptyDisposable;
            }

            if (eventName == "FinderMentionListReceived" && typeof(T) == typeof(FinderMentionNoticeDto))
            {
                return _eventBus.Subscribe<FinderMentionReceivedEvent>(async e =>
                {
                    if (handler is Action<FinderMentionNoticeDto> typedHandler)
                    {
                        typedHandler(e.data);
                    }

                    await Task.CompletedTask;
                });
            }

            if (eventName == "FinderUserPageReceived" && typeof(T) == typeof(FinderUserPageDto))
            {
                return _eventBus.Subscribe<FinderUserPageReceivedEvent>(async e =>
                {
                    if (handler is Action<FinderUserPageDto> typedHandler)
                    {
                        typedHandler(e.data);
                    }

                    await Task.CompletedTask;
                });
            }

            if (eventName == "FinderCommentListReceived" && typeof(T) == typeof(FinderCommentListDto))
            {
                return _eventBus.Subscribe<FinderCommentListReceivedEvent>(async e =>
                {
                    if (handler is Action<FinderCommentListDto> typedHandler)
                    {
                        typedHandler(e.data);
                    }

                    await Task.CompletedTask;
                });
            }

            if (eventName == "MomentTimelineReceived" && typeof(T) == typeof(MomentsTimelineDto))
            {
                return _eventBus.Subscribe<MomentTimelineReceivedEvent>(async e =>
                {
                    if (handler is Action<MomentsTimelineDto> typedHandler && e?.data != null)
                    {
                        typedHandler(e.data);
                    }

                    await Task.CompletedTask;
                });
            }

            if (eventName == "MomentTimelineChanged" && typeof(T) == typeof(RealtimeDataChangedNoticeDto))
            {
                return _eventBus.Subscribe<MomentTimelineChangedEvent>(async e =>
                {
                    if (handler is Action<RealtimeDataChangedNoticeDto> typedHandler && e?.data != null)
                    {
                        typedHandler(e.data);
                    }

                    await Task.CompletedTask;
                });
            }

            if (eventName == "FinderResultChanged" && typeof(T) == typeof(RealtimeDataChangedNoticeDto))
            {
                return _eventBus.Subscribe<FinderResultChangedEvent>(async e =>
                {
                    if (handler is Action<RealtimeDataChangedNoticeDto> typedHandler && e?.data != null)
                    {
                        typedHandler(e.data);
                    }

                    await Task.CompletedTask;
                });
            }

            // Return empty disposable instead of null to prevent null reference exceptions in caller 'using' blocks
            return _emptyDisposable; 
        }

        /// <summary>
        /// 订阅联系人列表更新
        /// </summary>
        public IDisposable SubscribeToContactsReceived(Action<string> handler)
        {
            return _eventBus.Subscribe<ContactsReceivedEvent>(async e => 
            {
                handler?.Invoke(e.accountId);
                await Task.CompletedTask;
            });
        }

        /// <summary>
        /// 订阅会话列表更新。
        /// </summary>
        public IDisposable SubscribeToConversationsUpdated(Action<string> handler)
        {
            return _eventBus.Subscribe<ConversationsUpdatedEvent>(async e =>
            {
                handler?.Invoke(e.accountId);
                await Task.CompletedTask;
            });
        }

        /// <summary>
        /// 订阅好友请求列表更新。
        /// </summary>
        public IDisposable SubscribeToFriendRequestsUpdated(Action<string> handler)
        {
            return _eventBus.Subscribe<FriendRequestsUpdatedEvent>(async e =>
            {
                handler?.Invoke(e.accountId);
                await Task.CompletedTask;
            });
        }

        /// <summary>
        /// 订阅群邀请列表更新。
        /// </summary>
        public IDisposable SubscribeToGroupInvitationsUpdated(Action<string> handler)
        {
            return _eventBus.Subscribe<GroupInvitationsUpdatedEvent>(async e =>
            {
                handler?.Invoke(e.accountId);
                await Task.CompletedTask;
            });
        }

        /// <summary>
        /// 订阅联系人标签列表更新。
        /// </summary>
        public IDisposable SubscribeToContactLabelsUpdated(Action<string> handler)
        {
            return _eventBus.Subscribe<ContactLabelsUpdatedEvent>(async e =>
            {
                handler?.Invoke(e.accountId);
                await Task.CompletedTask;
            });
        }

        /// <summary>
        /// 订阅手机短信记录更新。
        /// </summary>
        public IDisposable SubscribeToSmsRecordsUpdated(Action<string> handler)
        {
            return _eventBus.Subscribe<SmsRecordsUpdatedEvent>(async e =>
            {
                handler?.Invoke(e.accountId);
                await Task.CompletedTask;
            });
        }

        /// <summary>
        /// 订阅手机通话记录更新。
        /// </summary>
        public IDisposable SubscribeToCallLogRecordsUpdated(Action<string> handler)
        {
            return _eventBus.Subscribe<CallLogRecordsUpdatedEvent>(async e =>
            {
                handler?.Invoke(e.accountId);
                await Task.CompletedTask;
            });
        }

        /// <summary>
        /// 订阅异步任务结果。
        /// </summary>
        public IDisposable SubscribeToTaskResults(Action<TaskResultReceivedEvent> handler)
        {
            return _eventBus.Subscribe<TaskResultReceivedEvent>(async e =>
            {
                handler?.Invoke(e);
                await Task.CompletedTask;
            });
        }

        /// <summary>
        /// 订阅消息到达事件
        /// </summary>
        public IDisposable SubscribeToMessages(Action<Message> handler)
        {
            if (handler == null) return _emptyDisposable;

            return _eventBus.Subscribe<MessageReceivedEvent>(async e => 
            {
                if (e?.message is Message msg)
                {
                    handler(msg);
                }
                await Task.CompletedTask;
            });
        }

        #endregion

        private class EmptyDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
