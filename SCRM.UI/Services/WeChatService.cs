using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.UI.Services
{
    /// <summary>
    /// Unified WeChat Business Service (SignalR Client)
    /// </summary>
    public class WeChatService : IAsyncDisposable
    {
        private HubConnection? _hubConnection;
        private readonly ILocalStorageService _localStorage;

        public event Action<ReceiveMessageDto>? OnMessageReceived;
        public event Action<string, string, bool>? OnWeChatStatusChanged;
        public event Action<long>? OnContactsUpdated; // New Event
        public event Action<string>? OnScreenShotReceived;
        public event Action<string, string, bool>? OnDeviceStatusChanged; // New Event for connection updates
        public event Action<string?>? OnReconnected; // New Event for Reconnection
        public event Action<TaskResultDto>? OnTaskResultReceived; // New Event for Task Results
        public event Action<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>? OnMomentReceived; // New Event for Moments

        public WeChatService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public async Task InitializeAsync(string hubUrl)
        {
            if (_hubConnection is not null && _hubConnection.State == HubConnectionState.Connected)
            {
                return;
            }

            if (_hubConnection is not null && _hubConnection.State != HubConnectionState.Disconnected) 
            {
                // If connecting or reconnecting, wait or return
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

            _hubConnection.Reconnected += async (connectionId) =>
            {
                Console.WriteLine($"[WeChatService] SignalR Reconnected. New ConnectionId: {connectionId}");
                OnReconnected?.Invoke(connectionId);
                await Task.CompletedTask;
            };

            // 监听服务器推送的 "ReceiveMessage" 信号
            // 服务器发送的是一个 DTO 对象，必须使用 ReceiveMessageDto 接收
            _hubConnection.On<ReceiveMessageDto>("ReceiveMessage", (msg) =>
            {
                // 收到消息后，触发本地事件通知 UI 层
                OnMessageReceived?.Invoke(msg);
            });

            // 监听 "ContactsUpdated" 信号 (参数: accountId)
            // ... (rest of the handlers)
            _hubConnection.On<long>("ContactsUpdated", (accountId) =>
            {
                OnContactsUpdated?.Invoke(accountId);
            });

            _hubConnection.On<string, string, bool>("WeChatStatusChanged", (wxid, nick, isOnline) =>
            {
                OnWeChatStatusChanged?.Invoke(wxid, nick, isOnline);
            });

            // 监听 "DeviceConnectionChanged" 消息
            _hubConnection.On<string, string, bool>("DeviceConnectionChanged", (deviceId, connectionId, isOnline) =>
            {
                // 触发本地 C# 事件，供 UI 层订阅
                OnDeviceStatusChanged?.Invoke(deviceId, connectionId, isOnline);
            });

            _hubConnection.On<string>("ScreenShotReceived", (url) =>
            {
                OnScreenShotReceived?.Invoke(url);
            });

            // 监听任务结果通知
            _hubConnection.On<TaskResultDto>("OnTaskResult", (dto) =>
            {
                OnTaskResultReceived?.Invoke(dto);
            });

            _hubConnection.On<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>("MomentReceived", (dto) =>
            {
                OnMomentReceived?.Invoke(dto);
            });

            try
            {
                await _hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SignalR Connection Failed: {ex.Message}");
                // Consider throwing or handling gracefully
            }
        }
// ... (existing methods omitted for brevity in replace logic, matching indentation)



        public async Task JoinGroupAsync(string groupName)
        {
            if (IsConnected && _hubConnection is not null)
            {
                await _hubConnection.InvokeAsync("JoinGroup", groupName);
            }
        }

        public async Task LeaveGroupAsync(string groupName)
        {
            if (IsConnected && _hubConnection is not null)
            {
                await _hubConnection.InvokeAsync("LeaveGroup", groupName);
            }
        }

        public async Task<IEnumerable<SrClient>> GetDevicesAsync()
        {
            // Wait for connection (handling race condition on startup)
            int retries = 0;
            while (!IsConnected && retries < 20) // Wait up to 2 seconds
            {
                await Task.Delay(100);
                retries++;
            }

            if (IsConnected && _hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<IEnumerable<SrClient>>("GetDevices");
            }
            Console.WriteLine("[WeChatService] GetDevicesAsync skipped: SignalR not connected.");
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
            if (IsConnected && _hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<bool>("SyncContacts", connectionId);
            }
            return false;
        }

        public async Task<bool> SyncChatRoomsAsync(string connectionId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("SyncChatRooms", connectionId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        public async Task<bool> SyncMomentsAsync(string connectionId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("SyncMoments", connectionId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        public async Task<TaskResult> SendMessageAsync(string connectionId, string friendWxId, string content)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("SendMessage", connectionId, friendWxId, content);
                }
                catch (Exception ex)
                {
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 执行群聊操作
        /// </summary>
        public async Task<bool> ExecuteGroupActionAsync(string connectionId, string chatRoomId, int action, string content, int intValue)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("ExecuteGroupAction", connectionId, chatRoomId, action, content, intValue);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 调用 SignalR 同意入群
        /// </summary>
        public async Task<bool> AgreeJoinGroupAsync(string connectionId, string talker, long msgSvrId, string content)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("AgreeJoinGroup", connectionId, talker, msgSvrId, content);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 调用 SignalR 删除好友
        /// </summary>
        public async Task<bool> DeleteFriendAsync(string connectionId, string friendId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("DeleteFriend", connectionId, friendId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 调用 SignalR 接受好友请求
        /// </summary>
        public async Task<bool> AcceptFriendRequestAsync(string connectionId, string friendId, string friendNick)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("AcceptFriendRequest", connectionId, friendId, friendNick);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 调用 SignalR 请求截屏
        /// </summary>
        public async Task<bool> RequestScreenShotAsync(string connectionId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("RequestScreenShot", connectionId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 调用 SignalR 执行手机操作
        /// </summary>
        public async Task<bool> ExecutePhoneActionAsync(string connectionId, int action)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("ExecutePhoneAction", connectionId, action);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return false;
                }
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

        /// 调用 SignalR 获取账号配置
        /// </summary>
        public async Task<WechatAccountSettings> GetAccountSettingsAsync(long accountId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<WechatAccountSettings>("GetAccountSettings", accountId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return new WechatAccountSettings();
                }
            }
            return new WechatAccountSettings();
        }

        /// <summary>
        /// 调用 SignalR 更新账号配置
        /// </summary>
        public async Task<bool> UpdateAccountSettingsAsync(long accountId, WechatAccountSettings settings)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("UpdateAccountSettings", accountId, settings);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 调用 SignalR 获取朋友圈列表
        /// </summary>
        public async Task<IEnumerable<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>> GetMomentsTimelineAsync(string connectionId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<IEnumerable<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>>("GetMomentsTimeline", connectionId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return Enumerable.Empty<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>();
                }
            }
            return Enumerable.Empty<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>();
        }

        /// <summary>
        /// 调用 SignalR 发送朋友圈
        /// </summary>
        public async Task<TaskResult> PostMomentAsync(string connectionId, string content, List<string> imageUrls)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("PostMoment", connectionId, content, imageUrls);
                }
                catch (Exception ex)
                {
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }
    }

    public class ReceiveMessageDto
    {
        public string FriendId { get; set; } // 会话窗口ID (Wxid/@chatroom)
        public string Content { get; set; }  // 消息内容
        public bool IsSelf { get; set; }     // 是否是自己发出的
        public long MsgSvrId { get; set; }   // 微信消息服务器ID
        public bool IsGroup { get; set; }    // 是否群聊
        public long TaskId { get; set; }     // 任务关联ID
    }

    public class TaskResultDto
    {
        public long TaskId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string DeviceUuid { get; set; }
    }
}
