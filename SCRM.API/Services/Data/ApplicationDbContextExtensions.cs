using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.Core;
using SCRM.Services.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.Services.Data
{
    public static class ApplicationDbContextExtensions
    {
        private static readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

        #region SrClient (Device)

        public static async Task<SrClient?> GetSrClient(this ApplicationDbContext context, string uuid)
        {
            if (GlobleCache.SrClients.TryGetValue(uuid, out var cached))
            {
                return cached;
            }

            return await AsyncLockManager.ExecuteWithLockAsync($"srclient_{uuid}", async () =>
            {
                if (GlobleCache.SrClients.TryGetValue(uuid, out var client))
                {
                    return client;
                }

                var dbClient = await context.SrClients
                    .Include(c => c.Accounts) // Include accounts if needed
                    .FirstOrDefaultAsync(c => c.uuid == uuid);

                if (dbClient != null)
                {
                    dbClient.WechatAccountId = dbClient.Accounts.FirstOrDefault()?.AccountId;
                    GlobleCache.SrClients.TryAdd(dbClient.uuid, dbClient);
                }

                return dbClient;
            });
        }

        public static async Task<List<SrClient>> GetAllSrClients(this ApplicationDbContext context)
        {
             // For "GetAll", we might not want to rely solely on cache if we need fresh data, 
             // but usually we fill cache from DB first.
             // For now, simple implementation: DB -> Cache fill
             var clients = await context.SrClients
                 .Include(c => c.Accounts)
                 .ToListAsync();

             foreach(var c in clients)
             {
                 c.WechatAccountId = c.Accounts.FirstOrDefault()?.AccountId;
                 GlobleCache.SrClients.TryAdd(c.uuid, c);
                 
                 // Smart Cache: Populate the Index (WechatAccounts) with the SAME instances
                 foreach(var account in c.Accounts)
                 {
                     GlobleCache.WechatAccounts.TryAdd(account.AccountId, account);
                 }
             }
             return clients;
        }

        public static async Task<SrClient?> SaveSrClient(this ApplicationDbContext context, SrClient client)
        {
            return await AsyncLockManager.ExecuteWithLockAsync($"srclient_{client.uuid}", async () =>
            {
                GlobleCache.SrClients.AddOrUpdate(client.uuid, client, (key, old) => client);
                
                await context.BulkInsertOrUpdateAsync(new List<SrClient> { client });
                return client;
            });
        }

        #endregion

        #region WechatAccount (Login Account) - Optional, simplified

        public static async Task<WechatAccount?> GetWechatAccount(this ApplicationDbContext context, long accountId)
        {
             if (GlobleCache.WechatAccounts.TryGetValue(accountId, out var cached))
            {
                return cached;
            }

            return await AsyncLockManager.ExecuteWithLockAsync($"wxaccount_{accountId}", async () =>
            {
                if (GlobleCache.WechatAccounts.TryGetValue(accountId, out var acc)) return acc;

                var dbAcc = await context.WechatAccounts.FindAsync(accountId);
                if (dbAcc != null)
                {
                    GlobleCache.WechatAccounts.TryAdd(accountId, dbAcc);
                }
                return dbAcc;
            });
        }

        #endregion

        #region Contacts (Friends)

        public static async Task<List<Contact>> GetContacts(this ApplicationDbContext context, long accountId)
        {
            if (GlobleCache.Contacts.TryGetValue(accountId, out var cachedList))
            {
                return cachedList;
            }

            return await AsyncLockManager.ExecuteWithLockAsync($"contacts_{accountId}", async () =>
            {
                if (GlobleCache.Contacts.TryGetValue(accountId, out var list)) return list;

                var dbList = await context.Contacts
                    .Where(c => c.WechatAccountId == accountId && !c.IsDeleted)
                    .OrderByDescending(c => c.LastInteractionTime)
                    .ToListAsync();

                GlobleCache.Contacts.TryAdd(accountId, dbList);
                return dbList;
            });
        }

        public static async Task SaveContact(this ApplicationDbContext context, Contact contact)
        {
             await AsyncLockManager.ExecuteWithLockAsync($"contacts_{contact.WechatAccountId}", async () =>
            {
                // Update Cache
                if (GlobleCache.Contacts.TryGetValue(contact.WechatAccountId, out var list))
                {
                    var existing = list.FirstOrDefault(c => c.Wxid == contact.Wxid);
                    if (existing != null)
                    {
                        list.Remove(existing);
                    }
                    list.Add(contact);
                    // Resort if needed or just append
                }
                else
                {
                     // Cold start cache fill to avoid partial state
                     var dbList = await context.Contacts
                        .Where(c => c.WechatAccountId == contact.WechatAccountId && !c.IsDeleted)
                        .ToListAsync();
                     // Check if our new contact is already in there (from concurrent save)
                     if (!dbList.Any(c => c.Wxid == contact.Wxid)) 
                     {
                         dbList.Add(contact);
                     }
                     GlobleCache.Contacts.TryAdd(contact.WechatAccountId, dbList);
                }

                // Update DB
                await context.BulkInsertOrUpdateAsync(new List<Contact> { contact });
            });
        }
        
        /// <summary>
        /// Saves a list of contacts (e.g. from Sync)
        /// </summary>
        public static async Task SaveContacts(this ApplicationDbContext context, long accountId, List<Contact> contacts)
        {
            if (contacts == null || !contacts.Any()) return;

            await AsyncLockManager.ExecuteWithLockAsync($"contacts_{accountId}", async () =>
            {
                // Update Cache logic: Replace or Merge? 
                // Usually Sync implies Merge/Update.
                
                if (GlobleCache.Contacts.TryGetValue(accountId, out var existingList))
                {
                     // Merge Logic
                     foreach(var c in contacts)
                     {
                         var exist = existingList.FirstOrDefault(x => x.Wxid == c.Wxid);
                         if (exist != null) existingList.Remove(exist);
                         existingList.Add(c);
                     }
                }
                else
                {
                    // If not in cache, load from DB + Merge, OR just assume this is the partial list?
                    // Safer to just let next GetContacts load full list, but here we want to ensure cache consistency.
                    // Let's just update DB and invalidate cache or fill it if empty.
                    
                    // Simple approach: Invalidate cache to force reload next time, 
                    // OR if we want "In Memory Container", we should load and update.
                    
                    // Let's update DB first
                }

                await context.BulkInsertOrUpdateAsync(contacts);
                
                // Refresh Cache
                var allContacts = await context.Contacts.Where(c => c.WechatAccountId == accountId && !c.IsDeleted).ToListAsync();
                GlobleCache.Contacts.AddOrUpdate(accountId, allContacts, (k, v) => allContacts);
            });
        }

        #endregion
    }
}
