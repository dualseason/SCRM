using System;
using SCRM.API.Models.Entities;

namespace SCRM.Shared.Interfaces
{
    /// <summary>
    /// CRM 事件订阅接口 (消费者契约)
    /// 统一使用 IDisposable Subscribe Pattern
    /// </summary>
    public interface ICrmEvents
    {
        // --- 试点模块: 设备状态 ---
        
        /// <summary>
        /// 订阅设备状态变更 (上线/下线)
        /// Action 参数: (deviceId, isOnline)
        /// </summary>
        IDisposable SubscribeToDeviceStatus(Action<string, bool> handler);

        // --- 铺开模块 ---
        // IDisposable SubscribeToMessages(Action<MessageDto> handler);

        /// <summary>
        /// 通用事件订阅
        /// </summary>
        IDisposable SubscribeToEvent<T>(string eventName, Action<T> handler);
    }
}
