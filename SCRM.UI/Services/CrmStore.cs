using Microsoft.Extensions.Logging;
using SCRM.API.Models.Entities;
using SCRM.Shared.Interfaces;
using SCRM.SHARED.Models;
using SCRM.SHARED.Models.Dtos;
using SCRM.UI.Services.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SCRM.UI.Services
{
    /// <summary>
    /// SCRM 前端核心数据仓库 (Store) - Architecture v5 Refactor
    /// </summary>
    public class CrmStore : IDisposable
    {
        // --- Dependencies ---
        private readonly ICrmService _service;
        private readonly ICrmEvents _events;

        private readonly ILogger<CrmStore> _logger;

        // --- Subscriptions ---
        private readonly List<IDisposable> _subscriptions = new();

        // --- State ---
        public List<SrClient> Devices { get; private set; } = new();
        public SrClient? SelectedDevice { get; private set; }

        public List<Contact> Contacts { get; private set; } = new();
        public Contact? SelectedContact { get; private set; }
        public List<Conversation> Conversations { get; private set; } = new();
        public Conversation? SelectedConversation { get; private set; }
        public List<Message> CurrentMessages { get; private set; } = new();
        
        // Active IM Tab Context
        public string ActiveImTab { get; set; } = "chats";

        // Additional Stubs
        public bool HasInitialized { get; set; } = true;
        public List<MomentsTimeline> CurrentMoments { get; private set; } = new();
        public string? LastScreenShotUrl { get; set; }

        // Contacts reload concurrency control
        private readonly SemaphoreSlim _contactsReloadLock = new(1, 1);
        private CancellationTokenSource? _contactsReloadCts;
        private readonly object _contactsReloadSync = new();
        private readonly TimeSpan _contactsDebounceWindow = TimeSpan.FromMilliseconds(300);
        private volatile bool _isDisposed;

        // --- Events ---
        public event Action? OnChange;
        public event Action<Message>? MessageReceived; 
        public event Action<string, bool>? OnNotification;

        public CrmStore(
            ICrmService service, 
            ICrmEvents events, 
 
            ILogger<CrmStore> logger)
        {
            _service = service;
            _events = events;
            _logger = logger;

            InitializeSubscriptions();
        }

        private void InitializeSubscriptions()
        {
            // Subscribe to Device Status Changes
            _subscriptions.Add(_events.SubscribeToDeviceStatus((deviceId, isOnline) => 
            {
                _logger.LogInformation($"[CrmStore] Device Status Changed: {deviceId} -> {isOnline}");
                _ = LoadDevicesAsync(); 
            }));

            // Subscribe to Contacts Received using Wx object context
            _subscriptions.Add(_events.SubscribeToContactsReceived((accountId) =>
            {
                _logger.LogInformation("[CrmStore] Received Contacts Update for Account {AccountId}", accountId);
                QueueContactsReload(accountId);
            }));

            // [New] Subscribe to Screenshot Uploaded Event
            _subscriptions.Add(_events.SubscribeToEvent<string>("OnScreenShotUploaded", (url) =>
            {
                _logger.LogInformation($"[CrmStore] Received Screenshot: {url}");
                LastScreenShotUrl = url;
                NotifyStateChanged();
            }));

            // [New] Subscribe to WeChat Online Event
            _subscriptions.Add(_events.SubscribeToEvent<SCRM.SHARED.Models.Events.WeChatOnlineEvent>("OnWeChatOnline", (e) =>
            {
                _logger.LogInformation($"[CrmStore] WeChat Online: {e.nickName} ({e.weChatId})");
                OnNotification?.Invoke($"微信已上线: {e.nickName}", true);
                _ = LoadDevicesAsync();
            }));

            // [New] Subscribe to WeChat Offline Event
            _subscriptions.Add(_events.SubscribeToEvent<SCRM.SHARED.Models.Events.WeChatOfflineEvent>("OnWeChatOffline", (e) =>
            {
                _logger.LogInformation($"[CrmStore] WeChat Offline: ({e.deviceUuid})");
                OnNotification?.Invoke("微信已离线/未登录", false);
                _ = LoadDevicesAsync();
            }));

            // [New] Subscribe to specific Messages
            _subscriptions.Add(_events.SubscribeToEvent<Message>("OnMessageReceived", (msg) =>
            {
                _logger.LogInformation($"[CrmStore] Message Received from {msg.senderWxid} to {msg.receiverWxid}: {msg.content}");
                
                // If the message belongs to the currently active conversation/contact, append it directly
                bool isRelevantToConversation = SelectedConversation != null && 
                    (SelectedConversation.conversationWxid == msg.senderWxid || SelectedConversation.conversationWxid == msg.receiverWxid);
                bool isRelevantToContact = SelectedContact != null && 
                    (SelectedContact.wxid == msg.senderWxid || SelectedContact.wxid == msg.receiverWxid);

                if (isRelevantToConversation || isRelevantToContact)
                {
                    CurrentMessages.Add(msg);
                    NotifyStateChanged();
                }

                // Generic message event for decoupled consumers
                MessageReceived?.Invoke(msg);
            }));
        }

        private void QueueContactsReload(string accountId)
        {
            if (_isDisposed) return;

            CancellationTokenSource? oldCts;
            CancellationTokenSource newCts;

            lock (_contactsReloadSync)
            {
                oldCts = _contactsReloadCts;               // 拿到旧引用
                newCts = new CancellationTokenSource();    // 创建新令牌
                _contactsReloadCts = newCts;               // 先交换（关键）
            }

            // 在锁外取消并释放旧 CTS，避免持锁做潜在慢操作
            if (oldCts != null)
            {
                try { oldCts.Cancel(); } catch { }
                try { oldCts.Dispose(); } catch { }
            }

            _ = ReloadContactsDebouncedAsync(accountId, newCts.Token);
        }

        private async Task ReloadContactsDebouncedAsync(string accountId, CancellationToken token)
        {
            try
            {
                await Task.Delay(_contactsDebounceWindow, token);

                var entered = false;
                try
                {
                    await _contactsReloadLock.WaitAsync(token);
                    entered = true;

                    if (_isDisposed || token.IsCancellationRequested) return;
                    if (SelectedDevice?.weChatId != accountId) return;

                    if (SelectedDevice.wx == null)
                        SelectedDevice.wx = new Wx { srClient = SelectedDevice };

                    SelectedDevice.wx.contacts = await _service.GetContactsAsync(accountId);
                    NotifyStateChanged();
                }
                finally
                {
                    if (entered) _contactsReloadLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // 防抖覆盖导致的正常取消
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogDebug(ex, "[CrmStore] Reload canceled after disposal for Account {AccountId}", accountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CrmStore] Reload contacts failed for Account {AccountId}", accountId);
            }
        }

        public async Task InitializeAsync()
        {
             await LoadDevicesAsync();
        }

        public async Task LoadDevicesAsync()
        {
            try 
            {
                Devices = await _service.GetDevicesAsync();
                
                // Refresh SelectedDevice reference if it exists
                if (SelectedDevice != null)
                {
                    var freshDevice = Devices.FirstOrDefault(d => d.uuid == SelectedDevice.uuid);
                    if (freshDevice != null)
                    {
                        SelectedDevice = freshDevice;
                        // Determine if we need to reload contacts (e.g. if we switch objects)
                        // For now, assume Wx object is fresh from DB or Service
                    }
                }

                _logger.LogInformation($"[CrmStore] Loaded {Devices.Count} devices via ICrmService.");
                NotifyStateChanged();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load devices.");
            }
        }

        public async Task SelectDeviceAsync(SrClient device)
        {
            // 1. 设置当前选中设备
            SelectedDevice = device;
            // 2. 初始化 wx 对象 (如果为空)
            if (SelectedDevice.wx == null)
            {
                SelectedDevice.wx = new Wx();
            }

            // 3. 【关键】按需加载联系人
            // 如果联系人列表是空的，并且设备已登录(有WeChatId)，则去服务器拉取
            if ((SelectedDevice.wx.contacts == null || !SelectedDevice.wx.contacts.Any())
                && !string.IsNullOrEmpty(device.weChatId))
            {
                NotifyStateChanged(); // 先通知UI显示"加载中"状态（可选）

                try
                {
                    // 调用 Service 从 API 获取联系人
                    var contacts = await _service.GetContactsAsync(device.weChatId);
                    SelectedDevice.wx.contacts = contacts;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load contacts for device {Uuid}", device.uuid);
                }
            }
            // 4. 通知 UI 渲染数据
            NotifyStateChanged();
            //return Task.CompletedTask;
        }

        private void NotifyStateChanged() => OnChange?.Invoke();

        public void Dispose()
        {
            _isDisposed = true;

            CancellationTokenSource? oldCts;
            lock (_contactsReloadSync)
            {
                oldCts = _contactsReloadCts;
                _contactsReloadCts = null;
            }

            if (oldCts != null)
            {
                try { oldCts.Cancel(); } catch { }
                try { oldCts.Dispose(); } catch { }
            }

            _contactsReloadLock.Dispose();

            foreach(var sub in _subscriptions)
            {
                sub.Dispose();
            }
            _subscriptions.Clear();
        }

        // --- Stubbed Methods to prevent compilation errors in Pages ---

        public async Task SelectConversationAsync(Conversation conversation) 
        { 
            SelectedConversation = conversation;
            if (conversation != null)
            {
                CurrentMessages = await _service.GetMessagesAsync(conversation.conversationWxid, 50);
            }
            NotifyStateChanged();
        }
        public Task SelectContactAsync(Contact? contact) 
        { 
            SelectedContact = contact;
            NotifyStateChanged();
            return Task.CompletedTask; 
        }
        public async Task<bool> SendMessageAsync(string content) 
        { 
            // AntiGravity Fix: Support sending to Contact directly (Conversation might be null)
            string targetWxid = "";
            if (SelectedConversation != null)
            {
                targetWxid = SelectedConversation.conversationWxid;
            }
            else if (SelectedContact != null)
            {
                targetWxid = SelectedContact.wxid;
            }
            else
            {
                _logger.LogWarning("[SendMessageAsync] No Conversation or Contact selected.");
                return false;
            }

            // Best effort to find DeviceUUID:
            // 1. From SelectedDevice
            // 2. Or from Conversation's Account (if loaded)
            string deviceUuid = SelectedDevice?.uuid ?? "";
            
            if (string.IsNullOrEmpty(deviceUuid))
            {
                 // Try to fallback (but for now just return false or let Service handle empty)
                 _logger.LogWarning("SendMessageAsync: No Device Selected.");
                 return false;
            }
            
            var success = await _service.SendMessageAsync(deviceUuid, targetWxid, content);
            if (success)
            {
                // Optimistic UI Update or Wait for Event
                // For now, reload messages
                 CurrentMessages = await _service.GetMessagesAsync(targetWxid, 50);
                 NotifyStateChanged();
            }
            return success;
        }
        public async Task<bool> RequestScreenShotAsync(string deviceUuid) 
        { 
             return await _service.RequestScreenShotAsync(deviceUuid);
        }
        public async Task LoadConversationsAsync(long accountId) 
        {
             // For now load all, can filter by accountId later
             Conversations = await _service.GetConversationsAsync();
             NotifyStateChanged();
        }
        public Task ExecuteGroupActionAsync(string chatRoomId, int action, string content, int intValue = 0) { return Task.CompletedTask; }
        public Task AgreeJoinGroupAsync(string talker, long msgSvrId, string content) { return Task.CompletedTask; }
        public Task DeleteCurrentContactAsync() { return Task.CompletedTask; }
        public async Task SyncContactsAsync() 
        { 
            Contacts = await _service.GetContactsAsync();
            NotifyStateChanged();
        }
        public Task SyncChatRoomsAsync() { return Task.CompletedTask; }
        public Task<TaskResult> PostMomentAsync(string content, List<string> imageUrls) { return Task.FromResult(TaskResult.Fail("Not Implemented")); }
        public Task<bool> SyncMomentsAsync() { return Task.FromResult(false); }

        public Task DisconnectAsync() { return Task.CompletedTask; } 
        

        public Task DeleteFriendAsync(string uuid, string wxid) { return Task.CompletedTask; }
        public Task DeleteFriendAsync(string wxid) { return Task.CompletedTask; } // Overload for UI
        public Task RequestMomentsSyncAsync() { return Task.CompletedTask; }
        
        public Task DeleteDeviceAsync(string uuid) { return Task.CompletedTask; }
        public Task LoadMomentsAsync() { return Task.CompletedTask; }

        public Task DeleteWeChatAccountAsync(string wxid) { return Task.CompletedTask; }
        public Task<List<SrClient>> LoadAllDevicesAsync() { return _service.GetDevicesAsync(); }
        public Task<List<SrClient>> GetDevicesAsync() => _service.GetDevicesAsync();
        public Task<List<WechatAccount>> LoadAllWeChatAccountsAsync() { return Task.FromResult(new List<WechatAccount>()); }
    }
}
