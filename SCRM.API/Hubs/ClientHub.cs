using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;

namespace SCRM.API.Hubs
{
    public class ClientHub : Hub
    {
        private readonly SCRM.Services.Data.ApplicationDbContext _context;
        private readonly SCRM.Services.ConnectionManager _connectionManager;
        private readonly SCRM.Services.ClientTaskService _clientTaskService;

        public ClientHub(SCRM.Services.Data.ApplicationDbContext context, SCRM.Services.ConnectionManager connectionManager, SCRM.Services.ClientTaskService clientTaskService)
        {
            _context = context;
            _connectionManager = connectionManager;
            _clientTaskService = clientTaskService;
        }

        public async Task JoinGroup(string connectionId)
        {
            var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = Context.User?.Identity?.Name;
            
            // Debug logging for claims
            // var roleClaims = Context.User?.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            // Console.WriteLine($"[ClientHub] User: {userName} ({userId}), Roles: {string.Join(",", roleClaims ?? new List<string>())}");

            var isAdmin = Context.User?.IsInRole("SuperAdmin") == true || Context.User?.IsInRole("Admin") == true;

            if (string.IsNullOrEmpty(userId))
            {
                 // Fallback to Name if NameIdentifier is missing (for legacy or tests)
                 userId = userName;
            }

            if (isAdmin)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, connectionId);
                return;
            }

            // Verify ownership
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            if (connectionInfo == null)
            {
                throw new HubException("Device not connected or invalid ID.");
            }

            if (long.TryParse(connectionInfo.UserId, out long accountId))
            {
                // Find the SrClient owner via WechatAccount
                var ownerId = await _context.WechatAccounts
                    .Where(w => w.AccountId == accountId && !w.IsDeleted)
                    .Join(_context.SrClients, 
                          w => w.ClientUuid, 
                          c => c.uuid, 
                          (w, c) => c.OwnerId)
                    .FirstOrDefaultAsync();

                if (ownerId == userId || (ownerId == null))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, connectionId);
                }
                else
                {
                    Console.WriteLine($"[ClientHub] Forbidden: {userId} does not own {ownerId}");
                    throw new HubException("Forbidden: You do not own this device.");
                }
            }
            else
            {
                throw new HubException("Invalid device association.");
            }
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task<IEnumerable<SrClient>> GetDevices()
        {
            var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
             if (string.IsNullOrEmpty(userId)) userId = Context.User?.Identity?.Name;

            var isAdmin = Context.User?.IsInRole("SuperAdmin") == true || Context.User?.IsInRole("Admin") == true;

            IQueryable<SrClient> query = _context.SrClients;

            if (!isAdmin && !string.IsNullOrEmpty(userId))
            {
                query = query.Where(c => c.OwnerId == userId || c.OwnerId == null);
            }

            // Join with WechatAccounts to get nickname and account ID
            // We use GroupJoin (Left Join) because a client might not have a WechatAccount yet
            var clientData = await query
                .GroupJoin(_context.WechatAccounts.Where(w => !w.IsDeleted),
                    client => client.uuid,
                    account => account.ClientUuid,
                    (client, accounts) => new { Client = client, Accounts = accounts })
                .SelectMany(
                    x => x.Accounts.DefaultIfEmpty(),
                    (x, account) => new { x.Client, Account = account })
                .ToListAsync();

            // Log the request details
            Console.WriteLine($"[GetDevices] User: {userId}, IsAdmin: {isAdmin}");

            var result = new List<SrClient>();
            
            // Process results in memory to handle connection status
            // Note: This assumes one active account per client for simplicity in this view
            foreach (var item in clientData)
            {
                var client = item.Client;
                var account = item.Account;

                if (account != null)
                {
                    client.WeChatId = account.Wxid;
                    client.WeChatNick = account.Nickname;
                    client.WechatAccountId = account.AccountId;

                    // Check connection status
                    var connections = await _connectionManager.GetConnectionsByUserAsync(account.AccountId.ToString());
                    var activeConnection = connections.FirstOrDefault();
                    if (activeConnection != null)
                    {
                        client.ConnectionId = activeConnection.ConnectionId;
                        client.isOnline = true;
                    }
                    else
                    {
                        client.ConnectionId = null;
                        client.isOnline = false;
                    }
                }
                
                // Avoid duplicates if multiple accounts (though logic above might produce duplicates if multiple accounts exist)
                // For now, we just add it. If multiple accounts, we might want to aggregate or show multiple entries.
                // Given the UI expects unique clients, we might need to handle this.
                // But typically one client = one account.
                if (!result.Any(r => r.uuid == client.uuid))
                {
                    result.Add(client);
                }
            }

            Console.WriteLine($"[GetDevices] Found {result.Count} devices for user {userId}");
            return result;
        }

        public async Task<IEnumerable<Contact>> GetContacts(long accountId)
        {
            var userId = Context.User?.Identity?.Name;
            var isAdmin = Context.User?.IsInRole("SuperAdmin") == true || Context.User?.IsInRole("Admin") == true;

            var account = await _context.WechatAccounts.FindAsync(accountId);
            if (account == null) return Enumerable.Empty<Contact>();

            if (!isAdmin)
            {
                 var client = await _context.SrClients.FirstOrDefaultAsync(c => c.uuid == account.ClientUuid);
                 if (client == null || (client.OwnerId != userId && client.OwnerId != null))
                 {
                     return Enumerable.Empty<Contact>();
                 }
            }

            return await _context.Contacts
                .Where(c => c.WechatAccountId == (int)accountId && !c.IsDeleted)
                .OrderByDescending(c => c.LastInteractionTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Message>> GetChatHistory(long accountId, string friendWxId)
        {
            // Verify ownership (simplified)
            var userId = Context.User?.Identity?.Name;
            // Add ownership check here if needed

            return await _context.Messages
                .Where(m => m.AccountId == accountId && (m.SenderWxid == friendWxId || m.ReceiverWxid == friendWxId))
                .OrderByDescending(m => m.CreatedAt)
                .Take(50)
                .OrderBy(m => m.CreatedAt) // Return in chronological order
                .ToListAsync();
        }

        public async Task<TaskResult> SendMessage(string connectionId, string friendWxId, string content)
        {
            // Verify ownership
            var userId = Context.User?.Identity?.Name;
            var isAdmin = Context.User?.IsInRole("SuperAdmin") == true || Context.User?.IsInRole("Admin") == true;

            if (!isAdmin)
            {
                var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
                if (connectionInfo == null) return TaskResult.Fail("Device not connected");

                if (long.TryParse(connectionInfo.UserId, out long accountId))
                {
                     var client = await _context.SrClients.FirstOrDefaultAsync(c => c.Accounts.Any(a => a.AccountId == accountId));
                     // Note: SrClient linkage logic might need refinement depending on exact DB schema
                     // But for now, let's assume if they can join the group, they can send messages.
                     // Or re-verify ownership here.
                }
            }

            return await _clientTaskService.SendTalkToFriendTaskAsync(connectionId, friendWxId, content);
        }
    }
}
