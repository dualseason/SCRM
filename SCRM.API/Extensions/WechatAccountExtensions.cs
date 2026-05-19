using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.Services.Data; // For ApplicationDbContext
using SCRM.Shared.Interfaces; // Correct Namespace for ICrmService
using SCRM.SHARED.Models; // For ContactFilter/DTOs if needed
using SCRM.API.Services.Data;

namespace SCRM.API.Models.Entities // Trick: Same namespace as Entity for auto-discovery
{
    public static class WechatAccountExtensions
    {



        // ========== 联系人 (Contacts) ==========

        /// <summary>
        /// 获取该账号的联系人列表
        /// </summary>
        public static async Task<List<Contact>> GetContactsAsync(this WechatAccount account, ApplicationDbContext db)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            if (db == null) throw new ArgumentNullException(nameof(db));

            return await db.Contacts
                .AsNoTracking()
                .Where(c => c.ownerWxid == account.wxid)
                .OrderByDescending(c => c.createdAt)
                .ToListAsync();
        }
        
        /// <summary>
        /// 获取单个联系人
        /// </summary>
        public static async Task<Contact?> GetContactAsync(this WechatAccount account, ApplicationDbContext db, string targetWxid)
        {
            return await db.Contacts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ownerWxid == account.wxid && c.wxid == targetWxid);
        }

        // ========== 消息 (Messages) ==========

        /// <summary>
        /// 获取和某人的聊天记录
        /// </summary>
        public static async Task<List<Message>> GetMessagesAsync(this WechatAccount account, ApplicationDbContext db, string friendWxid, int count = 50)
        {
             var messages = await db.Messages
                .AsNoTracking()
                .Where(m => m.accountId == account.wxid && (m.senderWxid == friendWxid || m.receiverWxid == friendWxid)) 
                .OrderByDescending(m => m.createdAt)
                .Take(count)
                .OrderBy(m => m.createdAt) // Re-sort for display
                .ToListAsync();

             await db.EnrichMessagesWithMediaMetadataAsync(messages);
             return messages;
        }

        // ========== 业务操作 (Via CrmService) ==========

        /// <summary>
        /// 发送文本消息
        /// </summary>
        public static async Task<bool> SendMessageAsync(this WechatAccount account, ICrmService crm, string toWxid, string content)
        {
            if (string.IsNullOrEmpty(account.clientUuid)) return false;
            var result = await crm.SendMessageAsync(account.clientUuid, toWxid, content);
            return result.success;
        }

        /// <summary>
        /// 通过好友请求
        /// </summary>
        public static async Task<bool> AcceptFriendAsync(this WechatAccount account, ICrmService crm, string friendWxid, string friendNick)
        {
             if (string.IsNullOrEmpty(account.clientUuid)) return false;
             var result = await crm.AcceptFriendRequestAsync(account.clientUuid, friendWxid, friendNick);
             return result.success;
        }
    }
}
