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
                List<object> handlersToRun = handlers.ToList();
                foreach (object handlerObj in handlersToRun)
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
}

