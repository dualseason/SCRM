using Microsoft.Extensions.Logging;
using SCRM.Services.Data;
using SCRM.API.Models.Entities;

namespace SCRM.API.Services
{
    /// <summary>
    /// 系统审计日志服务接口
    /// </summary>
    public interface ISystemLogService
    {
        /// <summary>
        /// 异步记录日志
        /// </summary>
        Task LogAsync(string level, string module, string action, string message, string? operatorId = null, string? targetId = null, string? clientIp = null);
        
        /// <summary>
        /// 异步记录信息级别日志
        /// </summary>
        Task LogInfoAsync(string module, string action, string message, string? operatorId = null, string? targetId = null);
        
        /// <summary>
        /// 异步记录警告级别日志
        /// </summary>
        Task LogWarningAsync(string module, string action, string message, string? operatorId = null, string? targetId = null);
        
        /// <summary>
        /// 异步记录错误级别日志
        /// </summary>
        Task LogErrorAsync(string module, string action, string message, string? operatorId = null, string? targetId = null);
    }

    /// <summary>
    /// 系统审计日志服务实现
    /// </summary>
    public class SystemLogService : ISystemLogService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<SystemLogService> _logger;

        public SystemLogService(ApplicationDbContext dbContext, ILogger<SystemLogService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// 后台记录日志
        /// </summary>
        public async Task LogAsync(string level, string module, string action, string message, string? operatorId = null, string? targetId = null, string? clientIp = null)
        {
            try
            {
                var log = new SystemLog
                {
                    level = level,
                    module = module,
                    action = action,
                    message = message,
                    operatorId = operatorId,
                    targetId = targetId,
                    clientIp = clientIp,
                    createdAt = DateTime.UtcNow
                };

                _dbContext.SystemLogs.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // 如果数据库记录失败，使用标准日志作为兜底
                _logger.LogError(ex, "写入系统日志失败: {Module} - {Action}", module, action);
            }
        }

        public Task LogInfoAsync(string module, string action, string message, string? operatorId = null, string? targetId = null)
        {
            return LogAsync("Info", module, action, message, operatorId, targetId);
        }

        public Task LogWarningAsync(string module, string action, string message, string? operatorId = null, string? targetId = null)
        {
            return LogAsync("Warning", module, action, message, operatorId, targetId);
        }

        public Task LogErrorAsync(string module, string action, string message, string? operatorId = null, string? targetId = null)
        {
            return LogAsync("Error", module, action, message, operatorId, targetId);
        }
    }
}
