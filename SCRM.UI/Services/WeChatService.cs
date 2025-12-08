using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;

namespace SCRM.UI.Services
{
    /// <summary>
    /// Unified WeChat Business Service (SignalR Client)
    /// </summary>
    public class WeChatService : IAsyncDisposable
    {
        private HubConnection? _hubConnection;
        private readonly ILocalStorageService _localStorage;

        public event Action<string, string, bool>? OnMessageReceived;
        public event Action<string, string, bool>? OnWeChatStatusChanged;

        public WeChatService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public async Task InitializeAsync(string hubUrl)
        {
            if (_hubConnection is not null && _hubConnection.State != HubConnectionState.Disconnected)
            {
                return;
            }

            if (_hubConnection is not null) 
            {
                await _hubConnection.DisposeAsync();
            }

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = async () => 
                    {
                        var token = await _localStorage.GetItemAsStringAsync("authToken");
                        return token?.Trim('"');
                    };
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<string, string, bool>("ReceiveMessage", (friendId, content, isSent) =>
            {
                OnMessageReceived?.Invoke(friendId, content, isSent);
            });

            _hubConnection.On<string, string, bool>("WeChatStatusChanged", (wxid, nick, isOnline) =>
            {
                OnWeChatStatusChanged?.Invoke(wxid, nick, isOnline);
            });

            await _hubConnection.StartAsync();
        }

        public async Task JoinGroupAsync(string groupName)
        {
            if (_hubConnection is not null)
            {
                await _hubConnection.InvokeAsync("JoinGroup", groupName);
            }
        }

        public async Task<IEnumerable<SrClient>> GetDevicesAsync()
        {
            if (_hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<IEnumerable<SrClient>>("GetDevices");
            }
            return Enumerable.Empty<SrClient>();
        }

        public async Task<IEnumerable<Contact>> GetContactsAsync(long accountId)
        {
            if (_hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<IEnumerable<Contact>>("GetContacts", accountId);
            }
            return Enumerable.Empty<Contact>();
        }

        public async Task<IEnumerable<Message>> GetChatHistoryAsync(long accountId, string friendWxId)
        {
            if (_hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<IEnumerable<Message>>("GetChatHistory", accountId, friendWxId);
            }
            return Enumerable.Empty<Message>();
        }

        public async Task<bool> SyncContactsAsync(string connectionId)
        {
            if (_hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<bool>("SyncContacts", connectionId);
            }
            return false;
        }

        public async Task<TaskResult> SendMessageAsync(string connectionId, string friendWxId, string content)
        {
            if (_hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<TaskResult>("SendMessage", connectionId, friendWxId, content);
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection is not null)
            {
                await _hubConnection.DisposeAsync();
            }
        }
    }
}
