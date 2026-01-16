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
    public class CrmEventAggregator : ICrmEvents, ICrmEventPublisher
    {
        private readonly IEventBus _eventBus;

        public CrmEventAggregator(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        // --- Publisher Implementation ---

        public void PublishDeviceStatus(string deviceId, bool isOnline)
        {
            // Publish via IEventBus for consistency
            // Note: DeviceConnectedEvent exists, this might be redundant if we fully switch,
            // but for now we map it.
             _eventBus.PublishAsync(new DeviceStatusChangedEvent(deviceId, isOnline));
        }

        public void PublishEvent<T>(string eventName, T data)
        {
             // For Generic T, we try to wrap it if it matches known types, 
             // or rely on T being a class registered in IEventBus.
             
             // In the specific case of Screenshot, T is string (URL), which IEventBus doesn't like (where T: class).
             // So we should encourage using PublishAsync directly or map it here.
             
             if (eventName == "OnScreenShotUploaded" && data is string url)
             {
                 _eventBus.PublishAsync(new ScreenShotUploadedEvent(url));
             }
        }

        // --- Subscriber Implementation ---

        public IDisposable SubscribeToDeviceStatus(Action<string, bool> handler)
        {
            // Adapter: IEventBus (Task) -> UI Handler (Void)
            return _eventBus.Subscribe<DeviceStatusChangedEvent>(async e => 
            {
                handler(e.DeviceUuid, e.IsOnline);
                await Task.CompletedTask;
            });
        }

        public IDisposable SubscribeToEvent<T>(string eventName, Action<T> handler)
        {
            if (eventName == "OnScreenShotUploaded" && typeof(T) == typeof(string))
            {
                 // Subscribe to the Typed Event from EventBus
                 var subscription = _eventBus.Subscribe<ScreenShotUploadedEvent>(async e => 
                 {
                     if (handler is Action<string> stringHandler)
                     {
                         stringHandler(e.Url);
                     }
                     await Task.CompletedTask;
                 });
                 return subscription;
            }

            return null; 
        }
    }
}
