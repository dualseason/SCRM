using SCRM.Shared.Interfaces;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models.Dtos;
using Microsoft.EntityFrameworkCore;
using SCRM.Services.Data; // For ApplicationDbContext
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SCRM.API.Services
{
    public class CrmService : ICrmService
    {
        private readonly ApplicationDbContext _db;
        
        public CrmService(ApplicationDbContext db)
        {
            _db = db;
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

        public async Task<bool> SendMessageAsync(string conversationId, string content, int type = 1)
        {
            // Placeholder: Just log to Console or SystemLog
            // Integration with NettyMessageService will happen in next iteration
            Console.WriteLine($"[CrmService] Sending Message to {conversationId}: {content}");
            return await Task.FromResult(true);
        }
    }
}
