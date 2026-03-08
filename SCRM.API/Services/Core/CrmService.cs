using SCRM.Shared.Interfaces;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models.Dtos;
using Microsoft.EntityFrameworkCore;
using SCRM.Services.Data; // For ApplicationDbContext
using System.Collections.Generic;
using System.Threading.Tasks;
using SCRM.SHARED.Models;
using System.Text.Json;
using SCRM.API.Services.Data;

namespace SCRM.API.Services.Core
{
    public class CrmService : ICrmService
    {
        private readonly ApplicationDbContext _db;
        private readonly ServerDeviceCommandService _deviceCommandService;
        
        public CrmService(ApplicationDbContext db, ServerDeviceCommandService deviceCommandService)
        {
            _db = db;
            _deviceCommandService = deviceCommandService;
        }

        public async Task<List<SrClient>> GetDevicesAsync()
        {
            // Pilot Implementation
            var devices = DbHelper.GetAllSrClients();

            //foreach (SrClient device in devices)
            //{
            //    WechatAccount? activeAccount = device.accounts.FirstOrDefault(a => a.accountStatus == 1);
            //    if (activeAccount != null)
            //    {
            //        device.weChatId = activeAccount.wxid;
            //        device.weChatNick = activeAccount.nickname;
            //        device.weChatAvatar = activeAccount.avatar;
            //        device.wechatAccountId = activeAccount.accountId;
            //        // device.isOnline = true; // Use connection status or logic as needed
            //    }
            //} 
            
            return devices;
        }

        public async Task<SrClient?> GetDeviceAsync(string uuid)
        {
             return await _db.GetSrClient(uuid);
        }

        // --- Phase 3 Implementation ---
        
        public async Task<List<Contact>> GetContactsAsync(string? accountId = null)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return new List<Contact>();
            }

            return await _db.GetContacts(accountId);
        }

        public async Task<List<Conversation>> GetConversationsAsync(string? accountId = null)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return new List<Conversation>();
            }

            return await _db.Conversations
                .AsNoTracking()
                .Where(c => c.wechatAccountId == accountId && !c.isDeleted)
                .OrderByDescending(c => c.lastMessageTime)
                .Take(100)
                .ToListAsync();
        }

        public async Task<List<Message>> GetMessagesAsync(string accountId, string conversationId, int count = 50)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(conversationId))
            {
                return new List<Message>();
            }

            return await _db.Messages
                .AsNoTracking()
                .Where(m => m.accountId == accountId
                    && (m.senderWxid == conversationId || m.receiverWxid == conversationId)
                    && !m.isDeleted)
                .OrderByDescending(m => m.createdAt)
                .Take(count)
                .OrderBy(m => m.createdAt)
                .ToListAsync();
        }

        public async Task<bool> SendMessageAsync(string deviceUuid, string conversationId, string content, int type = 1)
        {
            if (string.IsNullOrEmpty(deviceUuid)) return false;
            
            // Delegate to DeviceCommandService (Netty)
            // Assuming ServerDeviceCommandService has SendMessageAsync
            return await _deviceCommandService.SendMessageAsync(deviceUuid, conversationId, content, type);
        }

        public async Task<bool> RequestScreenShotAsync(string deviceUuid)
        {
            return await _deviceCommandService.RequestScreenShotAsync(deviceUuid);
        }

        public async Task<WechatAccountSettings> GetAccountSettingsAsync(string accountId)
        {
            var account = await _db.WechatAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.wxid == accountId);
            if (account == null || string.IsNullOrEmpty(account.settings))
            {
                return new WechatAccountSettings();
            }

            try
            {
                return JsonSerializer.Deserialize<WechatAccountSettings>(account.settings) ?? new WechatAccountSettings();
            }
            catch
            {
                return new WechatAccountSettings();
            }
        }

        public async Task<bool> UpdateAccountSettingsAsync(string accountId, WechatAccountSettings settings)
        {
            var account = await _db.WechatAccounts.FirstOrDefaultAsync(a => a.wxid == accountId);
            if (account == null) return false;

            try
            {
                account.settings = JsonSerializer.Serialize(settings);
                account.updatedAt = DateTime.UtcNow;
                _db.WechatAccounts.Update(account);
                await _db.SaveChangesAsync();

                // Push to device if connected
                // We need clientUuid to get connectionId
                // account.clientUuid might be null if not linked properly, but usually it is.
                if (!string.IsNullOrEmpty(account.clientUuid))
                {
                    await _deviceCommandService.PushAccountSettingsAsync(account.clientUuid, settings);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CrmService] Error updating account settings: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExecuteGroupActionAsync(string deviceUuid, string chatRoomId, int action, string content, int intValue)
        {
            return await _deviceCommandService.ExecuteGroupActionAsync(deviceUuid, chatRoomId, action, content, intValue);
        }

        public async Task<bool> AcceptFriendRequestAsync(string deviceUuid, string friendId, string friendNick)
        {
            return await _deviceCommandService.AcceptFriendRequestAsync(deviceUuid, friendId, friendNick);
        }
    }
}
