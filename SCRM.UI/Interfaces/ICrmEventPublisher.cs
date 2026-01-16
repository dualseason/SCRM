using System;

namespace SCRM.Shared.Interfaces
{
    /// <summary>
    /// CRM 事件发布接口 (生产者契约)
    /// 供 Netty 或 后台服务调用
    /// </summary>
    public interface ICrmEventPublisher
    {
        // --- 试点模块 ---
        // --- 试点模块 ---
        void PublishDeviceStatus(string deviceId, bool isOnline);

        /// <summary>
        /// 通用事件发布
        /// </summary>
        void PublishEvent<T>(string eventName, T data);
    }
}
