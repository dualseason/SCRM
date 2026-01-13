using System;
using SCRM.Shared.Interfaces;

namespace SCRM.API.Services
{
    public class CrmEventAggregator : ICrmEvents, ICrmEventPublisher
    {
        // Internal events
        private event Action<string, bool>? OnDeviceStatusChanged;

        // --- Publisher Implementation ---
        public void PublishDeviceStatus(string deviceId, bool isOnline)
        {
            OnDeviceStatusChanged?.Invoke(deviceId, isOnline);
        }

        // --- Subscriber Implementation ---
        public IDisposable SubscribeToDeviceStatus(Action<string, bool> handler)
        {
            OnDeviceStatusChanged += handler;
            return new Subscription(this, handler);
        }

        // --- Helper for Unsubscribing ---
        private class Subscription : IDisposable
        {
            private readonly CrmEventAggregator _aggregator;
            private readonly Action<string, bool> _handler;

            public Subscription(CrmEventAggregator aggregator, Action<string, bool> handler)
            {
                _aggregator = aggregator;
                _handler = handler;
            }

            public void Dispose()
            {
                _aggregator.OnDeviceStatusChanged -= _handler;
            }
        }
    }
}
