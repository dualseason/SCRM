using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SCRM.SHARED.Models;
using EFCore.BulkExtensions;
using SCRM.API.Models.Entities;
using SCRM.API.Models;

namespace SCRM.API.Services.Data
{
    public static class DbHelper
    {
        /// <summary>
        /// 通用原子更新方法
        /// </summary>
        public static async Task<T?> UpdateAtomicGeneric<T>(
            this DbContext context,
            string id,
            ConcurrentDictionary<string, T> cache, // 指定缓存
            Action<T> updateAction)                // 指定修改逻辑
            where T : class, ICacheable<T>
        {
            // 自动生成锁 Key (如: "SrClient_12345")
            string lockKey = $"{typeof(T).Name}_{id}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                // A. 获取数据 (优先读缓存 -> 缓存没有读DB)
                if (!cache.TryGetValue(id, out var item))
                {
                    // 利用 EF Core 泛型 FindAsync 查找
                    item = await context.Set<T>().FindAsync(id);
                    if (item != null)
                    {
                        // 添加到缓存
                        cache.TryAdd(id, item);
                    }
                }
                
                // 1. 总是先尝试从 DB (Context) 获取被追踪的对象。
                //    这样确保我们修改的是 Context 能保存的对象。
                var dbItem = await context.Set<T>().FindAsync(id);
                if (dbItem == null) 
                {
                     // DB 都没有
                     return null;
                }
                
                // 2. 执行修改
                updateAction(dbItem);
                
                // 3. 保存
                await context.SaveChangesAsync();
                
                // 4. 更新缓存
                // dbItem 现在是最新的。更新 GlobalCache。
                // 仅当缓存中已存在时更新，或者总是 AddOrUpdate？
                cache.AddOrUpdate(id, dbItem, (k, old) => old.CopyFrom(dbItem));
                
                return dbItem;
            });
        }
        
        /// <summary>
        /// 通用原子更新方法 (Key为long的情况，如WechatAccount)
        /// </summary>
        public static async Task<T?> UpdateAtomicGeneric<T>(
            this DbContext context,
            long id,
            ConcurrentDictionary<long, T> cache, // 指定缓存
            Action<T> updateAction)              // 指定修改逻辑
            where T : class, ICacheable<T>
        {
            string lockKey = $"{typeof(T).Name}_{id}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                if (!cache.TryGetValue(id, out var item))
                {
                    item = await context.Set<T>().FindAsync(id);
                    if (item != null)
                    {
                        cache.TryAdd(id, item);
                    }
                }
                
                var dbItem = await context.Set<T>().FindAsync(id);
                if (dbItem == null) return null;
                
                updateAction(dbItem);
                
                await context.SaveChangesAsync();
                
                cache.AddOrUpdate(id, dbItem, (k, old) => old.CopyFrom(dbItem));
                
                return dbItem;
            });
        }

        /// <summary>
        /// 通用原子添加方法 (验证不存在->添加DB->保存->添加缓存)
        /// </summary>
        public static async Task<T?> AddAtomicGeneric<T>(
            this DbContext context,
            T entity,
            ConcurrentDictionary<string, T> cache)
            where T : class, ICacheable<T>
        {
            string id = entity.GetId();
            string lockKey = $"{typeof(T).Name}_{id}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                // 1. 检查是否存在 (缓存 或 DB)
                if (cache.ContainsKey(id)) return null; // 已存在

                var existing = await context.Set<T>().FindAsync(id);
                if (existing != null) return null; // DB已存在

                // 2. 添加到 DB
                await context.Set<T>().AddAsync(entity);
                await context.SaveChangesAsync();

                // 3. 添加到缓存
                cache.TryAdd(id, entity);

                return entity;
            });
        }
        
        /// <summary>
        /// 通用原子添加方法 (Long Key)
        /// </summary>
        public static async Task<T?> AddAtomicGeneric<T>(
            this DbContext context,
            T entity,
            long id,
            ConcurrentDictionary<long, T> cache)
            where T : class, ICacheable<T>
        {
            string lockKey = $"{typeof(T).Name}_{id}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                if (cache.ContainsKey(id)) return null;

                var existing = await context.Set<T>().FindAsync(id);
                if (existing != null) return null;

                await context.Set<T>().AddAsync(entity);
                await context.SaveChangesAsync();

                cache.TryAdd(id, entity);

                return entity;
            });
        }

        /// <summary>
        /// 通用原子删除方法 (获取->删除DB->保存->清除缓存)
        /// </summary>
        public static async Task<bool> DeleteAtomicGeneric<T>(
            this DbContext context,
            string id,
            ConcurrentDictionary<string, T> cache)
            where T : class, ICacheable<T>
        {
            string lockKey = $"{typeof(T).Name}_{id}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                // 1. 获取实体 (优先查库确保被Context追踪)
                var entity = await context.Set<T>().FindAsync(id);
                if (entity == null) 
                {
                    // DB没有，检查缓存是否残留，有则清理
                    if (cache.ContainsKey(id)) cache.TryRemove(id, out _);
                    return false;
                }

                // 2. 删除 DB
                context.Set<T>().Remove(entity);
                await context.SaveChangesAsync();

                // 3. 清除缓存
                cache.TryRemove(id, out _);

                return true;
            });
        }
        
        /// <summary>
        /// 通用原子删除方法 (Long Key)
        /// </summary>
        public static async Task<bool> DeleteAtomicGeneric<T>(
            this DbContext context,
            long id,
            ConcurrentDictionary<long, T> cache)
            where T : class, ICacheable<T>
        {
            string lockKey = $"{typeof(T).Name}_{id}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var entity = await context.Set<T>().FindAsync(id);
                if (entity == null) 
                {
                    if (cache.ContainsKey(id)) cache.TryRemove(id, out _);
                    return false;
                }

                context.Set<T>().Remove(entity);
                await context.SaveChangesAsync();

                cache.TryRemove(id, out _);

                return true;
            });
        }
        // ==================== ApplicationUser ====================

        public static async Task<ApplicationUser?> GetApplicationUser(this DbContext context, string uuid)
        {
            if (GlobalCache.applicationUsers.TryGetValue(uuid, out var cached)) return cached;

            return await AsyncLockManager.ExecuteWithLockAsync($"ApplicationUser_{uuid}", async () =>
            {
                if (GlobalCache.applicationUsers.TryGetValue(uuid, out var item)) return item;

                var dbItem = await context.Set<ApplicationUser>().FindAsync(uuid);
                if (dbItem != null)
                {
                    GlobalCache.applicationUsers.TryAdd(uuid, dbItem);
                }
                return dbItem;
            });
        }

        public static async Task<ApplicationUser?> SaveApplicationUser(this DbContext context, ApplicationUser user)
        {
            if (!GlobalCache.applicationUsers.ContainsKey(user.Id))
            {
                return await context.AddAtomicGeneric(user, GlobalCache.applicationUsers);
            }
            return await context.UpdateAtomicGeneric(user.Id, GlobalCache.applicationUsers, c => c.CopyFrom(user));
        }

        public static async Task DeleteApplicationUser(this DbContext context, ApplicationUser user)
        {
            await context.DeleteAtomicGeneric(user.Id, GlobalCache.applicationUsers);
        }

        // ==================== SrClient ====================

        public static async Task<SrClient?> GetSrClient(this DbContext context, string uuid)
        {
            if (GlobalCache.srClients.TryGetValue(uuid, out var cached)) return cached;

            return await AsyncLockManager.ExecuteWithLockAsync($"SrClient_{uuid}", async () =>
            {
                if (GlobalCache.srClients.TryGetValue(uuid, out var item)) return item;

                var dbItem = await context.Set<SrClient>().FindAsync(uuid);
                if (dbItem != null)
                {
                    GlobalCache.srClients.TryAdd(uuid, dbItem);
                }
                return dbItem;
            });
        }

        public static  List<SrClient> GetAllSrClients()
        {
            return GlobalCache.srClients.Values.ToList();
        }

        public static async Task<SrClient?> SaveSrClient(this DbContext context, SrClient client)
        {
            if (!GlobalCache.srClients.ContainsKey(client.uuid))
            {
                return await context.AddAtomicGeneric(client, GlobalCache.srClients);
            }
            return await context.UpdateAtomicGeneric(client.uuid, GlobalCache.srClients, c => c.CopyFrom(client));
        }

        public static async Task DeleteSrClient(this DbContext context, SrClient client)
        {
            await context.DeleteAtomicGeneric(client.uuid, GlobalCache.srClients);
        }

        // ==================== WechatAccount ====================

        public static async Task<WechatAccount?> GetWechatAccount(this DbContext context, long accountId)
        {
            if (GlobalCache.wechatAccounts.TryGetValue(accountId, out var cached)) return cached;

            return await AsyncLockManager.ExecuteWithLockAsync($"WechatAccount_{accountId}", async () =>
            {
                if (GlobalCache.wechatAccounts.TryGetValue(accountId, out var item)) return item;

                var dbItem = await context.Set<WechatAccount>()
                    .Include(w => w.Client)
                    .FirstOrDefaultAsync(w => w.accountId == accountId);

                if (dbItem != null)
                {
                    GlobalCache.wechatAccounts.TryAdd(accountId, dbItem);
                }
                return dbItem;
            });
        }

        public static async Task<WechatAccount?> SaveWechatAccount(this DbContext context, WechatAccount account)
        {
            // Note: WechatAccount Key is long
            if (!GlobalCache.wechatAccounts.ContainsKey(account.accountId))
            {
                return await context.AddAtomicGeneric(account, account.accountId, GlobalCache.wechatAccounts);
            }
            return await context.UpdateAtomicGeneric(account.accountId, GlobalCache.wechatAccounts, c => c.CopyFrom(account));
        }

        public static async Task DeleteWechatAccount(this DbContext context, WechatAccount account)
        {
            await context.DeleteAtomicGeneric(account.accountId, GlobalCache.wechatAccounts);
        }
        // ==================== Contacts ====================

        public static async Task<List<Contact>> GetContacts(this DbContext context, long accountId)
        {
            // Direct DB Query - No Memory Cache due to large data size
            // Robustness: Deduplicate by Wxid in case DB contains legacy duplicates
            var rawList = await context.Set<Contact>()
                .Where(c => c.wechatAccountId == accountId && !c.isDeleted)
                .OrderByDescending(c => c.createdAt) 
                .ToListAsync();

            return rawList.DistinctBy(c => c.wxid).ToList();
        }

        public static async Task SaveContacts(this DbContext context, long accountId, List<Contact> contacts)
        {
            if (contacts == null || !contacts.Any()) return;

            // Direct DB Update
            // Config: Match by AccountId + Wxid (Business Key) instead of ID (Surrogate Key)
            // This prevents duplicates when syncing the same friend multiple times.
            var bulkConfig = new BulkConfig 
            { 
                UpdateByProperties = new List<string> { nameof(Contact.wechatAccountId), nameof(Contact.wxid) },
                SetOutputIdentity = true,
                BatchSize = 1000
            };
            
            try 
            {
                await context.BulkInsertOrUpdateAsync(contacts, bulkConfig);
            }
            catch (Exception ex)
            {
                // FALLBACK STRATEGY: 
                // If Bulk Extensions fail (e.g. Postgres 55000 index error), fall back to standard EF Core.
                // This is slower but guarantees the connection won't drop due to DB errors.
                Console.WriteLine($"[WARNING] BulkInsertOrUpdateAsync failed: {ex.Message}. Falling back to standard EF Core.");

                var strategy = context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await context.Database.BeginTransactionAsync();
                    try 
                    {
                        foreach (var contact in contacts)
                        {
                            var existing = await context.Set<Contact>()
                                .FirstOrDefaultAsync(c => c.wechatAccountId == contact.wechatAccountId && c.wxid == contact.wxid);

                            if (existing != null)
                            {
                                // Update existing
                                existing.avatar = contact.avatar;
                                existing.nickname = contact.nickname;
                                existing.remarks = contact.remarks;
                                existing.description = contact.description;
                                existing.gender = contact.gender;
                                existing.province = contact.province;
                                existing.city = contact.city;
                                existing.phone = contact.phone;
                                existing.signature = contact.signature;
                                existing.source = contact.source;
                                existing.labelIds = contact.labelIds;
                                existing.contactType = contact.contactType;
                                existing.updatedAt = DateTime.UtcNow;
                                context.Entry(existing).State = EntityState.Modified;
                            }
                            else
                            {
                                // Insert new
                                await context.Set<Contact>().AddAsync(contact);
                            }
                        }
                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch (Exception fallbackEx)
                    {
                        await transaction.RollbackAsync();
                        // If standard EF also fails, then rethrow (fatal DB error)
                        throw new Exception($"SaveContacts Fallback also failed: {fallbackEx.Message}", fallbackEx);
                    }
                });
            }
        }
    }
}
