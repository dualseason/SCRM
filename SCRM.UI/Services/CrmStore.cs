using SCRM.API.Models.Entities;
using SCRM.Shared.Interfaces;
using SCRM.SHARED.Models.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SCRM.UI.Services.Data;

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
        
        // Additional Stubs
        public bool HasInitialized { get; set; } = true;
        public List<MomentsTimeline> CurrentMoments { get; private set; } = new();
        public string? LastScreenShotUrl { get; set; }

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

            // [New] Subscribe to Screenshot Uploaded Event
            _subscriptions.Add(_events.SubscribeToEvent("OnScreenShotUploaded", (string url) =>
            {
                _logger.LogInformation($"[CrmStore] Received Screenshot: {url}");
                LastScreenShotUrl = url;
                NotifyStateChanged();
            }));
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
                _logger.LogInformation($"[CrmStore] Loaded {Devices.Count} devices via ICrmService.");
                NotifyStateChanged();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load devices.");
            }
        }

        public Task SelectDeviceAsync(SrClient device)
        {
            SelectedDevice = device;
            NotifyStateChanged();
            return Task.CompletedTask;
        }

        private void NotifyStateChanged() => OnChange?.Invoke();

        public void Dispose()
        {
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
        public Task SelectContactAsync(Contact contact) 
        { 
            SelectedContact = contact;
            NotifyStateChanged();
            return Task.CompletedTask; 
        }
        public async Task<bool> SendMessageAsync(string content) 
        { 
            if (SelectedConversation == null) return false;
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
            
            var success = await _service.SendMessageAsync(deviceUuid, SelectedConversation.conversationWxid, content);
            if (success)
            {
                // Optimistic UI Update or Wait for Event
                // For now, reload messages
                 CurrentMessages = await _service.GetMessagesAsync(SelectedConversation.conversationWxid, 50);
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

        public Task DeleteWeChatAccountAsync(long accountId) { return Task.CompletedTask; }
        public Task<List<SrClient>> LoadAllDevicesAsync() { return _service.GetDevicesAsync(); }
        public Task<List<SrClient>> GetDevicesAsync() => _service.GetDevicesAsync();
        public Task<List<WechatAccount>> LoadAllWeChatAccountsAsync() { return Task.FromResult(new List<WechatAccount>()); }
    }
}
