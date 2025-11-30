using Serilog;
using SCRM.Models.Configurations;
using SCRM.API.Models.Entities;

namespace SCRM.Shared.Core
{
    /// <summary>
    /// 全局工具类，提供统一的日志实例和全局配置对象。
    /// </summary>
    public static class Utility
    {
        // 全局 Serilog 日志实例
        public static Serilog.ILogger logger;
        public static Serilog.ILogger efLogger;
        public static Serilog.ILogger loggerToDB;

        // 全局用户模型（示例）
        // 已删除：用户模型由 WechatAccount 实体表示

        // 配置对象，由 AppConfigurationManager 初始化后赋值
        public static Settings settings;
    }
}
