using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
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
        private readonly HashSet<string> _pendingGroups = new(); // Track groups to join

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
                await SyncPendingGroupsAsync();
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
                Console.WriteLine($"[WeChatService] SignalR Received 'ContactsUpdated' for Account: {accountId}");
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

            _hubConnection.On<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>("MomentTimelineReceived", (dto) =>
            {
                OnMomentReceived?.Invoke(dto);
            });

            // ... start connection block starts here in original ...
            try
            {
                await _hubConnection.StartAsync();
                Console.WriteLine("[WeChatService] SignalR Connected. Syncing pending groups...");
                await SyncPendingGroupsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SignalR Connection Failed: {ex.Message}");
                // Consider throwing or handling gracefully
            }
        }

        public async Task<IEnumerable<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>> GetMomentsTimelineAsync(string deviceUuid, int page = 1)
        {
            if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected) return Enumerable.Empty<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>();
            try
            {
                return await _hubConnection.InvokeAsync<IEnumerable<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>>("GetMomentsTimeline", deviceUuid, page);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] GetMomentsTimelineAsync failed: {ex.Message}");
                return Enumerable.Empty<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>();
            }
        }
// ... (existing methods omitted for brevity in replace logic, matching indentation)



        public async Task JoinGroupAsync(string groupName)
        {
            lock (_pendingGroups)
            {
                _pendingGroups.Add(groupName);
            }

            if (IsConnected && _hubConnection is not null)
            {
                Console.WriteLine($"[WeChatService] Joining SignalR Group: {groupName}");
                await _hubConnection.InvokeAsync("JoinGroup", groupName);
            }
            else
            {
                Console.WriteLine($"[WeChatService] JoinGroupAsync queued for {groupName}: Not connected yet.");
            }
        }

        private async Task SyncPendingGroupsAsync()
        {
            if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected) return;

            List<string> groups;
            lock (_pendingGroups)
            {
                groups = _pendingGroups.ToList();
            }

            foreach (var group in groups)
            {
                try
                {
                    Console.WriteLine($"[WeChatService] Syncing Group: {group}");
                    await _hubConnection.InvokeAsync("JoinGroup", group);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WeChatService] Failed to join group {group}: {ex.Message}");
                }
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
            // Wait for connection
            int retries = 0;
            while (!IsConnected && retries < 40) // Wait up to 4 seconds
            {
                await Task.Delay(100);
                retries++;
            }

            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<IEnumerable<Contact>>("GetContacts", accountId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WeChatService] GetContactsAsync Failed: {ex.Message}");
                }
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

        public async Task<IEnumerable<Conversation>> GetConversationsAsync(long accountId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<IEnumerable<Conversation>>("GetConversations", accountId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WeChatService] GetConversationsAsync Failed: {ex.Message}");
                }
            }
            return Enumerable.Empty<Conversation>();
        }

        public async Task<bool> SyncContactsAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                return await _hubConnection.InvokeAsync<bool>("SyncContacts", deviceUuid);
            }
            return false;
        }

        public async Task<bool> SyncChatRoomsAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("SyncChatRooms", deviceUuid);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        public async Task<bool> SyncMomentsAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("SyncMoments", deviceUuid);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        public async Task<TaskResult> SendMessageAsync(string deviceUuid, string friendWxId, string content)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("SendMessage", deviceUuid, friendWxId, content);
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
        public async Task<bool> ExecuteGroupActionAsync(string deviceUuid, string chatRoomId, int action, string content, int intValue)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("ExecuteGroupAction", deviceUuid, chatRoomId, action, content, intValue);
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
        public async Task<bool> AgreeJoinGroupAsync(string deviceUuid, string talker, long msgSvrId, string content)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("AgreeJoinGroup", deviceUuid, talker, msgSvrId, content);
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
        public async Task<bool> DeleteFriendAsync(string deviceUuid, string friendId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("DeleteFriend", deviceUuid, friendId);
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
        public async Task<bool> AcceptFriendRequestAsync(string deviceUuid, string friendId, string friendNick)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("AcceptFriendRequest", deviceUuid, friendId, friendNick);
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
        public async Task<bool> RequestScreenShotAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("RequestScreenShot", deviceUuid);
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
        public async Task<bool> ExecutePhoneActionAsync(string deviceUuid, int action)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("ExecutePhoneAction", deviceUuid, action);
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
        public async Task<IEnumerable<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>> GetMomentsTimelineAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<IEnumerable<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>>("GetMomentsTimeline", deviceUuid);
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
        public async Task<TaskResult> PostMomentAsync(string deviceUuid, string content, List<string> imageUrls)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("PostMoment", deviceUuid, content, imageUrls);
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
        public string friendId { get; set; } // 会话窗口ID (Wxid/@chatroom)
        public string content { get; set; }  // 消息内容
        public bool isSelf { get; set; }     // 是否是自己发出的
        public long msgSvrId { get; set; }   // 微信消息服务器ID
        public bool isGroup { get; set; }    // 是否群聊
        public long taskId { get; set; }     // 任务关联ID
    }

    public class TaskResultDto
    {
        public long taskId { get; set; }
        public bool success { get; set; }
        public string message { get; set; }
        public string deviceUuid { get; set; }
    }
}
