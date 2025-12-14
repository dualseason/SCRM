using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http.Json;

using SCRM.UI.Services.Data;

namespace SCRM.UI.Services
{
    public class CrmStore
    {
        private readonly WeChatService _weChatService;
        private readonly System.Net.Http.HttpClient _httpClient;
        private readonly Blazored.LocalStorage.ILocalStorageService _localStorage;
        private readonly IClientDbContext _dbContext;


        // State
        public List<SrClient> Devices { get; private set; } = new();
        public SrClient? SelectedDevice { get; private set; }
        
        public List<Contact> Contacts { get; private set; } = new();
        public Contact? SelectedContact { get; private set; }
        
        // Memory Optimization: Only keep current chat history
        public List<Message> CurrentMessages { get; private set; } = new();

        // Events
        public event Action? OnChange;

        public CrmStore(WeChatService weChatService, System.Net.Http.HttpClient httpClient, Blazored.LocalStorage.ILocalStorageService localStorage, IClientDbContext dbContext)
        {
            _weChatService = weChatService;
            _httpClient = httpClient;
            _localStorage = localStorage;
            _dbContext = dbContext;
            
            // Subscribe to SignalR events
            _weChatService.OnMessageReceived += HandleMessageReceived;
            _weChatService.OnWeChatStatusChanged += HandleWeChatStatusChanged;
            _weChatService.OnDeviceStatusChanged += HandleDeviceStatusChanged;
            _weChatService.OnContactsUpdated += HandleContactsUpdated;
            _weChatService.OnReconnected += HandleReconnected;
        }

        public async Task InitializeAsync()
        {
            // Initial load
            await LoadDevicesAsync();
        }

        public async Task LoadDevicesAsync()
        {
            // Use HTTP API for stable data loading
            try 
            {
                // Ensure auth token is attached
                var token = await _localStorage.GetItemAsStringAsync("authToken");
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Trim('"'));
                }

                var result = await _httpClient.GetFromJsonAsync<List<SrClient>>("api/device");
                if (result != null)
                {
                    Devices = result;
                    Console.WriteLine($"[CrmStore] Loaded {Devices.Count} devices via HTTP API.");
                    
                    // Auto-join groups for all devices to receive background events
                    foreach(var d in Devices)
                    {
                        if(!string.IsNullOrEmpty(d.uuid))
                        {
                            _ = _weChatService.JoinGroupAsync(d.uuid);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CrmStore] API Load Failed: {ex.Message}");
                // Fallback or empty
                Devices = new List<SrClient>();
            }

            // If we have a selected device, update its reference
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

        public async Task SelectDeviceAsync(SrClient device)
        {
            if (SelectedDevice == device) return;

            // Note: We no longer Leave/Join groups here because we join ALL groups in LoadDevicesAsync.
            // This allows us to receive "ContactsUpdated" even for background devices.

            SelectedDevice = device;
            SelectedContact = null;
            CurrentMessages.Clear(); // Clear history
            Contacts.Clear(); // Clear first
            NotifyStateChanged();
            
            // 1. Try Load from DB Cache (ClientDbContext)
            try 
            {
                if (device.WechatAccountId.HasValue)
                {
                    // Assuming "wechatAccountId" matches the index name in Schema
                    var cachedContacts = await _dbContext.Contacts.GetByIndexAsync("wechatAccountId", device.WechatAccountId.Value);
                    if (cachedContacts != null && cachedContacts.Any())
                    {
                        Contacts = cachedContacts;
                        Console.WriteLine($"[CrmStore] Loaded {Contacts.Count} contacts from DB Cache for {device.uuid}");
                        NotifyStateChanged();
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[CrmStore] Error loading from DB: {ex.Message}");
            }

            // 2. Fetch from Server if DB missed or empty
            if (SelectedDevice != null && SelectedDevice.WechatAccountId.HasValue)
            {
                if (Contacts.Count == 0)
                {
                     var result = await _weChatService.GetContactsAsync(SelectedDevice.WechatAccountId.Value);
                     Contacts = result.ToList();
                     NotifyStateChanged();
                     
                     // Save to DB
                     _ = SaveContactsToDbAsync(Contacts);

                     // If contact list is still empty and device is online, trigger sync automatically
                     if (Contacts.Count == 0 && !string.IsNullOrEmpty(SelectedDevice.ConnectionId))
                     {
                         Console.WriteLine($"[CrmStore] Contact list empty for Online Device {SelectedDevice.uuid}. Triggering Auto-Sync.");
                         _ = SyncContactsAsync();
                     }
                }
            }
        }
        
        private async Task SaveContactsToDbAsync(List<Contact> contacts)
        {
             foreach(var c in contacts)
             {
                 await _dbContext.Contacts.SaveAsync(c, c.Id); // Using ID as key
             }
        }

        public async Task SelectContactAsync(Contact contact)
        {
            if (SelectedContact == contact) return;

            SelectedContact = contact;
            NotifyStateChanged();

            if (SelectedDevice != null && SelectedDevice.WechatAccountId.HasValue && SelectedContact != null)
            {
                // Clear previous chat
                CurrentMessages.Clear();

                // 1. Try Load from DB
                var dbMessages = await LoadChatHistoryFromDbAsync(contact.Wxid);
                if (dbMessages.Any())
                {
                    CurrentMessages = dbMessages;
                    Console.WriteLine($"[CrmStore] Loaded {dbMessages.Count} messages from DB for {contact.Wxid}");
                    NotifyStateChanged();
                }

                if (CurrentMessages.Count == 0)
                {
                    // 2. Fetch from Server
                    var history = await _weChatService.GetChatHistoryAsync(SelectedDevice.WechatAccountId.Value, contact.Wxid);
                    var sortedList = history.OrderBy(m => m.CreatedAt).ToList();
                    CurrentMessages = sortedList;
                    
                    // 3. Save to DB
                    _ = SaveMessagesToDbAsync(sortedList);
                    
                    NotifyStateChanged();
                }
            }
        }
        
        private async Task<List<Message>> LoadChatHistoryFromDbAsync(string friendWxid)
        {
             try 
             {
                 // Method: Query Incoming (Sender=Friend) and Outgoing (Receiver=Friend)
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
                 Console.WriteLine($"[CrmStore] Error loading messages from DB: {ex.Message}");
                 return new List<Message>();
             }
        }
        
        private async Task SaveMessagesToDbAsync(List<Message> messages)
        {
             foreach(var m in messages)
             {
                 await _dbContext.Messages.SaveAsync(m, m.MessageId);
             }
        }
        
        // ... (methods omitted) ...

        private void HandleDeviceStatusChanged(string deviceId, string connectionId, bool isOnline)
        {
            Console.WriteLine($"[CrmStore] Device {deviceId} status changed: {(isOnline ? "Online" : "Offline")}");
            // 1. 刷新界面状态 (变绿灯)
            _ = LoadDevicesAsync();

            // If device just came online and it's the selected one, maybe refresh logic?
            //todo   SelectedDevice.ConnectionId == connectionId判断可能要移除
            if (isOnline && SelectedDevice != null && (SelectedDevice.uuid == deviceId || SelectedDevice.ConnectionId == connectionId))
            {
                // Auto-refresh contacts if needed?
                // For now, LoadDevicesAsync updates the connectionId which is enough to enable buttons.

                // 注意：这里需要传入新的 ConnectionId
                // 因为 LoadDevicesAsync 是异步的，可能还没跑完，所以直接用参数里的 connectionId 最稳妥
                Console.WriteLine($"[CrmStore] Device Online, auto-syncing contacts for {connectionId}");
                // 调用底层的 SignalR 方法通知手机上传通讯录
                _ = _weChatService.SyncContactsAsync(connectionId);
            }
        }

        private void HandleContactsUpdated(long accountId)
        {
            // Find which device this belongs to
            var device = Devices.FirstOrDefault(d => d.WechatAccountId == accountId);
            if (device != null)
            {
                // Reload for background cache
                _ = ReloadContactsForDeviceAsync(device);
            }
        }

        private async Task ReloadContactsForDeviceAsync(SrClient device)
        {
             try 
             {
                 if (device.WechatAccountId.HasValue)
                 {
                     var result = await _weChatService.GetContactsAsync(device.WechatAccountId.Value);
                     var list = result.ToList();
                     
                     // Update DB Cache
                     await SaveContactsToDbAsync(list); // Use helper
                     
                     Console.WriteLine($"[CrmStore] Background contact update for {device.uuid}: {list.Count} items saved to DB.");
                     
                     // If this device is currently selected, update the UI list
                     if (SelectedDevice != null && SelectedDevice.uuid == device.uuid)
                     {
                         Contacts = list;
                         NotifyStateChanged();
                     }
                 }
             }
             catch(Exception ex)
             {
                 Console.WriteLine($"[CrmStore] Error reloading contacts: {ex.Message}");
             }
        }

        public async Task SendMessageAsync(string content)
        {
            if (SelectedDevice == null || SelectedContact == null || string.IsNullOrWhiteSpace(content)) return;

            if (string.IsNullOrEmpty(SelectedDevice.ConnectionId))
            {
                // Warn: Device offline
                return;
            }

            var result = await _weChatService.SendMessageAsync(SelectedDevice.ConnectionId, SelectedContact.Wxid, content);
            if (result.Success)
            {
                // Optimistic UI update
                var msg = new Message
                {
                    MessageId = DateTime.UtcNow.Ticks, // Temp ID for local storage
                    SenderWxid = "self", // or known self wxid
                    ReceiverWxid = SelectedContact.Wxid,
                    Content = content,
                    CreatedAt = DateTime.UtcNow, // Use UtcNow for consistency
                    Direction = 1, // Sent
                    AccountId = SelectedDevice.WechatAccountId ?? 0
                };
                
                // If it's "self", we need to fix Wxid for DB indexing
            var currentAccount = SelectedDevice?.Accounts?.FirstOrDefault(a => a.AccountId == SelectedDevice.WechatAccountId);
            if (msg.SenderWxid == "self" && currentAccount?.Wxid != null) 
                msg.SenderWxid = currentAccount.Wxid;
            if (msg.ReceiverWxid == "self" && currentAccount?.Wxid != null)
                msg.ReceiverWxid = currentAccount.Wxid;

                // AddMessageToHistory(SelectedContact.Wxid, msg); -> Removed
                CurrentMessages.Add(msg);
                NotifyStateChanged();
                
                // Persist single message
                _ = _dbContext.Messages.SaveAsync(msg, msg.MessageId);
            }
            else
            {
                Console.WriteLine($"[CrmStore] SendMessage failed. Device: {SelectedDevice.WeChatNick}, Contact: {SelectedContact.Nickname}");
                // Optional: Notify user via UI service if available, or just log for now.
            }
        }
        
        /// <summary>
        /// Manually trigger friend sync
        /// </summary>
        public async Task SyncContactsAsync()
        {
            if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.ConnectionId))
            {
                await _weChatService.SyncContactsAsync(SelectedDevice.ConnectionId);
            }
        }

        public async Task SyncChatRoomsAsync()
        {
            if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.ConnectionId))
            {
                await _weChatService.SyncChatRoomsAsync(SelectedDevice.ConnectionId);
            }
        }

        public async Task SyncMomentsAsync()
        {
            if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.ConnectionId))
            {
                await _weChatService.SyncMomentsAsync(SelectedDevice.ConnectionId);
            }
        }

        /// <summary>
        /// 执行当前选中设备的群聊操作
        /// </summary>
        public async Task ExecuteGroupActionAsync(string chatRoomId, int action, string content, int intValue = 0)
        {
             if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.ConnectionId))
             {
                 await _weChatService.ExecuteGroupActionAsync(SelectedDevice.ConnectionId, chatRoomId, action, content, intValue);
             }
        }

        public async Task AgreeJoinGroupAsync(string talker, long msgSvrId, string content)
        {
             if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.ConnectionId))
             {
                 await _weChatService.AgreeJoinGroupAsync(SelectedDevice.ConnectionId, talker, msgSvrId, content);
             }
        }

        /// <summary>
        /// 删除当前选中的联系人
        /// </summary>
        public async Task DeleteCurrentContactAsync()
        {
             if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.ConnectionId) && SelectedContact != null)
             {
                 await _weChatService.DeleteFriendAsync(SelectedDevice.ConnectionId, SelectedContact.Wxid);
             }
        }

        /// <summary>
        /// 同意好友请求（自动通过）
        /// </summary>
        /// <param name="friendId">请求者ID</param>
        /// <param name="friendNick">请求者昵称</param>
        public async Task AgreeFriendRequestAsync(string friendId, string friendNick)
        {
             if (SelectedDevice != null && !string.IsNullOrEmpty(SelectedDevice.ConnectionId))
             {
                 await _weChatService.AcceptFriendRequestAsync(SelectedDevice.ConnectionId, friendId, friendNick);
             }
        }

        private void HandleMessageReceived(ReceiveMessageDto msgDto)
        {
            // Update history if exists
            var msg = new Message
            {
                MessageId = DateTime.UtcNow.Ticks, // Temp ID if not provided, or 0? Ideally convert generic ID
                // Note: Server usually provides MessageId. ReceiveMessageDto might not have it strictly mapped? 
                // Using Ticks as generic ID for local only if missing unique ID.
                // But safer to wait for sync or use UUID.
                // For "SaveAsync" key, we need an ID.
                SenderWxid = msgDto.IsSelf ? "self" : msgDto.FriendId,
                ReceiverWxid = msgDto.IsSelf ? msgDto.FriendId : "self",
                Content = msgDto.Content,
                CreatedAt = DateTime.UtcNow,
                Direction = (short)(msgDto.IsSelf ? 1 : 2), // 1=Sent, 2=Receive
                AccountId = SelectedDevice?.WechatAccountId ?? 0 
            };
            
            // If it's "self", we need to fix Wxid for DB indexing
            var currentAccount = SelectedDevice?.Accounts?.FirstOrDefault(a => a.AccountId == SelectedDevice.WechatAccountId);
            if (msg.SenderWxid == "self" && currentAccount?.Wxid != null) 
                msg.SenderWxid = currentAccount.Wxid;
            if (msg.ReceiverWxid == "self" && currentAccount?.Wxid != null)
                msg.ReceiverWxid = currentAccount.Wxid;

            // Only update memory if this message belongs to the currently selected contact
            if (SelectedContact != null && (msgDto.FriendId == SelectedContact.Wxid || msgDto.IsSelf))
            {
                 // Re-check logic: If I send to FriendA, friendId=FriendA.
                 // If FriendA sends to me, friendId=FriendA.
                 // So friendId matching SelectedContact.Wxid is correct.
                 if (msgDto.FriendId == SelectedContact.Wxid)
                 {
                     CurrentMessages.Add(msg);
                     NotifyStateChanged();
                 }
            }
            
            // Persist single message
            _ = _dbContext.Messages.SaveAsync(msg, msg.MessageId);
        }

        private void HandleWeChatStatusChanged(string wxid, string nick, bool isOnline)
        {
            // Reload devices to update status
            _ = LoadDevicesAsync();
        }





        private void HandleReconnected(string? connectionId)
        {
            Console.WriteLine($"[CrmStore] SignalR Reconnected! Restoring all group subscriptions...");
            // Re-join groups for ALL devices, not just the selected one
            foreach(var d in Devices)
            {
                if(!string.IsNullOrEmpty(d.uuid))
                {
                    _ = _weChatService.JoinGroupAsync(d.uuid);
                }
            }
        }


        private void NotifyStateChanged() => OnChange?.Invoke();

        public void Dispose()
        {
            _weChatService.OnMessageReceived -= HandleMessageReceived;
            _weChatService.OnWeChatStatusChanged -= HandleWeChatStatusChanged;
            _weChatService.OnDeviceStatusChanged -= HandleDeviceStatusChanged;
            _weChatService.OnContactsUpdated -= HandleContactsUpdated;
        }
    }
}
