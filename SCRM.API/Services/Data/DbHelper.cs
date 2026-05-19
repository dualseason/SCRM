using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SCRM.SHARED.Models;
using EFCore.BulkExtensions;
using SCRM.API.Models.Entities;
using SCRM.API.Models;
using SCRM.API.Services.Netty.Parsing;
using SCRM.SHARED.Models.Dtos;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProtoCDNFileType = Jubo.JuLiao.IM.Wx.Proto.CDNFileType;
using ProtoEnumContentType = Jubo.JuLiao.IM.Wx.Proto.EnumContentType;

namespace SCRM.API.Services.Data
{
    public static class DbHelper
    {
        private const string RevokedMessagePreview = "消息已撤回";
        private const string MediaCompensationHistoryExtensionKey = "media_compensation_history";
        private const string MediaCompensationLatestExtensionKey = "media_compensation_latest";
        private const string CdnDownloadPendingExtensionPrefix = "cdn_download_pending";
        private const string RequestTalkDetailPendingExtensionPrefix = "request_talk_detail_pending";
        private const string AdvancedContentLatestExtensionKey = "advanced_content_latest";
        private const string AdvancedContentHistoryExtensionKey = "advanced_content_history";
        private const string AdvancedContentSourcePrefix = "advanced_content";
        private const string AdvancedContentKindExtensionPrefix = AdvancedContentSourcePrefix + ":";
        private const int AdvancedContentHistoryMaxCount = 20;

        /// <summary>
        /// 高级消息扩展 JSON 序列化配置。
        /// <para>保留 PascalCase 字段名，方便网页端和分析文档按同一字段读取；空值不落库以减小扩展体积。</para>
        /// </summary>
        private static readonly JsonSerializerOptions AdvancedMessageContentJsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Android 未读列表快照不会返回公众号、陌生人和系统服务账号；清零缺失项时必须避开这些会话。
        /// </summary>
        private static readonly HashSet<string> UnreadSnapshotExcludedConversationWxids = new(StringComparer.OrdinalIgnoreCase)
        {
            "conversationboxservice",
            "opencustomerservicemsg",
            "appbrandcustomerservicemsg",
            "weibo",
            "pc_share",
            "officialaccounts",
            "voicevoipapp",
            "cardpackage",
            "qqfriend",
            "helper_entry",
            "medianote",
            "shakeapp",
            "qmessage",
            "voipapp",
            "qqsync",
            "qqmail",
            "blogapp",
            "lbsapp",
            "readerapp",
            "feedsapp",
            "newsapp",
            "floatbottle",
            "fmessage",
            "tmessage",
            "facebookapp",
            "meishiapp",
            "masssendapp",
            "voiceinputapp",
            "filehelper",
            "linkedinplugin",
            "notifymessage",
            "service_officialaccounts"
        };

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
            var clients = GlobalCache.srClients.Values.ToList();
            HydrateWechatAccountForClients(clients);
            return clients;
        }

        /// <summary>
        /// 为设备缓存补齐当前微信账号展示对象。
        /// <para>
        /// SrClient.wx 是非持久化展示结构，数据库中的真实账号数据保存在 WechatAccount。
        /// 服务重启或缓存预热后，SrClient 缓存可能只有设备本体，导致 Web 端 IM 中心显示“微信未登录”。
        /// 这里仅从 GlobalCache.wechatAccounts 读取并回填内存引用，不产生额外数据库写入。
        /// </para>
        /// </summary>
        private static void HydrateWechatAccountForClients(IEnumerable<SrClient> clients)
        {
            foreach (var client in clients)
            {
                if (client == null)
                {
                    continue;
                }

                if (client.wx?.wechatAccount != null)
                {
                    continue;
                }

                var account = GlobalCache.wechatAccounts.Values
                    .Where(a => a != null
                        && !a.isDeleted
                        && !string.IsNullOrWhiteSpace(a.clientUuid)
                        && a.clientUuid == client.uuid)
                    .OrderByDescending(a => a.accountStatus == 1)
                    .ThenByDescending(a => a.lastOnlineAt ?? DateTime.MinValue)
                    .FirstOrDefault();

                if (account != null)
                {
                    client.wx = new Wx
                    {
                        srClient = client,
                        wechatAccount = account
                    };
                }
            }
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

        // ==================== WechatAccount (Key = WxId) ====================
        
        /// <summary>
        /// 根据 WxId 获取账号 (支持缓存)
        /// </summary>
        public static async Task<WechatAccount?> GetWechatAccount(this DbContext context, string wxid)
        {
            if (string.IsNullOrEmpty(wxid)) return null;

            if (GlobalCache.wechatAccounts.TryGetValue(wxid, out var cached)) return cached;

            return await AsyncLockManager.ExecuteWithLockAsync($"WechatAccount_{wxid}", async () =>
            {
                if (GlobalCache.wechatAccounts.TryGetValue(wxid, out var item)) return item;

                var dbItem = await context.Set<WechatAccount>()
                    .Include(w => w.Client)
                    .FirstOrDefaultAsync(w => w.wxid == wxid);

                if (dbItem != null)
                {
                    GlobalCache.wechatAccounts.TryAdd(wxid, dbItem);
                }
                return dbItem;
            });
        }



        public static async Task<WechatAccount?> SaveWechatAccount(this DbContext context, WechatAccount account)
        {
            // Key is now WxId (from account.GetId())
            if (!GlobalCache.wechatAccounts.ContainsKey(account.GetId()))
            {
                return await context.AddAtomicGeneric(account, GlobalCache.wechatAccounts);
            }
            return await context.UpdateAtomicGeneric(account.GetId(), GlobalCache.wechatAccounts, c => c.CopyFrom(account));
        }

        public static async Task DeleteWechatAccount(this DbContext context, WechatAccount account)
        {
            await context.DeleteAtomicGeneric(account.GetId(), GlobalCache.wechatAccounts);
        }

        // ==================== FriendRequest ====================

        /// <summary>
        /// 保存或更新好友添加请求。
        /// <para>
        /// FriendRequests 当前没有进入 GlobalCache，因此这里只做数据库原子写入；
        /// 锁粒度保持为“微信账号 + 请求人 wxid”，避免同一好友请求重复通知时产生多条待处理记录。
        /// </para>
        /// </summary>
        /// <param name="ownerWxid">接收请求的微信账号 wxid</param>
        /// <param name="request">好友请求实体</param>
        /// <returns>落库后的好友请求</returns>
        public static async Task<FriendRequest?> SaveFriendRequestFromNotice(this DbContext context, string ownerWxid, FriendRequest request)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || request == null || string.IsNullOrWhiteSpace(request.requestWxid))
            {
                return null;
            }

            long accountKey = BuildWechatAccountNumericKey(ownerWxid);
            string lockKey = $"FriendRequest_{ownerWxid}_{request.requestWxid}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var now = DateTime.UtcNow;
                var existing = await context.Set<FriendRequest>()
                    .Where(r => r.wechatAccountId == accountKey
                        && r.requestWxid == request.requestWxid
                        && r.status == 0)
                    .OrderByDescending(r => r.requestTime)
                    .FirstOrDefaultAsync();

                if (existing == null)
                {
                    request.wechatAccountId = accountKey;
                    request.status = request.status == 0 ? 0 : request.status;
                    request.requestTime = request.requestTime == default ? now : request.requestTime;
                    request.responseTime = request.responseTime == default ? DateTime.MinValue : request.responseTime;
                    request.responseMessage ??= string.Empty;
                    request.createdAt = request.createdAt == default ? now : request.createdAt;
                    request.updatedAt = now;
                    await context.Set<FriendRequest>().AddAsync(request);
                    await context.SaveChangesAsync();
                    return request;
                }

                existing.nickname = string.IsNullOrWhiteSpace(request.nickname) ? existing.nickname : request.nickname;
                existing.avatar = string.IsNullOrWhiteSpace(request.avatar) ? existing.avatar : request.avatar;
                existing.gender = request.gender ?? existing.gender;
                existing.region = string.IsNullOrWhiteSpace(request.region) ? existing.region : request.region;
                existing.source = string.IsNullOrWhiteSpace(request.source) ? existing.source : request.source;
                existing.requestMessage = string.IsNullOrWhiteSpace(request.requestMessage) ? existing.requestMessage : request.requestMessage;
                existing.requestTime = request.requestTime == default ? existing.requestTime : request.requestTime;
                existing.updatedAt = now;
                context.Entry(existing).State = EntityState.Modified;
                await context.SaveChangesAsync();
                return existing;
            });
        }

        /// <summary>
        /// 将好友请求标记为已通过。
        /// <para>
        /// 通过好友请求本身由 Android 执行；服务端只在收到成功回执、好友通过验证文本或好友列表已包含该联系人时，
        /// 原子更新 FriendRequests 状态，避免网页一直显示“待处理”。
        /// FriendRequests 当前没有进入 GlobalCache，因此这里仅做数据库原子写入。
        /// </para>
        /// </summary>
        public static Task<bool> MarkFriendRequestAccepted(this DbContext context, string ownerWxid, string requestWxid, string? responseMessage = null)
        {
            return MarkFriendRequestProcessed(context, ownerWxid, requestWxid, status: 1, responseMessage);
        }

        /// <summary>
        /// 将好友请求标记为已拒绝。
        /// <para>
        /// 拒绝动作仍由 Android 执行；服务端只在成功回执后原子更新 FriendRequests 状态。
        /// FriendRequests 当前没有进入 GlobalCache，因此这里沿用 MarkFriendRequestProcessed 的锁粒度和写入模式。
        /// </para>
        /// </summary>
        public static Task<bool> MarkFriendRequestRejected(this DbContext context, string ownerWxid, string requestWxid, string? responseMessage = null)
        {
            return MarkFriendRequestProcessed(context, ownerWxid, requestWxid, status: 2, responseMessage);
        }

        /// <summary>
        /// 批量将好友列表中已存在的待处理好友请求标记为已通过。
        /// <para>FriendPushNotice 是最终联系人快照；如果某个待处理请求已经出现在好友列表中，则可以确定该请求已通过。</para>
        /// </summary>
        public static async Task<int> MarkFriendRequestsAccepted(this DbContext context, string ownerWxid, IEnumerable<string> requestWxids, string? responseMessage = null)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || requestWxids == null)
            {
                return 0;
            }

            var normalizedWxids = requestWxids
                .Where(wxid => !string.IsNullOrWhiteSpace(wxid))
                .Select(wxid => wxid.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!normalizedWxids.Any())
            {
                return 0;
            }

            long accountKey = BuildWechatAccountNumericKey(ownerWxid);
            string lockKey = $"FriendRequest_{ownerWxid}_BatchAccept";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var pendingRequests = await context.Set<FriendRequest>()
                    .Where(request => request.wechatAccountId == accountKey
                        && request.status == 0
                        && normalizedWxids.Contains(request.requestWxid))
                    .ToListAsync();

                if (!pendingRequests.Any())
                {
                    return 0;
                }

                var now = DateTime.UtcNow;
                var normalizedMessage = string.IsNullOrWhiteSpace(responseMessage)
                    ? "好友列表同步已确认通过"
                    : responseMessage.Trim();

                foreach (var request in pendingRequests)
                {
                    request.status = 1;
                    request.responseTime = now;
                    request.responseMessage = normalizedMessage;
                    request.updatedAt = now;
                    context.Entry(request).State = EntityState.Modified;
                }

                await context.SaveChangesAsync();
                return pendingRequests.Count;
            });
        }

        /// <summary>
        /// 原子更新好友请求处理状态。
        /// <para>优先更新待处理记录；如果没有待处理记录，则更新最近一条同 wxid 记录，保证重复回执幂等。</para>
        /// </summary>
        private static async Task<bool> MarkFriendRequestProcessed(DbContext context, string ownerWxid, string requestWxid, int status, string? responseMessage)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || string.IsNullOrWhiteSpace(requestWxid))
            {
                return false;
            }

            var normalizedRequestWxid = requestWxid.Trim();
            long accountKey = BuildWechatAccountNumericKey(ownerWxid);
            string lockKey = $"FriendRequest_{ownerWxid}_{normalizedRequestWxid}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var request = await context.Set<FriendRequest>()
                    .Where(item => item.wechatAccountId == accountKey && item.requestWxid == normalizedRequestWxid)
                    .OrderByDescending(item => item.status == 0)
                    .ThenByDescending(item => item.requestTime)
                    .ThenByDescending(item => item.id)
                    .FirstOrDefaultAsync();

                if (request == null)
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                request.status = status;
                request.responseTime = now;
                if (!string.IsNullOrWhiteSpace(responseMessage))
                {
                    request.responseMessage = responseMessage.Trim();
                }
                request.updatedAt = now;

                context.Entry(request).State = EntityState.Modified;
                await context.SaveChangesAsync();
                return true;
            });
        }

        // ==================== Contacts ====================

        public static async Task<List<Contact>> GetContacts(this DbContext context, string ownerWxid)
        {
            // Direct DB Query - No Memory Cache due to large data size
            // Robustness: Deduplicate by Wxid in case DB contains legacy duplicates
            var rawList = await context.Set<Contact>()
                .Where(c => c.ownerWxid == ownerWxid && !c.isDeleted)
                .OrderByDescending(c => c.createdAt) 
                .ToListAsync();

            return rawList.DistinctBy(c => c.wxid).ToList();
        }

        public static async Task SaveContacts(this DbContext context, string ownerWxid, List<Contact> contacts)
        {
            if (contacts == null || !contacts.Any()) return;

            var now = DateTime.UtcNow;
            foreach (var contact in contacts)
            {
                // 当前方法表示“安卓端确认仍存在的联系人”入库；若之前被软删除，重新同步时必须恢复可见。
                // 这与 MarkContactDeleted 的软删除策略配套，避免删除后再添加/重新同步仍被 GetContacts 过滤。
                contact.ownerWxid = string.IsNullOrWhiteSpace(contact.ownerWxid) ? ownerWxid : contact.ownerWxid;
                contact.isDeleted = false;
                contact.updatedAt = now;
                if (contact.createdAt == default)
                {
                    contact.createdAt = now;
                }
            }

            // Direct DB Update
            // Config: Match by AccountId + Wxid (Business Key) instead of ID (Surrogate Key)
            // This prevents duplicates when syncing the same friend multiple times.
            var bulkConfig = new BulkConfig 
            { 
                UpdateByProperties = new List<string> { nameof(Contact.ownerWxid), nameof(Contact.wxid) },
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
                                .FirstOrDefaultAsync(c => c.ownerWxid == contact.ownerWxid && c.wxid == contact.wxid);

                            if (existing != null)
                            {
                                // Update existing
                                existing.friendNo = contact.friendNo;
                                existing.sourceExt = contact.sourceExt;
                                existing.avatar = contact.avatar;
                                existing.nickname = contact.nickname;
                                existing.remarks = contact.remarks;
                                existing.description = contact.description;
                                existing.gender = contact.gender;
                                existing.country = contact.country;
                                existing.province = contact.province;
                                existing.city = contact.city;
                                existing.phone = contact.phone;
                                existing.signature = contact.signature;
                                existing.source = contact.source;
                                existing.labelIds = contact.labelIds;
                                existing.contactType = contact.contactType;
                                existing.isFriend = contact.isFriend;
                                existing.isBlocked = contact.isBlocked;
                                existing.isStarred = contact.isStarred;
                                existing.lastInteractionTime = contact.lastInteractionTime;
                                existing.isDeleted = false;
                                existing.updatedAt = now;
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

        /// <summary>
        /// 将单个联系人标记为已删除。
        /// <para>
        /// 联系人列表读取统一走 <see cref="GetContacts"/>，该方法已过滤 isDeleted；
        /// 因此删除好友回包到达时，只需要软删除并更新时间，前端收到 ContactsUpdated 后会自然刷新消失。
        /// </para>
        /// </summary>
        /// <param name="ownerWxid">所属微信账号 wxid</param>
        /// <param name="friendWxid">被删除好友 wxid</param>
        /// <returns>true 表示找到了联系人并完成标记；false 表示没有匹配记录</returns>
        public static async Task<bool> MarkContactDeleted(this DbContext context, string ownerWxid, string friendWxid)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || string.IsNullOrWhiteSpace(friendWxid))
            {
                return false;
            }

            string lockKey = $"Contact_{ownerWxid}_{friendWxid}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var contacts = await context.Set<Contact>()
                    .Where(c => c.ownerWxid == ownerWxid && c.wxid == friendWxid && !c.isDeleted)
                    .ToListAsync();

                if (!contacts.Any())
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                foreach (var contact in contacts)
                {
                    contact.isDeleted = true;
                    contact.isFriend = 0;
                    contact.updatedAt = now;
                }

                await context.SaveChangesAsync();
                return true;
            });
        }

        /// <summary>
        /// 新增或更新微信联系人标签字典。
        /// <para>
        /// ContactLabelAddNotice(1038) 与 ContactLabelInfoNotice(2032) 只表示标签字典变更，
        /// 不代表某个联系人已经拥有该标签；联系人关系仍以 Contacts.labelIds 或后续联系人同步为准。
        /// </para>
        /// </summary>
        /// <param name="ownerWxid">所属微信账号 wxid。</param>
        /// <param name="labelId">微信端标签 ID。</param>
        /// <param name="labelName">标签名称。</param>
        /// <param name="createTime">微信端创建时间，兼容秒/毫秒时间戳；0 表示使用当前时间。</param>
        /// <returns>新增或更新后的标签实体；参数无效时返回 null。</returns>
        public static async Task<ContactTag?> UpsertContactLabel(
            this DbContext context,
            string ownerWxid,
            int labelId,
            string? labelName,
            long createTime = 0)
        {
            var normalizedOwnerWxid = ownerWxid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedOwnerWxid) || labelId <= 0)
            {
                return null;
            }

            var accountKey = BuildWechatAccountNumericKey(normalizedOwnerWxid);
            var normalizedName = string.IsNullOrWhiteSpace(labelName)
                ? $"标签{labelId}"
                : labelName.Trim();
            var now = DateTime.UtcNow;
            var createdAt = ConvertWechatTimestampToUtc(createTime, now);
            var lockKey = $"ContactLabel_{accountKey}_{labelId}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var tag = await context.Set<ContactTag>()
                    .FirstOrDefaultAsync(item => item.wechatAccountId == accountKey && item.labelId == labelId);

                if (tag == null)
                {
                    tag = new ContactTag
                    {
                        wechatAccountId = accountKey,
                        labelId = labelId,
                        tagName = normalizedName,
                        tagColor = string.Empty,
                        tagDescription = string.Empty,
                        createdAt = createdAt,
                        updatedAt = now,
                        isDeleted = false
                    };
                    await context.Set<ContactTag>().AddAsync(tag);
                }
                else
                {
                    tag.tagName = normalizedName;
                    tag.isDeleted = false;
                    if (tag.createdAt == default && createdAt != default)
                    {
                        tag.createdAt = createdAt;
                    }
                    tag.updatedAt = now;
                    context.Entry(tag).State = EntityState.Modified;
                }

                await context.SaveChangesAsync();
                return tag;
            });
        }

        /// <summary>
        /// 用手机端推送的标签列表替换当前账号的标签字典快照。
        /// <para>
        /// 本方法只软删除本次快照中不存在的旧标签，不物理删除 ContactTagRelations，
        /// 避免历史联系人关系被误删；联系人真实标签集合由 Contacts.labelIds 继续承载。
        /// </para>
        /// </summary>
        /// <param name="ownerWxid">所属微信账号 wxid。</param>
        /// <param name="labels">标签快照，包含微信端 LabelId/LabelName/CreateTime。</param>
        /// <returns>最终可见标签数量。</returns>
        public static async Task<int> ReplaceContactLabels(
            this DbContext context,
            string ownerWxid,
            IEnumerable<(int LabelId, string LabelName, long CreateTime)>? labels)
        {
            var normalizedOwnerWxid = ownerWxid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedOwnerWxid))
            {
                return 0;
            }

            var normalizedLabels = (labels ?? Enumerable.Empty<(int LabelId, string LabelName, long CreateTime)>())
                .Where(item => item.LabelId > 0)
                .GroupBy(item => item.LabelId)
                .Select(group => group.Last())
                .ToList();

            var accountKey = BuildWechatAccountNumericKey(normalizedOwnerWxid);
            var activeLabelIds = normalizedLabels.Select(item => item.LabelId).ToHashSet();
            var now = DateTime.UtcNow;
            var lockKey = $"ContactLabelReplace_{accountKey}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var existingTags = await context.Set<ContactTag>()
                    .Where(item => item.wechatAccountId == accountKey)
                    .ToListAsync();
                var tagMap = existingTags
                    .GroupBy(item => item.labelId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.OrderByDescending(item => item.updatedAt).ThenByDescending(item => item.id).First());

                foreach (var label in normalizedLabels)
                {
                    var labelName = string.IsNullOrWhiteSpace(label.LabelName)
                        ? $"标签{label.LabelId}"
                        : label.LabelName.Trim();
                    var createdAt = ConvertWechatTimestampToUtc(label.CreateTime, now);

                    if (!tagMap.TryGetValue(label.LabelId, out var tag))
                    {
                        tag = new ContactTag
                        {
                            wechatAccountId = accountKey,
                            labelId = label.LabelId,
                            tagName = labelName,
                            tagColor = string.Empty,
                            tagDescription = string.Empty,
                            createdAt = createdAt,
                            updatedAt = now,
                            isDeleted = false
                        };
                        await context.Set<ContactTag>().AddAsync(tag);
                    }
                    else
                    {
                        tag.tagName = labelName;
                        tag.isDeleted = false;
                        if (tag.createdAt == default && createdAt != default)
                        {
                            tag.createdAt = createdAt;
                        }
                        tag.updatedAt = now;
                        context.Entry(tag).State = EntityState.Modified;
                    }
                }

                foreach (var tag in existingTags.Where(item => !activeLabelIds.Contains(item.labelId) && !item.isDeleted))
                {
                    tag.isDeleted = true;
                    tag.updatedAt = now;
                    context.Entry(tag).State = EntityState.Modified;
                }

                await context.SaveChangesAsync();
                return normalizedLabels.Count;
            });
        }

        /// <summary>
        /// 标记微信联系人标签已删除。
        /// <para>删除标签只软删标签字典，不物理删除联系人标签关系，便于后续全量联系人同步重新校准。</para>
        /// </summary>
        public static async Task<bool> MarkContactLabelDeleted(this DbContext context, string ownerWxid, int labelId)
        {
            var normalizedOwnerWxid = ownerWxid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedOwnerWxid) || labelId <= 0)
            {
                return false;
            }

            var accountKey = BuildWechatAccountNumericKey(normalizedOwnerWxid);
            var lockKey = $"ContactLabelDelete_{accountKey}_{labelId}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var tag = await context.Set<ContactTag>()
                    .FirstOrDefaultAsync(item => item.wechatAccountId == accountKey && item.labelId == labelId);

                if (tag == null)
                {
                    return false;
                }

                tag.isDeleted = true;
                tag.updatedAt = DateTime.UtcNow;
                context.Entry(tag).State = EntityState.Modified;
                await context.SaveChangesAsync();
                return true;
            });
        }

        /// <summary>
        /// 查询指定微信账号的联系人标签字典快照。
        /// <para>
        /// ContactTags.wechatAccountId 使用 ownerWxid 的稳定数值键；外部代码不要自行复制哈希逻辑，
        /// 统一通过本方法查询，避免同一个 wxid 在不同调用链中算出不一致的账号键。
        /// </para>
        /// </summary>
        /// <param name="ownerWxid">所属微信账号 wxid。</param>
        /// <param name="includeDeleted">是否包含已软删除标签。</param>
        /// <returns>联系人标签 DTO 列表。</returns>
        public static async Task<List<ContactLabelDto>> GetContactLabels(
            this DbContext context,
            string ownerWxid,
            bool includeDeleted = false)
        {
            var normalizedOwnerWxid = ownerWxid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedOwnerWxid))
            {
                return new List<ContactLabelDto>();
            }

            var accountKey = BuildWechatAccountNumericKey(normalizedOwnerWxid);
            var query = context.Set<ContactTag>()
                .AsNoTracking()
                .Where(tag => tag.wechatAccountId == accountKey);

            if (!includeDeleted)
            {
                query = query.Where(tag => !tag.isDeleted);
            }

            var tags = await query
                .OrderBy(tag => tag.isDeleted)
                .ThenBy(tag => tag.labelId)
                .ThenBy(tag => tag.id)
                .ToListAsync();

            return tags.Select(tag => new ContactLabelDto
            {
                id = tag.id,
                ownerWxid = normalizedOwnerWxid,
                wechatAccountId = tag.wechatAccountId,
                labelId = tag.labelId,
                tagName = tag.tagName ?? string.Empty,
                tagColor = tag.tagColor ?? string.Empty,
                tagDescription = tag.tagDescription ?? string.Empty,
                createdAt = tag.createdAt,
                updatedAt = tag.updatedAt,
                isDeleted = tag.isDeleted
            }).ToList();
        }

        /// <summary>
        /// 更新单个联系人的标签 ID 集合。
        /// <para>
        /// ContactSetLabelTask(1270) 成功回包后可先乐观更新 Contacts.labelIds，
        /// 再由后续 FriendPushNotice/ContactInfoNotice 做最终校准。
        /// </para>
        /// </summary>
        /// <returns>true 表示找到联系人并完成更新；false 表示联系人不存在或参数无效。</returns>
        public static async Task<bool> UpdateContactLabelIds(
            this DbContext context,
            string ownerWxid,
            string friendWxid,
            IEnumerable<int>? labelIds)
        {
            var normalizedOwnerWxid = ownerWxid?.Trim() ?? string.Empty;
            var normalizedFriendWxid = friendWxid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedOwnerWxid) || string.IsNullOrWhiteSpace(normalizedFriendWxid))
            {
                return false;
            }

            var normalizedLabelIds = NormalizeContactLabelIds(labelIds);
            var labelIdsCsv = string.Join(",", normalizedLabelIds);
            var lockKey = $"ContactLabelIds_{normalizedOwnerWxid}_{normalizedFriendWxid}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var contact = await context.Set<Contact>()
                    .FirstOrDefaultAsync(item => item.ownerWxid == normalizedOwnerWxid
                        && item.wxid == normalizedFriendWxid
                        && !item.isDeleted);

                if (contact == null)
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                contact.labelIds = labelIdsCsv;
                contact.updatedAt = now;
                context.Entry(contact).State = EntityState.Modified;

                await context.Set<ContactTagRelation>()
                    .Where(item => item.contactId == contact.id)
                    .ExecuteDeleteAsync();

                if (normalizedLabelIds.Count > 0)
                {
                    var accountKey = BuildWechatAccountNumericKey(normalizedOwnerWxid);
                    var tagIds = await context.Set<ContactTag>()
                        .Where(tag => tag.wechatAccountId == accountKey
                            && normalizedLabelIds.Contains(tag.labelId)
                            && !tag.isDeleted)
                        .Select(tag => tag.id)
                        .ToListAsync();

                    if (tagIds.Count > 0)
                    {
                        await context.Set<ContactTagRelation>().AddRangeAsync(tagIds.Select(tagId => new ContactTagRelation
                        {
                            contactId = contact.id,
                            tagId = tagId,
                            createdAt = now,
                            updatedAt = now
                        }));
                    }
                }

                await context.SaveChangesAsync();
                return true;
            });
        }

        /// <summary>
        /// 保存清粉任务的过程进度。
        /// <para>
        /// PostFriendDetectCountNotice(2028) 只携带任务进度和本轮疑似僵尸粉列表。
        /// 当前不新增汇总表，先复用 FriendDetectionLogs：
        /// <list type="bullet">
        /// <item>DetectionType=10：过程进度快照；</item>
        /// <item>DetectionType=11：过程进度中出现的疑似僵尸粉明细。</item>
        /// </list>
        /// 明细中的 taskId、ownerWxid、wxid、计数等信息统一写入 DetailInfo(JSON)，
        /// 这样不会破坏既有表结构，也便于后续页面或报表二次解析。
        /// </para>
        /// </summary>
        /// <returns>本次新增日志条数。</returns>
        public static async Task<int> SaveFriendDetectProgress(
            this DbContext context,
            string ownerWxid,
            long taskId,
            int count,
            int delCount,
            int skipCount,
            bool isFinished,
            IEnumerable<string>? zombies)
        {
            var normalizedOwnerWxid = ownerWxid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedOwnerWxid) || taskId <= 0)
            {
                return 0;
            }

            var zombieList = NormalizeWxidList(zombies);
            string lockKey = $"FriendDetectProgress_{normalizedOwnerWxid}_{taskId}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var now = DateTime.UtcNow;
                var logs = new List<FriendDetectionLog>
                {
                    BuildFriendDetectLog(
                        contactId: 0,
                        detectionType: 10,
                        detectionResult: isFinished ? 1 : 0,
                        detailInfo: BuildFriendDetectDetailJson(
                            taskId,
                            normalizedOwnerWxid,
                            wxid: string.Empty,
                            category: "progress",
                            count,
                            delCount,
                            skipCount,
                            isFinished,
                            startTime: 0,
                            endTime: 0,
                            extra: new { zombieCount = zombieList.Count }),
                        now)
                };

                if (zombieList.Count > 0)
                {
                    var contactIds = await GetContactIdMap(context, normalizedOwnerWxid, zombieList);
                    logs.AddRange(zombieList.Select(wxid =>
                        BuildFriendDetectLog(
                            contactIds.GetValueOrDefault(wxid),
                            detectionType: 11,
                            detectionResult: 0,
                            detailInfo: BuildFriendDetectDetailJson(
                                taskId,
                                normalizedOwnerWxid,
                                wxid,
                                category: "progress_zombie",
                                count,
                                delCount,
                                skipCount,
                                isFinished,
                                startTime: 0,
                                endTime: 0,
                                extra: null),
                            now)));
                }

                await context.Set<FriendDetectionLog>().AddRangeAsync(logs);
                await context.SaveChangesAsync();
                return logs.Count;
            });
        }

        /// <summary>
        /// 保存清粉任务的完整结果。
        /// <para>
        /// FriendDetectResultNotice(1280) 是 62203 的清粉最终结果通知。
        /// 当前表结构只有按联系人逐条日志的 FriendDetectionLogs，因此这里采用“1 条汇总 + N 条明细”的兼容落库方式：
        /// <list type="bullet">
        /// <item>DetectionType=20：最终结果汇总；</item>
        /// <item>DetectionType=1：僵尸粉；</item>
        /// <item>DetectionType=2：被拉黑；</item>
        /// <item>DetectionType=3：账号封禁/异常；</item>
        /// <item>DetectionType=4：已取消/已删除关系。</item>
        /// </list>
        /// 未找到联系人主键时 contactId 写 0，真实 wxid 保存在 DetailInfo(JSON) 中，避免伪造联系人记录。
        /// </para>
        /// </summary>
        /// <returns>本次新增日志条数。</returns>
        public static async Task<int> SaveFriendDetectResult(
            this DbContext context,
            string ownerWxid,
            long taskId,
            long startTime,
            long endTime,
            bool isFinished,
            int count,
            int skipCount,
            int delCount,
            IEnumerable<string>? zombies,
            IEnumerable<string>? blockedList,
            IEnumerable<string>? bannedList,
            IEnumerable<string>? canceledList)
        {
            var normalizedOwnerWxid = ownerWxid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedOwnerWxid) || taskId <= 0)
            {
                return 0;
            }

            var zombieWxids = NormalizeWxidList(zombies);
            var blockedWxids = NormalizeWxidList(blockedList);
            var bannedWxids = NormalizeWxidList(bannedList);
            var canceledWxids = NormalizeWxidList(canceledList);
            var allWxids = zombieWxids
                .Concat(blockedWxids)
                .Concat(bannedWxids)
                .Concat(canceledWxids)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string lockKey = $"FriendDetectResult_{normalizedOwnerWxid}_{taskId}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var now = DateTime.UtcNow;
                var contactIds = await GetContactIdMap(context, normalizedOwnerWxid, allWxids);
                var logs = new List<FriendDetectionLog>
                {
                    BuildFriendDetectLog(
                        contactId: 0,
                        detectionType: 20,
                        detectionResult: isFinished ? 1 : 0,
                        detailInfo: BuildFriendDetectDetailJson(
                            taskId,
                            normalizedOwnerWxid,
                            wxid: string.Empty,
                            category: "summary",
                            count,
                            delCount,
                            skipCount,
                            isFinished,
                            startTime,
                            endTime,
                            extra: new
                            {
                                zombieCount = zombieWxids.Count,
                                blockedCount = blockedWxids.Count,
                                bannedCount = bannedWxids.Count,
                                canceledCount = canceledWxids.Count
                            }),
                        now)
                };

                AddFriendDetectDetailLogs(logs, contactIds, normalizedOwnerWxid, taskId, zombieWxids, 1, "zombie", count, delCount, skipCount, isFinished, startTime, endTime, now);
                AddFriendDetectDetailLogs(logs, contactIds, normalizedOwnerWxid, taskId, blockedWxids, 2, "blocked", count, delCount, skipCount, isFinished, startTime, endTime, now);
                AddFriendDetectDetailLogs(logs, contactIds, normalizedOwnerWxid, taskId, bannedWxids, 3, "banned", count, delCount, skipCount, isFinished, startTime, endTime, now);
                AddFriendDetectDetailLogs(logs, contactIds, normalizedOwnerWxid, taskId, canceledWxids, 4, "canceled", count, delCount, skipCount, isFinished, startTime, endTime, now);

                await context.Set<FriendDetectionLog>().AddRangeAsync(logs);
                await context.SaveChangesAsync();
                return logs.Count;
            });
        }

        /// <summary>
        /// 规整清粉结果中的 wxid 列表，过滤空值并去重。
        /// </summary>
        private static List<string> NormalizeWxidList(IEnumerable<string>? wxids)
        {
            return (wxids ?? Enumerable.Empty<string>())
                .Select(item => item?.Trim() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// 一次性查出清粉结果对应的联系人主键。
        /// </summary>
        private static async Task<Dictionary<string, int>> GetContactIdMap(DbContext context, string ownerWxid, List<string> wxids)
        {
            if (wxids.Count == 0)
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            var contacts = await context.Set<Contact>()
                .Where(c => c.ownerWxid == ownerWxid && wxids.Contains(c.wxid))
                .Select(c => new { c.wxid, c.id })
                .ToListAsync();

            return contacts
                .GroupBy(item => item.wxid, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().id, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 构造清粉日志实体。
        /// </summary>
        private static FriendDetectionLog BuildFriendDetectLog(int contactId, int detectionType, int detectionResult, string detailInfo, DateTime now)
        {
            return new FriendDetectionLog
            {
                contactId = contactId,
                detectionType = detectionType,
                detectionResult = detectionResult,
                detailInfo = detailInfo,
                detectionTime = now,
                createdAt = now,
                updatedAt = now
            };
        }

        /// <summary>
        /// 批量追加清粉明细日志。
        /// </summary>
        private static void AddFriendDetectDetailLogs(
            List<FriendDetectionLog> logs,
            Dictionary<string, int> contactIds,
            string ownerWxid,
            long taskId,
            List<string> wxids,
            int detectionType,
            string category,
            int count,
            int delCount,
            int skipCount,
            bool isFinished,
            long startTime,
            long endTime,
            DateTime now)
        {
            foreach (var wxid in wxids)
            {
                logs.Add(BuildFriendDetectLog(
                    contactIds.GetValueOrDefault(wxid),
                    detectionType,
                    detectionResult: 1,
                    detailInfo: BuildFriendDetectDetailJson(taskId, ownerWxid, wxid, category, count, delCount, skipCount, isFinished, startTime, endTime, extra: null),
                    now));
            }
        }

        /// <summary>
        /// 构造清粉日志的 JSON 明细，保留原始任务计数与分类，方便后续报表解析。
        /// </summary>
        private static string BuildFriendDetectDetailJson(
            long taskId,
            string ownerWxid,
            string wxid,
            string category,
            int count,
            int delCount,
            int skipCount,
            bool isFinished,
            long startTime,
            long endTime,
            object? extra)
        {
            return JsonSerializer.Serialize(new
            {
                taskId,
                ownerWxid,
                wxid,
                category,
                count,
                delCount,
                skipCount,
                isFinished,
                startTime,
                endTime,
                extra
            });
        }

        // ==================== MomentsTimeline ====================

        /// <summary>
        /// 应用朋友圈点赞或取消点赞结果。
        /// <para>
        /// 该方法只维护 MomentsTimeline.likesJson，不绕开朋友圈表结构；
        /// 锁粒度为“微信账号 + 朋友圈 snsId”，避免点赞回执、评论回执和详情快照同时写入时互相覆盖。
        /// </para>
        /// </summary>
        public static async Task<MomentsTimeline?> ApplyMomentLikeResult(
            this DbContext context,
            string ownerWxid,
            long circleId,
            string likerWxid,
            string? likerNickname,
            bool isCancel,
            long publishTime)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || circleId == 0 || string.IsNullOrWhiteSpace(likerWxid))
            {
                return null;
            }

            string lockKey = $"MomentTimeline_{ownerWxid}_{circleId}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var moment = await GetOrCreateMomentTimelineForUpdate(context, ownerWxid, circleId);
                var likes = DeserializeMomentLikes(moment.likesJson);
                likes.RemoveAll(item => string.Equals(item.userName, likerWxid, StringComparison.OrdinalIgnoreCase));

                if (!isCancel)
                {
                    likes.Add(new MomentLikeDto
                    {
                        userName = likerWxid,
                        nickName = likerNickname ?? string.Empty,
                        createTime = publishTime > 0 ? publishTime : DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });
                }

                moment.likesJson = JsonSerializer.Serialize(likes.OrderBy(item => item.createTime).ToList());
                moment.receivedAt = DateTime.UtcNow.Ticks;
                await context.SaveChangesAsync();
                return moment;
            });
        }

        /// <summary>
        /// 应用朋友圈评论结果。
        /// <para>用于评论任务成功回执到达但安卓端没有立即上报 CircleCommentNotice 的场景。</para>
        /// </summary>
        public static async Task<MomentsTimeline?> ApplyMomentCommentResult(
            this DbContext context,
            string ownerWxid,
            long circleId,
            long commentId,
            long replyCommentId,
            string fromWxid,
            string? fromName,
            string? toWxid,
            string? toName,
            string content,
            long publishTime)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || circleId == 0 || string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            string lockKey = $"MomentTimeline_{ownerWxid}_{circleId}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var moment = await GetOrCreateMomentTimelineForUpdate(context, ownerWxid, circleId);
                var comments = DeserializeMomentComments(moment.commentsJson);
                var newComment = new MomentCommentDto
                {
                    commentId = commentId > 0 ? commentId : -DateTime.UtcNow.Ticks,
                    userName = fromWxid ?? string.Empty,
                    nickName = fromName ?? string.Empty,
                    content = content.Trim(),
                    createTime = publishTime > 0 ? publishTime : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    replyUserName = toWxid ?? string.Empty,
                    replyNickName = toName ?? string.Empty,
                    authorName = ownerWxid,
                };

                comments.RemoveAll(item => IsSameMomentComment(item, newComment));
                comments.Add(newComment);

                moment.commentsJson = JsonSerializer.Serialize(comments.OrderBy(item => item.createTime).ToList());
                moment.receivedAt = DateTime.UtcNow.Ticks;
                await context.SaveChangesAsync();
                return moment;
            });
        }

        /// <summary>
        /// 应用朋友圈删除评论结果。
        /// </summary>
        public static async Task<MomentsTimeline?> ApplyMomentCommentDeleteResult(
            this DbContext context,
            string ownerWxid,
            long circleId,
            long commentId)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || circleId == 0 || commentId == 0)
            {
                return null;
            }

            string lockKey = $"MomentTimeline_{ownerWxid}_{circleId}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var moment = await GetOrCreateMomentTimelineForUpdate(context, ownerWxid, circleId);
                var comments = DeserializeMomentComments(moment.commentsJson);
                comments.RemoveAll(item => item.commentId == commentId);
                moment.commentsJson = JsonSerializer.Serialize(comments.OrderBy(item => item.createTime).ToList());
                moment.receivedAt = DateTime.UtcNow.Ticks;
                await context.SaveChangesAsync();
                return moment;
            });
        }

        /// <summary>
        /// 保存聊天消息上报。
        /// <para>
        /// 普通实时消息、历史消息分页推送都统一走这里，避免同一条微信消息因实时上报和历史补偿重复入库。
        /// 优先按 ownerWxid + MsgSvrId 去重；没有 MsgSvrId 的少数本地消息再按 ownerWxid + LocalMessageId 去重。
        /// </para>
        /// </summary>
        /// <param name="ownerWxid">所属微信账号 wxid。</param>
        /// <param name="messages">待保存的消息集合。</param>
        /// <returns>实际新增或更新后的消息实体。</returns>
        public static async Task<List<Message>> SaveMessages(this DbContext context, string ownerWxid, List<Message> messages)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || messages == null || !messages.Any())
            {
                return new List<Message>();
            }

            var now = DateTime.UtcNow;
            foreach (var message in messages)
            {
                message.accountId = string.IsNullOrWhiteSpace(message.accountId) ? ownerWxid : message.accountId;
                message.updatedAt = now;
                if (message.createdAt == default)
                {
                    message.createdAt = now;
                }
            }

            string lockKey = $"Messages_{ownerWxid}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var savedMessages = new List<Message>(messages.Count);
                var strategy = context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    savedMessages.Clear();
                    using var transaction = await context.Database.BeginTransactionAsync();
                    try
                    {
                        foreach (var message in messages)
                        {
                            var existing = await FindExistingMessage(context, message);
                            if (existing != null)
                            {
                                CopyMessageForUpsert(existing, message, now);
                                context.Entry(existing).State = EntityState.Modified;
                                savedMessages.Add(existing);
                            }
                            else
                            {
                                await context.Set<Message>().AddAsync(message);
                                savedMessages.Add(message);
                            }
                        }

                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                return savedMessages;
            });
        }

        /// <summary>
        /// 根据 TalkToFriendTaskResultNotice(1028) 回填或补插发送方向消息。
        /// <para>
        /// 1028 不带正文和内容类型，因此调用方必须传入下发 TalkToFriendTask 时保存的 pending context；
        /// 本方法优先按 ownerWxid + MsgSvrId 查找，其次按 ownerWxid + TaskId(localMessageId) 查找，保证后续
        /// WeChatTalkToFriendNotice 或历史补偿到达时仍可通过 SaveMessages 幂等合并。
        /// </para>
        /// </summary>
        /// <param name="ownerWxid">当前登录微信账号 wxid。</param>
        /// <param name="friendId">好友或群聊 wxid，优先来自 pending context。</param>
        /// <param name="content">下发时保存的正文或结构化内容。</param>
        /// <param name="messageType">下发内容类型。</param>
        /// <param name="taskId">下发任务 ID，同时作为 localMessageId。</param>
        /// <param name="msgSvrId">微信服务器消息 ID。</param>
        /// <param name="createTime">微信消息创建时间，支持秒级或毫秒级时间戳。</param>
        /// <param name="success">1028 是否成功。</param>
        /// <param name="errorMessage">失败信息，仅保留给后续扩展；当前不写入正文。</param>
        /// <returns>回填或补插后的消息；失败且没有已有消息时返回 null。</returns>
        public static async Task<Message?> SaveTalkToFriendResultMessageAsync(
            this DbContext context,
            string ownerWxid,
            string friendId,
            string content,
            short messageType,
            long taskId,
            long msgSvrId,
            long createTime,
            bool success,
            string? errorMessage = null)
        {
            var normalizedOwnerWxid = ownerWxid?.Trim() ?? string.Empty;
            var normalizedFriendId = friendId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedOwnerWxid) || taskId <= 0)
            {
                return null;
            }

            // 没有目标会话时不能凭 1028.FriendId 盲造消息，避免 Tsk26/Tsk144 污染 receiverWxid。
            if (string.IsNullOrWhiteSpace(normalizedFriendId))
            {
                return null;
            }

            var now = DateTime.UtcNow;
            var localMessageId = taskId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var normalizedContent = content ?? string.Empty;
            var normalizedMessageType = messageType > 0 ? messageType : (short)1;
            DateTime? messageTime = createTime > 0
                ? ConvertWechatTimestampToUtc(createTime, now)
                : null;
            var lockKey = $"TalkToFriendResult_{normalizedOwnerWxid}_{localMessageId}_{msgSvrId}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                Message? savedMessage = null;
                var strategy = context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await context.Database.BeginTransactionAsync();
                    try
                    {
                        Message? existing = null;
                        if (msgSvrId > 0)
                        {
                            existing = await context.Set<Message>()
                                .FirstOrDefaultAsync(m => m.accountId == normalizedOwnerWxid && m.msgSvrId == msgSvrId);
                        }

                        if (existing == null)
                        {
                            existing = await context.Set<Message>()
                                .FirstOrDefaultAsync(m => m.accountId == normalizedOwnerWxid
                                    && m.localMessageId == localMessageId);
                        }

                        if (existing != null)
                        {
                            if (msgSvrId > 0 && (!existing.msgSvrId.HasValue || existing.msgSvrId.Value == 0))
                            {
                                existing.msgSvrId = msgSvrId;
                            }

                            if (string.IsNullOrWhiteSpace(existing.localMessageId))
                            {
                                existing.localMessageId = localMessageId;
                            }

                            if (string.IsNullOrWhiteSpace(existing.senderWxid))
                            {
                                existing.senderWxid = normalizedOwnerWxid;
                            }

                            if (string.IsNullOrWhiteSpace(existing.receiverWxid))
                            {
                                existing.receiverWxid = normalizedFriendId;
                            }

                            if (existing.chatType == 0)
                            {
                                existing.chatType = IsChatRoomWxid(normalizedFriendId) ? (short)2 : (short)1;
                            }

                            if (existing.messageType == 0)
                            {
                                existing.messageType = normalizedMessageType;
                            }

                            if (string.IsNullOrWhiteSpace(existing.content) && !string.IsNullOrEmpty(normalizedContent))
                            {
                                existing.content = normalizedContent;
                            }

                            if (string.IsNullOrWhiteSpace(existing.contentXml) && LooksLikeXmlContent(normalizedContent))
                            {
                                existing.contentXml = normalizedContent;
                            }

                            if (existing.direction == 0)
                            {
                                existing.direction = 1;
                            }

                            existing.sendStatus = MergeTalkToFriendSendStatus(existing.sendStatus, success);
                            if (success && !existing.readStatus.HasValue)
                            {
                                existing.readStatus = 1;
                            }

                            existing.sentAt = messageTime ?? existing.sentAt ?? now;
                            if (existing.createdAt == default)
                            {
                                existing.createdAt = now;
                            }
                            existing.updatedAt = now;
                            context.Entry(existing).State = EntityState.Modified;
                            savedMessage = existing;
                        }
                        else if (success)
                        {
                            var sentAt = messageTime ?? now;
                            savedMessage = new Message
                            {
                                accountId = normalizedOwnerWxid,
                                msgSvrId = msgSvrId > 0 ? msgSvrId : null,
                                localMessageId = localMessageId,
                                senderWxid = normalizedOwnerWxid,
                                receiverWxid = normalizedFriendId,
                                chatType = IsChatRoomWxid(normalizedFriendId) ? (short)2 : (short)1,
                                messageType = normalizedMessageType,
                                content = normalizedContent,
                                contentXml = LooksLikeXmlContent(normalizedContent) ? normalizedContent : null,
                                direction = 1,
                                sendStatus = 2,
                                readStatus = 1,
                                sentAt = sentAt,
                                receivedAt = null,
                                createdAt = now,
                                updatedAt = now,
                                isDeleted = false,
                                isRevoked = false
                            };
                            await context.Set<Message>().AddAsync(savedMessage);
                        }

                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                return savedMessage;
            });
        }

        /// <summary>
        /// 保存会话列表上报。
        /// <para>
        /// 安卓端 ConversationPushNotice 是会话快照/分页数据；这里按 ownerWxid + conversationWxid 做幂等 upsert。
        /// </para>
        /// </summary>
        public static async Task<List<Conversation>> SaveConversations(this DbContext context, string ownerWxid, List<Conversation> conversations)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || conversations == null || !conversations.Any())
            {
                return new List<Conversation>();
            }

            var now = DateTime.UtcNow;
            foreach (var conversation in conversations)
            {
                conversation.wechatAccountId = string.IsNullOrWhiteSpace(conversation.wechatAccountId)
                    ? ownerWxid
                    : conversation.wechatAccountId;
                conversation.conversationWxid ??= string.Empty;
                conversation.displayName ??= string.Empty;
                conversation.displayAvatar ??= string.Empty;
                conversation.isDeleted = false;
                conversation.updatedAt = now;
                if (conversation.createdAt == default)
                {
                    conversation.createdAt = now;
                }
                if (conversation.lastMessageTime == default)
                {
                    conversation.lastMessageTime = now;
                }
            }

            string lockKey = $"Conversations_{ownerWxid}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var savedConversations = new List<Conversation>(conversations.Count);
                var strategy = context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    savedConversations.Clear();
                    using var transaction = await context.Database.BeginTransactionAsync();
                    try
                    {
                        foreach (var conversation in conversations.Where(c => !string.IsNullOrWhiteSpace(c.conversationWxid)))
                        {
                            var existing = await context.Set<Conversation>()
                                .FirstOrDefaultAsync(c => c.wechatAccountId == conversation.wechatAccountId
                                    && c.conversationWxid == conversation.conversationWxid);

                            if (existing != null)
                            {
                                CopyConversationForUpsert(existing, conversation, now);
                                context.Entry(existing).State = EntityState.Modified;
                                savedConversations.Add(existing);
                            }
                            else
                            {
                                await context.Set<Conversation>().AddAsync(conversation);
                                savedConversations.Add(conversation);
                            }
                        }

                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                return savedConversations;
            });
        }

        /// <summary>
        /// 同步 Android 上报的未读会话快照。
        /// <para>
        /// UnreadListPushNotice 只返回当前普通微信会话中 unReadCount &gt; 0 的快照；
        /// 因此返回项应覆盖未读数，未返回但仍处于 Android 查询范围内的旧未读会话应清零。
        /// </para>
        /// <para>
        /// 该协议不携带置顶、头像、摘要、消息数等完整会话字段，所以不能复用
        /// SaveConversations 的全量快照覆盖逻辑，避免把已有置顶状态或展示字段冲掉。
        /// </para>
        /// </summary>
        public static async Task<(List<Conversation> savedConversations, int clearedCount)> SyncUnreadConversationSnapshot(
            this DbContext context,
            string ownerWxid,
            List<Conversation> unreadConversations)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid))
            {
                return (new List<Conversation>(), 0);
            }

            var now = DateTime.UtcNow;
            unreadConversations ??= new List<Conversation>();
            var normalizedConversations = unreadConversations
                .Where(conversation => conversation != null && !string.IsNullOrWhiteSpace(conversation.conversationWxid))
                .GroupBy(conversation => conversation.conversationWxid.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(conversation => conversation.lastMessageTime == default
                        ? conversation.updatedAt
                        : conversation.lastMessageTime)
                    .First())
                .ToList();

            foreach (var conversation in normalizedConversations)
            {
                conversation.wechatAccountId = ownerWxid;
                conversation.conversationWxid = conversation.conversationWxid.Trim();
                conversation.displayName = string.IsNullOrWhiteSpace(conversation.displayName)
                    ? conversation.conversationWxid
                    : conversation.displayName.Trim();
                conversation.displayAvatar ??= string.Empty;
                conversation.unreadCount = Math.Max(0, conversation.unreadCount);
                conversation.messageCount = Math.Max(0, conversation.messageCount);
                conversation.isMuted = conversation.isMuted == 0 ? 0 : 1;
                conversation.lastMessageContent ??= string.Empty;
                conversation.lastMessageTime = conversation.lastMessageTime == default
                    ? now
                    : conversation.lastMessageTime;
                conversation.createdAt = conversation.createdAt == default ? now : conversation.createdAt;
                conversation.updatedAt = now;
                conversation.isDeleted = false;
            }

            string lockKey = $"UnreadSnapshot_{ownerWxid}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var savedConversations = new List<Conversation>(normalizedConversations.Count);
                var clearedCount = 0;
                var strategy = context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    savedConversations.Clear();
                    clearedCount = 0;
                    using var transaction = await context.Database.BeginTransactionAsync();
                    try
                    {
                        var returnedWxids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (var source in normalizedConversations)
                        {
                            returnedWxids.Add(source.conversationWxid);
                            var existing = await context.Set<Conversation>()
                                .FirstOrDefaultAsync(conversation => conversation.wechatAccountId == ownerWxid
                                    && conversation.conversationWxid == source.conversationWxid);

                            if (existing != null)
                            {
                                CopyUnreadConversationSnapshotForUpsert(existing, source, now);
                                context.Entry(existing).State = EntityState.Modified;
                                savedConversations.Add(existing);
                            }
                            else
                            {
                                await context.Set<Conversation>().AddAsync(source);
                                savedConversations.Add(source);
                            }
                        }

                        var staleUnreadConversations = await context.Set<Conversation>()
                            .Where(conversation => conversation.wechatAccountId == ownerWxid
                                && !conversation.isDeleted
                                && conversation.unreadCount != 0)
                            .ToListAsync();

                        foreach (var staleConversation in staleUnreadConversations)
                        {
                            if (returnedWxids.Contains(staleConversation.conversationWxid)
                                || !ShouldClearUnreadByUnreadSnapshot(staleConversation.conversationWxid))
                            {
                                continue;
                            }

                            staleConversation.unreadCount = 0;
                            staleConversation.updatedAt = now;
                            context.Entry(staleConversation).State = EntityState.Modified;
                            clearedCount++;
                        }

                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                return (savedConversations, clearedCount);
            });
        }

        /// <summary>
        /// 按 MsgSvrId 更新已有消息正文。
        /// <para>
        /// RequestTalkContentTaskResultNotice 这类回包通常不携带 FriendId，
        /// 只能更新已存在消息，不能在 Handler 中猜测会话关系创建新记录。
        /// </para>
        /// </summary>
        public static async Task<Message?> UpdateMessageContentByMsgSvrId(
            this DbContext context,
            string ownerWxid,
            long msgSvrId,
            short messageType,
            string? content,
            bool preferXml)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || msgSvrId == 0)
            {
                return null;
            }

            string lockKey = $"MessageContent_{ownerWxid}_{msgSvrId}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var message = await context.Set<Message>()
                    .FirstOrDefaultAsync(m => m.accountId == ownerWxid
                        && m.msgSvrId == msgSvrId
                        && !m.isDeleted);

                if (message == null)
                {
                    return null;
                }

                message.messageType = messageType;
                message.content = content ?? string.Empty;
                if (preferXml)
                {
                    message.contentXml = content ?? string.Empty;
                }
                message.updatedAt = DateTime.UtcNow;

                context.Entry(message).State = EntityState.Modified;
                await context.SaveChangesAsync();
                return message;
            });
        }

        /// <summary>
        /// 保存 CDN 下载任务的待回填上下文。
        /// <para>
        /// CDNDownloadResultNotice(1271) 回包不再携带 FileType/FileFmt/FileSize，
        /// 因此下发 CDNDownloadFileTask(1269) 成功后先把上下文挂到已有消息扩展表里。
        /// 这里仍然只绑定已有消息，不凭 MsgSvrId 猜测 FriendId 创建脏消息。
        /// </para>
        /// </summary>
        /// <returns>找到消息并保存上下文时返回 true；没有匹配消息或入参无效时返回 false。</returns>
        public static async Task<bool> SaveCdnDownloadPendingContextByMsgSvrId(
            this DbContext context,
            string ownerWxid,
            long msgSvrId,
            string? cdnUrl,
            int cdnFileType,
            string? fileId,
            string? fileFmt,
            int fileSize,
            long taskId)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || msgSvrId == 0 || string.IsNullOrWhiteSpace(cdnUrl))
            {
                return false;
            }

            string lockKey = $"CdnDownloadPending_{ownerWxid}_{msgSvrId}_{NormalizeExtensionKeyPart(fileId)}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var message = await context.Set<Message>()
                    .FirstOrDefaultAsync(m => m.accountId == ownerWxid
                        && m.msgSvrId == msgSvrId
                        && !m.isDeleted);

                if (message == null || !TryGetMessageTableId(message, out var messageTableId))
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                var pending = new MessageMediaCompensationEntry
                {
                    SourceNotice = "CDNDownloadFileTask",
                    Url = cdnUrl.Trim(),
                    MsgSvrId = msgSvrId,
                    FileId = fileId?.Trim() ?? string.Empty,
                    CdnFileType = cdnFileType,
                    FileFmt = fileFmt?.Trim() ?? string.Empty,
                    FileSize = Math.Max(0, fileSize),
                    TaskId = taskId,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                await UpsertMessageExtensionAsync(
                    context,
                    messageTableId,
                    BuildCdnDownloadPendingExtensionKey(fileId, msgSvrId),
                    JsonSerializer.Serialize(pending),
                    now);

                await context.SaveChangesAsync();
                return true;
            });
        }

        /// <summary>
        /// 按 MsgSvrId 回填聊天媒体文件 URL。
        /// <para>
        /// ChatMsgFilePushNotice(1051) 与 CDNDownloadResultNotice(1271) 上报的是已上传后的文件 URL。
        /// 本方法会把 URL 写入 MessageMedias，并把 FileSize/SubType/FileId/FileType/FileFmt/sourceNotice
        /// 等上下文写入 MessageExtensions 的历史 JSON，避免多个媒体 URL 反复覆盖 Message.content。
        /// </para>
        /// <para>
        /// 为兼容旧 UI，只有当 content 为空或仍是原始 XML 时，才把首个媒体 URL 提升为 content；
        /// 若 content 已经是另一个媒体 URL，则不再覆盖，后续 URL 只进入结构化表。
        /// </para>
        /// </summary>
        /// <returns>找到并更新后的消息；没有匹配记录时返回 null。</returns>
        public static async Task<Message?> UpdateMessageMediaUrlByMsgSvrId(
            this DbContext context,
            string ownerWxid,
            long msgSvrId,
            string? mediaUrl,
            short? messageType = null,
            long? fileSize = null,
            int? subType = null,
            string? sourceNotice = null,
            string? fileId = null,
            int? cdnFileType = null,
            string? fileFmt = null)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || msgSvrId == 0 || string.IsNullOrWhiteSpace(mediaUrl))
            {
                return null;
            }

            var normalizedMediaUrl = mediaUrl.Trim();
            var normalizedSourceNotice = string.IsNullOrWhiteSpace(sourceNotice)
                ? "MediaUrlCompensation"
                : sourceNotice.Trim();

            string lockKey = $"MessageMediaUrl_{ownerWxid}_{msgSvrId}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var message = await context.Set<Message>()
                    .FirstOrDefaultAsync(m => m.accountId == ownerWxid
                        && m.msgSvrId == msgSvrId
                        && !m.isDeleted);

                if (message == null)
                {
                    return null;
                }

                var now = DateTime.UtcNow;
                MessageMediaCompensationEntry? pendingCdnContext = null;
                var canWriteStructuredMedia = TryGetMessageTableId(message, out var messageTableId);
                if (canWriteStructuredMedia
                    && string.Equals(normalizedSourceNotice, "CDNDownloadResultNotice", StringComparison.OrdinalIgnoreCase))
                {
                    pendingCdnContext = await FindCdnDownloadPendingContextAsync(context, messageTableId, fileId, msgSvrId);
                }

                var effectiveFileSize = NormalizeNonNegative(fileSize) ?? NormalizeNonNegative(pendingCdnContext?.FileSize);
                var effectiveSubType = subType ?? pendingCdnContext?.SubType;
                var effectiveFileId = string.IsNullOrWhiteSpace(fileId)
                    ? pendingCdnContext?.FileId ?? string.Empty
                    : fileId.Trim();
                var effectiveCdnFileType = cdnFileType ?? pendingCdnContext?.CdnFileType;
                var effectiveFileFmt = string.IsNullOrWhiteSpace(fileFmt)
                    ? pendingCdnContext?.FileFmt ?? string.Empty
                    : fileFmt.Trim();
                var effectiveMessageType = ResolveEffectiveMediaMessageType(
                    messageType,
                    message.messageType,
                    effectiveCdnFileType);

                var oldContent = message.content ?? string.Empty;
                if (string.IsNullOrWhiteSpace(message.contentXml)
                    && LooksLikeXmlContent(oldContent))
                {
                    message.contentXml = oldContent;
                }

                if (messageType.HasValue && messageType.Value > 0)
                {
                    message.messageType = messageType.Value;
                }
                else if (ShouldPromoteWeakMessageTypeFromMediaContext(message.messageType, effectiveMessageType))
                {
                    // CDNDownloadResultNotice 不回传聊天内容类型；若旧消息只是 Text/Unknown/UnSupport，
                    // 使用下发 1269 时保存的 CDNFileType 显式映射为 proto EnumContentType，避免把 6/7/8 等下载类型误写成媒体类型。
                    message.messageType = effectiveMessageType;
                }

                if (ShouldPromoteMediaUrlToMessageContent(oldContent, normalizedMediaUrl))
                {
                    message.content = normalizedMediaUrl;
                }

                if (canWriteStructuredMedia)
                {
                    await UpsertMessageMediaAsync(
                        context,
                        messageTableId,
                        effectiveMessageType,
                        normalizedMediaUrl,
                        effectiveFileSize.GetValueOrDefault(),
                        effectiveFileFmt,
                        effectiveFileId,
                        now);

                    var compensationEntry = new MessageMediaCompensationEntry
                    {
                        SourceNotice = normalizedSourceNotice,
                        Url = normalizedMediaUrl,
                        MsgSvrId = msgSvrId,
                        MessageType = effectiveMessageType,
                        FileSize = effectiveFileSize,
                        SubType = effectiveSubType,
                        FileId = effectiveFileId,
                        CdnFileType = effectiveCdnFileType,
                        FileFmt = effectiveFileFmt,
                        TaskId = pendingCdnContext?.TaskId,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    await UpsertMediaCompensationExtensionsAsync(context, messageTableId, compensationEntry, now);
                }

                message.updatedAt = now;

                context.Entry(message).State = EntityState.Modified;
                await context.SaveChangesAsync();
                return message;
            });
        }

        /// <summary>
        /// 为消息列表补齐媒体附件、补偿上下文和语音转文字结果。
        /// <para>
        /// 该方法只读 MessageMedias / MessageExtensions / VoiceToTextLogs，并把结果写入 Message 的 NotMapped 展示字段；
        /// 不修改数据库，不新增表结构，用于 IM 页面展示 v237/v238 已落库的媒体补偿结果和 v240 语音识别结果。
        /// </para>
        /// </summary>
        public static async Task EnrichMessagesWithMediaMetadataAsync(
            this DbContext context,
            IList<Message> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                return;
            }

            var messageIdMap = messages
                .Where(message => message != null && TryGetMessageTableId(message, out _))
                .GroupBy(message => (int)message.messageId)
                .ToDictionary(group => group.Key, group => group.ToList());

            if (messageIdMap.Count == 0)
            {
                return;
            }

            var messageTableIds = messageIdMap.Keys.ToList();

            var mediaList = await context.Set<MessageMedia>()
                .AsNoTracking()
                .Where(media => messageTableIds.Contains(media.messageId))
                .OrderBy(media => media.createdAt)
                .ThenBy(media => media.id)
                .Select(media => new MessageMediaAttachmentDto
                {
                    id = media.id,
                    messageId = media.messageId,
                    mediaType = media.mediaType,
                    mediaUrl = media.mediaUrl,
                    localPath = media.localPath,
                    mediaHash = media.mediaHash,
                    fileSize = media.fileSize,
                    fileExtension = media.fileExtension,
                    uploadStatus = media.uploadStatus,
                    createdAt = media.createdAt,
                    updatedAt = media.updatedAt
                })
                .ToListAsync();

            var extensionList = await context.Set<MessageExtension>()
                .AsNoTracking()
                .Where(extension => messageTableIds.Contains(extension.messageId)
                    && (extension.extensionKey == MediaCompensationLatestExtensionKey
                        || extension.extensionKey == MediaCompensationHistoryExtensionKey
                        || extension.extensionKey.StartsWith(CdnDownloadPendingExtensionPrefix)
                        || extension.extensionKey.StartsWith(RequestTalkDetailPendingExtensionPrefix)
                        || extension.extensionKey == AdvancedContentLatestExtensionKey
                        || extension.extensionKey == AdvancedContentHistoryExtensionKey
                        || extension.extensionKey.StartsWith(AdvancedContentKindExtensionPrefix)))
                .OrderByDescending(extension => extension.updatedAt)
                .ThenByDescending(extension => extension.id)
                .Select(extension => new MessageExtensionViewDto
                {
                    id = extension.id,
                    messageId = extension.messageId,
                    extensionKey = extension.extensionKey,
                    extensionValue = extension.extensionValue,
                    createdAt = extension.createdAt,
                    updatedAt = extension.updatedAt
                })
                .ToListAsync();

            var voiceToTextList = await context.Set<VoiceToTextLog>()
                .AsNoTracking()
                .Where(log => messageTableIds.Contains(log.messageId))
                .OrderByDescending(log => log.updatedAt)
                .ThenByDescending(log => log.id)
                .Select(log => new VoiceToTextLogViewDto
                {
                    id = log.id,
                    messageId = log.messageId,
                    voiceUrl = log.voiceUrl,
                    transcribedText = log.transcribedText,
                    transcribeStatus = log.transcribeStatus,
                    accuracy = log.accuracy,
                    errorMessage = log.errorMessage,
                    transcribeTime = log.transcribeTime,
                    createdAt = log.createdAt,
                    updatedAt = log.updatedAt
                })
                .ToListAsync();

            var mediaMap = mediaList
                .GroupBy(media => media.messageId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var extensionMap = extensionList
                .GroupBy(extension => extension.messageId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var voiceToTextMap = voiceToTextList
                .GroupBy(log => log.messageId)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var (messageTableId, mappedMessages) in messageIdMap)
            {
                mediaMap.TryGetValue(messageTableId, out var mediaAttachments);
                extensionMap.TryGetValue(messageTableId, out var messageExtensions);
                voiceToTextMap.TryGetValue(messageTableId, out var voiceTransText);

                foreach (var message in mappedMessages)
                {
                    message.mediaAttachments = mediaAttachments ?? new List<MessageMediaAttachmentDto>();
                    message.messageExtensions = messageExtensions ?? new List<MessageExtensionViewDto>();
                    message.voiceTransText = voiceTransText;
                }
            }
        }

        /// <summary>
        /// 保存高级聊天内容的结构化展示元数据。
        /// <para>
        /// 第一版只写 MessageExtensions，不写 MessageMedias，不覆盖 Message.messageType/content，
        /// 避免把链接、小程序缩略图误渲染成图片气泡。
        /// </para>
        /// </summary>
        /// <returns>成功写入扩展时返回 true；没有可保存语义或消息尚未入库时返回 false。</returns>
        public static async Task<bool> SaveAdvancedMessageContentMetadata(
            this DbContext context,
            Message message,
            AdvancedMessageContent? advanced)
        {
            if (context == null || message == null || advanced == null)
            {
                return false;
            }

            if (!TryGetMessageTableId(message, out var messageTableId))
            {
                return false;
            }

            var semanticKind = NormalizeAdvancedSemanticKind(advanced.SemanticKind);
            if (string.IsNullOrWhiteSpace(semanticKind)
                || string.Equals(semanticKind, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var lockKey = $"AdvancedMessageContent_{message.accountId}_{messageTableId}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var now = DateTime.UtcNow;
                advanced.ParsedAt = now;
                var json = JsonSerializer.Serialize(advanced, AdvancedMessageContentJsonOptions);

                await UpsertMessageExtensionAsync(
                    context,
                    messageTableId,
                    AdvancedContentLatestExtensionKey,
                    json,
                    now);

                await UpsertMessageExtensionAsync(
                    context,
                    messageTableId,
                    BuildAdvancedContentExtensionKey(semanticKind),
                    json,
                    now);

                await UpsertAdvancedContentHistoryAsync(
                    context,
                    messageTableId,
                    advanced,
                    now);

                await context.SaveChangesAsync();
                return true;
            });
        }

        /// <summary>
        /// 为 RequestTalkDetailTask 下发前解析本地消息。
        /// <para>
        /// Android/62203 运行时必须拿到可解析且非 0 的 MsgSvrId；服务端可在只有本地 MsgId 时，
        /// 按 ownerWxid + localMessageId 自动补齐 MsgSvrId，避免任务下发到手机端后直接返回“msgSvrId错误”。
        /// </para>
        /// <para>
        /// 该方法只读已有消息，不创建、不修改消息；优先按 MsgSvrId 精确查找，其次按本地 MsgId 查找。
        /// 若传入 FriendId，会先尝试限定会话，未命中时再退回账号内唯一消息，兼容旧数据会话字段缺失。
        /// </para>
        /// </summary>
        public static async Task<Message?> FindMessageForRequestTalkDetailTask(
            this DbContext context,
            string ownerWxid,
            long msgId,
            string? msgSvrId,
            string? friendId = null)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid))
            {
                return null;
            }

            var normalizedOwnerWxid = ownerWxid.Trim();
            var normalizedFriendId = friendId?.Trim() ?? string.Empty;
            var query = context.Set<Message>()
                .AsNoTracking()
                .Where(m => m.accountId == normalizedOwnerWxid && !m.isDeleted);

            if (TryParsePositiveInt64(msgSvrId, out var parsedMsgSvrId))
            {
                var byMsgSvrId = query.Where(m => m.msgSvrId == parsedMsgSvrId);
                var matched = await FirstMessageWithOptionalFriendFilter(byMsgSvrId, normalizedFriendId);
                if (matched != null)
                {
                    return matched;
                }
            }

            if (msgId == 0)
            {
                return null;
            }

            var localMessageId = msgId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var byLocalMsgId = query.Where(m => m.localMessageId == localMessageId);
            return await FirstMessageWithOptionalFriendFilter(byLocalMsgId, normalizedFriendId);
        }

        /// <summary>
        /// 保存 RequestTalkDetailTask 的待回填上下文。
        /// <para>
        /// RequestTalkDetailTaskResultNotice(1029) 不带 MsgSvrId；如果服务端下发时仅靠 MsgSvrId
        /// 定位，回包阶段必须通过这里保存的上下文把结果重新关联到原消息。
        /// </para>
        /// </summary>
        public static async Task<bool> SaveRequestTalkDetailPendingContext(
            this DbContext context,
            string ownerWxid,
            string? friendId,
            long msgId,
            long msgSvrId,
            string? md5,
            bool getOriginal,
            long taskId)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || (msgId == 0 && msgSvrId == 0))
            {
                return false;
            }

            var normalizedOwnerWxid = ownerWxid.Trim();
            var normalizedFriendId = friendId?.Trim() ?? string.Empty;
            string lockKey = $"RequestTalkDetailPending_{normalizedOwnerWxid}_{msgId}_{msgSvrId}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var baseQuery = context.Set<Message>()
                    .Where(m => m.accountId == normalizedOwnerWxid && !m.isDeleted);

                Message? message = null;
                if (msgSvrId > 0)
                {
                    message = await FirstMessageWithOptionalFriendFilter(
                        baseQuery.Where(m => m.msgSvrId == msgSvrId),
                        normalizedFriendId);
                }

                if (message == null && msgId > 0)
                {
                    var localMessageId = msgId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    message = await FirstMessageWithOptionalFriendFilter(
                        baseQuery.Where(m => m.localMessageId == localMessageId),
                        normalizedFriendId);
                }

                if (message == null || !TryGetMessageTableId(message, out var messageTableId))
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                var pending = new RequestTalkDetailPendingContextInfo
                {
                    WeChatId = normalizedOwnerWxid,
                    FriendId = normalizedFriendId,
                    MsgId = msgId,
                    MsgSvrId = msgSvrId,
                    Md5 = md5?.Trim() ?? string.Empty,
                    GetOriginal = getOriginal,
                    TaskId = taskId,
                    MessageTableId = messageTableId,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                foreach (var extensionKey in BuildRequestTalkDetailPendingExtensionKeys(msgId, msgSvrId))
                {
                    await UpsertMessageExtensionAsync(
                        context,
                        messageTableId,
                        extensionKey,
                        JsonSerializer.Serialize(pending),
                        now);
                }

                await context.SaveChangesAsync();
                return true;
            });
        }

        /// <summary>
        /// 查找 RequestTalkDetailTaskResultNotice 可用的待回填上下文。
        /// <para>优先按 MsgId 精确命中；若 MsgId 为空，则退回到同会话最近的 pending 记录。</para>
        /// </summary>
        public static async Task<RequestTalkDetailPendingContextInfo?> FindRequestTalkDetailPendingContext(
            this DbContext context,
            string ownerWxid,
            long msgId,
            string? friendId)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid))
            {
                return null;
            }

            var normalizedOwnerWxid = ownerWxid.Trim();
            var normalizedFriendId = friendId?.Trim() ?? string.Empty;

            if (msgId > 0)
            {
                var exact = await FindRequestTalkDetailPendingByExtensionKeys(
                    context,
                    normalizedOwnerWxid,
                    normalizedFriendId,
                    BuildRequestTalkDetailPendingByMsgIdKey(msgId));

                if (exact != null)
                {
                    return exact;
                }
            }

            var messageQuery = context.Set<Message>()
                .AsNoTracking()
                .Where(m => m.accountId == normalizedOwnerWxid && !m.isDeleted);

            if (!string.IsNullOrWhiteSpace(normalizedFriendId))
            {
                messageQuery = messageQuery.Where(m => m.senderWxid == normalizedFriendId || m.receiverWxid == normalizedFriendId);
            }

            if (msgId > 0)
            {
                var localMessageId = msgId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                messageQuery = messageQuery.Where(m => m.localMessageId == localMessageId);
            }

            var messageIds = await messageQuery
                .OrderByDescending(m => m.updatedAt)
                .Take(100)
                .Select(m => m.messageId)
                .ToListAsync();

            var mappedMessageIds = messageIds
                .Where(id => id > 0 && id <= int.MaxValue)
                .Select(id => (int)id)
                .ToList();

            if (!mappedMessageIds.Any())
            {
                return null;
            }

            var extensions = await context.Set<MessageExtension>()
                .AsNoTracking()
                .Where(item => mappedMessageIds.Contains(item.messageId)
                    && item.extensionKey.StartsWith(RequestTalkDetailPendingExtensionPrefix))
                .OrderByDescending(item => item.updatedAt)
                .Take(100)
                .ToListAsync();

            foreach (var extension in extensions)
            {
                var pending = DeserializeRequestTalkDetailPendingContext(extension.extensionValue);
                if (IsMatchingRequestTalkDetailPendingContext(pending, normalizedOwnerWxid, normalizedFriendId, msgId))
                {
                    return pending;
                }
            }

            return null;
        }

        /// <summary>
        /// 保存语音转文字结果。
        /// <para>
        /// VoiceTransTextTask(1226) 的 Android 回包是通用 TaskResultNotice，
        /// 成功时识别文本放在 ErrMsg 中，失败时 ErrMsg 是错误信息。
        /// 这里按 ownerWxid + MsgSvrId 查找已有语音消息，只写 VoiceToTextLogs，
        /// 不覆盖原消息正文/XML/媒体地址，避免破坏后续媒体补偿链路。
        /// </para>
        /// </summary>
        /// <returns>保存后的语音转文字日志；没有找到原消息或消息主键无法映射时返回 null。</returns>
        public static async Task<VoiceToTextLog?> SaveVoiceToTextResult(
            this DbContext context,
            string ownerWxid,
            string friendId,
            long msgSvrId,
            bool success,
            string? text,
            string? errorMsg,
            long taskId = 0)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || msgSvrId == 0)
            {
                return null;
            }

            var normalizedOwnerWxid = ownerWxid.Trim();
            var normalizedFriendId = friendId?.Trim() ?? string.Empty;
            string lockKey = $"VoiceToText_{normalizedOwnerWxid}_{msgSvrId}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var baseQuery = context.Set<Message>()
                    .Where(m => m.accountId == normalizedOwnerWxid
                        && m.msgSvrId == msgSvrId
                        && !m.isDeleted);

                Message? message = null;
                if (!string.IsNullOrWhiteSpace(normalizedFriendId))
                {
                    // 优先精确限定到目标会话，避免仅靠 ownerWxid + MsgSvrId 时异常碰撞到其他会话。
                    message = await baseQuery
                        .Where(m => m.senderWxid == normalizedFriendId
                            || m.receiverWxid == normalizedFriendId)
                        .OrderByDescending(m => m.messageId)
                        .FirstOrDefaultAsync();
                }

                // 兼容旧数据：如果 friendId 未传或会话字段缺失，再退回 ownerWxid + MsgSvrId。
                message ??= await baseQuery
                    .OrderByDescending(m => m.messageId)
                    .FirstOrDefaultAsync();

                if (message == null || message.messageId <= 0 || message.messageId > int.MaxValue)
                {
                    return null;
                }

                var now = DateTime.UtcNow;
                var messageId = (int)message.messageId;
                var log = await context.Set<VoiceToTextLog>()
                    .FirstOrDefaultAsync(item => item.messageId == messageId);

                if (log == null)
                {
                    log = new VoiceToTextLog
                    {
                        messageId = messageId,
                        createdAt = now
                    };
                    await context.Set<VoiceToTextLog>().AddAsync(log);
                }

                log.voiceUrl = await ResolveVoiceToTextLogVoiceUrlAsync(context, messageId, message);
                log.transcribeStatus = success ? 1 : -1;
                log.transcribedText = success ? text?.Trim() ?? string.Empty : string.Empty;
                log.errorMessage = success ? string.Empty : errorMsg?.Trim() ?? string.Empty;
                log.accuracy = success ? 1.0 : 0.0;
                log.transcribeTime = now;
                log.updatedAt = now;

                if (log.id != 0)
                {
                    context.Entry(log).State = EntityState.Modified;
                }

                await context.SaveChangesAsync();
                return log;
            });
        }

        /// <summary>
        /// 将单条聊天消息标记为已删除。
        /// <para>
        /// MsgDelNotice(1054) 表示手机微信侧删除了一条具体消息；这里只做软删除，
        /// 不调用 <see cref="SaveMessages"/>，避免 SaveMessages 的 upsert 逻辑把 isDeleted 恢复为 false。
        /// 优先按 ownerWxid + MsgSvrId 匹配；没有服务器消息 ID 时再按本地 MsgId 匹配。
        /// </para>
        /// </summary>
        /// <returns>找到并更新后的消息；没有匹配记录时返回 null。</returns>
        public static async Task<Message?> MarkMessageDeleted(
            this DbContext context,
            string ownerWxid,
            long msgSvrId,
            long localMsgId,
            string friendWxid,
            string? originalContent = null)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || (msgSvrId == 0 && localMsgId == 0))
            {
                return null;
            }

            var localMessageId = localMsgId == 0
                ? string.Empty
                : localMsgId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lockKey = msgSvrId != 0
                ? $"MessageDelete_{ownerWxid}_svr_{msgSvrId}"
                : $"MessageDelete_{ownerWxid}_local_{localMessageId}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                Message? message = null;
                if (msgSvrId != 0)
                {
                    message = await context.Set<Message>()
                        .FirstOrDefaultAsync(m => m.accountId == ownerWxid && m.msgSvrId == msgSvrId);
                }

                if (message == null && !string.IsNullOrWhiteSpace(localMessageId))
                {
                    message = await context.Set<Message>()
                        .FirstOrDefaultAsync(m => m.accountId == ownerWxid && m.localMessageId == localMessageId);
                }

                if (message == null)
                {
                    return null;
                }

                var now = DateTime.UtcNow;
                message.isDeleted = true;
                message.readStatus = 2;
                message.updatedAt = now;

                // 保留原正文，避免删除后排障缺少上下文；仅在旧记录为空时回填删除通知携带的正文。
                if (string.IsNullOrWhiteSpace(message.content) && !string.IsNullOrWhiteSpace(originalContent))
                {
                    message.content = originalContent;
                }

                context.Entry(message).State = EntityState.Modified;
                await RefreshConversationPreviewAfterMessageStateChanged(
                    context,
                    ownerWxid,
                    message,
                    excludeChangedMessage: true);
                await context.SaveChangesAsync();
                return message;
            });
        }

        /// <summary>
        /// 将单条聊天消息标记为已撤回。
        /// <para>
        /// RevokeMessageTask 成功回执可能早于微信侧删除/撤回通知；
        /// 这里仅设置 isRevoked/revokedAt 并记录 MessageRevocations，不把消息软删除，
        /// 便于 UI 区分“撤回”和“删除”。后续若收到 MsgDelNotice，会再走删除软标记进行校准。
        /// </para>
        /// </summary>
        /// <returns>找到并更新后的消息；没有匹配记录时返回 null。</returns>
        public static async Task<Message?> MarkMessageRevoked(
            this DbContext context,
            string ownerWxid,
            long msgSvrId,
            string friendWxid,
            string revokerWxid,
            string reason = "")
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || msgSvrId == 0)
            {
                return null;
            }

            var normalizedOwnerWxid = ownerWxid.Trim();
            var normalizedFriendWxid = friendWxid?.Trim() ?? string.Empty;
            string lockKey = $"MessageRevoke_{normalizedOwnerWxid}_{msgSvrId}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var query = context.Set<Message>()
                    .Where(m => m.accountId == normalizedOwnerWxid
                        && m.msgSvrId == msgSvrId
                        && !m.isDeleted);

                Message? message = null;
                if (!string.IsNullOrWhiteSpace(normalizedFriendWxid))
                {
                    message = await query
                        .Where(m => m.senderWxid == normalizedFriendWxid
                            || m.receiverWxid == normalizedFriendWxid
                            || m.senderWxid == normalizedOwnerWxid
                            || m.receiverWxid == normalizedOwnerWxid)
                        .OrderByDescending(m => m.messageId)
                        .FirstOrDefaultAsync();
                }

                message ??= await query
                    .OrderByDescending(m => m.messageId)
                    .FirstOrDefaultAsync();

                if (message == null)
                {
                    return null;
                }

                var now = DateTime.UtcNow;
                message.isRevoked = true;
                message.revokedAt = now;
                message.updatedAt = now;

                context.Entry(message).State = EntityState.Modified;

                if (message.messageId > 0 && message.messageId <= int.MaxValue)
                {
                    var messageId = (int)message.messageId;
                    var revocation = await context.Set<MessageRevocation>()
                        .FirstOrDefaultAsync(item => item.messageId == messageId);

                    if (revocation == null)
                    {
                        revocation = new MessageRevocation
                        {
                            messageId = messageId,
                            createdAt = now
                        };
                        await context.Set<MessageRevocation>().AddAsync(revocation);
                    }

                    revocation.revokerWxid = string.IsNullOrWhiteSpace(revokerWxid)
                        ? normalizedOwnerWxid
                        : revokerWxid.Trim();
                    revocation.revocationReason = string.IsNullOrWhiteSpace(reason)
                        ? "消息撤回"
                        : reason.Trim();
                    revocation.revocationTime = now;
                    revocation.updatedAt = now;

                    if (revocation.id != 0)
                    {
                        context.Entry(revocation).State = EntityState.Modified;
                    }
                }

                await RefreshConversationPreviewAfterMessageStateChanged(
                    context,
                    normalizedOwnerWxid,
                    message,
                    excludeChangedMessage: false);
                await context.SaveChangesAsync();
                return message;
            });
        }

        /// <summary>
        /// 保存群发助手历史消息。
        /// <para>
        /// GroupSendHistoryPushNotice 是 Android 从微信 massendinfo 表同步出来的快照；
        /// 当前 MassMessages / MassMessageDetails 没有稳定外部唯一键，因此按“账号 + 创建时间 + 内容 + 目标列表”
        /// 做幂等更新，避免每次拉历史都重复插入。
        /// </para>
        /// </summary>
        public static async Task<List<MassMessage>> SaveMassSendHistory(
            this DbContext context,
            string ownerWxid,
            IEnumerable<MassMessage> messages,
            Dictionary<string, List<MassMessageDetail>>? detailsByFingerprint = null)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || messages == null)
            {
                return new List<MassMessage>();
            }

            var normalizedMessages = messages
                .Where(message => message != null)
                .ToList();
            if (!normalizedMessages.Any())
            {
                return new List<MassMessage>();
            }

            long accountKey = BuildWechatAccountNumericKey(ownerWxid);
            string lockKey = $"MassSendHistory_{ownerWxid}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var now = DateTime.UtcNow;
                var saved = new List<MassMessage>(normalizedMessages.Count);
                var strategy = context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    saved.Clear();
                    using var transaction = await context.Database.BeginTransactionAsync();
                    try
                    {
                        foreach (var incoming in normalizedMessages)
                        {
                            incoming.wechatAccountId = incoming.wechatAccountId == 0 ? accountKey : incoming.wechatAccountId;
                            incoming.messageTitle ??= string.Empty;
                            incoming.messageContent ??= string.Empty;
                            incoming.createdAt = incoming.createdAt == default ? now : incoming.createdAt;
                            incoming.updatedAt = now;
                            incoming.scheduledTime = incoming.scheduledTime == default ? incoming.sentTime : incoming.scheduledTime;
                            incoming.sentTime = incoming.sentTime == default ? now : incoming.sentTime;

                            var existing = await context.Set<MassMessage>()
                                .FirstOrDefaultAsync(m => m.wechatAccountId == accountKey
                                    && m.sentTime == incoming.sentTime
                                    && m.messageType == incoming.messageType
                                    && m.messageContent == incoming.messageContent
                                    && m.messageTitle == incoming.messageTitle);

                            if (existing == null)
                            {
                                await context.Set<MassMessage>().AddAsync(incoming);
                                await context.SaveChangesAsync();
                                existing = incoming;
                            }
                            else
                            {
                                existing.targetType = incoming.targetType;
                                existing.totalRecipients = incoming.totalRecipients;
                                existing.successSentCount = incoming.successSentCount;
                                existing.failedSentCount = incoming.failedSentCount;
                                existing.sendStatus = incoming.sendStatus;
                                existing.updatedAt = now;
                                context.Entry(existing).State = EntityState.Modified;
                                await context.SaveChangesAsync();
                            }

                            var fingerprint = BuildMassSendHistoryFingerprint(incoming);
                            if (detailsByFingerprint != null
                                && detailsByFingerprint.TryGetValue(fingerprint, out var details)
                                && details != null)
                            {
                                await UpsertMassSendDetails(context, existing.id, details, now);
                            }

                            saved.Add(existing);
                        }

                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                return saved;
            });
        }

        /// <summary>
        /// 根据实时聊天消息补齐会话。
        /// <para>
        /// 实时 FriendTalkNotice / WeChatTalkToFriendNotice 可能先于 ConversationPushNotice 到达；
        /// 如果只保存 Message 而不补 Conversation，Web 端会收到 SignalR 消息但会话列表仍为空，群聊尤其明显。
        /// 这里仍复用 SaveConversations 的 ownerWxid + conversationWxid 幂等口径，不绕过现有持久化规则。
        /// </para>
        /// </summary>
        /// <param name="ownerWxid">当前登录微信账号 wxid。</param>
        /// <param name="message">已经归一化后的聊天消息。</param>
        /// <param name="displayName">会话显示名；没有群资料时可先使用 roomId。</param>
        /// <param name="displayAvatar">会话头像。</param>
        /// <returns>保存后的会话；参数不足时返回 null。</returns>
        public static async Task<Conversation?> SaveConversationFromMessage(
            this DbContext context,
            string ownerWxid,
            Message message,
            string? displayName = null,
            string? displayAvatar = null)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || message == null)
            {
                return null;
            }

            var conversationWxid = ResolveConversationWxidFromMessage(ownerWxid, message);
            if (string.IsNullOrWhiteSpace(conversationWxid))
            {
                return null;
            }

            var now = DateTime.UtcNow;
            var messageTime = message.sentAt ?? message.receivedAt ?? now;
            var incomingUnread = message.direction == 2 ? 1 : 0;
            string lockKey = $"Conversation_{ownerWxid}_{conversationWxid}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var existing = await context.Set<Conversation>()
                    .FirstOrDefaultAsync(c => c.wechatAccountId == ownerWxid
                        && c.conversationWxid == conversationWxid);

                var resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? conversationWxid : displayName.Trim();
                var resolvedAvatar = displayAvatar ?? string.Empty;
                var preview = BuildConversationMessagePreview(ownerWxid, message, conversationWxid);

                if (existing == null)
                {
                    var conversation = new Conversation
                    {
                        wechatAccountId = ownerWxid,
                        conversationWxid = conversationWxid,
                        conversationType = IsChatRoomWxid(conversationWxid) ? 2 : 1,
                        displayName = resolvedDisplayName,
                        displayAvatar = resolvedAvatar,
                        unreadCount = incomingUnread,
                        messageCount = 1,
                        isPinned = 0,
                        isMuted = 0,
                        lastMessageContent = preview,
                        lastMessageTime = messageTime,
                        createdAt = now,
                        updatedAt = now,
                        isDeleted = false
                    };
                    await context.Set<Conversation>().AddAsync(conversation);
                    await context.SaveChangesAsync();
                    return conversation;
                }

                existing.conversationType = IsChatRoomWxid(conversationWxid) ? 2 : 1;
                if (string.IsNullOrWhiteSpace(existing.displayName)
                    || string.Equals(existing.displayName, existing.conversationWxid, StringComparison.OrdinalIgnoreCase))
                {
                    existing.displayName = resolvedDisplayName;
                }
                if (!string.IsNullOrWhiteSpace(resolvedAvatar))
                {
                    existing.displayAvatar = resolvedAvatar;
                }
                existing.unreadCount = Math.Max(0, existing.unreadCount) + incomingUnread;
                existing.messageCount = Math.Max(0, existing.messageCount) + 1;
                existing.lastMessageContent = preview;
                existing.lastMessageTime = messageTime;
                existing.isDeleted = false;
                existing.updatedAt = now;
                context.Entry(existing).State = EntityState.Modified;
                await context.SaveChangesAsync();
                return existing;
            });
        }

        /// <summary>
        /// 根据群资料确保群聊会话存在。
        /// <para>
        /// 群列表同步回包只会写入 Groups 表；如果不同时补 Conversations，Web 端“群聊”页即使同步成功也仍然没有可选会话。
        /// 这里只创建缺失会话或补齐群名/头像，不覆盖已有会话的最后消息、未读数和消息数，避免群资料同步冲掉聊天列表状态。
        /// </para>
        /// </summary>
        /// <param name="ownerWxid">当前登录微信账号 wxid。</param>
        /// <param name="groups">已保存或待补齐的群资料。</param>
        /// <returns>新增或更新过的群聊会话。</returns>
        public static async Task<List<Conversation>> EnsureChatRoomConversations(
            this DbContext context,
            string ownerWxid,
            IEnumerable<Group> groups)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || groups == null)
            {
                return new List<Conversation>();
            }

            var normalizedGroups = groups
                .Where(group => group != null && !string.IsNullOrWhiteSpace(group.groupWxid))
                .GroupBy(group => group.groupWxid, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (!normalizedGroups.Any())
            {
                return new List<Conversation>();
            }

            string lockKey = $"ChatRoomConversations_{ownerWxid}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var now = DateTime.UtcNow;
                var savedConversations = new List<Conversation>(normalizedGroups.Count);

                foreach (var group in normalizedGroups)
                {
                    var roomId = group.groupWxid.Trim();
                    var displayName = string.IsNullOrWhiteSpace(group.groupName) ? roomId : group.groupName.Trim();
                    var displayAvatar = group.groupAvatar ?? string.Empty;
                    var lastTime = group.updatedAt != default ? group.updatedAt : now;

                    var existing = await context.Set<Conversation>()
                        .FirstOrDefaultAsync(c => c.wechatAccountId == ownerWxid
                            && c.conversationWxid == roomId);

                    if (existing == null)
                    {
                        existing = new Conversation
                        {
                            wechatAccountId = ownerWxid,
                            conversationWxid = roomId,
                            conversationType = 2,
                            displayName = displayName,
                            displayAvatar = displayAvatar,
                            unreadCount = 0,
                            messageCount = 0,
                            isPinned = 0,
                            isMuted = 0,
                            lastMessageContent = string.Empty,
                            lastMessageTime = lastTime,
                            createdAt = now,
                            updatedAt = now,
                            isDeleted = false
                        };
                        await context.Set<Conversation>().AddAsync(existing);
                    }
                    else
                    {
                        existing.conversationType = 2;
                        existing.isDeleted = false;

                        // 群消息先到时 displayName 往往只是 roomId；群资料同步后再替换为真实群名。
                        if (!string.IsNullOrWhiteSpace(displayName)
                            && (!string.Equals(displayName, roomId, StringComparison.OrdinalIgnoreCase)
                                || string.IsNullOrWhiteSpace(existing.displayName)
                                || string.Equals(existing.displayName, existing.conversationWxid, StringComparison.OrdinalIgnoreCase)))
                        {
                            existing.displayName = displayName;
                        }

                        if (!string.IsNullOrWhiteSpace(displayAvatar))
                        {
                            existing.displayAvatar = displayAvatar;
                        }

                        if (existing.lastMessageTime == default)
                        {
                            existing.lastMessageTime = lastTime;
                        }

                        existing.updatedAt = now;
                        context.Entry(existing).State = EntityState.Modified;
                    }

                    savedConversations.Add(existing);
                }

                await context.SaveChangesAsync();
                return savedConversations;
            });
        }

        /// <summary>
        /// 将指定会话标记为已删除。
        /// </summary>
        public static async Task<bool> MarkConversationDeleted(this DbContext context, string ownerWxid, string conversationWxid)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || string.IsNullOrWhiteSpace(conversationWxid))
            {
                return false;
            }

            string lockKey = $"Conversation_{ownerWxid}_{conversationWxid}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var conversations = await context.Set<Conversation>()
                    .Where(c => c.wechatAccountId == ownerWxid
                        && c.conversationWxid == conversationWxid
                        && !c.isDeleted)
                    .ToListAsync();

                if (!conversations.Any())
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                foreach (var conversation in conversations)
                {
                    conversation.isDeleted = true;
                    conversation.unreadCount = 0;
                    conversation.updatedAt = now;
                }

                await context.SaveChangesAsync();
                return true;
            });
        }

        /// <summary>
        /// 保存群聊列表或单个群聊通知。
        /// <para>
        /// 安卓端群同步目前上报的是账号 wxid，而 Groups 表历史字段仍是 long 类型的 wechatAccountId；
        /// 因此这里使用 ownerWxid 的稳定哈希作为账号数值键，保证同一账号重复同步可幂等 upsert。
        /// </para>
        /// </summary>
        /// <param name="ownerWxid">所属微信账号 wxid。</param>
        /// <param name="groups">待保存的群聊实体。</param>
        /// <param name="membersByRoom">可选：按 roomId 分组的群成员列表。</param>
        /// <returns>实际新增或更新后的群聊实体。</returns>
        public static async Task<List<Group>> SaveChatRooms(
            this DbContext context,
            string ownerWxid,
            List<Group> groups,
            Dictionary<string, List<GroupMember>>? membersByRoom = null)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || groups == null || !groups.Any())
            {
                return new List<Group>();
            }

            long accountKey = BuildWechatAccountNumericKey(ownerWxid);
            var now = DateTime.UtcNow;
            foreach (var group in groups)
            {
                group.wechatAccountId = group.wechatAccountId == 0 ? accountKey : group.wechatAccountId;
                group.groupWxid ??= string.Empty;
                group.groupName ??= string.Empty;
                group.groupNotice ??= string.Empty;
                group.ownerWxid ??= string.Empty;
                group.groupAvatar ??= string.Empty;
                group.groupDescription ??= string.Empty;
                group.isDeleted = false;
                group.updatedAt = now;
                if (group.createdAt == default)
                {
                    group.createdAt = now;
                }
            }

            string lockKey = $"ChatRooms_{ownerWxid}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var savedGroups = new List<Group>(groups.Count);
                var strategy = context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    savedGroups.Clear();
                    using var transaction = await context.Database.BeginTransactionAsync();
                    try
                    {
                        foreach (var group in groups.Where(g => !string.IsNullOrWhiteSpace(g.groupWxid)))
                        {
                            var existing = await context.Set<Group>()
                                .FirstOrDefaultAsync(g => g.wechatAccountId == accountKey && g.groupWxid == group.groupWxid);

                            // 兼容早期未写账号数值键的数据，避免升级后产生同一 roomId 的重复群记录。
                            existing ??= await context.Set<Group>()
                                .FirstOrDefaultAsync(g => g.wechatAccountId == 0 && g.groupWxid == group.groupWxid);

                            if (existing != null)
                            {
                                CopyChatRoomForUpsert(existing, group, now, accountKey);
                                context.Entry(existing).State = EntityState.Modified;
                                savedGroups.Add(existing);
                            }
                            else
                            {
                                await context.Set<Group>().AddAsync(group);
                                savedGroups.Add(group);
                            }
                        }

                        await context.SaveChangesAsync();

                        if (membersByRoom != null && membersByRoom.Count > 0)
                        {
                            foreach (var group in savedGroups)
                            {
                                if (!membersByRoom.TryGetValue(group.groupWxid, out var members) || members == null || members.Count == 0)
                                {
                                    continue;
                                }

                                await UpsertChatRoomMembers(context, group, members, now);
                            }

                            await context.SaveChangesAsync();
                        }

                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                return savedGroups;
            });
        }

        /// <summary>
        /// 将群聊标记为已删除。
        /// <para>群删除通知到达时只做软删除，保留历史消息和历史群资料。</para>
        /// </summary>
        public static async Task<bool> MarkChatRoomDeleted(this DbContext context, string ownerWxid, string roomId)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || string.IsNullOrWhiteSpace(roomId))
            {
                return false;
            }

            long accountKey = BuildWechatAccountNumericKey(ownerWxid);
            string lockKey = $"ChatRoom_{ownerWxid}_{roomId}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var groups = await context.Set<Group>()
                    .Where(g => g.groupWxid == roomId
                        && !g.isDeleted
                        && (g.wechatAccountId == accountKey || g.wechatAccountId == 0))
                    .ToListAsync();

                if (!groups.Any())
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                var groupIds = groups.Select(g => g.id).ToList();
                foreach (var group in groups)
                {
                    group.wechatAccountId = accountKey;
                    group.isDeleted = true;
                    group.groupStatus = 0;
                    group.updatedAt = now;
                }

                await context.Set<GroupMember>()
                    .Where(m => groupIds.Contains(m.groupId) && !m.isDeleted)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.isDeleted, true)
                        .SetProperty(m => m.updatedAt, now));

                await context.SaveChangesAsync();
                return true;
            });
        }

        /// <summary>
        /// 保存 62203 群邀请通知。
        /// <para>
        /// ChatRoomInvitePushNotice / ChatRoomInviteListNotice 都只携带 WeChatId、ChatRoomId、MsgId 等协议字段，
        /// 旧版 GroupInvitation 的 GroupId/InviteeWxid 不足以表达完整数据；这里统一把协议字段归一后幂等 upsert。
        /// </para>
        /// </summary>
        public static async Task<List<GroupInvitation>> SaveGroupInvitations(
            this DbContext context,
            string ownerWxid,
            IEnumerable<GroupInvitation>? invitations)
        {
            var normalizedOwner = ownerWxid?.Trim() ?? string.Empty;
            var incoming = (invitations ?? Enumerable.Empty<GroupInvitation>())
                .Where(inv => inv != null)
                .Where(inv => !string.IsNullOrWhiteSpace(inv.chatRoomId) || inv.msgId != 0)
                .ToList();

            if (string.IsNullOrWhiteSpace(normalizedOwner) || incoming.Count == 0)
            {
                return new List<GroupInvitation>();
            }

            var now = DateTime.UtcNow;
            var accountKey = BuildWechatAccountNumericKey(normalizedOwner);
            var lockKey = $"GroupInvitations_{normalizedOwner}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var saved = new List<GroupInvitation>();
                var strategy = context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    saved.Clear();
                    using var transaction = await context.Database.BeginTransactionAsync();
                    try
                    {
                        foreach (var invitation in incoming)
                        {
                            NormalizeGroupInvitationForSave(invitation, normalizedOwner, now);

                            if (invitation.groupId <= 0 && !string.IsNullOrWhiteSpace(invitation.chatRoomId))
                            {
                                var group = await context.Set<Group>()
                                    .AsNoTracking()
                                    .Where(g => g.groupWxid == invitation.chatRoomId
                                        && !g.isDeleted
                                        && (g.wechatAccountId == accountKey || g.wechatAccountId == 0))
                                    .OrderByDescending(g => g.updatedAt)
                                    .FirstOrDefaultAsync();

                                if (group != null)
                                {
                                    invitation.groupId = group.id;
                                }
                            }

                            var existing = await FindExistingGroupInvitationAsync(context, invitation);
                            if (existing == null)
                            {
                                await context.Set<GroupInvitation>().AddAsync(invitation);
                                saved.Add(invitation);
                                continue;
                            }

                            CopyGroupInvitationForUpsert(existing, invitation, now);
                            context.Entry(existing).State = EntityState.Modified;
                            saved.Add(existing);
                        }

                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                return saved;
            });
        }

        /// <summary>
        /// 查询群邀请列表。
        /// </summary>
        public static async Task<List<GroupInvitationDto>> GetGroupInvitations(
            this DbContext context,
            string ownerWxid,
            string chatRoomId = "",
            int count = 100,
            bool pendingOnly = false)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid))
            {
                return new List<GroupInvitationDto>();
            }

            var normalizedOwner = ownerWxid.Trim();
            var take = count <= 0 ? 100 : Math.Min(count, 500);
            var query = context.Set<GroupInvitation>()
                .AsNoTracking()
                .Where(inv => inv.weChatId == normalizedOwner);

            if (!string.IsNullOrWhiteSpace(chatRoomId))
            {
                var room = chatRoomId.Trim();
                query = query.Where(inv => inv.chatRoomId == room);
            }

            if (pendingOnly)
            {
                query = query.Where(inv => inv.invitationStatus == 0);
            }

            var items = await query
                .OrderByDescending(inv => inv.updateTime)
                .ThenByDescending(inv => inv.invitationTime)
                .ThenByDescending(inv => inv.id)
                .Take(take)
                .ToListAsync();

            return items.Select(ToGroupInvitationDto).ToList();
        }

        /// <summary>
        /// 标记群邀请审批成功。
        /// </summary>
        public static async Task<bool> MarkGroupInvitationApproved(this DbContext context, string ownerWxid, long msgId)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || msgId == 0)
            {
                return false;
            }

            var normalizedOwner = ownerWxid.Trim();
            var lockKey = $"GroupInvitationApprove_{normalizedOwner}_{msgId}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var invitations = await context.Set<GroupInvitation>()
                    .Where(inv => inv.weChatId == normalizedOwner && inv.msgId == msgId)
                    .ToListAsync();

                if (invitations.Count == 0)
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                foreach (var invitation in invitations)
                {
                    invitation.invitationStatus = 1;
                    invitation.responseTime = now;
                    invitation.updatedAt = now;
                }

                await context.SaveChangesAsync();
                return true;
            });
        }

        /// <summary>
        /// 应用群资料变更通知。
        /// <para>
        /// ChatRoomChangedNotice 只携带变更类型和内容，不携带完整群快照；
        /// 这里仅更新可以确定语义的字段，不猜测成员详情。
        /// </para>
        /// </summary>
        public static async Task<bool> ApplyChatRoomChange(this DbContext context, string ownerWxid, string roomId, int changeType, string? content)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || string.IsNullOrWhiteSpace(roomId))
            {
                return false;
            }

            long accountKey = BuildWechatAccountNumericKey(ownerWxid);
            string lockKey = $"ChatRoom_{ownerWxid}_{roomId}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var group = await context.Set<Group>()
                    .FirstOrDefaultAsync(g => g.groupWxid == roomId
                        && (g.wechatAccountId == accountKey || g.wechatAccountId == 0));

                if (group == null)
                {
                    group = new Group
                    {
                        wechatAccountId = accountKey,
                        groupWxid = roomId,
                        groupName = roomId,
                        groupStatus = 1,
                        createdAt = DateTime.UtcNow
                    };
                    await context.Set<Group>().AddAsync(group);
                }

                var now = DateTime.UtcNow;
                group.wechatAccountId = accountKey;
                group.isDeleted = false;
                group.updatedAt = now;

                switch (changeType)
                {
                    case 1: // Change_PublicNotice
                        group.groupNotice = content ?? string.Empty;
                        break;
                    case 2: // Change_NickName
                        group.groupName = content ?? group.groupName;
                        break;
                    case 5: // Change_Owner
                        group.ownerWxid = content ?? string.Empty;
                        break;
                    case 6: // Change_Avatar
                        group.groupAvatar = content ?? string.Empty;
                        break;
                    case 7: // Change_MemberAdd
                        group.memberCount = Math.Max(0, group.memberCount + 1);
                        break;
                    case 8: // Change_MemberDel
                        group.memberCount = Math.Max(0, group.memberCount - 1);
                        break;
                    default:
                        // 成员列表、群名片、自身群昵称等变更缺少完整字段，先只刷新更新时间。
                        break;
                }

                await context.SaveChangesAsync();
                return true;
            });
        }

        /// <summary>
        /// 媒体补偿结构化记录。
        /// <para>该结构序列化到 MessageExtensions，不新增表结构，便于保留 1051/1271 的上下文。</para>
        /// </summary>
        private sealed class MessageMediaCompensationEntry
        {
            public MessageMediaCompensationEntry()
            {
            }

            public string SourceNotice { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
            public long? MsgSvrId { get; set; }
            public short? MessageType { get; set; }
            public long? FileSize { get; set; }
            public int? SubType { get; set; }
            public string FileId { get; set; } = string.Empty;
            public int? CdnFileType { get; set; }
            public string FileFmt { get; set; } = string.Empty;
            public long? TaskId { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        /// <summary>
        /// RequestTalkDetailTask 待回填上下文。
        /// <para>用于弥补 1029 回包不带 MsgSvrId 的协议缺口。</para>
        /// </summary>
        public sealed class RequestTalkDetailPendingContextInfo
        {
            public RequestTalkDetailPendingContextInfo()
            {
            }

            public string WeChatId { get; set; } = string.Empty;
            public string FriendId { get; set; } = string.Empty;
            public long MsgId { get; set; }
            public long MsgSvrId { get; set; }
            public string Md5 { get; set; } = string.Empty;
            public bool GetOriginal { get; set; }
            public long TaskId { get; set; }
            public int MessageTableId { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        private static bool TryGetMessageTableId(Message message, out int messageTableId)
        {
            if (message.messageId > 0 && message.messageId <= int.MaxValue)
            {
                messageTableId = (int)message.messageId;
                return true;
            }

            messageTableId = 0;
            return false;
        }

        private static long? NormalizeNonNegative(long? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return Math.Max(0, value.Value);
        }

        private static bool LooksLikeXmlContent(string? content)
        {
            return !string.IsNullOrWhiteSpace(content)
                && content.TrimStart().StartsWith("<", StringComparison.Ordinal);
        }

        /// <summary>
        /// 合并 1028 回包对应的发送状态。
        /// <para>成功回包只保证“已发送”，不覆盖已有已送达/已读；失败回包明确标记失败。</para>
        /// </summary>
        private static short MergeTalkToFriendSendStatus(short? currentStatus, bool success)
        {
            if (!success)
            {
                return 5;
            }

            if (!currentStatus.HasValue || currentStatus.Value < 2 || currentStatus.Value == 5)
            {
                return 2;
            }

            return currentStatus.Value;
        }

        private static bool ShouldPromoteMediaUrlToMessageContent(string? oldContent, string mediaUrl)
        {
            if (string.IsNullOrWhiteSpace(oldContent))
            {
                return true;
            }

            var normalizedOldContent = oldContent.Trim();
            if (string.Equals(normalizedOldContent, mediaUrl, StringComparison.Ordinal))
            {
                return false;
            }

            return LooksLikeXmlContent(normalizedOldContent);
        }

        private static async Task<string> ResolveVoiceToTextLogVoiceUrlAsync(
            DbContext context,
            int messageTableId,
            Message message)
        {
            var mediaList = await context.Set<MessageMedia>()
                .AsNoTracking()
                .Where(media => media.messageId == messageTableId
                    && media.mediaUrl != string.Empty)
                .OrderByDescending(media => media.updatedAt)
                .ThenByDescending(media => media.id)
                .ToListAsync();

            var voiceMedia = mediaList.FirstOrDefault(IsVoiceMessageMedia)
                ?? mediaList.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(voiceMedia?.mediaUrl))
            {
                return voiceMedia.mediaUrl.Trim();
            }

            if (!string.IsNullOrWhiteSpace(message.content) && !LooksLikeXmlContent(message.content))
            {
                return message.content.Trim();
            }

            return message.contentXml ?? string.Empty;
        }

        private static bool IsVoiceMessageMedia(MessageMedia media)
        {
            if (media == null)
            {
                return false;
            }

            return media.mediaType == 3
                || LooksLikeAudioMediaExtension(media.fileExtension)
                || LooksLikeAudioMediaUrl(media.mediaUrl);
        }

        private static bool LooksLikeAudioMediaExtension(string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            var normalized = extension.Trim().TrimStart('.').ToLowerInvariant();
            return normalized is "amr" or "mp3" or "wav" or "m4a" or "aac" or "ogg" or "silk";
        }

        private static bool LooksLikeAudioMediaUrl(string? mediaUrl)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl))
            {
                return false;
            }

            var path = mediaUrl.Trim();
            var queryIndex = path.IndexOfAny(new[] { '?', '#' });
            if (queryIndex >= 0)
            {
                path = path[..queryIndex];
            }

            var dotIndex = path.LastIndexOf('.');
            return dotIndex >= 0
                && dotIndex < path.Length - 1
                && LooksLikeAudioMediaExtension(path[(dotIndex + 1)..]);
        }

        private static bool IsVisibleMessageExtensionKey(string? extensionKey)
        {
            if (string.IsNullOrWhiteSpace(extensionKey))
            {
                return false;
            }

            return string.Equals(extensionKey, MediaCompensationLatestExtensionKey, StringComparison.Ordinal)
                || string.Equals(extensionKey, MediaCompensationHistoryExtensionKey, StringComparison.Ordinal)
                || extensionKey.StartsWith(CdnDownloadPendingExtensionPrefix, StringComparison.Ordinal)
                || extensionKey.StartsWith(RequestTalkDetailPendingExtensionPrefix, StringComparison.Ordinal)
                || string.Equals(extensionKey, AdvancedContentLatestExtensionKey, StringComparison.Ordinal)
                || string.Equals(extensionKey, AdvancedContentHistoryExtensionKey, StringComparison.Ordinal)
                || extensionKey.StartsWith(AdvancedContentKindExtensionPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// 解析本次媒体回填最终应写入 MessageMedias.mediaType 的聊天内容类型。
        /// <para>
        /// CDNFileType 是 1269 下载任务枚举，不是聊天消息 EnumContentType；只有原消息类型较弱
        /// （Unknown/Text/UnSupport）且没有显式 messageType 时，才允许通过显式映射兜底。
        /// </para>
        /// </summary>
        private static short ResolveEffectiveMediaMessageType(
            short? explicitMessageType,
            short existingMessageType,
            int? cdnFileType)
        {
            if (explicitMessageType.HasValue && explicitMessageType.Value > 0)
            {
                return explicitMessageType.Value;
            }

            if (!IsWeakMediaMessageType(existingMessageType))
            {
                return existingMessageType;
            }

            return TryMapCdnFileTypeToContentType(cdnFileType, out var mappedMessageType)
                ? mappedMessageType
                : existingMessageType;
        }

        /// <summary>
        /// 判断旧消息类型是否适合被媒体补偿上下文提升。
        /// </summary>
        private static bool ShouldPromoteWeakMessageTypeFromMediaContext(short existingMessageType, short effectiveMessageType)
        {
            return IsWeakMediaMessageType(existingMessageType)
                && !IsWeakMediaMessageType(effectiveMessageType)
                && existingMessageType != effectiveMessageType;
        }

        /// <summary>
        /// 弱类型通常来自旧解析器无法识别原始微信消息，此时允许媒体回填按 CDNFileType 兜底修正。
        /// </summary>
        private static bool IsWeakMediaMessageType(short messageType)
        {
            return messageType <= 0
                || messageType == (short)ProtoEnumContentType.UnknownContent
                || messageType == (short)ProtoEnumContentType.Text
                || messageType == (short)ProtoEnumContentType.UnSupport;
        }

        /// <summary>
        /// 将 CDN 下载任务类型显式映射为聊天内容类型，禁止直接混用两个枚举的数值。
        /// </summary>
        private static bool TryMapCdnFileTypeToContentType(int? cdnFileType, out short messageType)
        {
            messageType = 0;
            if (!cdnFileType.HasValue)
            {
                return false;
            }

            messageType = (ProtoCDNFileType)cdnFileType.Value switch
            {
                ProtoCDNFileType.NoteMsgPicture or
                ProtoCDNFileType.NoteMsgThumb or
                ProtoCDNFileType.ChatMsgPicture or
                ProtoCDNFileType.ChatMsgThumb => (short)ProtoEnumContentType.Picture,

                ProtoCDNFileType.NoteMsgVideo or
                ProtoCDNFileType.ChatMsgVideo => (short)ProtoEnumContentType.Video,

                ProtoCDNFileType.NoteMsgFile or
                ProtoCDNFileType.ChatMsgFile => (short)ProtoEnumContentType.File,

                ProtoCDNFileType.ChatMsgEmoji => (short)ProtoEnumContentType.Emoji,

                _ => 0
            };

            return messageType > 0;
        }

        private static async Task UpsertMessageMediaAsync(
            DbContext context,
            int messageTableId,
            short messageType,
            string mediaUrl,
            long fileSize,
            string? fileFmt,
            string? fileId,
            DateTime now)
        {
            var normalizedUrl = mediaUrl.Trim();
            var media = await context.Set<MessageMedia>()
                .FirstOrDefaultAsync(item => item.messageId == messageTableId
                    && item.mediaUrl == normalizedUrl);

            if (media == null)
            {
                media = new MessageMedia
                {
                    messageId = messageTableId,
                    mediaType = messageType,
                    mediaUrl = normalizedUrl,
                    localPath = string.Empty,
                    mediaHash = fileId?.Trim() ?? string.Empty,
                    fileSize = Math.Max(0, fileSize),
                    fileExtension = ResolveMediaFileExtension(normalizedUrl, fileFmt),
                    uploadStatus = 1,
                    createdAt = now,
                    updatedAt = now
                };
                await context.Set<MessageMedia>().AddAsync(media);
                return;
            }

            media.mediaType = messageType > 0 ? messageType : media.mediaType;
            media.fileSize = Math.Max(media.fileSize, Math.Max(0, fileSize));
            if (!string.IsNullOrWhiteSpace(fileId))
            {
                media.mediaHash = fileId.Trim();
            }

            var fileExtension = ResolveMediaFileExtension(normalizedUrl, fileFmt);
            if (!string.IsNullOrWhiteSpace(fileExtension))
            {
                media.fileExtension = fileExtension;
            }

            media.uploadStatus = 1;
            media.updatedAt = now;
            context.Entry(media).State = EntityState.Modified;
        }

        private static async Task UpsertMediaCompensationExtensionsAsync(
            DbContext context,
            int messageTableId,
            MessageMediaCompensationEntry entry,
            DateTime now)
        {
            await UpsertMessageExtensionAsync(
                context,
                messageTableId,
                MediaCompensationLatestExtensionKey,
                JsonSerializer.Serialize(entry),
                now);

            var history = await ReadMediaCompensationHistoryAsync(context, messageTableId);
            var existing = history.FirstOrDefault(item => IsSameMediaCompensationEntry(item, entry));
            if (existing == null)
            {
                history.Add(entry);
            }
            else
            {
                entry.CreatedAt = existing.CreatedAt == default ? entry.CreatedAt : existing.CreatedAt;
                history[history.IndexOf(existing)] = entry;
            }

            var compactHistory = history
                .OrderByDescending(item => item.UpdatedAt == default ? item.CreatedAt : item.UpdatedAt)
                .Take(50)
                .OrderBy(item => item.CreatedAt == default ? item.UpdatedAt : item.CreatedAt)
                .ToList();

            await UpsertMessageExtensionAsync(
                context,
                messageTableId,
                MediaCompensationHistoryExtensionKey,
                JsonSerializer.Serialize(compactHistory),
                now);
        }

        private static async Task<List<MessageMediaCompensationEntry>> ReadMediaCompensationHistoryAsync(
            DbContext context,
            int messageTableId)
        {
            var extension = await context.Set<MessageExtension>()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.messageId == messageTableId
                    && item.extensionKey == MediaCompensationHistoryExtensionKey);

            if (extension == null || string.IsNullOrWhiteSpace(extension.extensionValue))
            {
                return new List<MessageMediaCompensationEntry>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<MessageMediaCompensationEntry>>(extension.extensionValue)
                    ?? new List<MessageMediaCompensationEntry>();
            }
            catch
            {
                return new List<MessageMediaCompensationEntry>();
            }
        }

        private static async Task<MessageMediaCompensationEntry?> FindCdnDownloadPendingContextAsync(
            DbContext context,
            int messageTableId,
            string? fileId,
            long msgSvrId)
        {
            var extensionKeys = new List<string>
            {
                BuildCdnDownloadPendingExtensionKey(fileId, msgSvrId)
            };

            if (!string.IsNullOrWhiteSpace(fileId))
            {
                extensionKeys.Add(BuildCdnDownloadPendingExtensionKey(string.Empty, msgSvrId));
            }

            foreach (var extensionKey in extensionKeys.Distinct(StringComparer.Ordinal))
            {
                var extension = await context.Set<MessageExtension>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.messageId == messageTableId
                        && item.extensionKey == extensionKey);

                var pending = DeserializeMediaCompensationEntry(extension?.extensionValue);
                if (pending != null)
                {
                    return pending;
                }
            }

            var fallbackExtension = await context.Set<MessageExtension>()
                .AsNoTracking()
                .Where(item => item.messageId == messageTableId
                    && item.extensionKey.StartsWith(CdnDownloadPendingExtensionPrefix))
                .OrderByDescending(item => item.updatedAt)
                .FirstOrDefaultAsync();

            return DeserializeMediaCompensationEntry(fallbackExtension?.extensionValue);
        }

        private static MessageMediaCompensationEntry? DeserializeMediaCompensationEntry(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<MessageMediaCompensationEntry>(json);
            }
            catch
            {
                return null;
            }
        }

        private static async Task UpsertMessageExtensionAsync(
            DbContext context,
            int messageTableId,
            string extensionKey,
            string extensionValue,
            DateTime now)
        {
            var extension = await context.Set<MessageExtension>()
                .FirstOrDefaultAsync(item => item.messageId == messageTableId
                    && item.extensionKey == extensionKey);

            if (extension == null)
            {
                extension = new MessageExtension
                {
                    messageId = messageTableId,
                    extensionKey = extensionKey,
                    extensionValue = extensionValue,
                    createdAt = now,
                    updatedAt = now
                };
                await context.Set<MessageExtension>().AddAsync(extension);
                return;
            }

            extension.extensionValue = extensionValue;
            extension.updatedAt = now;
            context.Entry(extension).State = EntityState.Modified;
        }

        /// <summary>
        /// 将高级消息解析结果追加到最近历史。
        /// </summary>
        /// <remarks>
        /// history 复用 MessageExtensions，最新记录在前，按稳定解析指纹去重并限制长度。
        /// 本方法不调用 SaveChangesAsync，由外层在同一把锁内统一提交 latest/kind/history。
        /// </remarks>
        private static async Task UpsertAdvancedContentHistoryAsync(
            DbContext context,
            int messageTableId,
            AdvancedMessageContent advanced,
            DateTime now)
        {
            var history = await ReadAdvancedContentHistoryAsync(context, messageTableId);

            history.RemoveAll(item => item == null || IsSameAdvancedContentHistoryEntry(item, advanced));
            history.Insert(0, advanced);

            var compactHistory = history
                .Where(item => item != null)
                .Take(AdvancedContentHistoryMaxCount)
                .ToList();

            await UpsertMessageExtensionAsync(
                context,
                messageTableId,
                AdvancedContentHistoryExtensionKey,
                JsonSerializer.Serialize(compactHistory, AdvancedMessageContentJsonOptions),
                now);
        }

        /// <summary>
        /// 读取高级消息解析历史。
        /// </summary>
        /// <remarks>
        /// 旧数据或人工编辑导致 JSON 损坏时，直接返回空列表，避免影响 latest/kind 的正常写入。
        /// </remarks>
        private static async Task<List<AdvancedMessageContent>> ReadAdvancedContentHistoryAsync(
            DbContext context,
            int messageTableId)
        {
            var extension = await context.Set<MessageExtension>()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.messageId == messageTableId
                    && item.extensionKey == AdvancedContentHistoryExtensionKey);

            if (string.IsNullOrWhiteSpace(extension?.extensionValue))
            {
                return new List<AdvancedMessageContent>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<AdvancedMessageContent>>(
                    extension.extensionValue,
                    AdvancedMessageContentJsonOptions) ?? new List<AdvancedMessageContent>();
            }
            catch
            {
                return new List<AdvancedMessageContent>();
            }
        }

        /// <summary>
        /// 判断两次高级消息解析结果是否属于同一条历史样本。
        /// </summary>
        private static bool IsSameAdvancedContentHistoryEntry(
            AdvancedMessageContent? left,
            AdvancedMessageContent? right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.SourceNotice ?? string.Empty, right.SourceNotice ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(left.SemanticKind ?? string.Empty, right.SemanticKind ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.RawKind ?? string.Empty, right.RawKind ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.RawHash ?? string.Empty, right.RawHash ?? string.Empty, StringComparison.Ordinal)
                && left.MessageType == right.MessageType
                && left.OriginalMsgType == right.OriginalMsgType;
        }

        private static bool IsSameMediaCompensationEntry(
            MessageMediaCompensationEntry left,
            MessageMediaCompensationEntry right)
        {
            return string.Equals(left.SourceNotice ?? string.Empty, right.SourceNotice ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Url ?? string.Empty, right.Url ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(left.FileId ?? string.Empty, right.FileId ?? string.Empty, StringComparison.Ordinal)
                && left.CdnFileType == right.CdnFileType
                && left.SubType == right.SubType;
        }

        private static string BuildCdnDownloadPendingExtensionKey(string? fileId, long msgSvrId)
        {
            var normalizedFileId = fileId?.Trim() ?? string.Empty;
            var keyPart = string.IsNullOrWhiteSpace(normalizedFileId)
                ? msgSvrId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : normalizedFileId;
            return $"{CdnDownloadPendingExtensionPrefix}:{NormalizeExtensionKeyPart(keyPart)}";
        }

        private static string BuildAdvancedContentExtensionKey(string semanticKind)
        {
            return $"{AdvancedContentKindExtensionPrefix}{NormalizeExtensionKeyPart(semanticKind).ToLowerInvariant()}";
        }

        private static string NormalizeAdvancedSemanticKind(string? semanticKind)
        {
            if (string.IsNullOrWhiteSpace(semanticKind))
            {
                return string.Empty;
            }

            return semanticKind.Trim().ToLowerInvariant();
        }

        private static string NormalizeExtensionKeyPart(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "none";
            }

            var chars = value.Trim()
                .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' ? ch : '_')
                .Take(120)
                .ToArray();
            return chars.Length == 0 ? "none" : new string(chars);
        }

        private static string ResolveMediaFileExtension(string mediaUrl, string? fileFmt)
        {
            if (!string.IsNullOrWhiteSpace(fileFmt))
            {
                return fileFmt.Trim().TrimStart('.').ToLowerInvariant();
            }

            var path = mediaUrl.Trim();
            var queryIndex = path.IndexOfAny(new[] { '?', '#' });
            if (queryIndex >= 0)
            {
                path = path[..queryIndex];
            }

            var slashIndex = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
            var dotIndex = path.LastIndexOf('.');
            if (dotIndex <= slashIndex || dotIndex < 0 || dotIndex == path.Length - 1)
            {
                return string.Empty;
            }

            var extension = path[(dotIndex + 1)..].Trim();
            return extension.Length > 16 ? string.Empty : extension.ToLowerInvariant();
        }

        private static IEnumerable<string> BuildRequestTalkDetailPendingExtensionKeys(long msgId, long msgSvrId)
        {
            if (msgId > 0)
            {
                yield return BuildRequestTalkDetailPendingByMsgIdKey(msgId);
            }

            if (msgSvrId > 0)
            {
                yield return $"{RequestTalkDetailPendingExtensionPrefix}:svr:{msgSvrId.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            }
        }

        private static string BuildRequestTalkDetailPendingByMsgIdKey(long msgId)
        {
            return $"{RequestTalkDetailPendingExtensionPrefix}:msg:{msgId.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }

        private static async Task<RequestTalkDetailPendingContextInfo?> FindRequestTalkDetailPendingByExtensionKeys(
            DbContext context,
            string ownerWxid,
            string friendId,
            params string[] extensionKeys)
        {
            foreach (var extensionKey in extensionKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal))
            {
                var extensions = await context.Set<MessageExtension>()
                    .AsNoTracking()
                    .Where(item => item.extensionKey == extensionKey)
                    .OrderByDescending(item => item.updatedAt)
                    .Take(20)
                    .ToListAsync();

                foreach (var extension in extensions)
                {
                    var pending = DeserializeRequestTalkDetailPendingContext(extension.extensionValue);
                    if (IsMatchingRequestTalkDetailPendingContext(pending, ownerWxid, friendId, 0))
                    {
                        return pending;
                    }
                }
            }

            return null;
        }

        private static RequestTalkDetailPendingContextInfo? DeserializeRequestTalkDetailPendingContext(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<RequestTalkDetailPendingContextInfo>(json);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsMatchingRequestTalkDetailPendingContext(
            RequestTalkDetailPendingContextInfo? pending,
            string ownerWxid,
            string friendId,
            long msgId)
        {
            if (pending == null)
            {
                return false;
            }

            if (!string.Equals(pending.WeChatId?.Trim(), ownerWxid, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (msgId > 0 && pending.MsgId > 0 && pending.MsgId != msgId)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(friendId)
                || string.IsNullOrWhiteSpace(pending.FriendId)
                || string.Equals(pending.FriendId.Trim(), friendId, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<Message?> FindExistingMessage(DbContext context, Message message)
        {
            if (message.msgSvrId.HasValue && message.msgSvrId.Value != 0)
            {
                var existingBySvrId = await context.Set<Message>()
                    .FirstOrDefaultAsync(m => m.accountId == message.accountId && m.msgSvrId == message.msgSvrId);
                if (existingBySvrId != null)
                {
                    return existingBySvrId;
                }
            }

            if (!string.IsNullOrWhiteSpace(message.localMessageId))
            {
                return await context.Set<Message>()
                    .FirstOrDefaultAsync(m => m.accountId == message.accountId
                        && m.localMessageId == message.localMessageId);
            }

            return null;
        }

        private static async Task<Message?> FirstMessageWithOptionalFriendFilter(
            IQueryable<Message> query,
            string friendId)
        {
            if (!string.IsNullOrWhiteSpace(friendId))
            {
                var matchedByFriend = await query
                    .Where(m => m.senderWxid == friendId || m.receiverWxid == friendId)
                    .OrderByDescending(m => m.updatedAt)
                    .FirstOrDefaultAsync();

                if (matchedByFriend != null)
                {
                    return matchedByFriend;
                }
            }

            return await query
                .OrderByDescending(m => m.updatedAt)
                .FirstOrDefaultAsync();
        }

        private static bool TryParsePositiveInt64(string? value, out long result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return long.TryParse(
                    value.Trim(),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out result)
                && result > 0;
        }

        /// <summary>
        /// 获取或创建待更新的朋友圈时间线记录。
        /// </summary>
        private static async Task<MomentsTimeline> GetOrCreateMomentTimelineForUpdate(DbContext context, string ownerWxid, long circleId)
        {
            var moment = await context.Set<MomentsTimeline>()
                .FirstOrDefaultAsync(m => m.ownerWxid == ownerWxid && m.snsId == circleId);
            if (moment != null)
            {
                return moment;
            }

            moment = new MomentsTimeline
            {
                ownerWxid = ownerWxid,
                snsId = circleId,
                wechatAccountId = 0,
                receivedAt = DateTime.UtcNow.Ticks,
                createTime = 0,
                userName = string.Empty,
                nickName = string.Empty,
                content = string.Empty,
                imagesJson = "[]",
                commentsJson = "[]",
                likesJson = "[]",
                videoUrl = string.Empty,
                linkInfoJson = string.Empty,
                xmlContent = string.Empty
            };
            await context.Set<MomentsTimeline>().AddAsync(moment);
            return moment;
        }

        /// <summary>
        /// 反序列化朋友圈点赞列表。
        /// </summary>
        private static List<MomentLikeDto> DeserializeMomentLikes(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<MomentLikeDto>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<MomentLikeDto>>(json) ?? new List<MomentLikeDto>();
            }
            catch
            {
                return new List<MomentLikeDto>();
            }
        }

        /// <summary>
        /// 反序列化朋友圈评论列表。
        /// </summary>
        private static List<MomentCommentDto> DeserializeMomentComments(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<MomentCommentDto>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<MomentCommentDto>>(json) ?? new List<MomentCommentDto>();
            }
            catch
            {
                return new List<MomentCommentDto>();
            }
        }

        /// <summary>
        /// 判断两条朋友圈评论是否表示同一次评论。
        /// </summary>
        private static bool IsSameMomentComment(MomentCommentDto left, MomentCommentDto right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (left.commentId > 0 && right.commentId > 0 && left.commentId == right.commentId)
            {
                return true;
            }

            return string.Equals(left.userName?.Trim(), right.userName?.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.content?.Trim(), right.content?.Trim(), StringComparison.Ordinal)
                && string.Equals(left.replyUserName?.Trim() ?? string.Empty, right.replyUserName?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static void CopyMessageForUpsert(Message target, Message source, DateTime now)
        {
            var keepDeleted = target.isDeleted || source.isDeleted;
            var keepRevoked = target.isRevoked || source.isRevoked;
            var revokedAt = target.revokedAt ?? source.revokedAt;

            target.msgSvrId = source.msgSvrId ?? target.msgSvrId;
            target.conversationId = source.conversationId ?? target.conversationId;
            target.senderWxid = source.senderWxid ?? target.senderWxid;
            target.receiverWxid = source.receiverWxid ?? target.receiverWxid;
            target.chatType = source.chatType;
            target.messageType = source.messageType;
            target.content = source.content;
            target.contentXml = source.contentXml ?? target.contentXml;
            target.direction = source.direction;
            target.sendStatus = source.sendStatus ?? target.sendStatus;
            target.readStatus = source.readStatus ?? target.readStatus;
            target.isRevoked = keepRevoked;
            target.isDeleted = keepDeleted;
            target.localMessageId = source.localMessageId ?? target.localMessageId;
            target.clientMsgId = source.clientMsgId ?? target.clientMsgId;
            target.sentAt = source.sentAt ?? target.sentAt;
            target.receivedAt = source.receivedAt ?? target.receivedAt;
            target.readAt = source.readAt ?? target.readAt;
            target.revokedAt = revokedAt;
            target.updatedAt = now;
        }

        private static void CopyConversationForUpsert(Conversation target, Conversation source, DateTime now)
        {
            target.conversationType = source.conversationType;
            if (!string.IsNullOrWhiteSpace(source.displayName)
                && (string.IsNullOrWhiteSpace(target.displayName)
                || string.Equals(target.displayName, target.conversationWxid, StringComparison.OrdinalIgnoreCase)
                || string.Equals(target.displayName, source.conversationWxid, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(source.displayName, source.conversationWxid, StringComparison.OrdinalIgnoreCase)))
            {
                target.displayName = source.displayName;
            }
            if (!string.IsNullOrWhiteSpace(source.displayAvatar))
            {
                target.displayAvatar = source.displayAvatar;
            }
            target.unreadCount = source.unreadCount;
            if (source.messageCount > 0)
            {
                target.messageCount = source.messageCount;
            }
            target.isPinned = source.isPinned;
            target.isMuted = source.isMuted;
            if (!string.IsNullOrWhiteSpace(source.lastMessageContent))
            {
                target.lastMessageContent = source.lastMessageContent;
            }
            if (source.lastMessageTime != default)
            {
                target.lastMessageTime = source.lastMessageTime;
            }
            target.isDeleted = false;
            target.updatedAt = now;
        }

        private static void CopyUnreadConversationSnapshotForUpsert(Conversation target, Conversation source, DateTime now)
        {
            target.conversationType = source.conversationType;
            if (!string.IsNullOrWhiteSpace(source.displayName)
                && (string.IsNullOrWhiteSpace(target.displayName)
                || string.Equals(target.displayName, target.conversationWxid, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(source.displayName, source.conversationWxid, StringComparison.OrdinalIgnoreCase)))
            {
                target.displayName = source.displayName;
            }
            if (!string.IsNullOrWhiteSpace(source.displayAvatar))
            {
                target.displayAvatar = source.displayAvatar;
            }
            target.unreadCount = source.unreadCount;
            target.isMuted = source.isMuted;
            if (source.lastMessageTime != default)
            {
                target.lastMessageTime = source.lastMessageTime;
            }
            target.isDeleted = false;
            target.updatedAt = now;
        }

        private static bool ShouldClearUnreadByUnreadSnapshot(string? conversationWxid)
        {
            if (string.IsNullOrWhiteSpace(conversationWxid))
            {
                return false;
            }

            var normalizedWxid = conversationWxid.Trim();
            if (normalizedWxid.StartsWith("gh_", StringComparison.OrdinalIgnoreCase)
                || normalizedWxid.EndsWith("@stranger", StringComparison.OrdinalIgnoreCase)
                || UnreadSnapshotExcludedConversationWxids.Contains(normalizedWxid))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 从消息中解析会话 ID。
        /// </summary>
        private static string ResolveConversationWxidFromMessage(string ownerWxid, Message message)
        {
            var sender = message.senderWxid ?? string.Empty;
            var receiver = message.receiverWxid ?? string.Empty;

            if (IsChatRoomWxid(sender))
            {
                return sender;
            }

            if (IsChatRoomWxid(receiver))
            {
                return receiver;
            }

            if (!string.Equals(sender, ownerWxid, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(sender))
            {
                return sender;
            }

            if (!string.Equals(receiver, ownerWxid, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(receiver))
            {
                return receiver;
            }

            return string.Empty;
        }

        /// <summary>
        /// 消息删除或撤回后重算会话摘要，避免会话列表继续显示已删除/已撤回消息原文。
        /// </summary>
        private static async Task<Conversation?> RefreshConversationPreviewAfterMessageStateChanged(
            DbContext context,
            string ownerWxid,
            Message changedMessage,
            bool excludeChangedMessage)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || changedMessage == null)
            {
                return null;
            }

            var conversationWxid = ResolveConversationWxidFromMessage(ownerWxid, changedMessage);
            if (string.IsNullOrWhiteSpace(conversationWxid))
            {
                return null;
            }

            var conversation = await context.Set<Conversation>()
                .FirstOrDefaultAsync(item => item.wechatAccountId == ownerWxid
                    && item.conversationWxid == conversationWxid
                    && !item.isDeleted);
            if (conversation == null)
            {
                return null;
            }

            var latestMessageQuery = context.Set<Message>()
                .Where(message => message.accountId == ownerWxid
                    && !message.isDeleted
                    && (message.senderWxid == conversationWxid || message.receiverWxid == conversationWxid));

            if (excludeChangedMessage && changedMessage.messageId > 0)
            {
                latestMessageQuery = latestMessageQuery.Where(message => message.messageId != changedMessage.messageId);
            }

            var latestMessage = await latestMessageQuery
                .OrderByDescending(message => message.sentAt ?? message.receivedAt ?? message.createdAt)
                .ThenByDescending(message => message.messageId)
                .FirstOrDefaultAsync();

            var now = DateTime.UtcNow;
            if (latestMessage == null)
            {
                conversation.lastMessageContent = string.Empty;
                conversation.lastMessageTime = default;
            }
            else
            {
                conversation.lastMessageContent = BuildConversationMessagePreview(ownerWxid, latestMessage, conversationWxid);
                conversation.lastMessageTime = latestMessage.sentAt
                    ?? latestMessage.receivedAt
                    ?? latestMessage.createdAt;
            }

            conversation.updatedAt = now;
            context.Entry(conversation).State = EntityState.Modified;
            return conversation;
        }

        /// <summary>
        /// 生成会话最后一条消息预览。
        /// <para>群聊收到的文本通常是“群成员wxid:\n正文”，列表中保留前缀方便定位发送者。</para>
        /// </summary>
        private static string BuildConversationMessagePreview(string ownerWxid, Message message, string conversationWxid)
        {
            if (message.isRevoked)
            {
                return RevokedMessagePreview;
            }

            var content = message.content ?? string.Empty;
            if (message.direction == 1)
            {
                return content;
            }

            if (IsChatRoomWxid(conversationWxid))
            {
                return content;
            }

            return content;
        }

        /// <summary>
        /// 判断 wxid 是否为群聊 ID。
        /// </summary>
        private static bool IsChatRoomWxid(string? wxid)
        {
            return !string.IsNullOrWhiteSpace(wxid)
                && wxid.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase);
        }

        private static void CopyChatRoomForUpsert(Group target, Group source, DateTime now, long accountKey)
        {
            target.wechatAccountId = accountKey;
            target.groupWxid = source.groupWxid;
            target.groupName = source.groupName ?? target.groupName;
            target.groupNotice = source.groupNotice ?? target.groupNotice;
            target.ownerWxid = source.ownerWxid ?? target.ownerWxid;
            target.memberCount = source.memberCount;
            target.groupAvatar = source.groupAvatar ?? target.groupAvatar;
            target.groupDescription = source.groupDescription ?? target.groupDescription;
            target.isMuted = source.isMuted;
            target.isPinned = source.isPinned;
            target.groupStatus = source.groupStatus;
            target.isDeleted = false;
            target.updatedAt = now;
        }

        private static async Task UpsertChatRoomMembers(DbContext context, Group group, List<GroupMember> members, DateTime now)
        {
            var normalizedMembers = members
                .Where(m => !string.IsNullOrWhiteSpace(m.memberWxid))
                .GroupBy(m => m.memberWxid)
                .Select(g => g.First())
                .ToList();

            if (!normalizedMembers.Any())
            {
                return;
            }

            var incomingWxids = normalizedMembers.Select(m => m.memberWxid).ToHashSet();
            var existingMembers = await context.Set<GroupMember>()
                .Where(m => m.groupId == group.id)
                .ToListAsync();
            var existingMap = existingMembers.ToDictionary(m => m.memberWxid, m => m);

            foreach (var member in normalizedMembers)
            {
                member.groupId = group.id;
                member.createdAt = member.createdAt == default ? now : member.createdAt;
                member.updatedAt = now;
                member.isDeleted = false;

                if (existingMap.TryGetValue(member.memberWxid, out var existing))
                {
                    existing.memberNickname = member.memberNickname ?? existing.memberNickname;
                    existing.memberAvatar = member.memberAvatar ?? existing.memberAvatar;
                    existing.alias = member.alias ?? existing.alias;
                    existing.memberGender = member.memberGender ?? existing.memberGender;
                    existing.region = member.region ?? existing.region;
                    existing.memberRole = member.memberRole;
                    existing.joinSource = member.joinSource;
                    existing.inviterWxid = member.inviterWxid ?? existing.inviterWxid;
                    if (member.joinTime != default)
                    {
                        existing.joinTime = member.joinTime;
                    }
                    existing.isMuted = member.isMuted;
                    existing.memberRemarks = member.memberRemarks ?? existing.memberRemarks;
                    existing.isDeleted = false;
                    existing.updatedAt = now;
                    context.Entry(existing).State = EntityState.Modified;
                }
                else
                {
                    await context.Set<GroupMember>().AddAsync(member);
                }
            }

            // 当前群列表同步携带的是快照；快照中已经消失的成员软删除，避免网页继续显示旧成员。
            foreach (var existing in existingMembers.Where(m => !incomingWxids.Contains(m.memberWxid) && !m.isDeleted))
            {
                existing.isDeleted = true;
                existing.updatedAt = now;
                context.Entry(existing).State = EntityState.Modified;
            }

            group.memberCount = incomingWxids.Count;
            group.updatedAt = now;
            context.Entry(group).State = EntityState.Modified;
        }

        /// <summary>
        /// 归一化 62203 群邀请字段，保证同一套数据既能兼容旧表字段，又能表达新协议字段。
        /// </summary>
        private static void NormalizeGroupInvitationForSave(GroupInvitation invitation, string ownerWxid, DateTime now)
        {
            invitation.weChatId = string.IsNullOrWhiteSpace(invitation.weChatId)
                ? ownerWxid
                : invitation.weChatId.Trim();
            invitation.chatRoomId = invitation.chatRoomId?.Trim() ?? string.Empty;
            invitation.inviterWxid = invitation.inviterWxid?.Trim() ?? string.Empty;
            invitation.inviteName = invitation.inviteName?.Trim() ?? string.Empty;
            invitation.inviteeWxid = invitation.inviteeWxid?.Trim() ?? string.Empty;
            invitation.reason = invitation.reason?.Trim() ?? string.Empty;
            invitation.invitationMessage = string.IsNullOrWhiteSpace(invitation.invitationMessage)
                ? invitation.reason
                : invitation.invitationMessage.Trim();
            invitation.invitedJson = string.IsNullOrWhiteSpace(invitation.invitedJson)
                ? "[]"
                : invitation.invitedJson.Trim();
            invitation.source = string.IsNullOrWhiteSpace(invitation.source)
                ? "Unknown"
                : invitation.source.Trim();

            // 微信侧 updateTime 可能是秒或毫秒；无效时沿用已有邀请时间，仍为空再使用当前时间。
            if (invitation.updateTime > 0)
            {
                invitation.invitationTime = ConvertWechatTimestampToUtc(invitation.updateTime, now);
            }
            else
            {
                invitation.invitationTime = invitation.invitationTime == default
                    ? now
                    : EnsureUtc(invitation.invitationTime);
            }

            invitation.responseTime = invitation.responseTime == default
                ? DateTime.UnixEpoch
                : EnsureUtc(invitation.responseTime);
            invitation.createdAt = invitation.createdAt == default
                ? now
                : EnsureUtc(invitation.createdAt);
            invitation.updatedAt = now;

            if (string.IsNullOrWhiteSpace(invitation.inviteeWxid))
            {
                var firstMember = DeserializeGroupInvitationMembers(invitation.invitedJson).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstMember?.userName))
                {
                    invitation.inviteeWxid = firstMember.userName.Trim();
                }
            }
        }

        /// <summary>
        /// 查找已有群邀请，优先使用 62203 MsgId 幂等，其次按群、邀请人和更新时间兜底。
        /// </summary>
        private static async Task<GroupInvitation?> FindExistingGroupInvitationAsync(DbContext context, GroupInvitation invitation)
        {
            if (!string.IsNullOrWhiteSpace(invitation.weChatId) && invitation.msgId != 0)
            {
                var byMsgId = await context.Set<GroupInvitation>()
                    .FirstOrDefaultAsync(inv => inv.weChatId == invitation.weChatId && inv.msgId == invitation.msgId);

                if (byMsgId != null)
                {
                    return byMsgId;
                }
            }

            if (!string.IsNullOrWhiteSpace(invitation.weChatId) && invitation.msgSvrId != 0)
            {
                var byMsgSvrId = await context.Set<GroupInvitation>()
                    .FirstOrDefaultAsync(inv => inv.weChatId == invitation.weChatId && inv.msgSvrId == invitation.msgSvrId);

                if (byMsgSvrId != null)
                {
                    return byMsgSvrId;
                }
            }

            if (string.IsNullOrWhiteSpace(invitation.weChatId)
                || string.IsNullOrWhiteSpace(invitation.chatRoomId)
                || invitation.updateTime == 0)
            {
                return null;
            }

            return await context.Set<GroupInvitation>()
                .FirstOrDefaultAsync(inv => inv.weChatId == invitation.weChatId
                    && inv.chatRoomId == invitation.chatRoomId
                    && inv.inviterWxid == invitation.inviterWxid
                    && inv.updateTime == invitation.updateTime);
        }

        /// <summary>
        /// 合并群邀请字段，避免后续列表补偿回包把已审批状态回滚为待处理。
        /// </summary>
        private static void CopyGroupInvitationForUpsert(GroupInvitation target, GroupInvitation source, DateTime now)
        {
            var approved = target.invitationStatus == 1;

            target.groupId = source.groupId > 0 ? source.groupId : target.groupId;
            target.weChatId = source.weChatId;
            target.chatRoomId = source.chatRoomId;
            target.inviterWxid = source.inviterWxid;
            target.inviteName = source.inviteName;
            target.inviteeWxid = string.IsNullOrWhiteSpace(source.inviteeWxid) ? target.inviteeWxid : source.inviteeWxid;
            target.invitationStatus = approved ? target.invitationStatus : source.invitationStatus;
            target.invitationMessage = source.invitationMessage;
            target.reason = source.reason;
            target.msgId = source.msgId != 0 ? source.msgId : target.msgId;
            target.msgSvrId = source.msgSvrId != 0 ? source.msgSvrId : target.msgSvrId;
            target.updateTime = source.updateTime != 0 ? source.updateTime : target.updateTime;
            target.taskId = source.taskId != 0 ? source.taskId : target.taskId;
            target.invitedJson = string.IsNullOrWhiteSpace(source.invitedJson) ? target.invitedJson : source.invitedJson;
            target.source = source.source;
            target.invitationTime = source.invitationTime == default ? target.invitationTime : EnsureUtc(source.invitationTime);
            target.responseTime = approved ? target.responseTime : EnsureUtc(source.responseTime);
            target.updatedAt = now;
        }

        /// <summary>
        /// 转换群邀请实体为前端 DTO。
        /// </summary>
        private static GroupInvitationDto ToGroupInvitationDto(GroupInvitation invitation)
        {
            return new GroupInvitationDto
            {
                id = invitation.id,
                weChatId = invitation.weChatId ?? string.Empty,
                chatRoomId = invitation.chatRoomId ?? string.Empty,
                inviter = invitation.inviterWxid ?? string.Empty,
                inviteName = invitation.inviteName ?? string.Empty,
                reason = string.IsNullOrWhiteSpace(invitation.reason)
                    ? invitation.invitationMessage ?? string.Empty
                    : invitation.reason,
                msgId = invitation.msgId,
                msgSvrId = invitation.msgSvrId,
                updateTime = invitation.updateTime,
                taskId = invitation.taskId,
                status = invitation.invitationStatus,
                source = invitation.source ?? string.Empty,
                invitationTime = EnsureUtc(invitation.invitationTime),
                updatedAt = EnsureUtc(invitation.updatedAt),
                invited = DeserializeGroupInvitationMembers(invitation.invitedJson)
            };
        }

        // ==================== Phone Records ====================

        /// <summary>
        /// 新增或更新单条短信记录。
        /// <para>
        /// Android 的 SmsId 只在同一设备短信库内稳定，因此幂等键优先使用 ownerWxid + imei + smsId；
        /// 少数 SmsId 缺失时退回 ownerWxid + imei + threadId + rawDate + number + type。
        /// </para>
        /// </summary>
        public static async Task<SmsRecord?> UpsertSmsRecord(
            this DbContext context,
            string ownerWxid,
            string imei,
            int smsId,
            int threadId,
            string? number,
            int type,
            long rawDate,
            string? content,
            bool isRead,
            int simId,
            int blockType,
            string source = "Push")
        {
            var normalizedOwner = ownerWxid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedOwner))
            {
                return null;
            }

            var normalizedImei = imei?.Trim() ?? string.Empty;
            var normalizedNumber = number?.Trim() ?? string.Empty;
            var now = DateTime.UtcNow;
            var smsTime = ConvertWechatTimestampToUtc(rawDate, now);
            var lockKey = smsId > 0
                ? $"SmsRecord_{normalizedOwner}_{normalizedImei}_{smsId}"
                : $"SmsRecord_{normalizedOwner}_{normalizedImei}_{threadId}_{rawDate}_{normalizedNumber}_{type}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var query = context.Set<SmsRecord>()
                    .Where(item => item.ownerWxid == normalizedOwner && item.imei == normalizedImei);

                SmsRecord? record = null;
                if (smsId > 0)
                {
                    record = await query.FirstOrDefaultAsync(item => item.smsId == smsId);
                }

                record ??= await query.FirstOrDefaultAsync(item =>
                    item.smsId == smsId
                    && item.threadId == threadId
                    && item.rawDate == rawDate
                    && item.number == normalizedNumber
                    && item.type == type);

                if (record == null)
                {
                    record = new SmsRecord
                    {
                        ownerWxid = normalizedOwner,
                        imei = normalizedImei,
                        smsId = smsId,
                        threadId = threadId,
                        createdAt = now
                    };
                    await context.Set<SmsRecord>().AddAsync(record);
                }

                record.number = normalizedNumber;
                record.type = type;
                record.rawDate = rawDate;
                record.smsTime = smsTime;
                record.content = content?.Trim() ?? string.Empty;
                record.isRead = isRead;
                record.simId = simId;
                record.blockType = blockType;
                record.source = string.IsNullOrWhiteSpace(source) ? record.source : source.Trim();
                record.updatedAt = now;

                if (context.Entry(record).State != EntityState.Added)
                {
                    context.Entry(record).State = EntityState.Modified;
                }

                await context.SaveChangesAsync();
                return record;
            });
        }

        /// <summary>
        /// 批量新增或更新短信记录。
        /// </summary>
        public static async Task<List<SmsRecord>> UpsertSmsRecords(
            this DbContext context,
            string ownerWxid,
            string imei,
            IEnumerable<SmsRecord>? records,
            string source = "Pull")
        {
            var saved = new List<SmsRecord>();
            if (records == null)
            {
                return saved;
            }

            foreach (var item in records)
            {
                var record = await context.UpsertSmsRecord(
                    ownerWxid,
                    imei,
                    item.smsId,
                    item.threadId,
                    item.number,
                    item.type,
                    item.rawDate,
                    item.content,
                    item.isRead,
                    item.simId,
                    item.blockType,
                    source);

                if (record != null)
                {
                    saved.Add(record);
                }
            }

            return saved;
        }

        /// <summary>
        /// 根据短信已读通知更新本地短信状态。
        /// <para>如果历史短信尚未落库，会创建一条最小占位记录，后续 PullSmsTaskResultNotice 再补齐正文。</para>
        /// </summary>
        public static async Task<SmsRecord?> MarkSmsRead(
            this DbContext context,
            string ownerWxid,
            string imei,
            int smsId,
            int threadId)
        {
            var normalizedOwner = ownerWxid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedOwner) || (smsId <= 0 && threadId <= 0))
            {
                return null;
            }

            var normalizedImei = imei?.Trim() ?? string.Empty;
            var lockKey = smsId > 0
                ? $"SmsRead_{normalizedOwner}_{normalizedImei}_{smsId}"
                : $"SmsRead_{normalizedOwner}_{normalizedImei}_thread_{threadId}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var query = context.Set<SmsRecord>()
                    .Where(item => item.ownerWxid == normalizedOwner && item.imei == normalizedImei);

                SmsRecord? record = null;
                if (smsId > 0)
                {
                    record = await query.FirstOrDefaultAsync(item => item.smsId == smsId);
                }

                record ??= await query
                    .Where(item => item.threadId == threadId)
                    .OrderByDescending(item => item.smsTime)
                    .FirstOrDefaultAsync();

                var now = DateTime.UtcNow;
                if (record == null)
                {
                    record = new SmsRecord
                    {
                        ownerWxid = normalizedOwner,
                        imei = normalizedImei,
                        smsId = smsId,
                        threadId = threadId,
                        smsTime = now,
                        source = "ReadNotice",
                        createdAt = now
                    };
                    await context.Set<SmsRecord>().AddAsync(record);
                }

                record.isRead = true;
                record.readAt = now;
                record.source = "ReadNotice";
                record.updatedAt = now;
                if (context.Entry(record).State != EntityState.Added)
                {
                    context.Entry(record).State = EntityState.Modified;
                }

                await context.SaveChangesAsync();
                return record;
            });
        }

        /// <summary>
        /// 根据短信发送状态通知更新本地短信记录。
        /// <para>如果记录尚未存在，会创建最小占位记录，便于页面看到 SmsSentNotice 已到达。</para>
        /// </summary>
        public static async Task<SmsRecord?> MarkSmsSent(
            this DbContext context,
            string ownerWxid,
            string imei,
            int smsId,
            int type)
        {
            var normalizedOwner = ownerWxid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedOwner) || smsId <= 0)
            {
                return null;
            }

            var normalizedImei = imei?.Trim() ?? string.Empty;
            var lockKey = $"SmsSent_{normalizedOwner}_{normalizedImei}_{smsId}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var record = await context.Set<SmsRecord>()
                    .FirstOrDefaultAsync(item => item.ownerWxid == normalizedOwner
                        && item.imei == normalizedImei
                        && item.smsId == smsId);

                var now = DateTime.UtcNow;
                if (record == null)
                {
                    record = new SmsRecord
                    {
                        ownerWxid = normalizedOwner,
                        imei = normalizedImei,
                        smsId = smsId,
                        type = type,
                        smsTime = now,
                        source = "SentNotice",
                        createdAt = now
                    };
                    await context.Set<SmsRecord>().AddAsync(record);
                }

                record.sentNoticeType = type;
                record.isSentNoticeReceived = true;
                record.sentNoticeAt = now;
                record.source = "SentNotice";
                record.updatedAt = now;
                if (context.Entry(record).State != EntityState.Added)
                {
                    context.Entry(record).State = EntityState.Modified;
                }

                await context.SaveChangesAsync();
                return record;
            });
        }

        /// <summary>
        /// 查询短信记录快照。
        /// </summary>
        public static async Task<List<SmsRecordDto>> GetSmsRecords(
            this DbContext context,
            string ownerWxid,
            string imei = "",
            int count = 200)
        {
            var normalizedOwner = ownerWxid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedOwner))
            {
                return new List<SmsRecordDto>();
            }

            var normalizedImei = imei?.Trim() ?? string.Empty;
            var take = Math.Clamp(count, 1, 1000);
            var query = context.Set<SmsRecord>()
                .AsNoTracking()
                .Where(item => item.ownerWxid == normalizedOwner);

            if (!string.IsNullOrWhiteSpace(normalizedImei))
            {
                query = query.Where(item => item.imei == normalizedImei);
            }

            return await query
                .OrderByDescending(item => item.smsTime)
                .ThenByDescending(item => item.id)
                .Take(take)
                .Select(item => new SmsRecordDto
                {
                    id = item.id,
                    ownerWxid = item.ownerWxid,
                    imei = item.imei,
                    smsId = item.smsId,
                    threadId = item.threadId,
                    number = item.number,
                    type = item.type,
                    rawDate = item.rawDate,
                    smsTime = item.smsTime,
                    content = item.content,
                    isRead = item.isRead,
                    simId = item.simId,
                    blockType = item.blockType,
                    sentNoticeType = item.sentNoticeType,
                    isSentNoticeReceived = item.isSentNoticeReceived,
                    readAt = item.readAt,
                    sentNoticeAt = item.sentNoticeAt,
                    source = item.source,
                    createdAt = item.createdAt,
                    updatedAt = item.updatedAt
                })
                .ToListAsync();
        }

        /// <summary>
        /// 新增或更新单条通话记录。
        /// <para>
        /// Android 的 CallLogId 只在同一设备通话库内稳定，因此幂等键优先使用 ownerWxid + imei + callLogId；
        /// 少数 CallLogId 缺失时退回 ownerWxid + imei + rawDate + number + type。
        /// </para>
        /// </summary>
        public static async Task<CallLogRecord?> UpsertCallLogRecord(
            this DbContext context,
            string ownerWxid,
            string imei,
            int callLogId,
            string? number,
            int type,
            long rawDate,
            int durationSeconds,
            string? recordUrl,
            int simId,
            int blockType,
            string source = "Push")
        {
            var normalizedOwner = ownerWxid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedOwner))
            {
                return null;
            }

            var normalizedImei = imei?.Trim() ?? string.Empty;
            var normalizedNumber = number?.Trim() ?? string.Empty;
            var now = DateTime.UtcNow;
            var callTime = ConvertWechatTimestampToUtc(rawDate, now);
            var lockKey = callLogId > 0
                ? $"CallLogRecord_{normalizedOwner}_{normalizedImei}_{callLogId}"
                : $"CallLogRecord_{normalizedOwner}_{normalizedImei}_{rawDate}_{normalizedNumber}_{type}";

            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var query = context.Set<CallLogRecord>()
                    .Where(item => item.ownerWxid == normalizedOwner && item.imei == normalizedImei);

                CallLogRecord? record = null;
                if (callLogId > 0)
                {
                    record = await query.FirstOrDefaultAsync(item => item.callLogId == callLogId);
                }

                record ??= await query.FirstOrDefaultAsync(item =>
                    item.callLogId == callLogId
                    && item.rawDate == rawDate
                    && item.number == normalizedNumber
                    && item.type == type);

                if (record == null)
                {
                    record = new CallLogRecord
                    {
                        ownerWxid = normalizedOwner,
                        imei = normalizedImei,
                        callLogId = callLogId,
                        createdAt = now
                    };
                    await context.Set<CallLogRecord>().AddAsync(record);
                }

                record.number = normalizedNumber;
                record.type = type;
                record.rawDate = rawDate;
                record.callTime = callTime;
                record.durationSeconds = durationSeconds;
                record.recordUrl = recordUrl?.Trim() ?? string.Empty;
                record.simId = simId;
                record.blockType = blockType;
                record.source = string.IsNullOrWhiteSpace(source) ? record.source : source.Trim();
                record.updatedAt = now;

                if (context.Entry(record).State != EntityState.Added)
                {
                    context.Entry(record).State = EntityState.Modified;
                }

                await context.SaveChangesAsync();
                return record;
            });
        }

        /// <summary>
        /// 批量新增或更新通话记录。
        /// </summary>
        public static async Task<List<CallLogRecord>> UpsertCallLogRecords(
            this DbContext context,
            string ownerWxid,
            string imei,
            IEnumerable<CallLogRecord>? records,
            string source = "Pull")
        {
            var saved = new List<CallLogRecord>();
            if (records == null)
            {
                return saved;
            }

            foreach (var item in records)
            {
                var record = await context.UpsertCallLogRecord(
                    ownerWxid,
                    imei,
                    item.callLogId,
                    item.number,
                    item.type,
                    item.rawDate,
                    item.durationSeconds,
                    item.recordUrl,
                    item.simId,
                    item.blockType,
                    source);

                if (record != null)
                {
                    saved.Add(record);
                }
            }

            return saved;
        }

        /// <summary>
        /// 查询通话记录快照。
        /// </summary>
        public static async Task<List<CallLogRecordDto>> GetCallLogRecords(
            this DbContext context,
            string ownerWxid,
            string imei = "",
            int count = 200)
        {
            var normalizedOwner = ownerWxid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedOwner))
            {
                return new List<CallLogRecordDto>();
            }

            var normalizedImei = imei?.Trim() ?? string.Empty;
            var take = Math.Clamp(count, 1, 1000);
            var query = context.Set<CallLogRecord>()
                .AsNoTracking()
                .Where(item => item.ownerWxid == normalizedOwner);

            if (!string.IsNullOrWhiteSpace(normalizedImei))
            {
                query = query.Where(item => item.imei == normalizedImei);
            }

            return await query
                .OrderByDescending(item => item.callTime)
                .ThenByDescending(item => item.id)
                .Take(take)
                .Select(item => new CallLogRecordDto
                {
                    id = item.id,
                    ownerWxid = item.ownerWxid,
                    imei = item.imei,
                    callLogId = item.callLogId,
                    number = item.number,
                    type = item.type,
                    rawDate = item.rawDate,
                    callTime = item.callTime,
                    durationSeconds = item.durationSeconds,
                    hasRecording = item.recordUrl != null && item.recordUrl != string.Empty,
                    recordUrl = item.recordUrl ?? string.Empty,
                    simId = item.simId,
                    blockType = item.blockType,
                    source = item.source,
                    createdAt = item.createdAt,
                    updatedAt = item.updatedAt
                })
                .ToListAsync();
        }

        /// <summary>
        /// 反序列化群邀请成员 JSON；客户端数据不可信，解析失败时返回空列表。
        /// </summary>
        private static List<GroupInvitationMemberDto> DeserializeGroupInvitationMembers(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<GroupInvitationMemberDto>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<GroupInvitationMemberDto>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<GroupInvitationMemberDto>();
            }
            catch
            {
                return new List<GroupInvitationMemberDto>();
            }
        }

        /// <summary>
        /// 统一 DateTime Kind，避免 Npgsql timestamp with time zone 写入本地时间。
        /// </summary>
        private static DateTime EnsureUtc(DateTime value)
        {
            if (value == default)
            {
                return DateTime.UnixEpoch;
            }

            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private static long BuildWechatAccountNumericKey(string ownerWxid)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid))
            {
                return 0;
            }

            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (char ch in ownerWxid)
                {
                    hash ^= ch;
                    hash *= 1099511628211UL;
                }

                return (long)(hash & 0x7FFFFFFFFFFFFFFFUL);
            }
        }

        /// <summary>
        /// 归一化微信标签 ID 列表，去掉无效值并保持升序，便于 Contacts.labelIds 稳定存储。
        /// </summary>
        private static List<int> NormalizeContactLabelIds(IEnumerable<int>? labelIds)
        {
            return (labelIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
        }

        /// <summary>
        /// 兼容微信侧秒/毫秒时间戳；无效值回退到指定时间。
        /// </summary>
        private static DateTime ConvertWechatTimestampToUtc(long timestamp, DateTime fallback)
        {
            try
            {
                if (timestamp > 10_000_000_000L)
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
                }

                if (timestamp > 0)
                {
                    return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                }
            }
            catch
            {
                // 时间戳来自客户端，不让异常影响标签主链路。
            }

            return fallback.Kind == DateTimeKind.Utc ? fallback : DateTime.SpecifyKind(fallback, DateTimeKind.Utc);
        }

        /// <summary>
        /// 生成群发历史幂等指纹。
        /// <para>必须与 Handler 构造详情字典时保持一致。</para>
        /// </summary>
        public static string BuildMassSendHistoryFingerprint(MassMessage message)
        {
            if (message == null)
            {
                return string.Empty;
            }

            return $"{message.wechatAccountId}|{message.sentTime.Ticks}|{message.messageType}|{message.messageContent ?? string.Empty}|{message.messageTitle ?? string.Empty}";
        }

        /// <summary>
        /// 按接收人 upsert 群发详情。
        /// </summary>
        private static async Task UpsertMassSendDetails(
            DbContext context,
            int massMessageId,
            IEnumerable<MassMessageDetail> details,
            DateTime now)
        {
            if (massMessageId <= 0 || details == null)
            {
                return;
            }

            foreach (var incoming in details.Where(detail => detail != null && !string.IsNullOrWhiteSpace(detail.recipientWxid)))
            {
                var recipientWxid = incoming.recipientWxid.Trim();
                var existing = await context.Set<MassMessageDetail>()
                    .FirstOrDefaultAsync(d => d.massMessageId == massMessageId && d.recipientWxid == recipientWxid);

                if (existing == null)
                {
                    incoming.massMessageId = massMessageId;
                    incoming.recipientWxid = recipientWxid;
                    incoming.errorMessage ??= string.Empty;
                    incoming.sentTime = incoming.sentTime == default ? now : incoming.sentTime;
                    incoming.createdAt = incoming.createdAt == default ? now : incoming.createdAt;
                    incoming.updatedAt = now;
                    await context.Set<MassMessageDetail>().AddAsync(incoming);
                }
                else
                {
                    existing.sendStatus = incoming.sendStatus;
                    existing.errorMessage = incoming.errorMessage ?? existing.errorMessage ?? string.Empty;
                    existing.retryCount = incoming.retryCount;
                    if (incoming.sentTime != default)
                    {
                        existing.sentTime = incoming.sentTime;
                    }
                    existing.updatedAt = now;
                    context.Entry(existing).State = EntityState.Modified;
                }
            }
        }

        /// <summary>
        /// 将指定会话标记为已读。
        /// <para>
        /// 消息和会话当前没有进入 GlobalCache，因此这里只做数据库原子更新；
        /// 仍然沿用 DbHelper 的锁粒度，避免同一账号同一会话的读状态与未读数并发写乱序。
        /// </para>
        /// </summary>
        /// <param name="ownerWxid">所属微信账号 wxid</param>
        /// <param name="friendWxid">会话对方 wxid 或群 id</param>
        /// <returns>更新的消息数量</returns>
        public static async Task<int> MarkConversationMessagesRead(this DbContext context, string ownerWxid, string friendWxid)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid) || string.IsNullOrWhiteSpace(friendWxid))
            {
                return 0;
            }

            string lockKey = $"ConversationRead_{ownerWxid}_{friendWxid}";
            return await AsyncLockManager.ExecuteWithLockAsync(lockKey, async () =>
            {
                var now = DateTime.UtcNow;

                var updatedMessages = await context.Set<Message>()
                    .Where(m => m.accountId == ownerWxid
                        && !m.isDeleted
                        && m.readStatus != 1
                        && (m.senderWxid == friendWxid || m.receiverWxid == friendWxid))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.readStatus, (short?)1)
                        .SetProperty(m => m.readAt, now)
                        .SetProperty(m => m.updatedAt, now));

                await context.Set<Conversation>()
                    .Where(c => c.wechatAccountId == ownerWxid
                        && c.conversationWxid == friendWxid
                        && !c.isDeleted
                        && c.unreadCount != 0)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.unreadCount, 0)
                        .SetProperty(c => c.updatedAt, now));

                return updatedMessages;
            });
        }

        // ==================== System Initialization ====================

        /// <summary>
        /// 服务端启动时，重置所有设备和微信账号为离线状态
        /// </summary>
        /// <returns>返回重置的设备数量和微信账号数量元组</returns>
        public static async Task<(int resetClientCount, int resetWechatCount)> ResetAllDevicesToOffline(this DbContext context)
        {
            int clientCount = 0;
            int wechatCount = 0;
            try
            {
                // 1. 批量更新数据库状态为离线
                clientCount = await context.Set<SrClient>()
                    .Where(c => c.isOnline)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.isOnline, false)
                                              .SetProperty(c => c.updatedAt, DateTime.UtcNow));

                wechatCount = await context.Set<WechatAccount>()
                    .Where(a => a.accountStatus != 0) // 将所有非离线状态(0=Offline)重置
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.accountStatus, (short)0) 
                                              .SetProperty(a => a.updatedAt, DateTime.UtcNow));

                // 2. 清理相关全局缓存，强制后续请求重新从数据库加载最新状态
                GlobalCache.srClients.Clear();
                GlobalCache.wechatAccounts.Clear();
                
                return (clientCount, wechatCount);
            }
            catch (Exception ex)
            {
                // 异常抛出交由上层 SeedData 统一处理并记录日志
                throw new Exception($"Failed to reset device and wechat account status on startup: {ex.Message}", ex);
            }
        }
    }
}
