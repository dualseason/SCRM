using System;
using System.Threading.Tasks;
using SCRM.API.Models.Entities;
using SCRM.API.Models.Events;
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

            // Return empty disposable instead of null to prevent null reference exceptions in caller 'using' blocks
            return _emptyDisposable; 
        }

        #endregion

        private class EmptyDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
