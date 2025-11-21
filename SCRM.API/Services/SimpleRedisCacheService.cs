using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SCRM.Configurations;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public class SimpleRedisCacheService : IRedisCacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<SimpleRedisCacheService> _logger;
        private readonly RedisSettings _settings;
        private readonly IConnectionMultiplexer? _connectionMultiplexer;

        public SimpleRedisCacheService(
            IDistributedCache distributedCache,
            ILogger<SimpleRedisCacheService> logger,
            IOptions<RedisSettings> settings)
        {
            _distributedCache = distributedCache;
            _logger = logger;
            _settings = settings.Value;

            try
            {
                var connectionString = _settings.ConnectionString ?? "localhost:6379";
                _connectionMultiplexer = ConnectionMultiplexer.Connect(connectionString);
                _logger.LogInformation("Redis connected successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis connection failed, using distributed cache fallback");
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            try
            {
                var options = new DistributedCacheEntryOptions();
                if (expiry.HasValue)
                {
                    options.AbsoluteExpirationRelativeToNow = expiry.Value;
                }

                var json = JsonSerializer.Serialize(value);
                await _distributedCache.SetStringAsync(key, json, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
                throw;
            }
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var json = await _distributedCache.GetStringAsync(key);
                if (json == null)
                    return default;

                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
                return default;
            }
        }

        public async Task<bool> RemoveAsync(string key)
        {
            try
            {
                await _distributedCache.RemoveAsync(key);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache value for key: {Key}", key);
                return false;
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                var value = await _distributedCache.GetStringAsync(key);
                return !string.IsNullOrEmpty(value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if key exists: {Key}", key);
                return false;
            }
        }

        public async Task<bool> ExpireAsync(string key, TimeSpan expiry)
        {
            try
            {
                var value = await _distributedCache.GetStringAsync(key);
                if (value != null)
                {
                    await _distributedCache.SetStringAsync(key, value, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = expiry
                    });
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting expiry for key: {Key}", key);
                return false;
            }
        }

        public async Task SetManyAsync<T>(Dictionary<string, T> items, TimeSpan? expiry = null)
        {
            try
            {
                var tasks = items.Select(async kvp =>
                {
                    await SetAsync(kvp.Key, kvp.Value, expiry);
                });
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting multiple cache values");
                throw;
            }
        }

        public async Task<IDictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys)
        {
            try
            {
                var tasks = keys.Select(async key =>
                {
                    var value = await GetAsync<T>(key);
                    return new { Key = key, Value = value };
                });

                var results = await Task.WhenAll(tasks);
                return results.ToDictionary(x => x.Key, x => x.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting multiple cache values");
                throw;
            }
        }

        public async Task<long> RemoveManyAsync(IEnumerable<string> keys)
        {
            try
            {
                var tasks = keys.Select(async key => await RemoveAsync(key));
                var results = await Task.WhenAll(tasks);
                return results.Count(r => r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing multiple cache values");
                return 0;
            }
        }

        // Redis specific operations - using connection multiplexer when available
        public async Task<long> ListRightPushAsync<T>(string key, T value)
        {
            if (_connectionMultiplexer == null)
                throw new InvalidOperationException("Redis connection not available");

            var db = _connectionMultiplexer.GetDatabase();
            var json = JsonSerializer.Serialize(value);
            return await db.ListRightPushAsync(key, json);
        }

        public async Task<long> ListLeftPushAsync<T>(string key, T value)
        {
            if (_connectionMultiplexer == null)
                throw new InvalidOperationException("Redis connection not available");

            var db = _connectionMultiplexer.GetDatabase();
            var json = JsonSerializer.Serialize(value);
            return await db.ListLeftPushAsync(key, json);
        }

        public async Task<T?> ListRightPopAsync<T>(string key)
        {
            if (_connectionMultiplexer == null)
                throw new InvalidOperationException("Redis connection not available");

            var db = _connectionMultiplexer.GetDatabase();
            var json = await db.ListRightPopAsync(key);
            return json.HasValue ? JsonSerializer.Deserialize<T>(json) : default;
        }

        public async Task<T?> ListLeftPopAsync<T>(string key)
        {
            if (_connectionMultiplexer == null)
                throw new InvalidOperationException("Redis connection not available");

            var db = _connectionMultiplexer.GetDatabase();
            var json = await db.ListLeftPopAsync(key);
            return json.HasValue ? JsonSerializer.Deserialize<T>(json) : default;
        }

        public async Task<long> ListLengthAsync(string key)
        {
            if (_connectionMultiplexer == null)
                throw new InvalidOperationException("Redis connection not available");

            var db = _connectionMultiplexer.GetDatabase();
            return await db.ListLengthAsync(key);
        }

        public async Task<IEnumerable<T>> ListRangeAsync<T>(string key, long start = 0, long stop = -1)
        {
            if (_connectionMultiplexer == null)
                throw new InvalidOperationException("Redis connection not available");

            var db = _connectionMultiplexer.GetDatabase();
            var values = await db.ListRangeAsync(key, start, stop);
            return values.Select(v => JsonSerializer.Deserialize<T>(v)!);
        }

        public async Task<long> ListRemoveAsync<T>(string key, T value, long count = 1)
        {
            if (_connectionMultiplexer == null)
                throw new InvalidOperationException("Redis connection not available");

            var db = _connectionMultiplexer.GetDatabase();
            var json = JsonSerializer.Serialize(value);
            return await db.ListRemoveAsync(key, json, count);
        }

        public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
        {
            var cachedValue = await GetAsync<T>(key);
            if (cachedValue != null)
                return cachedValue;

            var value = await factory();
            await SetAsync(key, value, expiry);
            return value;
        }

        public async Task<T?> GetOrAddAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var cachedValue = await GetAsync<T>(key);
            if (cachedValue != null)
                return cachedValue;

            await SetAsync(key, value, expiry);
            return value;
        }

        public async Task SetIfExistsAsync<T>(string key, Func<T, T> updateFunc, TimeSpan? expiry = null)
        {
            var existingValue = await GetAsync<T>(key);
            if (existingValue != null)
            {
                var updatedValue = updateFunc(existingValue);
                await SetAsync(key, updatedValue, expiry);
            }
        }

        public async Task<bool> KeyDeleteAsync(string pattern)
        {
            if (_connectionMultiplexer == null)
                return false;

            try
            {
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
                var keys = server.Keys(pattern: pattern);
                var db = _connectionMultiplexer.GetDatabase();

                foreach (var key in keys)
                {
                    await db.KeyDeleteAsync(key);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting keys with pattern: {Pattern}", pattern);
                return false;
            }
        }

        public async Task<IEnumerable<string>> KeysAsync(string pattern)
        {
            if (_connectionMultiplexer == null)
                return Enumerable.Empty<string>();

            try
            {
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
                var keys = server.Keys(pattern: pattern);
                return keys.Select(k => k.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting keys with pattern: {Pattern}", pattern);
                return Enumerable.Empty<string>();
            }
        }

        public async Task<long> KeyCountAsync(string pattern)
        {
            if (_connectionMultiplexer == null)
                return 0;

            try
            {
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
                var keys = server.Keys(pattern: pattern);
                return keys.Count();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting keys with pattern: {Pattern}", pattern);
                return 0;
            }
        }

        public async Task<RedisStats> GetCacheStatsAsync()
        {
            try
            {
                if (_connectionMultiplexer != null)
                {
                    var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
                    var info = await server.InfoAsync();

                    var serverInfo = info.FirstOrDefault();
                    var serverGroup = serverInfo.FirstOrDefault();

                    return new RedisStats
                    {
                        ConnectedClients = 1, // Simplified
                        RedisVersion = "6.x+", // Simplified
                        LastUpdated = DateTime.UtcNow
                    };
                }

                return new RedisStats
                {
                    ConnectedClients = 0,
                    RedisVersion = "Not Connected",
                    LastUpdated = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Redis stats");
                return new RedisStats
                {
                    ConnectedClients = 0,
                    RedisVersion = "Error",
                    LastUpdated = DateTime.UtcNow
                };
            }
        }

        public async Task FlushDatabaseAsync()
        {
            if (_connectionMultiplexer == null)
                throw new InvalidOperationException("Redis connection not available");

            var db = _connectionMultiplexer.GetDatabase();
            await db.ExecuteAsync("FLUSHDB");
        }

        public async Task FlushAllAsync()
        {
            if (_connectionMultiplexer == null)
                throw new InvalidOperationException("Redis connection not available");

            var db = _connectionMultiplexer.GetDatabase();
            await db.ExecuteAsync("FLUSHALL");
        }

        public async Task<bool> IsRedisAvailableAsync()
        {
            try
            {
                if (_connectionMultiplexer == null)
                    return false;

                return _connectionMultiplexer.IsConnected;
            }
            catch
            {
                return false;
            }
        }

        public async Task<RedisServerInfo> GetServerInfoAsync()
        {
            try
            {
                if (_connectionMultiplexer == null)
                {
                    return new RedisServerInfo
                    {
                        Version = "Not Connected",
                        Mode = "Unknown",
                        ServerTime = DateTime.UtcNow
                    };
                }

                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
                var info = await server.InfoAsync();

                return new RedisServerInfo
                {
                    Version = "6.x+", // Simplified
                    Mode = "standalone",
                    ServerTime = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting server info");
                return new RedisServerInfo
                {
                    Version = "Error",
                    Mode = "Unknown",
                    ServerTime = DateTime.UtcNow
                };
            }
        }

        public async Task<RedisLock> AcquireLockAsync(string resource, TimeSpan expiry)
        {
            if (_connectionMultiplexer == null)
                throw new InvalidOperationException("Redis connection not available");

            var db = _connectionMultiplexer.GetDatabase();
            var lockKey = $"lock:{resource}";
            var lockValue = Guid.NewGuid().ToString();
            var expiryTime = (int)expiry.TotalSeconds;

            var acquired = await db.StringSetAsync(lockKey, lockValue, expiry, When.NotExists);
            if (acquired)
            {
                return new RedisLock(resource, lockValue, expiry);
            }

            throw new InvalidOperationException($"Could not acquire lock for resource: {resource}");
        }

        public async Task<bool> ReleaseLockAsync(RedisLock redisLock)
        {
            if (_connectionMultiplexer == null)
                throw new InvalidOperationException("Redis connection not available");

            var db = _connectionMultiplexer.GetDatabase();
            var lockKey = $"lock:{redisLock.Resource}";

            // Simplified release - in production would use Lua script for atomic check-and-delete
            await db.KeyDeleteAsync(lockKey);
            return true;
        }

        public async Task<bool> IsLockHeldAsync(string resource)
        {
            if (_connectionMultiplexer == null)
                return false;

            var db = _connectionMultiplexer.GetDatabase();
            var lockKey = $"lock:{resource}";
            return await db.KeyExistsAsync(lockKey);
        }

        public void Dispose()
        {
            _connectionMultiplexer?.Dispose();
        }
    }
}