using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SCRM.API.Models.Entities; // Assuming Entities are here or in SHARED. If SHARED, adjust namespace.
using SCRM.SHARED.Models.Dtos;

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
        Task<bool> SendMessageAsync(string conversationId, string content, int type = 1); // 1 = Text 
    }
}
