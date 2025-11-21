using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public interface IRedisCacheService
    {
        // 基本缓存操作
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
        Task<T?> GetAsync<T>(string key);
        Task<bool> RemoveAsync(string key);
        Task<bool> ExistsAsync(string key);
        Task<bool> ExpireAsync(string key, TimeSpan expiry);

        // 批量操作
        Task SetManyAsync<T>(Dictionary<string, T> items, TimeSpan? expiry = null);
        Task<IDictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys);
        Task<long> RemoveManyAsync(IEnumerable<string> keys);

        // 列表操作
        Task<long> ListRightPushAsync<T>(string key, T value);
        Task<long> ListLeftPushAsync<T>(string key, T value);
        Task<T?> ListRightPopAsync<T>(string key);
        Task<T?> ListLeftPopAsync<T>(string key);
        Task<long> ListLengthAsync(string key);
        Task<IEnumerable<T>> ListRangeAsync<T>(string key, long start = 0, long stop = -1);
        Task<long> ListRemoveAsync<T>(string key, T value, long count = 1);

        // 集合操作
        Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null);
        Task<T?> GetOrAddAsync<T>(string key, T value, TimeSpan? expiry = null);
        Task SetIfExistsAsync<T>(string key, Func<T, T> updateFunc, TimeSpan? expiry = null);

        // 模式匹配
        Task<bool> KeyDeleteAsync(string pattern);
        Task<IEnumerable<string>> KeysAsync(string pattern);
        Task<long> KeyCountAsync(string pattern);

        // 缓存统计和管理
        Task<RedisStats> GetCacheStatsAsync();
        Task FlushDatabaseAsync();
        Task FlushAllAsync();

        // 健康检查
        Task<bool> IsRedisAvailableAsync();
        Task<RedisServerInfo> GetServerInfoAsync();

        // 分布式锁
        Task<RedisLock> AcquireLockAsync(string resource, TimeSpan expiry);
        Task<bool> ReleaseLockAsync(RedisLock redisLock);
        Task<bool> IsLockHeldAsync(string resource);
    }

    public class RedisStats
    {
        public long TotalCommands { get; set; }
        public long TotalOperations { get; set; }
        public long UsedMemory { get; set; }
        public long KeyspaceHits { get; set; }
        public long KeyspaceMisses { get; set; }
        public double HitRatio => KeyspaceHits + KeyspaceMisses > 0 ? (double)KeyspaceHits / (KeyspaceHits + KeyspaceMisses) * 100 : 0;
        public int ConnectedClients { get; set; }
        public string RedisVersion { get; set; } = "Unknown";
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class RedisServerInfo
    {
        public string Version { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public string ProcessId { get; set; } = string.Empty;
        public int ConnectedClients { get; set; }
        public int UsedMemoryMB { get; set; }
        public int MaxMemoryMB { get; set; }
        public TimeSpan Uptime { get; set; }
        public DateTime ServerTime { get; set; } = DateTime.UtcNow;
    }

    public class RedisLock
    {
        public string Resource { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime Expiry { get; set; }
        public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;
        public bool IsAcquired { get; set; }

        public RedisLock(string resource, string value, TimeSpan expiry)
        {
            Resource = resource;
            Value = value;
            Expiry = DateTime.UtcNow.Add(expiry);
            IsAcquired = true;
        }
    }
}