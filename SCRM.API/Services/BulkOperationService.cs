using Microsoft.EntityFrameworkCore;
using SCRM.Data;
using System.Diagnostics;
using System.Linq.Expressions;
using EFCore.BulkExtensions;

namespace SCRM.Services
{
    public class BulkOperationService : IBulkOperationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BulkOperationService> _logger;

        public BulkOperationService(ApplicationDbContext context, ILogger<BulkOperationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task BulkInsertAsync<T>(List<T> entities) where T : class
        {
            try
            {
                _logger.LogInformation("开始批量插入 {Count} 条 {EntityType} 记录", entities.Count, typeof(T).Name);
                await _context.BulkInsertAsync(entities);
                _logger.LogInformation("批量插入 {EntityType} 记录完成", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量插入 {EntityType} 记录时发生错误", typeof(T).Name);
                throw;
            }
        }

        public async Task BulkInsertAsync<T>(List<T> entities, BulkConfig bulkConfig) where T : class
        {
            try
            {
                _logger.LogInformation("开始批量插入 {Count} 条 {EntityType} 记录 (自定义配置)", entities.Count, typeof(T).Name);
                await _context.BulkInsertAsync(entities, bulkConfig);
                _logger.LogInformation("自定义配置批量插入 {EntityType} 记录完成", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自定义配置批量插入 {EntityType} 记录时发生错误", typeof(T).Name);
                throw;
            }
        }

        public async Task BulkUpdateAsync<T>(List<T> entities) where T : class
        {
            try
            {
                _logger.LogInformation("开始批量更新 {Count} 条 {EntityType} 记录", entities.Count, typeof(T).Name);
                await _context.BulkUpdateAsync(entities);
                _logger.LogInformation("批量更新 {EntityType} 记录完成", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量更新 {EntityType} 记录时发生错误", typeof(T).Name);
                throw;
            }
        }

        public async Task BulkUpdateAsync<T>(List<T> entities, BulkConfig bulkConfig) where T : class
        {
            try
            {
                _logger.LogInformation("开始批量更新 {Count} 条 {EntityType} 记录 (自定义配置)", entities.Count, typeof(T).Name);
                await _context.BulkUpdateAsync(entities, bulkConfig);
                _logger.LogInformation("自定义配置批量更新 {EntityType} 记录完成", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自定义配置批量更新 {EntityType} 记录时发生错误", typeof(T).Name);
                throw;
            }
        }

        public async Task BulkUpdateAsync<T>(Expression<Func<T, T>> updateExpression, Expression<Func<T, bool>> whereCondition) where T : class
        {
            try
            {
                _logger.LogInformation("开始条件批量更新 {EntityType} 记录", typeof(T).Name);
                var entitiesToUpdate = await _context.Set<T>().Where(whereCondition).ToListAsync();
                if (entitiesToUpdate.Any())
                {
                    await _context.BulkUpdateAsync(entitiesToUpdate);
                }
                _logger.LogInformation("条件批量更新 {EntityType} 记录完成", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "条件批量更新 {EntityType} 记录时发生错误", typeof(T).Name);
                throw;
            }
        }

        public async Task BulkDeleteAsync<T>(List<T> entities) where T : class
        {
            try
            {
                _logger.LogInformation("开始批量删除 {Count} 条 {EntityType} 记录", entities.Count, typeof(T).Name);
                await _context.BulkDeleteAsync(entities);
                _logger.LogInformation("批量删除 {EntityType} 记录完成", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量删除 {EntityType} 记录时发生错误", typeof(T).Name);
                throw;
            }
        }

        public async Task BulkDeleteAsync<T>(Expression<Func<T, bool>> whereCondition) where T : class
        {
            try
            {
                _logger.LogInformation("开始条件批量删除 {EntityType} 记录", typeof(T).Name);
                var entitiesToDelete = await _context.Set<T>().Where(whereCondition).ToListAsync();
                if (entitiesToDelete.Any())
                {
                    await _context.BulkDeleteAsync(entitiesToDelete);
                }
                _logger.LogInformation("条件批量删除 {EntityType} 记录完成", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "条件批量删除 {EntityType} 记录时发生错误", typeof(T).Name);
                throw;
            }
        }

        public async Task BulkDeleteAsync<T>(List<T> entities, BulkConfig bulkConfig) where T : class
        {
            try
            {
                _logger.LogInformation("开始批量删除 {Count} 条 {EntityType} 记录 (自定义配置)", entities.Count, typeof(T).Name);
                await _context.BulkDeleteAsync(entities, bulkConfig);
                _logger.LogInformation("自定义配置批量删除 {EntityType} 记录完成", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自定义配置批量删除 {EntityType} 记录时发生错误", typeof(T).Name);
                throw;
            }
        }

        public async Task<List<T>> BulkReadAsync<T>(Expression<Func<T, bool>> whereCondition) where T : class
        {
            try
            {
                _logger.LogInformation("开始批量读取 {EntityType} 记录", typeof(T).Name);
                var result = await _context.Set<T>().Where(whereCondition).ToListAsync();
                _logger.LogInformation("批量读取 {Count} 条 {EntityType} 记录完成", result.Count, typeof(T).Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量读取 {EntityType} 记录时发生错误", typeof(T).Name);
                throw;
            }
        }

        public async Task BulkMergeAsync<T>(List<T> entities) where T : class
        {
            try
            {
                _logger.LogInformation("开始批量合并 {Count} 条 {EntityType} 记录", entities.Count, typeof(T).Name);
                await _context.BulkInsertOrUpdateAsync(entities);
                _logger.LogInformation("批量合并 {EntityType} 记录完成", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量合并 {EntityType} 记录时发生错误", typeof(T).Name);
                throw;
            }
        }

        public async Task BulkMergeAsync<T>(List<T> entities, BulkConfig bulkConfig) where T : class
        {
            try
            {
                _logger.LogInformation("开始批量合并 {Count} 条 {EntityType} 记录 (自定义配置)", entities.Count, typeof(T).Name);
                await _context.BulkInsertOrUpdateAsync(entities, bulkConfig);
                _logger.LogInformation("自定义配置批量合并 {EntityType} 记录完成", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自定义配置批量合并 {EntityType} 记录时发生错误", typeof(T).Name);
                throw;
            }
        }

        public async Task TruncateAsync<T>() where T : class
        {
            try
            {
                _logger.LogInformation("开始截断 {EntityType} 表", typeof(T).Name);
                await _context.TruncateAsync<T>();
                _logger.LogInformation("截断 {EntityType} 表完成", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "截断 {EntityType} 表时发生错误", typeof(T).Name);
                throw;
            }
        }

        public async Task<BulkOperationResult> BenchmarkBulkInsertAsync<T>(List<T> entities) where T : class
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("开始批量插入性能测试 - {Count} 条记录", entities.Count);
                await _context.BulkInsertAsync(entities);
                stopwatch.Stop();

                var result = new BulkOperationResult
                {
                    RecordCount = entities.Count,
                    ElapsedTime = stopwatch.Elapsed,
                    OperationType = "BulkInsert"
                };

                _logger.LogInformation("批量插入性能测试完成 - {PerformanceMetrics}", result.PerformanceMetrics);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "批量插入性能测试失败");
                throw;
            }
        }

        public async Task<BulkOperationResult> BenchmarkRegularInsertAsync<T>(List<T> entities) where T : class
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("开始常规插入性能测试 - {Count} 条记录", entities.Count);

                await _context.Set<T>().AddRangeAsync(entities);
                await _context.SaveChangesAsync();

                stopwatch.Stop();

                var result = new BulkOperationResult
                {
                    RecordCount = entities.Count,
                    ElapsedTime = stopwatch.Elapsed,
                    OperationType = "RegularInsert"
                };

                _logger.LogInformation("常规插入性能测试完成 - {PerformanceMetrics}", result.PerformanceMetrics);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "常规插入性能测试失败");
                throw;
            }
        }
    }
}