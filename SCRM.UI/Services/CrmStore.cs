using SCRM.API.Models.Entities;
using SCRM.SHARED.Models.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

using SCRM.UI.Services.Data;

namespace SCRM.UI.Services
{
    /// <summary>
    /// SCRM 前端核心数据仓库 (Store)
    /// 负责管理设备列表、联系人、聊天记录等状态，并处理与后端的交互
    /// </summary>
    public class CrmStore
    {
        private readonly WeChatService _weChatService;
        private readonly System.Net.Http.HttpClient _httpClient;
        private readonly Blazored.LocalStorage.ILocalStorageService _localStorage;
        private readonly IClientDbContext _dbContext;
        private readonly ILogger<CrmStore> _logger;


        // --- 状态数据 (State) ---
        
        /// <summary>
        /// 当前所有设备列表
        /// </summary>
        public List<SrClient> Devices { get; private set; } = new();
        
        /// <summary>
        /// 当前选中的设备
        /// </summary>
        public SrClient? SelectedDevice { get; private set; }
        
        /// <summary>
        /// 当前设备的联系人列表
        /// </summary>
        public List<Contact> Contacts { get; private set; } = new();
        
        /// <summary>
        /// 当前选中的联系人（聊天对象）
        /// </summary>
        public Contact? SelectedContact { get; private set; }
        
        // 内存优化：仅保留当前会话的聊天记录
        public List<Message> CurrentMessages { get; private set; } = new();
        
        // 朋友圈动态列表
        public List<MomentsTimeline> CurrentMoments { get; private set; } = new();

        // --- 事件 (Events) ---
        
        /// <summary>
        /// 通知事件 (消息内容, 是否成功)
        /// </summary>
        public event Action<string, bool>? OnNotification; 
        
        /// <summary>
        /// 状态变更事件 (通知 UI 刷新)
        /// </summary>
        public event Action? OnChange;

        public CrmStore(WeChatService weChatService, System.Net.Http.HttpClient httpClient, Blazored.LocalStorage.ILocalStorageService localStorage, IClientDbContext dbContext, ILogger<CrmStore> logger)
        {
            _weChatService = weChatService;
            _httpClient = httpClient;
            _localStorage = localStorage;
            _dbContext = dbContext;
            _logger = logger;
            
            // 订阅 SignalR 事件
            _weChatService.OnMessageReceived += HandleMessageReceived;       // 收到消息
            _weChatService.OnWeChatStatusChanged += HandleWeChatStatusChanged; // 微信状态变更
            _weChatService.OnDeviceStatusChanged += HandleDeviceStatusChanged; // 设备上下线
            _weChatService.OnContactsUpdated += HandleContactsUpdated;       // 联系人列表更新
            _weChatService.OnReconnected += HandleReconnected;               // SignalR 重连
            _weChatService.OnTaskResultReceived += HandleTaskResultReceived; // 任务结果回调
            _weChatService.OnScreenShotReceived += HandleScreenShotReceived; // 收到截屏
            _weChatService.OnMomentReceived += HandleMomentReceived;         // 收到朋友圈
        }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool HasInitialized { get; private set; }

        /// <summary>
        /// 初始化 Store (加载设备列表)
        /// </summary>
        public async Task InitializeAsync()
        {
            // 持久化状态检查：如果已经初始化且有数据，跳过加载
            if (HasInitialized && Devices.Count > 0)
            {
                _logger.LogInformation("[CrmStore] Store 已初始化，跳过重载。");
                return;
            }

            await LoadDevicesAsync();
            HasInitialized = true;
        }

        /// <summary>
        /// 加载设备列表 (通过 HTTP API)
        /// </summary>
        public async Task LoadDevicesAsync()
        {
            try 
            {
                // 确保请求头带上 Auth Token
                var token = await _localStorage.GetItemAsStringAsync("authToken");
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Trim('"'));
                }

                // 从后端 API 拉取设备列表
                var result = await _httpClient.GetFromJsonAsync<List<SrClient>>("api/device");
                if (result != null)
                {
                    Devices = result;
                    _logger.LogInformation("[CrmStore] 通过 HTTP API 加载了 {Count} 个设备。", Devices.Count);
                    
                    // 为所有设备自动加入 SignalR 组，以便接收后台消息（即使未选中）
                    foreach(var d in Devices)
                    {
                        if(!string.IsNullOrEmpty(d.uuid))
                        {
                            _ = _weChatService.JoinGroupAsync(d.uuid);
                        }
                        
                        // 关键修复：将 Account 保存到本地 DB以满足外键约束
                        if (d.Accounts != null)
                        {
                            foreach(var acc in d.Accounts)
                            {
                                await _dbContext.WechatAccounts.SaveAsync(acc, acc.AccountId);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CrmStore] 设备列表加载失败");
                // 异常时重置为空列表
                Devices = new List<SrClient>();
            }

            // 如果当前有选中的设备，更新其引用（保持状态同步）
            if (SelectedDevice != null)
            {
                var updated = Devices.FirstOrDefault(d => d.uuid == SelectedDevice.uuid);
                if (updated != null)
                {
                    SelectedDevice = updated;
                }
            }
            
            NotifyStateChanged();
        }

        /// <summary>
        /// 选中某个设备
        /// </summary>
        /// <param name="device">目标设备</param>
        public async Task SelectDeviceAsync(SrClient device)
        {
            if (SelectedDevice == device) return;

            // 优化：如果在不同设备间切换，需要清理当前上下文
            
            SelectedDevice = device;
            SelectedContact = null;
            CurrentMessages.Clear(); // 清空聊天记录
            LastScreenShotUrl = null;
            
            // 切换设备时，联系人列表必须清空重载
            Contacts.Clear(); 
            NotifyStateChanged();
            
            // 1. 尝试从本地数据库 (IndexedDB) 加载联系人
            try 
            {
                if (device.WechatAccountId.HasValue)
                {
                    var cachedContacts = await _dbContext.Contacts.GetByIndexAsync("wechatAccountId", device.WechatAccountId.Value);
                    if (cachedContacts != null && cachedContacts.Any())
                    {
                        Contacts = cachedContacts;
                        _logger.LogInformation("[CrmStore] 从本地 DB 缓存加载了 {Count} 个联系人 (设备: {Uuid})", Contacts.Count, device.uuid);
                        NotifyStateChanged();
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "[CrmStore] 读取本地 DB 失败");
            }

            // 2. 如果本地没有数据，从服务器获取
            if (SelectedDevice != null)
            {
                // 兜底逻辑：如果设备对象缺少 AccountId，尝试从关联集合中查找
                long? accountId = device.WechatAccountId;
                _logger.LogInformation("[CrmStore] 调试: 设备 UUID={Uuid}, WxId={WeChatId}, AccountId={AccountId}", device.uuid, device.WeChatId, accountId);

                if (!accountId.HasValue && device.Accounts != null && device.Accounts.Any())
                {
                    accountId = device.Accounts.First().AccountId;
                    // 更新设备对象缓存
                    device.WechatAccountId = accountId;
                    _logger.LogInformation("[CrmStore] 调试: 从 Accounts 集合中找到了 AccountId {AccountId}", accountId);
                }

                if (accountId.HasValue && Contacts.Count == 0)
                {
                     _logger.LogInformation("[CrmStore] 调试: 正在从服务器获取联系人 (AccountId {AccountId})...", accountId);
                     var result = await _weChatService.GetContactsAsync(accountId.Value);
                     Contacts = result.ToList();
                     _logger.LogInformation("[CrmStore] 调试: 从服务器成功获取 {Count} 个联系人。", Contacts.Count);
                     NotifyStateChanged();
                     
                     // 异步保存到本地数据库
                     _ = SaveContactsToDbAsync(Contacts);

                     // 3. 自动同步逻辑：如果服务器也没有数据，且设备在线，触发手机端上传
                     if (Contacts.Count == 0 && !string.IsNullOrEmpty(SelectedDevice.uuid))
                     {
                         _logger.LogInformation("[CrmStore] 在线设备 {Uuid} 联系人列表为空，触发自动同步。", SelectedDevice.uuid);
                         _ = SyncContactsAsync();
                     }
                }
                else if (!accountId.HasValue)
                {
                     _logger.LogWarning("[CrmStore] 调试: 该设备未关联 AccountId，无法获取联系人。");
                }
            }
        }
        
        /// <summary>
        /// 将联系人保存到本地数据库
        /// </summary>
        private async Task SaveContactsToDbAsync(List<Contact> contacts)
        {
             foreach(var c in contacts)
             {
                 await _dbContext.Contacts.SaveAsync(c, c.Id); // 使用 ID 作为主键
             }
        }

        /// <summary>
        /// 选中某个联系人
        /// </summary>
        public async Task SelectContactAsync(Contact contact)
        {
            if (SelectedContact == contact) return;

            SelectedContact = contact;
            NotifyStateChanged();

            if (SelectedDevice != null && SelectedDevice.WechatAccountId.HasValue && SelectedContact != null)
            {
                // 清空旧聊天记录
                CurrentMessages.Clear();

                // 1. 尝试从本地数据库加载聊天记录
                var dbMessages = await LoadChatHistoryFromDbAsync(contact.Wxid);
                if (dbMessages.Any())
                {
                    CurrentMessages = dbMessages;
                    _logger.LogInformation("[CrmStore] 从 DB 加载了 {Count} 条消息 (对象: {Wxid})", dbMessages.Count, contact.Wxid);
                    NotifyStateChanged();
                }

                if (CurrentMessages.Count == 0)
                {
                    // 2. 如果本地为空，从服务器拉取历史记录
                    var history = await _weChatService.GetChatHistoryAsync(SelectedDevice.WechatAccountId.Value, contact.Wxid);
                    var sortedList = history.OrderBy(m => m.CreatedAt).ToList();
                    CurrentMessages = sortedList;
                    
                    // 3. 保存拉取到的消息到本地 DB
                    _ = SaveMessagesToDbAsync(sortedList);
                    
                    NotifyStateChanged();
                }
            }
        }
        
        /// <summary>
        /// 从本地 DB 读取聊天记录 (合并发送和接收)
        /// </summary>
        private async Task<List<Message>> LoadChatHistoryFromDbAsync(string friendWxid)
        {
             try 
             {
                 // 查询逻辑：我发的 (Receiver=Friend) + 朋友发的 (Sender=Friend)
                 var incomingTask = _dbContext.Messages.GetByIndexAsync("senderWxid", friendWxid);
                 var outgoingTask = _dbContext.Messages.GetByIndexAsync("receiverWxid", friendWxid);
                 
                 await Task.WhenAll(incomingTask, outgoingTask);
                 
                 var incoming = incomingTask.Result;
                 var outgoing = outgoingTask.Result;
                 
                 var combined = incoming.Concat(outgoing)
                                        .OrderBy(m => m.CreatedAt)
                                        .ToList();
                                        
                 return combined;
             }
             catch(Exception ex)
             {
                 _logger.LogError(ex, "[CrmStore] 读取本地聊天记录出错");
                 return new List<Message>();
             }
        }
        
        
        /// <summary>
        /// 发布朋友圈
        /// </summary>
        public async Task<TaskResult> PostMomentAsync(string content, List<string> imageUrls)
        {
             if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.uuid))
             {
                 _logger.LogInformation("[CrmStore] 正在为设备 {Uuid} 发布朋友圈", SelectedDevice.uuid);
                 return await _weChatService.PostMomentAsync(SelectedDevice.uuid, content, imageUrls);
             }
             return TaskResult.Fail("未选择设备");
        }

        /// <summary>
        /// 同步朋友圈
        /// </summary>
        public async Task<bool> SyncMomentsAsync()
        {
             if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.uuid))
             {
                 return await _weChatService.SyncMomentsAsync(SelectedDevice.uuid);
             }
             return false;
        }

        private async Task SaveMessagesToDbAsync(List<Message> messages)
        {
             foreach(var m in messages)
             {
                 await _dbContext.Messages.SaveAsync(m, m.MessageId);
             }
        }
        

        // --- 事件处理程序 (Event Handlers) ---

        /// <summary>
        /// 处理设备状态变更 (上线/下线)
        /// </summary>
        private void HandleDeviceStatusChanged(string deviceId, string connectionId, bool isOnline)
        {
            _logger.LogInformation("[CrmStore] 设备 {DeviceId} 状态变更: {Status}", deviceId, isOnline ? "在线" : "离线");
            // 刷新设备列表以更新 UI 状态灯
            _ = LoadDevicesAsync();

            // 如果当前选中设备刚上线，触发自动操作
            // 根据 connectionId 匹配可能更准确
            if (isOnline && SelectedDevice != null && (SelectedDevice.uuid == deviceId || SelectedDevice.ConnectionId == connectionId))
            {
                _logger.LogInformation("[CrmStore] 设备上线，自动请求同步联系人 (连接ID: {ConnectionId})", connectionId);
                // 调用 SignalR 通知手机上传通讯录
                _ = _weChatService.SyncContactsAsync(connectionId);
            }
        }

        /// <summary>
        /// 处理联系人列表更新通知
        /// </summary>
        private void HandleContactsUpdated(long accountId)
        {
            Console.WriteLine($"[CrmStore] DEBUG: HandleContactsUpdated triggered for AccountId: {accountId}");
            _logger.LogInformation("[CrmStore] 收到联系人更新通知 (AccountId: {AccountId})", accountId);
            // 找到对应的设备
            var device = Devices.FirstOrDefault(d => d.WechatAccountId == accountId);
            if (device != null)
            {
                // 后台静默重载联系人
                _ = ReloadContactsForDeviceAsync(device);
            }
        }

        /// <summary>
        /// 后台重载指定设备的联系人
        /// </summary>
        private async Task ReloadContactsForDeviceAsync(SrClient device)
        {
             try 
             {
                 if (device.WechatAccountId.HasValue)
                 {
                     var result = await _weChatService.GetContactsAsync(device.WechatAccountId.Value);
                     var list = result.ToList();
                     
                     // 更新本地数据库缓存
                     await SaveContactsToDbAsync(list); 
                     
                     _logger.LogInformation("[CrmStore] 设备 {Uuid} 联系人后台更新完成: {Count} 条数据存入 DB。", device.uuid, list.Count);
                     
                     // 如果当前刚好选中此设备，立即刷新 UI
                     if (SelectedDevice != null && SelectedDevice.uuid == device.uuid)
                     {
                         Contacts = list;
                         NotifyStateChanged();
                     }
                 }
             }
             catch(Exception ex)
             {
                 _logger.LogError(ex, "[CrmStore] 重载联系人失败");
             }
        }


        
        /// <summary>
        /// 手动触发联系人同步
        /// </summary>
        public async Task SyncContactsAsync()
        {
            if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.uuid))
            {
                await _weChatService.SyncContactsAsync(SelectedDevice.uuid);
            }
        }

        /// <summary>
        /// 手动触发群聊通过
        /// </summary>
        public async Task SyncChatRoomsAsync()
        {
            if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.uuid))
            {
                await _weChatService.SyncChatRoomsAsync(SelectedDevice.uuid);
            }
        }



        /// <summary>
        /// 执行群聊操作 (如拉人、踢人)
        /// </summary>
        public async Task ExecuteGroupActionAsync(string chatRoomId, int action, string content, int intValue = 0)
        {
             if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.uuid))
             {
                 await _weChatService.ExecuteGroupActionAsync(SelectedDevice.uuid, chatRoomId, action, content, intValue);
             }
        }

        /// <summary>
        /// 同意加入群聊
        /// </summary>
        public async Task AgreeJoinGroupAsync(string talker, long msgSvrId, string content)
        {
             if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.uuid))
             {
                 await _weChatService.AgreeJoinGroupAsync(SelectedDevice.uuid, talker, msgSvrId, content);
             }
        }

        /// <summary>
        /// 删除当前选中的联系人
        /// </summary>
        public async Task DeleteCurrentContactAsync()
        {
             if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.uuid) && SelectedContact != null)
             {
                 await _weChatService.DeleteFriendAsync(SelectedDevice.uuid, SelectedContact.Wxid);
             }
        }

        /// <summary>
        /// 请求手机截屏
        /// </summary>
        public async Task RequestScreenShotAsync(string uuid)
        {
             // 必须使用实时的 ConnectionId -> NO! Hub expects UUID
             if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.uuid))
             {
                 await _weChatService.RequestScreenShotAsync(SelectedDevice.uuid);
             }
        }

        public async Task RequestScreenShotAsync()
        {
            if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.uuid))
            {
                 await _weChatService.RequestScreenShotAsync(SelectedDevice.uuid); 
             }
        }

        /// <summary>
        /// 发送文字消息 (乐观更新 + 本地优先)
        /// </summary>
        public async Task<bool> SendMessageAsync(string content)
        {
             if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.uuid) && SelectedContact != null)
             {
                 // 生成本地 TaskId 用于消息追踪
                 var taskId = DateTime.UtcNow.Ticks;
                 var accountId = SelectedDevice.WechatAccountId ?? 0;

                 // 1. 确保 WechatAccount存在 (防止 FK 错误)
                 await EnsureAccountAsync(SelectedDevice);
                 
                 // 2. 确保会话存在 (防止 FK 错误)
                 var conversation = await EnsureConversationAsync(accountId, SelectedContact.Wxid, SelectedContact.Nickname, SelectedContact.Avatar);

                 // 2. 创建本地消息 (Status = 发送中)
                 var msg = new Message
                 {
                     MessageId = taskId, // 临时 ID
                     SenderWxid = SelectedDevice.WechatNumber, 
                     ReceiverWxid = SelectedContact.Wxid,
                     Content = content,
                     CreatedAt = DateTime.UtcNow,
                     Direction = 1, // 发送方
                     AccountId = accountId,
                     ConversationId = conversation?.Id ?? 0,
                     SendStatus = 1, // 1=发送中
                     ClientMsgId = taskId.ToString()
                 };
                 
                 // 3. 立即更新 UI 和 本地存储
                 CurrentMessages.Add(msg);
                 NotifyStateChanged();
                 _ = _dbContext.Messages.SaveAsync(msg, msg.MessageId); // 异步保存
                 
                 try 
                 {
                     // 4. 发送网络请求
                     var result = await _weChatService.SendMessageAsync(SelectedDevice.uuid, SelectedContact.Wxid, content);
                     
                     if (result.Success)
                     {
                         // 5. 发送成功 -> 更新状态
                         msg.SendStatus = 2; // 2=已发送(等待服务器确认)
                         // 注意：MsgSvrId 此时还未知，等待 HandleTaskResult 或 MessageReceived 更新
                     }
                     else
                     {
                         // 6. 发送失败 -> 标记失败
                         msg.SendStatus = 0; // 0=失败
                         _logger.LogWarning("消息发送失败: {Msg}", result.Message);
                     }
                 }
                 catch(Exception ex)
                 {
                     _logger.LogError(ex, "发送消息异常");
                     msg.SendStatus = 0; // 失败
                 }
                 
                 // 更新本地 DB 状态
                 _ = _dbContext.Messages.SaveAsync(msg, msg.MessageId);
                 NotifyStateChanged();
                 
                 return msg.SendStatus != 0;
             }
             return false;
        }

        /// <summary>
        /// 确保账号存在 (本地 DB)
        /// </summary>
        private async Task EnsureAccountAsync(SrClient device)
        {
            try
            {
                if (device == null || !device.WechatAccountId.HasValue) return;
                var accountId = device.WechatAccountId.Value;

                var existing = await _dbContext.WechatAccounts.GetAsync(accountId);
                if (existing != null) return;
                
                // 尝试从 Device object 找 Account
                var account = device.Accounts?.FirstOrDefault(a => a.AccountId == accountId);
                if (account != null)
                {
                    await _dbContext.WechatAccounts.SaveAsync(account, accountId);
                    return;
                }
                
                // 如果找不到，创建一个最小化的占位符? 
                // 防止 FK 崩溃，但这可能会导致脏数据。
                // 既然 Device 存在，应该能找到 Account。
                _logger.LogWarning("EnsureAccountAsync: 无法在设备对象中找到 AccountId {Id}, 尝试创建一个临时记录...", accountId);
                
                var temp = new WechatAccount
                {
                    AccountId = accountId,
                    Wxid = device.WeChatId ?? "unknown",
                    Nickname = device.WeChatNick ?? "Unknown Device",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };
                await _dbContext.WechatAccounts.SaveAsync(temp, accountId);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "EnsureAccountAsync 失败");
            }
        }

        /// <summary>
        /// 确保会话存在 (本地 DB)
        /// </summary>
        private async Task<Conversation?> EnsureConversationAsync(long accountId, string talkerWxid, string talkerName, string? avatar)
        {
            try
            {
                // 简单查找：根据 AccountId + Talker
                // 由于 ClientDbContext 可能是简单的 KV 或 SQLite，这里假设它支持 Linq 或 GetByIndex
                // 如果不支持复杂查询，可能需要 Scan。这里假设 Messages 和 Contacts 存储方式差不多。
                // 暂时使用约定：Conversation 主键可能是 accountId_talkerWxid 组合？
                // 或者我们查询所有.
                
                // 优化：假设 Converseation 表很小，或者有索引。
                var all = await _dbContext.Conversations.GetAllAsync(); 
                var existing = all.FirstOrDefault(c => c.WechatAccountId == accountId && c.ConversationWxid == talkerWxid); 
                
                if (existing != null) return existing;

                // 创建新会话
                var newConv = new Conversation
                {
                    WechatAccountId = accountId,
                    ConversationWxid = talkerWxid, // 用 Wxid 作为唯一标识
                    DisplayName = talkerName,
                    DisplayAvatar = avatar,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };
                
                // 由于 SQLite 自增 ID，我们需要保存后获取 ID吗？
                // 如果 ID 是 int/long，通常 SaveAsync 会回填? 
                // 这里假设 SaveAsync 是类似于 AddOrUpdate. 
                // 如果 ID 是 0，应该会生成。
                
                await _dbContext.Conversations.SaveAsync(newConv, 0); // 0 or specific Key? 
                
                // 重新获取以拿到 ID (如果 SaveAsync 不回填)
                // 实际上 Blazor DB wrapper 不同实现行为不同。
                // 假设它能工作。
                
                // 为了保险，再次查询
                var verified = (await _dbContext.Conversations.GetAllAsync())
                                .FirstOrDefault(c => c.WechatAccountId == accountId && c.ConversationWxid == talkerWxid);
                return verified;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "EnsureConversationAsync 失败");
                return null;
            }
        }

        /// <summary>
        /// 删除好友
        /// </summary>
        public async Task DeleteFriendAsync(string wxid)
        {
             if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.uuid))
             {
                 await _weChatService.DeleteFriendAsync(SelectedDevice.uuid, wxid);
             }
        }
        
        /// <summary>
        /// 同意好友请求（自动通过）
        /// </summary>
        /// <param name="friendId">请求者ID</param>
        /// <param name="friendNick">请求者昵称</param>
        public async Task AgreeFriendRequestAsync(string friendId, string friendNick)
        {
             if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.uuid))
             {
                 await _weChatService.AcceptFriendRequestAsync(SelectedDevice.uuid, friendId, friendNick);
             }
        }

        /// <summary>
        /// 处理收到的新消息
        /// </summary>
        private async void HandleMessageReceived(ReceiveMessageDto msgDto)
        {
            // --- 消息去重与合并逻辑 (Deduplication) ---
            
            // 1. 检查内存中是否已存在该消息 (通过 MsgSvrId 或 本地 TaskId)
            var existing = CurrentMessages.FirstOrDefault(m => 
                (msgDto.MsgSvrId > 0 && m.MsgSvrId == msgDto.MsgSvrId) || 
                (msgDto.TaskId > 0 && m.ClientMsgId == msgDto.TaskId.ToString()));

            if (existing != null)
            {
                // 如果是之前自己发的“自信更新”消息，现在更新为服务器确认状态
                existing.MsgSvrId = msgDto.MsgSvrId;
                existing.SendStatus = 3; // 发送成功/已同步
                existing.UpdatedAt = DateTime.UtcNow;
                NotifyStateChanged();
                await _dbContext.Messages.SaveAsync(existing, existing.MessageId);
                return;
            }
            
            var accountId = SelectedDevice?.WechatAccountId ?? 0;
            var friendId = msgDto.IsSelf ? msgDto.FriendId : msgDto.FriendId; // 始终是对方

            // 2. 确保会话存在
            var conversation = await EnsureConversationAsync(accountId, friendId, "", "");

            // 3. 构造新消息实体
            var msg = new Message
            {
                MessageId = DateTime.UtcNow.Ticks, 
                SenderWxid = msgDto.IsSelf ? "self" : msgDto.FriendId,
                ReceiverWxid = msgDto.IsSelf ? msgDto.FriendId : "self",
                Content = msgDto.Content,
                CreatedAt = DateTime.UtcNow,
                Direction = (short)(msgDto.IsSelf ? 1 : 2),
                AccountId = accountId,
                ConversationId = conversation?.Id ?? 0,
                MsgSvrId = msgDto.MsgSvrId,
                ChatType = (short)(msgDto.IsGroup ? 2 : 1),
                SendStatus = 3
            };
            
            // 将 "self" 替换为真实的微信号
            var currentAccount = SelectedDevice?.Accounts?.FirstOrDefault(a => a.AccountId == SelectedDevice.WechatAccountId);
            if (msg.SenderWxid == "self" && currentAccount?.Wxid != null) 
                msg.SenderWxid = currentAccount.Wxid;
            if (msg.ReceiverWxid == "self" && currentAccount?.Wxid != null)
                msg.ReceiverWxid = currentAccount.Wxid;

            // 仅当消息属于当前打开的对话框时，才添加到内存列表
            if (SelectedContact != null && (msgDto.FriendId == SelectedContact.Wxid || (msgDto.IsSelf && msgDto.FriendId == SelectedContact.Wxid)))
            {
                 CurrentMessages.Add(msg);
                 NotifyStateChanged();
            }
            else if (msgDto.IsGroup && SelectedContact != null && msgDto.FriendId == SelectedContact.Wxid)
            {
                 // 群消息匹配逻辑
                 CurrentMessages.Add(msg);
                 NotifyStateChanged();
            }
            
            // 总是保存到本地数据库
            await _dbContext.Messages.SaveAsync(msg, msg.MessageId);
        }

        private void HandleWeChatStatusChanged(string wxid, string nick, bool isOnline)
        {
            // 重新加载设备列表以更新在线状态
            _ = LoadDevicesAsync();
        }

        /// <summary>
        /// 处理 SignalR 重连
        /// </summary>
        private void HandleReconnected(string? connectionId)
        {
            _logger.LogInformation("[CrmStore] SignalR 已重连！正在恢复所有设备组订阅...");
            // 为所有设备重新加入组
            foreach(var d in Devices)
            {
                if(!string.IsNullOrEmpty(d.uuid))
                {
                    _ = _weChatService.JoinGroupAsync(d.uuid);
                }
            }
        }


        private void NotifyStateChanged() => OnChange?.Invoke();

        public string? LastScreenShotUrl { get; private set; }

        private void HandleTaskResultReceived(TaskResultDto result)
        {
            string msg = result.Message;
            if (string.IsNullOrEmpty(msg))
            {
                msg = result.Success ? "任务执行成功" : "任务执行失败";
            }
            OnNotification?.Invoke(msg, result.Success);
            NotifyStateChanged();
        }

        private void HandleScreenShotReceived(string url)
        {
            LastScreenShotUrl = url;
            NotifyStateChanged();
        }
        
        public async Task RequestMomentsSyncAsync()
        {
            if (SelectedDevice != null)
            {
                await _weChatService.SyncMomentsAsync(SelectedDevice.uuid);
            }
        }

        /// <summary>
        /// 加载朋友圈数据
        /// </summary>
        public async Task LoadMomentsAsync()
        {
            if (SelectedDevice == null) 
            {
                CurrentMoments.Clear();
                NotifyStateChanged();
                return;
            }

            // 从本地 DB 加载
            var list = await _dbContext.MomentsTimelines.GetAllAsync();
            
            // 按时间倒序，取前 20 条
            CurrentMoments = list.OrderByDescending(x => x.CreateTime).Take(20).ToList();
            NotifyStateChanged();
            
            // 可选：加载时自动触发同步
            // await RequestMomentsSyncAsync(); 
        }

        /// <summary>
        /// 处理收到的朋友圈动态
        /// </summary>
        private void HandleMomentReceived(SCRM.SHARED.Models.Dtos.MomentsTimelineDto dto)
        {
            // 转换为实体对象
            var entity = new MomentsTimeline
            {
                SnsId = dto.SnsId,
                UserName = dto.UserName,
                NickName = dto.NickName,
                Content = dto.Content,
                CreateTime = dto.CreateTime,
                ImagesJson = System.Text.Json.JsonSerializer.Serialize(dto.Images),
                CommentsJson = System.Text.Json.JsonSerializer.Serialize(dto.Comments),
                LikesJson = System.Text.Json.JsonSerializer.Serialize(dto.Likes),
                ReceivedAt = DateTime.UtcNow.Ticks,
                OwnerWxid = SelectedDevice?.WeChatId 
            };

            // 保存到本地 DB
            _dbContext.MomentsTimelines.SaveAsync(entity, entity.SnsId);

            // 更新 UI 列表 (去重)
            var existing = CurrentMoments.FirstOrDefault(x => x.SnsId == entity.SnsId);
            if (existing == null)
            {
                CurrentMoments.Insert(0, entity);
                // 保持列表长度限制
                if (CurrentMoments.Count > 50) CurrentMoments.RemoveAt(CurrentMoments.Count - 1);
            }
            
            NotifyStateChanged();
        }

        /// <summary>
        /// 清理历史记录
        /// </summary>
        /// <param name="days">保留最近几天的数据 (默认30天)</param>
        public async Task ClearHistoryAsync(int days = 30)
        {
             try
             {
                 var threshold = DateTime.UtcNow.AddDays(-days);
                 _logger.LogInformation("[CrmStore] 开始清理 {Days} 天前的历史记录 (早于 {Date})...", days, threshold);
                 
                 // 获取所有本地消息
                 var allMsgs = await _dbContext.Messages.GetAllAsync();
                 var toDelete = allMsgs.Where(m => m.CreatedAt < threshold).ToList();
                 
                 foreach(var m in toDelete)
                 {
                     await _dbContext.Messages.DeleteAsync(m.MessageId);
                 }
                 
                 // 如果当前视图有被删除的消息，刷新UI
                 if (toDelete.Any())
                 {
                     CurrentMessages.RemoveAll(m => m.CreatedAt < threshold);
                     NotifyStateChanged();
                 }
                 
                 _logger.LogInformation("[CrmStore] 清理完成，删除了 {Count} 条消息。", toDelete.Count);
             }
             catch(Exception ex)
             {
                 _logger.LogError(ex, "[CrmStore] 清理历史记录失败");
             }
        }

        /// <summary>
        /// 销毁 Store，取消事件订阅
        /// </summary>
        public void Dispose()
        {
            _weChatService.OnMessageReceived -= HandleMessageReceived;
            _weChatService.OnWeChatStatusChanged -= HandleWeChatStatusChanged;
            _weChatService.OnDeviceStatusChanged -= HandleDeviceStatusChanged;
            _weChatService.OnContactsUpdated -= HandleContactsUpdated;
            _weChatService.OnTaskResultReceived -= HandleTaskResultReceived;
            _weChatService.OnScreenShotReceived -= HandleScreenShotReceived;
            _weChatService.OnMomentReceived -= HandleMomentReceived;
        }
    }
}
