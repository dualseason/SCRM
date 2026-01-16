using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SCRM.API.Models.Entities; // Assuming Entities are here or in SHARED. If SHARED, adjust namespace.
using SCRM.SHARED.Models.Dtos;
using SCRM.SHARED.Models;

namespace SCRM.Shared.Interfaces
{
    /// <summary>
    /// CRM 核心业务服务接口 (操作契约)
    /// </summary>
    public interface ICrmService
    {
        // --- 试点模块: 设备管理 (Phase 2) ---
        Task<List<SrClient>> GetDevicesAsync();
        Task<SrClient?> GetDeviceAsync(string uuid);

        // --- 铺开模块: 聊天/联系人 (Phase 3) ---
        Task<List<Contact>> GetContactsAsync(string? deviceId = null);
        Task<List<Conversation>> GetConversationsAsync(string? deviceId = null);
        
        // Chat
        Task<List<Message>> GetMessagesAsync(string conversationId, int count = 50);
        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="deviceUuid">设备UUID (标识哪个微信)</param>
        /// <param name="conversationId">对方WXID</param>
        /// <param name="content">内容</param>
        /// <param name="type">类型</param>
        Task<bool> SendMessageAsync(string deviceUuid, string conversationId, string content, int type = 1);

        /// <summary>
        /// 请求设备截屏
        /// </summary>
        Task<bool> RequestScreenShotAsync(string deviceUuid); 
        // ... existing methods ...
        Task<WechatAccountSettings> GetAccountSettingsAsync(long accountId);
        Task<bool> UpdateAccountSettingsAsync(long accountId, WechatAccountSettings settings);
        Task<bool> ExecuteGroupActionAsync(string deviceUuid, string chatRoomId, int action, string content, int intValue);
        Task<bool> AcceptFriendRequestAsync(string deviceUuid, string friendId, string friendNick);
    }
}
