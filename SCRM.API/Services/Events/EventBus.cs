using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.Services.Events
{
    public interface IEventBus
    {
        Task PublishAsync<T>(T e) where T : class;
        IDisposable Subscribe<T>(Func<T, Task> handler) where T : class;
    }

    public class InMemoryEventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Type, List<object>> _subscriptions = new();

        public async Task PublishAsync<T>(T e) where T : class
        {
            var eventType = typeof(T);
            if (_subscriptions.TryGetValue(eventType, out var handlers))
            {
                // Snapshot handlers to avoid modification during iteration
                var handlersToRun = handlers.ToList();
                foreach (var handlerObj in handlersToRun)
                {
                    if (handlerObj is Func<T, Task> handler)
                    {
                        try
                        {
                            await handler(e);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error handling event {eventType.Name}: {ex}");
                        }
                    }
                }
            }
        }

        //订阅 
        public IDisposable Subscribe<T>(Func<T, Task> handler) where T : class
        {
            var eventType = typeof(T);
            var handlers = _subscriptions.GetOrAdd(eventType, _ => new List<object>());

            lock (handlers)
            {
                handlers.Add(handler);
            }

            return new SubscriptionToken(() =>
            {
                lock (handlers)
                {
                    handlers.Remove(handler);
                }
            });
        }

        private class SubscriptionToken : IDisposable
        {
            private readonly Action _disposeAction;
            public SubscriptionToken(Action disposeAction) => _disposeAction = disposeAction;
            public void Dispose() => _disposeAction();
        }
    }

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
}
