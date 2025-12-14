using System.Collections.Concurrent;

namespace SCRM.API.Services.Data
{
    public static class AsyncLockManager
    {
        // 存储所有异步锁的字典
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        // 获取指定键的锁
        public static SemaphoreSlim GetLock(string key)
        {
            return _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        }

        // 辅助方法：执行需要锁保护的异步操作
        public static async Task<T> ExecuteWithLockAsync<T>(string key, Func<Task<T>> action)
        {
            var asyncLock = GetLock(key);
            await asyncLock.WaitAsync();
            try
            {
                return await action();
            }
            finally
            {
                asyncLock.Release();
            }
        }

        // 无返回值版本
        public static async Task ExecuteWithLockAsync(string key, Func<Task> action)
        {
            var asyncLock = GetLock(key);
            await asyncLock.WaitAsync();
            try
            {
                await action();
            }
            finally
            {
                asyncLock.Release();
            }
        }

        // 可选：清理长时间未使用的锁
        public static void CleanupUnusedLocks()
        {
            // 实现清理逻辑，如果需要的话
        }
    }
}
