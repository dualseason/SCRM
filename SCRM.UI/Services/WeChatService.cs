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
        public event Action<string>? OnContactsUpdated; // New Event
        public event Action<string>? OnFriendRequestsUpdated; // 好友请求列表更新事件
        public event Action<string>? OnGroupInvitationsUpdated; // 群邀请列表更新事件
        public event Action<string>? OnContactLabelsUpdated; // 联系人标签列表更新事件
        public event Action<string>? OnSmsRecordsUpdated; // 手机短信记录更新事件
        public event Action<string>? OnCallLogRecordsUpdated; // 手机通话记录更新事件
        public event Action<string>? OnScreenShotReceived;
        public event Action<string, string, bool>? OnDeviceStatusChanged; // New Event for connection updates
        public event Action<string?>? OnReconnected; // New Event for Reconnection
        public event Action<TaskResultDto>? OnTaskResultReceived; // New Event for Task Results
        public event Action<RealtimeDataChangedNoticeDto>? OnRealtimeDataChanged; // 朋友圈/视频号安全变更通知

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

            // 监听 "ContactsUpdated" 信号 (参数: accountId，当前使用微信 wxid 字符串)
            // ... (rest of the handlers)
            _hubConnection.On<string>("ContactsUpdated", (accountId) =>
            {
                Console.WriteLine($"[WeChatService] SignalR Received 'ContactsUpdated' for Account: {accountId}");
                OnContactsUpdated?.Invoke(accountId);
            });

            _hubConnection.On<string>("FriendRequestsUpdated", (accountId) =>
            {
                Console.WriteLine($"[WeChatService] SignalR Received 'FriendRequestsUpdated' for Account: {accountId}");
                OnFriendRequestsUpdated?.Invoke(accountId);
            });

            _hubConnection.On<string>("GroupInvitationsUpdated", (accountId) =>
            {
                Console.WriteLine($"[WeChatService] SignalR Received 'GroupInvitationsUpdated' for Account: {accountId}");
                OnGroupInvitationsUpdated?.Invoke(accountId);
            });

            _hubConnection.On<string>("ContactLabelsUpdated", (accountId) =>
            {
                Console.WriteLine($"[WeChatService] SignalR Received 'ContactLabelsUpdated' for Account: {accountId}");
                OnContactLabelsUpdated?.Invoke(accountId);
            });

            _hubConnection.On<string>("SmsRecordsUpdated", (accountId) =>
            {
                Console.WriteLine($"[WeChatService] SignalR Received 'SmsRecordsUpdated' for Account: {accountId}");
                OnSmsRecordsUpdated?.Invoke(accountId);
            });

            _hubConnection.On<string>("CallLogRecordsUpdated", (accountId) =>
            {
                Console.WriteLine($"[WeChatService] SignalR Received 'CallLogRecordsUpdated' for Account: {accountId}");
                OnCallLogRecordsUpdated?.Invoke(accountId);
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

            // 2026-05-12 静态修复：
            // 服务端当前统一转发的是 OnScreenShotUploaded；
            // 这里保留旧事件名 ScreenShotReceived 的同时，也兼容新事件名，避免前端直连 WeChatService 的消费者收不到截图完成回调。
            _hubConnection.On<string>("OnScreenShotUploaded", (url) =>
            {
                OnScreenShotReceived?.Invoke(url);
            });

            // 监听任务结果通知
            _hubConnection.On<TaskResultDto>("OnTaskResult", (dto) =>
            {
                OnTaskResultReceived?.Invoke(dto);
            });

            _hubConnection.On<RealtimeDataChangedNoticeDto>("MomentTimelineChanged", (dto) =>
            {
                OnRealtimeDataChanged?.Invoke(dto);
            });

            _hubConnection.On<RealtimeDataChangedNoticeDto>("FinderResultChanged", (dto) =>
            {
                OnRealtimeDataChanged?.Invoke(dto);
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

        public async Task<IEnumerable<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>> GetMomentsTimelineAsync(string deviceUuid, int page = 1, string weChatId = "")
        {
            if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected) return Enumerable.Empty<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>();
            try
            {
                return await _hubConnection.InvokeAsync<IEnumerable<SCRM.SHARED.Models.Dtos.MomentsTimelineDto>>("GetMomentsTimeline", deviceUuid, page, weChatId);
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

        /// <summary>
        /// 获取指定群聊成员。
        /// </summary>
        public async Task<IEnumerable<GroupMemberDto>> GetGroupMembersAsync(string accountId, string chatRoomId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<IEnumerable<GroupMemberDto>>("GetGroupMembers", accountId, chatRoomId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WeChatService] GetGroupMembersAsync Failed: {ex.Message}");
                }
            }

            return Enumerable.Empty<GroupMemberDto>();
        }

        /// <summary>
        /// 获取会话列表。
        /// <para>当前服务端 Conversations.wechatAccountId 使用微信 wxid 字符串，不能再按 long 账号 ID 调用。</para>
        /// </summary>
        public async Task<IEnumerable<Conversation>> GetConversationsAsync(string accountId)
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

        /// <summary>
        /// 兼容旧调用：早期页面把账号当作 long 传入。
        /// </summary>
        public Task<IEnumerable<Conversation>> GetConversationsAsync(long accountId)
        {
            return GetConversationsAsync(accountId.ToString());
        }

        /// <summary>
        /// 通过 SignalR 下发 3056 好友异步同步任务。
        /// <para>返回值只表示任务是否成功下发；真实联系人数据等待 FriendPushNotice 落库后刷新。</para>
        /// </summary>
        public async Task<TaskResult> SyncContactsAsync(string deviceUuid, string weChatId = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("SyncContacts", deviceUuid, weChatId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WeChatService] SyncContactsAsync Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 通过 SignalR 下发 3050 微信账号状态查询任务。
        /// </summary>
        public async Task<TaskResult> RefreshWeChatAccountsAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("RefreshWeChatAccounts", deviceUuid);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WeChatService] RefreshWeChatAccountsAsync Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<bool> SyncChatRoomsAsync(string deviceUuid, int flag = 0, string weChatId = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("SyncChatRooms", deviceUuid, flag, weChatId);
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
        /// 请求设备拉取群邀请列表。
        /// </summary>
        public async Task<bool> GetChatRoomInviteListAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("GetChatRoomInviteList", deviceUuid);
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
        /// 查询已落库的群邀请审批列表。
        /// </summary>
        public async Task<List<GroupInvitationDto>> GetGroupInvitationsAsync(string accountId, string chatRoomId = "", int count = 100, bool pendingOnly = false)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<List<GroupInvitationDto>>("GetGroupInvitations", accountId, chatRoomId, count, pendingOnly);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return new List<GroupInvitationDto>();
                }
            }

            return new List<GroupInvitationDto>();
        }

        /// <summary>
        /// 调用 SignalR 执行二维码入群。
        /// </summary>
        public async Task<TaskResult> JoinGroupByQrAsync(string deviceUuid, string qrUrl = "", string qrContent = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("JoinGroupByQr", deviceUuid, qrUrl, qrContent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 拉取群二维码，成功时 message 中返回二维码地址。
        /// </summary>
        public async Task<TaskResult> PullChatRoomQrCodeAsync(string deviceUuid, string chatRoomId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("PullChatRoomQrCode", deviceUuid, chatRoomId);
                }
                catch (Exception ex)
                {
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 拉取个人微信二维码。
        /// </summary>
        public async Task<TaskResult> PullWeChatQrCodeAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("PullWeChatQrCode", deviceUuid); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 拉取 POI 列表。
        /// </summary>
        public async Task<TaskResult> GetPoiListAsync(string deviceUuid, double lat, double lng, string keyword = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("GetPoiList", deviceUuid, lat, lng, keyword); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 拉取表情详情。
        /// </summary>
        public async Task<TaskResult> PullEmojiInfoAsync(string deviceUuid, string md5)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("PullEmojiInfo", deviceUuid, md5); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 为指定聊天表情消息补图。
        /// </summary>
        public async Task<TaskResult> PullEmojiInfoForMessageAsync(string deviceUuid, string md5, long msgSvrId, string friendId = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("PullEmojiInfoForMessage", deviceUuid, md5, msgSvrId, friendId); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 搜索联系人。
        /// </summary>
        public async Task<TaskResult> FindContactAsync(string deviceUuid, string content)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("FindContact", deviceUuid, content); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 查询微信定位。
        /// </summary>
        public async Task<TaskResult> GetWeChatLocationAsync(string deviceUuid, bool noCache = false)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("GetWeChatLocation", deviceUuid, noCache); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 查询钱包余额。
        /// </summary>
        public async Task<TaskResult> GetWalletBalanceAsync(string deviceUuid, int flag = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("GetWalletBalance", deviceUuid, flag); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 查询手机状态。
        /// </summary>
        public async Task<TaskResult> GetPhoneStateAsync(string deviceUuid, string imei = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("GetPhoneState", deviceUuid, imei); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 发送手机短信。
        /// </summary>
        public async Task<TaskResult> SendSmsAsync(string deviceUuid, string number, string content)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SendSms", deviceUuid, number, content); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 拉取短信历史。
        /// </summary>
        public async Task<TaskResult> PullSmsAsync(string deviceUuid, long startTime, long endTime)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("PullSms", deviceUuid, startTime, endTime); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 拉取通话记录。
        /// </summary>
        public async Task<TaskResult> PullCallLogsAsync(string deviceUuid, long startTime, long endTime)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("PullCallLogs", deviceUuid, startTime, endTime); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 读取短信记录。
        /// </summary>
        public async Task<List<SmsRecordDto>> GetSmsRecordsAsync(string accountId, string imei = "", int count = 200)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<List<SmsRecordDto>>("GetSmsRecords", accountId, imei, count); }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WeChatService] GetSmsRecordsAsync Failed: {ex.Message}");
                }
            }
            return new List<SmsRecordDto>();
        }

        /// <summary>
        /// 调用 SignalR 读取通话记录。
        /// </summary>
        public async Task<List<CallLogRecordDto>> GetCallLogRecordsAsync(string accountId, string imei = "", int count = 200)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<List<CallLogRecordDto>>("GetCallLogRecords", accountId, imei, count); }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WeChatService] GetCallLogRecordsAsync Failed: {ex.Message}");
                }
            }
            return new List<CallLogRecordDto>();
        }

        /// <summary>
        /// 调用 SignalR 同步企微用户列表。
        /// </summary>
        public async Task<TaskResult> SyncQwUsersAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SyncQwUsers", deviceUuid); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 同步聊天消息 MsgSvrId 快照。
        /// </summary>
        public async Task<TaskResult> SyncChatMsgIdsAsync(string deviceUuid, long startTime, long endTime)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SyncChatMsgIds", deviceUuid, startTime, endTime); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SyncHistoryMessagesAsync(string deviceUuid, string friendId = "", long startTime = 0, long endTime = 0, int flag = 0, int count = 50)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SyncHistoryMessages", deviceUuid, friendId, startTime, endTime, flag, count); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SyncMessageReadAsync(string deviceUuid, string friendId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SyncMessageRead", deviceUuid, friendId); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SyncUnreadListAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SyncUnreadList", deviceUuid); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SyncConversationUnreadAsync(string deviceUuid, string friendId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SyncConversationUnread", deviceUuid, friendId); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SyncBizContactsAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SyncBizContacts", deviceUuid); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SyncQwConversationsAsync(string deviceUuid, long startTime = 0, long endTime = 0, int limit = 100, int offset = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SyncQwConversations", deviceUuid, startTime, endTime, limit, offset); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 同步联系人标签列表。
        /// </summary>
        public async Task<TaskResult> SyncContactLabelsAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SyncContactLabels", deviceUuid); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 读取已落库的联系人标签字典。
        /// </summary>
        public async Task<List<ContactLabelDto>> GetContactLabelsAsync(string accountId, bool includeDeleted = false)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<List<ContactLabelDto>>("GetContactLabels", accountId, includeDeleted); }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WeChatService] GetContactLabelsAsync Failed: {ex.Message}");
                    return new List<ContactLabelDto>();
                }
            }

            return new List<ContactLabelDto>();
        }

        /// <summary>
        /// 调用 SignalR 创建或重命名联系人标签。
        /// </summary>
        public async Task<TaskResult> SaveContactLabelAsync(string deviceUuid, string labelName, int labelId = 0, string addList = "", string delList = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SaveContactLabel", deviceUuid, labelName, labelId, addList, delList); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 删除联系人标签。
        /// </summary>
        public async Task<TaskResult> DeleteContactLabelAsync(string deviceUuid, int labelId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("DeleteContactLabel", deviceUuid, labelId); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 设置单个好友的完整标签集合。
        /// </summary>
        public async Task<TaskResult> SetContactLabelsAsync(string deviceUuid, string friendId, IEnumerable<int>? labelIds)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SetContactLabels", deviceUuid, friendId, labelIds?.ToArray() ?? Array.Empty<int>()); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 请求客户端回传当前配置快照。
        /// </summary>
        public async Task<TaskResult> TriggerConfigPushAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("TriggerConfigPush", deviceUuid); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 下发 Android 设备级配置。
        /// </summary>
        public async Task<TaskResult> SetDeviceConfigAsync(string deviceUuid, DeviceConfigDto config)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SetDeviceConfig", deviceUuid, config); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 下发微信违禁词列表。
        /// </summary>
        public async Task<TaskResult> SetForbiddenWordAsync(string deviceUuid, IEnumerable<string>? words)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SetForbiddenWord", deviceUuid, words?.ToArray() ?? Array.Empty<string>()); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 拉取微信好友申请历史/补偿列表。
        /// </summary>
        public async Task<TaskResult> PullFriendAddReqListAsync(string deviceUuid, long startTime = 0, bool onlyNew = true, bool getAll = false)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("PullFriendAddReqList", deviceUuid, startTime, onlyNew, getAll); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 补偿单条聊天消息。
        /// </summary>
        public async Task<TaskResult> RequestTalkMsgAsync(string deviceUuid, long msgSvrId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("RequestTalkMsg", deviceUuid, msgSvrId); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 补偿原始聊天正文/XML。
        /// </summary>
        public async Task<TaskResult> RequestTalkContentAsync(string deviceUuid, long msgSvrId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("RequestTalkContent", deviceUuid, msgSvrId); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 补偿聊天消息详情。
        /// </summary>
        public async Task<TaskResult> RequestTalkDetailAsync(string deviceUuid, string friendId, long msgId, string msgSvrId = "", string md5 = "", bool getOriginal = false)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("RequestTalkDetail", deviceUuid, friendId, msgId, msgSvrId, md5, getOriginal); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 请求语音转文字。
        /// </summary>
        public async Task<TaskResult> VoiceTransTextAsync(string deviceUuid, string friendId, long msgSvrId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("VoiceTransText", deviceUuid, friendId, msgSvrId); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 执行 GetA8Key。
        /// </summary>
        public async Task<TaskResult> GetA8KeyAsync(string deviceUuid, int type, string url, string userName = "", string msgSvrId = "", int reason = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("GetA8Key", deviceUuid, type, url, userName, msgSvrId, reason); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 修改微信资料或隐私设置。
        /// </summary>
        public async Task<TaskResult> UpdateWechatSettingAsync(string deviceUuid, int action, string content = "", int intParam = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("UpdateWechatSetting", deviceUuid, action, content, intParam); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 请求撤回聊天消息。
        /// </summary>
        public async Task<TaskResult> RevokeMessageAsync(string deviceUuid, string friendId, long msgSvrId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("RevokeMessage", deviceUuid, friendId, msgSvrId); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 请求转发一条已有聊天消息。
        /// </summary>
        public async Task<TaskResult> ForwardMessageAsync(string deviceUuid, string talker, long msgSvrId, string friendIds, string extMsg = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("ForwardMessage", deviceUuid, talker, msgSvrId, friendIds, extMsg); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 请求转发多条已有聊天消息。
        /// </summary>
        public async Task<TaskResult> ForwardMultiMessageAsync(string deviceUuid, string talker, IEnumerable<long>? msgIds, string friendIds, string extMsg = "", bool sendRecord = false)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("ForwardMultiMessage", deviceUuid, talker, (msgIds ?? Enumerable.Empty<long>()).ToArray(), friendIds, extMsg, sendRecord); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 请求按原始内容转发消息。
        /// </summary>
        public async Task<TaskResult> ForwardMessageByContentAsync(string deviceUuid, string friendIds, long msgSvrId, int msgType, string content, string thumb = "", string extMsg = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("ForwardMessageByContent", deviceUuid, friendIds, msgSvrId, msgType, content, thumb, extMsg); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 请求清空微信端聊天记录。
        /// </summary>
        public async Task<TaskResult> ClearAllChatMsgAsync(string deviceUuid, int flag = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("ClearAllChatMsg", deviceUuid, flag); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 请求查询红包详情。
        /// </summary>
        public async Task<TaskResult> QueryHbDetailAsync(string deviceUuid, string hbUrl)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("QueryHbDetail", deviceUuid, hbUrl); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 请求查询红包状态。
        /// </summary>
        public async Task<TaskResult> QueryHbStatusAsync(string deviceUuid, string hbUrl)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("QueryHbStatus", deviceUuid, hbUrl); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 请求发送微信红包，金额单位为分。
        /// </summary>
        public async Task<TaskResult> SendLuckyMoneyAsync(string deviceUuid, string friendId, int money, int number, string passwd, string wish = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SendLuckyMoney", deviceUuid, friendId, money, number, passwd, wish); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 请求执行微信转账，金额单位为分。
        /// </summary>
        public async Task<TaskResult> RemittanceAsync(string deviceUuid, string friendId, int money, string passwd, string memo = "", string roomId = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("Remittance", deviceUuid, friendId, money, passwd, memo, roomId); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 请求微信账号登出。
        /// </summary>
        public async Task<TaskResult> WechatLogoutAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("WechatLogout", deviceUuid); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 请求 CDN 文件下载补偿。
        /// </summary>
        public async Task<TaskResult> DownloadCdnFileAsync(string deviceUuid, string cdnUrl, string cdnKey, int fileType, string fileId = "", string fileFmt = "", int fileSize = 0, long msgSvrId = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("DownloadCdnFile", deviceUuid, cdnUrl, cdnKey, fileType, fileId, fileFmt, fileSize, msgSvrId); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 启动好友检测/清粉任务。
        /// </summary>
        public async Task<TaskResult> StartFriendDetectAsync(string deviceUuid, string message, bool onlyCheck = true, int skipHour = 24, int mode = 0, int max = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("StartFriendDetect", deviceUuid, message, onlyCheck, skipHour, mode, max); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 停止好友检测/清粉任务。
        /// </summary>
        public async Task<TaskResult> StopFriendDetectAsync(string deviceUuid, long taskId = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("StopFriendDetect", deviceUuid, taskId); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 拉取好友检测/清粉最终结果。
        /// </summary>
        public async Task<TaskResult> GetFriendDetectResultAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("GetFriendDetectResult", deviceUuid); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 确认群邀请。
        /// </summary>
        public async Task<bool> ApproveChatRoomInviteAsync(string deviceUuid, long msgSvrId, string roomId = "", string msgContent = "", long msgId = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("ApproveChatRoomInvite", deviceUuid, msgSvrId, roomId, msgContent, msgId);
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
        /// 调用 SignalR 下发群接龙任务。
        /// </summary>
        public async Task<TaskResult> SendJielongAsync(string deviceUuid, string chatRoomId, string content, string title = "", string sample = "", string memo = "", long msgSvrId = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("SendJielong", deviceUuid, chatRoomId, content, title, sample, memo, msgSvrId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }


        public async Task<TaskResult> SphGetMentionAsync(string deviceUuid, long lastLikeId = 0, long lastCommentId = 0, long lastFollowId = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SphGetMention", deviceUuid, lastLikeId, lastCommentId, lastFollowId); }
                catch (Exception ex) { Console.WriteLine($"SignalR Invoke Failed: {ex.Message}"); return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SphGetCommentAsync(string deviceUuid, long feedId, string nonceId, string feedAuth, long refCommentId = 0, long replyCommentId = 0, int sortType = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SphGetComment", deviceUuid, feedId, nonceId, feedAuth, refCommentId, replyCommentId, sortType); }
                catch (Exception ex) { Console.WriteLine($"SignalR Invoke Failed: {ex.Message}"); return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SphUserPageAsync(string deviceUuid, string sphUserName)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SphUserPage", deviceUuid, sphUserName); }
                catch (Exception ex) { Console.WriteLine($"SignalR Invoke Failed: {ex.Message}"); return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SphPostAsync(string deviceUuid, string content, List<string> medias, int mediaType = 0, string cover = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SphPost", deviceUuid, content, medias, mediaType, cover); }
                catch (Exception ex) { Console.WriteLine($"SignalR Invoke Failed: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SphCommentAsync(string deviceUuid, long feedId, string nonceId, string feedAuth, int type, string content, string media = "", long replyCommentId = 0, string replyUsername = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SphComment", deviceUuid, feedId, nonceId, feedAuth, type, content, media, replyCommentId, replyUsername); }
                catch (Exception ex) { Console.WriteLine($"SignalR Invoke Failed: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SphLikeAsync(string deviceUuid, long feedId, int type = 1, bool isCancel = false)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SphLike", deviceUuid, feedId, type, isCancel); }
                catch (Exception ex) { Console.WriteLine($"SignalR Invoke Failed: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SphDelCommentAsync(string deviceUuid, long feedId, long commentId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SphDelComment", deviceUuid, feedId, commentId); }
                catch (Exception ex) { Console.WriteLine($"SignalR Invoke Failed: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SyncMomentsAsync(string deviceUuid, long startTime = 0, IEnumerable<long>? circleIds = null, string weChatId = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("SyncMoments", deviceUuid, startTime, (circleIds ?? Enumerable.Empty<long>()).ToArray(), weChatId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> OneKeyLikeMomentsAsync(string deviceUuid, int rate = 100, int num = 0, int endTime = 0, int timeOut = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("OneKeyLikeMoments", deviceUuid, rate, num, endTime, timeOut);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> DeleteMomentAsync(string deviceUuid, long circleId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("DeleteMoment", deviceUuid, circleId); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> LikeMomentAsync(string deviceUuid, long circleId, bool isCancel = false)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("LikeMoment", deviceUuid, circleId, isCancel); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> DeleteMomentCommentAsync(string deviceUuid, long circleId, long commentId, long publishTime)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("DeleteMomentComment", deviceUuid, circleId, commentId, publishTime); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> ReplyMomentCommentAsync(string deviceUuid, long circleId, string toWeChatId, string content, long replyCommentId, bool isResend = false)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("ReplyMomentComment", deviceUuid, circleId, toWeChatId, content, replyCommentId, isResend); }
                catch (Exception ex) { return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> PullFriendMomentsAsync(string deviceUuid, string friendId, long refSnsId = 0, int count = 20, long startTime = 0, long refTime = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("PullFriendMoments", deviceUuid, friendId, refSnsId, count, startTime, refTime); }
                catch (Exception ex) { Console.WriteLine($"SignalR Invoke Failed: {ex.Message}"); return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> PullMomentDetailAsync(string deviceUuid, long circleId, bool getBigMap = false)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("PullMomentDetail", deviceUuid, circleId, getBigMap); }
                catch (Exception ex) { Console.WriteLine($"SignalR Invoke Failed: {ex.Message}"); return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SyncMomentMessagesAsync(string deviceUuid, bool onlyComment = false, bool getAll = true)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("SyncMomentMessages", deviceUuid, onlyComment, getAll); }
                catch (Exception ex) { Console.WriteLine($"SignalR Invoke Failed: {ex.Message}"); return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> MarkMomentMessageReadAsync(string deviceUuid, long circleId, int commentId = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("MarkMomentMessageRead", deviceUuid, circleId, commentId); }
                catch (Exception ex) { Console.WriteLine($"SignalR Invoke Failed: {ex.Message}"); return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> ClearMomentMessageAsync(string deviceUuid, long circleId, int commentId = 0, bool isRead = true)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try { return await _hubConnection.InvokeAsync<TaskResult>("ClearMomentMessage", deviceUuid, circleId, commentId, isRead); }
                catch (Exception ex) { Console.WriteLine($"SignalR Invoke Failed: {ex.Message}"); return TaskResult.Fail($"SignalR Error: {ex.Message}"); }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SendMessageAsync(string deviceUuid, string friendWxId, string content, int type = 1, string atIds = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("SendMessage", deviceUuid, friendWxId, content, type, atIds);
                }
                catch (Exception ex)
                {
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }


        /// <summary>
        /// 调用 SignalR 发送微信群发任务。
        /// </summary>

        /// <summary>
        /// 调用 SignalR 下发群内加好友任务。
        /// </summary>
        public async Task<TaskResult> AddFriendInChatRoomAsync(string deviceUuid, string chatRoomId, string friendId, string message, string remark = "", int permission = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("AddFriendInChatRoom", deviceUuid, chatRoomId, friendId, message, remark, permission);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 下发场景加好友任务。
        /// <para>
        /// 参数顺序必须与 ClientHub.AddFriendWithScene 保持一致：
        /// message 是验证消息，remark 是添加后备注，label 是标签，verificationImagePath 是验证申请图片路径。
        /// </para>
        /// </summary>
        public async Task<TaskResult> AddFriendWithSceneAsync(
            string deviceUuid,
            string friendWxid,
            string message,
            string remark = "",
            string label = "",
            int scene = 3,
            int permission = 0,
            string verificationImagePath = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>(
                        "AddFriendWithScene",
                        deviceUuid,
                        friendWxid,
                        message,
                        remark,
                        label,
                        scene,
                        permission,
                        verificationImagePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }

            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 通过手机号添加好友。
        /// </summary>
        public async Task<TaskResult> AddFriendsByPhoneAsync(string deviceUuid, IEnumerable<string> phones, string message, string remark = "", string label = "", int permission = 0)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("AddFriendsByPhone", deviceUuid, phones?.ToArray() ?? Array.Empty<string>(), message, remark, label, permission);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }

            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 从通讯录添加好友。
        /// </summary>
        public async Task<TaskResult> AddFriendFromPhonebookAsync(string deviceUuid, string message, int count = 1, int index = 0, bool reset = false)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("AddFriendFromPhonebook", deviceUuid, message, count, index, reset);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }

            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 通过名片消息添加好友。
        /// </summary>
        public async Task<TaskResult> AddFriendNameCardAsync(string deviceUuid, long msgSvrId, string message, string remark = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("AddFriendNameCard", deviceUuid, msgSvrId, message, remark);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }

            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 重新发送好友验证。
        /// </summary>
        public async Task<TaskResult> SendFriendVerifyAsync(string deviceUuid, string friendId, string message)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("SendFriendVerify", deviceUuid, friendId, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }

            return TaskResult.Fail("SignalR not connected");
        }

        public async Task<TaskResult> SendGroupMessageAsync(string deviceUuid, List<string> friendIds, string content, int contentType = 0, int duration = 0, bool original = false)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("SendGroupMessage", deviceUuid, friendIds, content, contentType, duration, original);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 请求设备同步微信“群发助手”历史。
        /// </summary>
        public async Task<TaskResult> SyncMassSendHistoryAsync(string deviceUuid, long endTime = 0, string weChatId = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("SyncMassSendHistory", deviceUuid, endTime, weChatId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }

            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 读取群发助手历史。
        /// </summary>
        public async Task<IEnumerable<MassSendHistoryDto>> GetMassSendHistoryAsync(string accountId, int count = 50)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<IEnumerable<MassSendHistoryDto>>("GetMassSendHistory", accountId, count);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WeChatService] GetMassSendHistoryAsync Failed: {ex.Message}");
                }
            }

            return Enumerable.Empty<MassSendHistoryDto>();
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
        public async Task<TaskResult> DeleteFriendAsync(string deviceUuid, string friendId)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("DeleteFriend", deviceUuid, friendId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 设置好友权限。
        /// permissionMask：8=仅聊天，2=不让他看我朋友圈，1=不看他朋友圈。
        /// </summary>
        public async Task<TaskResult> SetFriendPermissionAsync(string deviceUuid, string friendId, int permissionMask)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("SetFriendPermission", deviceUuid, friendId, permissionMask);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 接受好友请求
        /// </summary>
        public async Task<TaskResult> AcceptFriendRequestAsync(
            string deviceUuid,
            string friendId,
            string friendNick,
            string remark = "",
            string replyMsg = "",
            bool addWithWW = false,
            bool onlyWW = false,
            int permission = 0,
            int operation = 1)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>(
                        "AcceptFriendRequest",
                        deviceUuid,
                        friendId,
                        friendNick,
                        remark,
                        replyMsg,
                        addWithWW,
                        onlyWW,
                        permission,
                        operation);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"SignalR Error: {ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR not connected");
        }

        /// <summary>
        /// 调用 SignalR 请求截屏
        /// </summary>
        public async Task<TaskResult> RequestScreenShotAsync(string deviceUuid)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("RequestScreenShot", deviceUuid);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR Invoke Failed: {ex.Message}");
                    return TaskResult.Fail($"截图请求失败：{ex.Message}");
                }
            }
            return TaskResult.Fail("SignalR 未连接");
        }

        /// <summary>
        /// 调用 SignalR 执行手机操作
        /// </summary>
        public async Task<bool> ExecutePhoneActionAsync(
            string deviceUuid,
            int action,
            string strParam = "",
            int intParam = 0,
            string weChatId = "",
            string imei = "")
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<bool>("ExecutePhoneAction", deviceUuid, action, strParam, intParam, weChatId, imei);
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

        /// <summary>
        /// 调用 SignalR 发送高级朋友圈协议任务。
        /// </summary>
        public async Task<TaskResult> PostMomentAdvancedAsync(string deviceUuid, MomentPostRequestDto request)
        {
            if (IsConnected && _hubConnection is not null)
            {
                try
                {
                    return await _hubConnection.InvokeAsync<TaskResult>("PostMomentAdvanced", deviceUuid, request);
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
        public object? data { get; set; }
    }
}
