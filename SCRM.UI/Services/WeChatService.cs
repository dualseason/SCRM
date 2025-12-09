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
        public event Action<string>? OnScreenShotReceived;

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

            _hubConnection.On<string>("ScreenShotReceived", (url) =>
            {
                OnScreenShotReceived?.Invoke(url);
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
                // Note: ClientHub uses SyncContacts which might use SyncFriendListAsyncReq
                // To be consistent with "Trigger" logic, we might need to update ClientHub to use TriggerFriendPushTask
                // But for now, we just call the existing Hub method.
                return await _hubConnection.InvokeAsync<bool>("SyncContacts", connectionId);
            }
            return false;
        }

        public async Task<bool> SyncChatRoomsAsync(string connectionId)
        {
            if (_hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<bool>("SyncChatRooms", connectionId);
            }
            return false;
        }

        public async Task<bool> SyncMomentsAsync(string connectionId)
        {
            if (_hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<bool>("SyncMoments", connectionId);
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

        /// <summary>
        /// 调用 SignalR 执行群聊操作
        /// </summary>
        /// <param name="connectionId">设备连接ID</param>
        /// <param name="chatRoomId">群聊ID</param>
        /// <param name="action">操作类型</param>
        /// <param name="content">参数内容</param>
        /// <param name="intValue">整数参数</param>
        public async Task<bool> ExecuteGroupActionAsync(string connectionId, string chatRoomId, int action, string content, int intValue)
        {
            if (_hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<bool>("ExecuteGroupAction", connectionId, chatRoomId, action, content, intValue);
            }
            return false;
        }

        /// <summary>
        /// 调用 SignalR 同意入群
        /// </summary>
        /// <param name="connectionId">设备连接ID</param>
        /// <param name="talker">邀请人</param>
        /// <param name="msgSvrId">消息ID</param>
        /// <param name="content">原始内容</param>
        public async Task<bool> AgreeJoinGroupAsync(string connectionId, string talker, long msgSvrId, string content)
        {
            if (_hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<bool>("AgreeJoinGroup", connectionId, talker, msgSvrId, content);
            }
            return false;
        }

        /// <summary>
        /// 调用 SignalR 删除好友
        /// </summary>
        /// <param name="connectionId">设备连接ID</param>
        /// <param name="friendId">好友微信ID</param>
        public async Task<bool> DeleteFriendAsync(string connectionId, string friendId)
        {
            if (_hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<bool>("DeleteFriend", connectionId, friendId);
            }
            return false;
        }

        /// <summary>
        /// 调用 SignalR 接受好友请求
        /// </summary>
        /// <param name="connectionId">设备连接ID</param>
        /// <param name="friendId">好友微信ID</param>
        /// <param name="friendNick">好友昵称</param>
        public async Task<bool> AcceptFriendRequestAsync(string connectionId, string friendId, string friendNick)
        {
            if (_hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<bool>("AcceptFriendRequest", connectionId, friendId, friendNick);
            }
            return false;
        }

        /// <summary>
        /// 调用 SignalR 请求截屏
        /// </summary>
        public async Task<bool> RequestScreenShotAsync(string connectionId)
        {
            if (_hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<bool>("RequestScreenShot", connectionId);
            }
            return false;
        }

        /// <summary>
        /// 调用 SignalR 执行手机操作
        /// </summary>
        /// <param name="action">1=重启, 4=清App缓存, 5=清微信缓存, 9=重启微信</param>
        public async Task<bool> ExecutePhoneActionAsync(string connectionId, int action)
        {
            if (_hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<bool>("ExecutePhoneAction", connectionId, action);
            }
            return false;
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
