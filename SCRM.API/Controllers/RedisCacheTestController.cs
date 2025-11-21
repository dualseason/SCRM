using Microsoft.AspNetCore.Mvc;
using SCRM.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SCRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RedisCacheTestController : ControllerBase
    {
        private readonly IRedisCacheService _redisCacheService;

        public RedisCacheTestController(IRedisCacheService redisCacheService)
        {
            _redisCacheService = redisCacheService;
        }

        /// <summary>
        /// 测试基本缓存操作
        /// </summary>
        [HttpPost("basic-operations")]
        public async Task<IActionResult> TestBasicOperations()
        {
            var results = new Dictionary<string, object>();

            try
            {
                // 测试设置和获取
                var testKey = "test:basic:123";
                var testValue = new { Id = 1, Name = "Redis Test", CreatedAt = DateTime.UtcNow };

                await _redisCacheService.SetAsync(testKey, testValue, TimeSpan.FromMinutes(5));
                var retrievedValue = await _redisCacheService.GetAsync<object>(testKey);

                results["SetAndGet"] = new
                {
                    Success = retrievedValue != null,
                    OriginalValue = testValue,
                    RetrievedValue = retrievedValue
                };

                // 测试存在性检查
                var exists = await _redisCacheService.ExistsAsync(testKey);
                results["Exists"] = exists;

                // 测试删除
                var deleted = await _redisCacheService.RemoveAsync(testKey);
                results["Deleted"] = deleted;

                // 验证删除后不存在
                var existsAfterDelete = await _redisCacheService.ExistsAsync(testKey);
                results["ExistsAfterDelete"] = existsAfterDelete;

                return Ok(new { Success = true, Results = results });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// 测试批量操作
        /// </summary>
        [HttpPost("batch-operations")]
        public async Task<IActionResult> TestBatchOperations()
        {
            var results = new Dictionary<string, object>();

            try
            {
                // 准备测试数据
                var testItems = new Dictionary<string, object>
                {
                    ["batch:test:1"] = new { Id = 1, Name = "Batch Test 1" },
                    ["batch:test:2"] = new { Id = 2, Name = "Batch Test 2" },
                    ["batch:test:3"] = new { Id = 3, Name = "Batch Test 3" },
                    ["batch:test:4"] = new { Id = 4, Name = "Batch Test 4" },
                    ["batch:test:5"] = new { Id = 5, Name = "Batch Test 5" }
                };

                // 测试批量设置
                var setStartTime = DateTime.UtcNow;
                await _redisCacheService.SetManyAsync(testItems, TimeSpan.FromMinutes(10));
                var setDuration = DateTime.UtcNow - setStartTime;

                // 测试批量获取
                var getStartTime = DateTime.UtcNow;
                var retrievedItems = await _redisCacheService.GetManyAsync<object>(testItems.Keys);
                var getDuration = DateTime.UtcNow - getStartTime;

                // 测试批量删除
                var deleteStartTime = DateTime.UtcNow;
                var deletedCount = await _redisCacheService.RemoveManyAsync(testItems.Keys);
                var deleteDuration = DateTime.UtcNow - deleteStartTime;

                results["BatchSet"] = new
                {
                    Success = true,
                    ItemCount = testItems.Count,
                    Duration = $"{setDuration.TotalMilliseconds:F2}ms",
                    Performance = $"{testItems.Count / setDuration.TotalSeconds:F2} ops/sec"
                };

                results["BatchGet"] = new
                {
                    Success = retrievedItems.Count == testItems.Count,
                    RetrievedCount = retrievedItems.Count,
                    ExpectedCount = testItems.Count,
                    Duration = $"{getDuration.TotalMilliseconds:F2}ms",
                    Performance = $"{retrievedItems.Count / getDuration.TotalSeconds:F2} ops/sec",
                    Items = retrievedItems
                };

                results["BatchDelete"] = new
                {
                    Success = deletedCount == testItems.Count,
                    DeletedCount = deletedCount,
                    ExpectedCount = testItems.Count,
                    Duration = $"{deleteDuration.TotalMilliseconds:F2}ms"
                };

                return Ok(new { Success = true, Results = results });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// 测试列表操作
        /// </summary>
        [HttpPost("list-operations")]
        public async Task<IActionResult> TestListOperations()
        {
            var results = new Dictionary<string, object>();

            try
            {
                var listKey = "test:list:operations";

                // 清空列表
                await _redisCacheService.RemoveAsync(listKey);

                // 测试右推操作
                var pushResults = new List<object>();
                for (int i = 1; i <= 5; i++)
                {
                    var item = new { Id = i, Value = $"List Item {i}" };
                    var length = await _redisCacheService.ListRightPushAsync(listKey, item);
                    pushResults.Add(new { ItemId = i, ListLength = length });
                }

                // 测试列表长度
                var listLength = await _redisCacheService.ListLengthAsync(listKey);

                // 测试获取范围
                var allItems = await _redisCacheService.ListRangeAsync<object>(listKey);

                // 测试左推操作
                await _redisCacheService.ListLeftPushAsync(listKey, new { Id = 0, Value = "First Item" });
                var newLength = await _redisCacheService.ListLengthAsync(listKey);

                // 测试右弹出
                var poppedItem = await _redisCacheService.ListRightPopAsync<object>(listKey);
                var finalLength = await _redisCacheService.ListLengthAsync(listKey);

                results["RightPush"] = pushResults;
                results["ListLength"] = new { Initial = listLength, AfterLeftPush = newLength, AfterRightPop = finalLength };
                results["ListRange"] = new
                {
                    RetrievedCount = allItems.Count(),
                    Items = allItems
                };
                results["RightPop"] = poppedItem;

                // 清理
                await _redisCacheService.RemoveAsync(listKey);

                return Ok(new { Success = true, Results = results });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// 测试分布式锁
        /// </summary>
        [HttpPost("distributed-lock")]
        public async Task<IActionResult> TestDistributedLock()
        {
            var results = new Dictionary<string, object>();

            try
            {
                var resource = "test:lock:resource";
                var expiry = TimeSpan.FromSeconds(10);

                // 获取锁
                var redisLock = await _redisCacheService.AcquireLockAsync(resource, expiry);

                results["AcquireLock"] = new
                {
                    Success = redisLock != null,
                    Resource = redisLock?.Resource,
                    AcquiredAt = redisLock?.AcquiredAt,
                    Expiry = redisLock?.Expiry
                };

                if (redisLock != null)
                {
                    // 检查锁是否持有
                    var isHeld = await _redisCacheService.IsLockHeldAsync(resource);
                    results["IsLockHeld"] = isHeld;

                    // 等待1秒后释放锁
                    await Task.Delay(1000);

                    // 释放锁
                    var released = await _redisCacheService.ReleaseLockAsync(redisLock);
                    results["ReleaseLock"] = new { Success = released };

                    // 检查锁是否已释放
                    var isHeldAfterRelease = await _redisCacheService.IsLockHeldAsync(resource);
                    results["IsLockHeldAfterRelease"] = isHeldAfterRelease;
                }

                return Ok(new { Success = true, Results = results });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// 测试GetOrCreate模式
        /// </summary>
        [HttpPost("get-or-create")]
        public async Task<IActionResult> TestGetOrCreate()
        {
            var results = new Dictionary<string, object>();

            try
            {
                var key = "test:getorcreate:user:123";

                // 第一次调用 - 应该执行factory
                var startTime1 = DateTime.UtcNow;
                var result1 = await _redisCacheService.GetOrCreateAsync(key, async () =>
                {
                    await Task.Delay(100); // 模拟数据库查询
                    return new { Id = 123, Name = "Test User", CreatedAt = DateTime.UtcNow };
                }, TimeSpan.FromMinutes(5));
                var duration1 = DateTime.UtcNow - startTime1;

                // 第二次调用 - 应该从缓存获取
                var startTime2 = DateTime.UtcNow;
                var result2 = await _redisCacheService.GetOrCreateAsync(key, async () =>
                {
                    await Task.Delay(100); // 这个不应该执行
                    return new { Id = 0, Name = "Should Not Execute" };
                }, TimeSpan.FromMinutes(5));
                var duration2 = DateTime.UtcNow - startTime2;

                results["FirstCall"] = new
                {
                    Result = result1,
                    Duration = $"{duration1.TotalMilliseconds:F2}ms",
                    FromCache = false
                };

                results["SecondCall"] = new
                {
                    Result = result2,
                    Duration = $"{duration2.TotalMilliseconds:F2}ms",
                    FromCache = duration2.TotalMilliseconds < duration1.TotalMilliseconds / 2
                };

                results["CacheHitPerformance"] = new
                {
                    ImprovementRatio = duration1.TotalMilliseconds / duration2.TotalMilliseconds,
                    SpeedupPercent = ((duration1.TotalMilliseconds - duration2.TotalMilliseconds) / duration1.TotalMilliseconds) * 100
                };

                // 清理
                await _redisCacheService.RemoveAsync(key);

                return Ok(new { Success = true, Results = results });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// 性能压力测试
        /// </summary>
        [HttpPost("performance-test")]
        public async Task<IActionResult> PerformanceTest([FromQuery] int operations = 1000)
        {
            var results = new Dictionary<string, object>();

            try
            {
                var testKeys = new List<string>();
                var testValues = new Dictionary<string, object>();

                // 准备测试数据
                for (int i = 0; i < operations; i++)
                {
                    var key = $"perf:test:{i}";
                    var value = new { Id = i, Data = $"Performance Test Data {i}", Timestamp = DateTime.UtcNow };

                    testKeys.Add(key);
                    testValues[key] = value;
                }

                // 测试写入性能
                var writeStartTime = DateTime.UtcNow;
                await _redisCacheService.SetManyAsync(testValues, TimeSpan.FromMinutes(5));
                var writeDuration = DateTime.UtcNow - writeStartTime;

                // 测试读取性能
                var readStartTime = DateTime.UtcNow;
                var retrievedItems = await _redisCacheService.GetManyAsync<object>(testKeys);
                var readDuration = DateTime.UtcNow - readStartTime;

                // 测试删除性能
                var deleteStartTime = DateTime.UtcNow;
                var deletedCount = await _redisCacheService.RemoveManyAsync(testKeys);
                var deleteDuration = DateTime.UtcNow - deleteStartTime;

                results["PerformanceTest"] = new
                {
                    OperationsCount = operations,
                    WritePerformance = new
                    {
                        Duration = $"{writeDuration.TotalMilliseconds:F2}ms",
                        OperationsPerSecond = $"{operations / writeDuration.TotalSeconds:F2} ops/sec",
                        AverageLatency = $"{writeDuration.TotalMilliseconds / operations:F4}ms"
                    },
                    ReadPerformance = new
                    {
                        Duration = $"{readDuration.TotalMilliseconds:F2}ms",
                        OperationsPerSecond = $"{retrievedItems.Count / readDuration.TotalSeconds:F2} ops/sec",
                        AverageLatency = $"{readDuration.TotalMilliseconds / retrievedItems.Count:F4}ms",
                        SuccessRate = (double)retrievedItems.Count / operations * 100
                    },
                    DeletePerformance = new
                    {
                        Duration = $"{deleteDuration.TotalMilliseconds:F2}ms",
                        OperationsPerSecond = $"{deletedCount / deleteDuration.TotalSeconds:F2} ops/sec",
                        AverageLatency = $"{deleteDuration.TotalMilliseconds / deletedCount:F4}ms"
                    },
                    OverallThroughput = $"{(operations * 3) / (writeDuration + readDuration + deleteDuration).TotalSeconds:F2} ops/sec"
                };

                return Ok(new { Success = true, Results = results });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// 获取Redis服务器信息和状态
        /// </summary>
        [HttpGet("server-info")]
        public async Task<IActionResult> GetServerInfo()
        {
            try
            {
                // 检查Redis可用性
                var isAvailable = await _redisCacheService.IsRedisAvailableAsync();

                if (!isAvailable)
                {
                    return StatusCode(503, new { Success = false, Message = "Redis server is not available" });
                }

                // 获取服务器信息
                var serverInfo = await _redisCacheService.GetServerInfoAsync();

                // 获取缓存统计信息
                var cacheStats = await _redisCacheService.GetCacheStatsAsync();

                return Ok(new {
                    Success = true,
                    ServerInfo = serverInfo,
                    CacheStats = cacheStats
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// 模式匹配和键管理测试
        /// </summary>
        [HttpPost("pattern-operations")]
        public async Task<IActionResult> TestPatternOperations()
        {
            var results = new Dictionary<string, object>();

            try
            {
                var patternPrefix = "test:pattern:";
                var testKeys = new List<string>();

                // 创建测试键
                for (int i = 1; i <= 10; i++)
                {
                    var key = $"{patternPrefix}{i}";
                    await _redisCacheService.SetAsync(key, new { Index = i, Data = $"Pattern test {i}" }, TimeSpan.FromMinutes(5));
                    testKeys.Add(key);
                }

                // 创建一些不同模式的键
                await _redisCacheService.SetAsync("test:other:1", new { Data = "Other pattern" }, TimeSpan.FromMinutes(5));
                await _redisCacheService.SetAsync("different:pattern:1", new { Data = "Different pattern" }, TimeSpan.FromMinutes(5));

                // 测���键模式匹配
                var matchedKeys = await _redisCacheService.KeysAsync($"{patternPrefix}*");
                var keyCount = await _redisCacheService.KeyCountAsync($"{patternPrefix}*");

                results["CreatedKeys"] = testKeys;
                results["MatchedKeys"] = matchedKeys;
                results["KeyCount"] = new { Matched = keyCount, Expected = testKeys.Count };

                // 测试模式删除
                var deletedBefore = await _redisCacheService.KeyDeleteAsync($"{patternPrefix}*");

                // 验证删除结果
                var matchedKeysAfterDelete = await _redisCacheService.KeysAsync($"{patternPrefix}*");
                var keyCountAfterDelete = await _redisCacheService.KeyCountAsync($"{patternPrefix}*");

                results["PatternDelete"] = new
                {
                    DeletedCount = deletedBefore,
                    KeysRemaining = matchedKeysAfterDelete.Count(),
                    Success = matchedKeysAfterDelete.Count() == 0
                };

                return Ok(new { Success = true, Results = results });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }
    }
}