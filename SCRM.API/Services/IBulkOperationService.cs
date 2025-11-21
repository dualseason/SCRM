using Microsoft.EntityFrameworkCore;
using SCRM.Data;
using System.Linq.Expressions;
using EFCore.BulkExtensions;

namespace SCRM.Services
{
    public interface IBulkOperationService
    {
        // 批量插入
        Task BulkInsertAsync<T>(List<T> entities) where T : class;
        Task BulkInsertAsync<T>(List<T> entities, BulkConfig bulkConfig) where T : class;

        // 批量更新
        Task BulkUpdateAsync<T>(List<T> entities) where T : class;
        Task BulkUpdateAsync<T>(List<T> entities, BulkConfig bulkConfig) where T : class;
        Task BulkUpdateAsync<T>(Expression<Func<T, T>> updateExpression, Expression<Func<T, bool>> whereCondition) where T : class;

        // 批量删除
        Task BulkDeleteAsync<T>(List<T> entities) where T : class;
        Task BulkDeleteAsync<T>(Expression<Func<T, bool>> whereCondition) where T : class;
        Task BulkDeleteAsync<T>(List<T> entities, BulkConfig bulkConfig) where T : class;

        // 批量读取
        Task<List<T>> BulkReadAsync<T>(Expression<Func<T, bool>> whereCondition) where T : class;

        // 批量合并（插入或更新）
        Task BulkMergeAsync<T>(List<T> entities) where T : class;
        Task BulkMergeAsync<T>(List<T> entities, BulkConfig bulkConfig) where T : class;

        // 截断表
        Task TruncateAsync<T>() where T : class;

        // 批量操作性能测试
        Task<BulkOperationResult> BenchmarkBulkInsertAsync<T>(List<T> entities) where T : class;
        Task<BulkOperationResult> BenchmarkRegularInsertAsync<T>(List<T> entities) where T : class;
    }

    public class BulkOperationResult
    {
        public int RecordCount { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public decimal RecordsPerSecond => RecordCount == 0 ? 0 : (decimal)RecordCount / (decimal)ElapsedTime.TotalSeconds;
        public string OperationType { get; set; } = string.Empty;
        public string PerformanceMetrics => $"{RecordCount} records in {ElapsedTime.TotalSeconds:F2}s ({RecordsPerSecond:F0} records/sec)";
    }
}