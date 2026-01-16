using SCRM.Shared.Interfaces;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models.Dtos;
using Microsoft.EntityFrameworkCore;
using SCRM.Services.Data; // For ApplicationDbContext
using System.Collections.Generic;
using System.Threading.Tasks;
using SCRM.SHARED.Models;
using System.Text.Json;

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
            return await _db.SrClients
                .Include(c => c.accounts)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<SrClient?> GetDeviceAsync(string uuid)
        {
             return await _db.SrClients
                .Include(c => c.accounts)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.uuid == uuid);
        }

        // --- Phase 3 Implementation ---
        
        public async Task<List<Contact>> GetContactsAsync(string? deviceId = null)
        {
            var query = _db.Contacts.AsNoTracking();
            // In a real scenario, filter by deviceId if provided (via associated WechatAccount)
            // For now return top 100 to check UI
            return await query.OrderByDescending(c => c.id).Take(100).ToListAsync();
        }

        public async Task<List<Conversation>> GetConversationsAsync(string? deviceId = null)
        {
             var query = _db.Conversations.AsNoTracking();
             return await query.OrderByDescending(c => c.lastMessageTime).Take(50).ToListAsync();
        }

        public async Task<List<Message>> GetMessagesAsync(string conversationId, int count = 50)
        {
            // conversationId is expected to be a WXID (e.g., wxid_... or xxxxx@chatroom)
            // We need to fetch messages where this WXID is involved.
            // Note: This simple query might return messages from multiple local accounts if they talk to the same person.
            // In Phase 4, we should filter by current Account Context.
            
            return await _db.Messages
                .Where(m => m.senderWxid == conversationId || m.receiverWxid == conversationId)
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
            return await _deviceCommandService.SendMessageAsync(deviceUuid, conversationId, content);
        }

        public async Task<bool> RequestScreenShotAsync(string deviceUuid)
        {
            return await _deviceCommandService.RequestScreenShotAsync(deviceUuid);
        }

        public async Task<WechatAccountSettings> GetAccountSettingsAsync(long accountId)
        {
            var account = await _db.WechatAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.accountId == accountId);
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

        public async Task<bool> UpdateAccountSettingsAsync(long accountId, WechatAccountSettings settings)
        {
            var account = await _db.WechatAccounts.FirstOrDefaultAsync(a => a.accountId == accountId);
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
