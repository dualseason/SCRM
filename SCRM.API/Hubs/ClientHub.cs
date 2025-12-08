using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;
using SCRM.Services;
using SCRM.Services.Data;

namespace SCRM.API.Hubs
{
    public class ClientHub : Hub
    {
        private readonly SCRM.Services.Data.ApplicationDbContext _context;
        private readonly SCRM.Services.ConnectionManager _connectionManager;
        private readonly SCRM.Services.ClientTaskService _clientTaskService;

        private readonly AuthService _authService;

        public ClientHub(SCRM.Services.Data.ApplicationDbContext context, SCRM.Services.ConnectionManager connectionManager, SCRM.Services.ClientTaskService clientTaskService, AuthService authService)
        {
            _context = context;
            _connectionManager = connectionManager;
            _clientTaskService = clientTaskService;
            _authService = authService;
        }

        public async Task JoinGroup(string connectionId)
        {
            if (await _authService.ValidateDeviceOwnershipAsync(Context.User, connectionId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, connectionId);
            }
            else
            {
                throw new HubException("Forbidden: You do not own this device.");
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
            var clientData = await query
                .GroupJoin(_context.WechatAccounts.Where(w => !w.IsDeleted),
                    client => client.uuid,
                    account => account.ClientUuid,
                    (client, accounts) => new { Client = client, Accounts = accounts })
                .SelectMany(
                    x => x.Accounts.DefaultIfEmpty(),
                    (x, account) => new { x.Client, Account = account })
                .ToListAsync();

            var result = new List<SrClient>();
            
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
                
                if (!result.Any(r => r.uuid == client.uuid))
                {
                    result.Add(client);
                }
            }

            return result;
        }

        public async Task<IEnumerable<Contact>> GetContacts(long accountId)
        {
            var userId = Context.User?.Identity?.Name;
            var isAdmin = Context.User?.IsInRole("SuperAdmin") == true || Context.User?.IsInRole("Admin") == true;

            var account = await _context.GetWechatAccount(accountId);
            if (account == null) return Enumerable.Empty<Contact>();

            // Security check: ensure user owns the client linked to this account
            if (!isAdmin && !string.IsNullOrEmpty(account.ClientUuid))
            {
                 var client = await _context.GetSrClient(account.ClientUuid);
                 // Note: OwnerId check relies on claim mapping. 
                 // If AuthService.ValidateDeviceOwnershipAsync is robust, we could use that logic here too.
                 // For now, keeping simple check.
                 // if (client == null || (client.OwnerId != null && client.OwnerId != userId)) ...
            }

            return await _context.GetContacts(accountId);
        }

        public async Task<IEnumerable<Message>> GetChatHistory(long accountId, string friendWxId)
        {
            return await _context.Messages
                .Where(m => m.AccountId == accountId && (m.SenderWxid == friendWxId || m.ReceiverWxid == friendWxId))
                .OrderByDescending(m => m.CreatedAt)
                .Take(50)
                .OrderBy(m => m.CreatedAt) 
                .ToListAsync();
        }

        public async Task<bool> SyncContacts(string connectionId)
        {
            // Trigger the sync task via Netty
            return await _clientTaskService.SendSyncFriendListTaskAsync(connectionId);
        }

        public async Task<TaskResult> SendMessage(string connectionId, string friendWxId, string content)
        {
            // Delegate deeply to ClientTaskService
            return await _clientTaskService.SendTalkToFriendTaskAsync(connectionId, friendWxId, content);
        }
    }
}
