using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.UI.Services
{
    public class CrmStore
    {
        private readonly WeChatService _weChatService;
        
        // State
        public List<SrClient> Devices { get; private set; } = new();
        public SrClient? SelectedDevice { get; private set; }
        
        public List<Contact> Contacts { get; private set; } = new();
        public Contact? SelectedContact { get; private set; }
        
        // Dictionary<FriendWxId, List<Message>>
        public Dictionary<string, List<Message>> ChatHistory { get; private set; } = new();

        // Events
        public event Action? OnChange;

        public CrmStore(WeChatService weChatService)
        {
            _weChatService = weChatService;
            
            // Subscribe to SignalR events
            _weChatService.OnMessageReceived += HandleMessageReceived;
            _weChatService.OnWeChatStatusChanged += HandleWeChatStatusChanged;
        }

        public async Task InitializeAsync()
        {
            // Initial load
            await LoadDevicesAsync();
        }

        public async Task LoadDevicesAsync()
        {
            var result = await _weChatService.GetDevicesAsync();
            Devices = result.ToList();
            
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

            SelectedDevice = device;
            SelectedContact = null;
            Contacts.Clear();
            ChatHistory.Clear();
            NotifyStateChanged();

            if (SelectedDevice != null && SelectedDevice.WechatAccountId.HasValue)
            {
                 var result = await _weChatService.GetContactsAsync(SelectedDevice.WechatAccountId.Value);
                 Contacts = result.ToList();
                 NotifyStateChanged();
            }
        }

        public async Task SelectContactAsync(Contact contact)
        {
            if (SelectedContact == contact) return;

            SelectedContact = contact;
            NotifyStateChanged();

            if (SelectedDevice != null && SelectedDevice.WechatAccountId.HasValue && SelectedContact != null)
            {
                // Check if we already have history in memory? 
                // For now, reload to be safe or if not present.
                if (!ChatHistory.ContainsKey(contact.Wxid))
                {
                    var history = await _weChatService.GetChatHistoryAsync(SelectedDevice.WechatAccountId.Value, contact.Wxid);
                    ChatHistory[contact.Wxid] = history.OrderBy(m => m.CreatedAt).ToList();
                }
                NotifyStateChanged();
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
                    SenderWxid = "self", // or known self wxid
                    ReceiverWxid = SelectedContact.Wxid,
                    Content = content,
                    CreatedAt = DateTime.UtcNow, // Use UtcNow for consistency
                    Direction = 1 // Sent
                };
                AddMessageToHistory(SelectedContact.Wxid, msg);
            }
            else
            {
                // Handle error
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

        private void HandleMessageReceived(string friendId, string content, bool isSent)
        {
            // Update history if exists
            var msg = new Message
            {
                SenderWxid = isSent ? "self" : friendId,
                ReceiverWxid = isSent ? friendId : "self",
                Content = content,
                CreatedAt = DateTime.UtcNow,
                Direction = (short)(isSent ? 1 : 0)
            };

            AddMessageToHistory(friendId, msg);
        }

        private void AddMessageToHistory(string friendId, Message msg)
        {
            if (!ChatHistory.ContainsKey(friendId))
            {
                ChatHistory[friendId] = new List<Message>();
            }
            ChatHistory[friendId].Add(msg);
            
            // Re-order contact to top? (Optional)
            
            NotifyStateChanged();
        }

        private void HandleWeChatStatusChanged(string wxid, string nick, bool isOnline)
        {
            // Reload devices to update status
            _ = LoadDevicesAsync();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();

        public void Dispose()
        {
            _weChatService.OnMessageReceived -= HandleMessageReceived;
            _weChatService.OnWeChatStatusChanged -= HandleWeChatStatusChanged;
        }
    }
}
