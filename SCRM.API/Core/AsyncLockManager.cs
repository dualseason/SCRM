using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SCRM.Core
{
    public static class AsyncLockManager
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public static async Task<T> ExecuteWithLockAsync<T>(string key, Func<Task<T>> action)
        {
            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                return await action();
            }
            finally
            {
                semaphore.Release();
                // Optional: Cleanup unused locks if needed, but for fixed entities usually fine
            }
        }

        public static async Task ExecuteWithLockAsync(string key, Func<Task> action)
        {
            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                await action();
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
