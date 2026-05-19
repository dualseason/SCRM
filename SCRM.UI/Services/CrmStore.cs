using Microsoft.Extensions.Logging;





using SCRM.API.Models.Entities;





using SCRM.Shared.Interfaces;





using SCRM.SHARED.Models;





using SCRM.SHARED.Models.Dtos;
using SCRM.SHARED.Utils;





using SCRM.UI.Services.Data;





using System;





using System.Collections.Generic;





using System.Linq;





using System.Threading;





using System.Threading.Tasks;

using System.Text.Json;











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



        private static readonly JsonSerializerOptions MomentPostResultJsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };











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

        /// <summary>
        /// 当前会话的群成员缓存。
        /// <para>用于聊天页把群消息前缀 wxid 解析成群昵称；非群聊时为空。</para>
        /// </summary>
        public List<GroupMemberDto> CurrentGroupMembers { get; private set; } = new();





        





        // Active IM Tab Context





        public string ActiveImTab { get; set; } = "chats";











        // Additional Stubs





        public bool HasInitialized { get; set; } = true;





        public List<MomentsTimeline> CurrentMoments { get; private set; } = new();





        public FinderMentionNoticeDto? CurrentFinderMentions { get; private set; }





        public FinderUserPageDto? CurrentFinderUserPage { get; private set; }





        public FinderCommentListDto? CurrentFinderComments { get; private set; }





        public IReadOnlyList<FinderMentionNoticeDto> SelectedFinderMentionHistory => GetSelectedFinderHistory(_finderMentionHistoryByDevice);





        public IReadOnlyList<FinderUserPageDto> SelectedFinderUserPageHistory => GetSelectedFinderHistory(_finderUserPageHistoryByDevice);





        public IReadOnlyList<FinderCommentListDto> SelectedFinderCommentHistory => GetSelectedFinderHistory(_finderCommentHistoryByDevice);





        public string? LastScreenShotUrl { get; set; }

        /// <summary>
        /// 最近一次截图所属设备。
        /// <para>用于避免切换设备后仍展示上一台设备的旧截图。</para>
        /// </summary>
        public string? LastScreenShotDeviceUuid { get; private set; }

        /// <summary>
        /// 最近一次截图状态文案。
        /// </summary>
        public string? LastScreenShotStatusMessage { get; private set; }

        /// <summary>
        /// 最近一次截图是否成功。
        /// </summary>
        public bool? LastScreenShotSuccess { get; private set; }

        /// <summary>
        /// 最近一次截图状态更新时间。
        /// </summary>
        public DateTimeOffset? LastScreenShotUpdatedAt { get; private set; }

        /// <summary>
        /// 最近一次截图的短期预览 URL。
        /// <para>页面预览截图时只绑定该短期 URL，不直接绑定 LastScreenShotUrl。</para>
        /// </summary>
        public string? LastScreenShotAccessUrl { get; private set; }

        /// <summary>
        /// 最近一次截图短期预览 URL 的过期时间。
        /// </summary>
        public DateTimeOffset? LastScreenShotAccessExpiresAt { get; private set; }

        /// <summary>
        /// 是否正在为最近一次截图生成短期预览 URL。
        /// </summary>
        public bool IsPreparingScreenShotAccess { get; private set; }

        /// <summary>
        /// 最近一次朋友圈同步所属设备。
        /// </summary>
        public string? LastMomentsSyncDeviceUuid { get; private set; }

        /// <summary>
        /// 最近一次朋友圈同步任务ID。
        /// </summary>
        public long LastMomentsSyncTaskId { get; private set; }

        /// <summary>
        /// 最近一次朋友圈同步状态文案。
        /// </summary>
        public string? LastMomentsSyncMessage { get; private set; }

        /// <summary>
        /// 最近一次朋友圈同步是否成功。
        /// </summary>
        public bool? LastMomentsSyncSuccess { get; private set; }

        /// <summary>
        /// 最近一次朋友圈同步状态更新时间。
        /// </summary>
        public DateTimeOffset? LastMomentsSyncUpdatedAt { get; private set; }

        /// <summary>
        /// 最近一次朋友圈发布所属设备。
        /// </summary>
        public string? LastMomentPostDeviceUuid { get; private set; }

        /// <summary>
        /// 最近一次朋友圈发布任务 ID。
        /// </summary>
        public long LastMomentPostTaskId { get; private set; }

        /// <summary>
        /// 最近一次朋友圈发布 ClientRequestId。
        /// </summary>
        public string? LastMomentPostClientRequestId { get; private set; }

        /// <summary>
        /// 最近一次朋友圈发布状态。
        /// </summary>
        public string? LastMomentPostStatus { get; private set; }

        /// <summary>
        /// 最近一次朋友圈发布状态文案。
        /// </summary>
        public string? LastMomentPostMessage { get; private set; }

        /// <summary>
        /// 最近一次朋友圈发布是否成功；null 表示已下发但仍在等待或校准。
        /// </summary>
        public bool? LastMomentPostSuccess { get; private set; }

        /// <summary>
        /// 最近一次朋友圈发布回传的 CircleId。
        /// </summary>
        public long LastMomentPostCircleId { get; private set; }

        /// <summary>
        /// 最近一次朋友圈发布结果是否为迟到回包。
        /// </summary>
        public bool LastMomentPostIsLate { get; private set; }

        /// <summary>
        /// 最近一次朋友圈发布成功后是否需要继续同步校准。
        /// </summary>
        public bool LastMomentPostNeedSync { get; private set; }

        /// <summary>
        /// 最近一次朋友圈发布附件类型。
        /// </summary>
        public string? LastMomentPostAttachmentType { get; private set; }

        /// <summary>
        /// 最近一次朋友圈发布附件数量。
        /// </summary>
        public int LastMomentPostAttachmentCount { get; private set; }

        /// <summary>
        /// 最近一次朋友圈发布更新时间。
        /// </summary>
        public DateTimeOffset? LastMomentPostUpdatedAt { get; private set; }











        // Contacts reload concurrency control





        private readonly SemaphoreSlim _contactsReloadLock = new(1, 1);

        private readonly SemaphoreSlim _momentsReloadLock = new(1, 1);

        private readonly SemaphoreSlim _finderHistoryLoadLock = new(1, 1);

        /// <summary>
        /// 会话列表刷新串行锁。
        /// <para>SignalR 会话更新、发消息后刷新、延迟兜底刷新可能同时触发，串行化避免同一个 scoped DbContext 并发查询。</para>
        /// </summary>
        private readonly SemaphoreSlim _conversationsReloadLock = new(1, 1);

        /// <summary>
        /// 当前会话消息刷新串行锁。
        /// <para>发送媒体、SignalR 实时消息、手动刷新可能同时查询消息列表，串行化避免同一个 scoped DbContext 并发使用。</para>
        /// </summary>
        private readonly SemaphoreSlim _messagesReloadLock = new(1, 1);

        /// <summary>
        /// 设备列表刷新串行锁。
        /// <para>上线通知、设备状态事件、页面初始化可能同时触发 LoadDevicesAsync；串行化避免同一个 scoped CrmService/DbContext 并发查询。</para>
        /// </summary>
        private readonly SemaphoreSlim _devicesReloadLock = new(1, 1);

        /// <summary>
        /// 服务调用串行锁。
        /// <para>当前 CrmStore 为 scoped，_service 底层共享同一个 DbContext；不同 UI 事件同时查询会触发 EF Core 并发异常。
        /// 这里仅串行化读取/下发调用，不改变数据库持久化路径，持久化仍由服务端 DbHelper 负责。</para>
        /// </summary>
        private readonly SemaphoreSlim _serviceCallLock = new(1, 1);





        private CancellationTokenSource? _contactsReloadCts;

        /// <summary>
        /// 朋友圈列表防抖刷新版本号。
        /// <para>多条实时推送连续到达时只保留最后一次刷新，避免反复取消 Task.Delay 产生调试器一阶 TaskCanceledException 噪声。</para>
        /// </summary>
        private long _momentsReloadVersion;





        private readonly object _contactsReloadSync = new();

        private readonly object _momentsReloadSync = new();


        /// <summary>
        /// 朋友圈互动本地待确认状态锁。
        /// <para>点赞/评论任务成功回执通常早于朋友圈详情快照更新；刷新列表时需要合并这些本地互动，避免旧快照把页面刚显示的点赞、评论覆盖掉。</para>
        /// </summary>
        private readonly object _pendingMomentInteractionSync = new();

        /// <summary>
        /// 朋友圈点赞待确认缓存，外层 Key 为朋友圈 snsId，内层 Key 为点赞人 wxid。
        /// </summary>
        private readonly Dictionary<long, Dictionary<string, PendingMomentLikeUpdate>> _pendingMomentLikesByCircleId = new();

        /// <summary>
        /// 朋友圈评论待确认缓存，Key 为朋友圈 snsId。
        /// </summary>
        private readonly Dictionary<long, List<PendingMomentCommentUpdate>> _pendingMomentCommentsByCircleId = new();




        private readonly TimeSpan _contactsDebounceWindow = TimeSpan.FromMilliseconds(300);

        private readonly TimeSpan _momentsDebounceWindow = TimeSpan.FromMilliseconds(800);

        /// <summary>
        /// 朋友圈本地互动最多保留时间。
        /// <para>超过该时间仍未被服务端快照确认时，认为微信侧没有继续回推，避免临时状态永久残留。</para>
        /// </summary>
        private static readonly TimeSpan MomentInteractionPendingTtl = TimeSpan.FromMinutes(30);

        /// <summary>
        /// 最近一次收到服务端朋友圈实时推送的时间。
        /// <para>如果点赞/评论结果已经由服务端主动推送，短时间内的自动回拉就不再重复执行，避免旧快照覆盖刚收到的互动状态。</para>
        /// </summary>
        private DateTimeOffset _lastMomentRealtimePushAt = DateTimeOffset.MinValue;

        /// <summary>
        /// 判断最近多久内收到过朋友圈实时推送。
        /// </summary>
        private bool HasRecentMomentRealtimePush(TimeSpan window)
        {
            return DateTimeOffset.UtcNow - _lastMomentRealtimePushAt <= window;
        }

        /// <summary>
        /// 单条朋友圈点赞待确认记录。
        /// </summary>
        private sealed class PendingMomentLikeUpdate
        {
            /// <summary>
            /// 点赞展示数据。
            /// </summary>
            public MomentLikeDto Like { get; init; } = new();

            /// <summary>
            /// 是否为取消点赞。
            /// </summary>
            public bool IsCancel { get; init; }

            /// <summary>
            /// 过期时间。
            /// </summary>
            public DateTimeOffset ExpiresAt { get; init; }
        }

        /// <summary>
        /// 单条朋友圈评论待确认记录。
        /// </summary>
        private sealed class PendingMomentCommentUpdate
        {
            /// <summary>
            /// 评论展示数据。
            /// </summary>
            public MomentCommentDto Comment { get; init; } = new();

            /// <summary>
            /// 过期时间。
            /// </summary>
            public DateTimeOffset ExpiresAt { get; init; }
        }





        private volatile bool _isDisposed;

        /// <summary>
        /// 最近一次前端主动下发但还在等待异步回执的聊天任务。
        /// <para>SignalR Invoke 返回下发结果时不会自动推送 OnTaskResult；这里记录 TaskId，用于主动安排当前会话延迟刷新。</para>
        /// </summary>
        private readonly Dictionary<long, string> _pendingChatSendContentHints = new();





        private const int FinderHistoryLimitPerDevice = 10;





        private readonly Dictionary<string, List<FinderMentionNoticeDto>> _finderMentionHistoryByDevice = new(StringComparer.OrdinalIgnoreCase);





        private readonly Dictionary<string, List<FinderUserPageDto>> _finderUserPageHistoryByDevice = new(StringComparer.OrdinalIgnoreCase);





        private readonly Dictionary<string, List<FinderCommentListDto>> _finderCommentHistoryByDevice = new(StringComparer.OrdinalIgnoreCase);

        private readonly WeChatService? _realTimeService;











        // --- Events ---





        public event Action? OnChange;





        public event Action<Message>? MessageReceived; 





        public event Action<string, bool>? OnNotification;

        /// <summary>
        /// 异步任务结果事件。
        /// <para>供群发页等需要把异步回执落到局部表格的页面使用。</para>
        /// </summary>
        public event Action<TaskResultDto>? OnAsyncTaskResultReceived;

        /// <summary>
        /// 好友请求列表更新事件。
        /// <para>FriendRequests 页面使用该事件做精确刷新，避免所有 OnChange 都触发数据库查询。</para>
        /// </summary>
        public event Action<string>? OnFriendRequestsUpdated;

        /// <summary>
        /// 群邀请列表更新事件。
        /// <para>群邀请审批页面使用该事件精确刷新列表。</para>
        /// </summary>
        public event Action<string>? OnGroupInvitationsUpdated;

        /// <summary>
        /// 联系人标签列表更新事件。
        /// <para>标签管理页面使用该事件在手机端异步 Notice 落库后重新查询 ContactTags。</para>
        /// </summary>
        public event Action<string>? OnContactLabelsUpdated;

        /// <summary>
        /// 手机短信记录更新事件。
        /// <para>手机管理页面使用该事件在 SmsPush/PullSmsResult 落库后重新查询短信列表。</para>
        /// </summary>
        public event Action<string>? OnSmsRecordsUpdated;

        /// <summary>
        /// 手机通话记录更新事件。
        /// <para>手机管理页面使用该事件在 CallLogPush/PullCallLogResult 落库后重新查询通话列表。</para>
        /// </summary>
        public event Action<string>? OnCallLogRecordsUpdated;











        public CrmStore(





            ICrmService service, 





            ICrmEvents events, 





 





            ILogger<CrmStore> logger)





        {





            _service = service;





            _events = events;





            _logger = logger;

            _realTimeService = service as WeChatService;
            if (_realTimeService != null)
            {
                _realTimeService.OnRealtimeDataChanged += HandleRealtimeDataChanged;
                _realTimeService.OnTaskResultReceived += HandleTaskResultReceived;
                _realTimeService.OnFriendRequestsUpdated += HandleFriendRequestsUpdated;
                _realTimeService.OnGroupInvitationsUpdated += HandleGroupInvitationsUpdated;
                _realTimeService.OnContactLabelsUpdated += HandleContactLabelsUpdated;
                _realTimeService.OnSmsRecordsUpdated += HandleSmsRecordsUpdated;
                _realTimeService.OnCallLogRecordsUpdated += HandleCallLogRecordsUpdated;
            }











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
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(3));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(10));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(30));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(60));





            }));

            _subscriptions.Add(_events.SubscribeToConversationsUpdated((accountId) =>
            {
                _logger.LogInformation("[CrmStore] Received Conversations Update for Account {AccountId}", accountId);
                QueueConversationsReload(accountId);
                ScheduleConversationsReload(accountId, TimeSpan.FromSeconds(2));
                ScheduleConversationsReload(accountId, TimeSpan.FromSeconds(6));

                // 单条消息删除、会话快照更新等事件可能不会产生新的 ReceiveMessage；
                // 如果当前正打开同一账号的会话，顺手刷新消息列表，避免已删除消息继续停留在页面内存中。
                if (SelectedConversation != null
                    && string.Equals(SelectedConversation.wechatAccountId, accountId, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(SelectedConversation.conversationWxid))
                {
                    _ = ReloadCurrentMessagesAsync(accountId, SelectedConversation.conversationWxid);
                }
            }));

            _subscriptions.Add(_events.SubscribeToFriendRequestsUpdated((accountId) =>
            {
                HandleFriendRequestsUpdated(accountId);
            }));

            _subscriptions.Add(_events.SubscribeToGroupInvitationsUpdated((accountId) =>
            {
                HandleGroupInvitationsUpdated(accountId);
            }));

            _subscriptions.Add(_events.SubscribeToContactLabelsUpdated((accountId) =>
            {
                HandleContactLabelsUpdated(accountId);
            }));

            _subscriptions.Add(_events.SubscribeToSmsRecordsUpdated((accountId) =>
            {
                HandleSmsRecordsUpdated(accountId);
            }));

            _subscriptions.Add(_events.SubscribeToCallLogRecordsUpdated((accountId) =>
            {
                HandleCallLogRecordsUpdated(accountId);
            }));

            _subscriptions.Add(_events.SubscribeToTaskResults((taskResult) =>
            {
                if (taskResult == null)
                {
                    return;
                }

                HandleTaskResultReceived(new TaskResultDto
                {
                    taskId = taskResult.taskId,
                    success = taskResult.success,
                    message = taskResult.message,
                    deviceUuid = taskResult.deviceUuid,
                    data = taskResult.data
                });
            }));

            _subscriptions.Add(_events.SubscribeToEvent<RealtimeDataChangedNoticeDto>("MomentTimelineChanged", HandleRealtimeDataChanged));











            // [New] Subscribe to Screenshot Uploaded Event





            _subscriptions.Add(_events.SubscribeToEvent<SCRM.SHARED.Models.Events.ScreenShotUploadedEvent>("OnScreenShotUploaded", (screenshotEvent) =>





            {
                if (screenshotEvent == null || string.IsNullOrWhiteSpace(screenshotEvent.Url))
                {
                    return;
                }

                var targetDeviceUuid = screenshotEvent.DeviceUuid ?? string.Empty;
                if (SelectedDevice != null
                    && !string.IsNullOrWhiteSpace(targetDeviceUuid)
                    && !string.Equals(SelectedDevice.uuid, targetDeviceUuid, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(
                        "[CrmStore] 跳过非当前设备截图：SelectedDevice={SelectedDeviceUuid}, EventDevice={EventDeviceUuid}",
                        SelectedDevice.uuid,
                        targetDeviceUuid);
                    return;
                }





                _logger.LogInformation(
                    "[CrmStore] Received Screenshot: DeviceUuid={DeviceUuid}, Url={Url}",
                    targetDeviceUuid,
                    screenshotEvent.Url);





                UpdateScreenShotStatus(targetDeviceUuid, true, "截图已上传，预览链接已更新", screenshotEvent.Url);
                OnNotification?.Invoke("截图已上传，预览链接已更新", true);





                NotifyStateChanged();





            }));











            // [New] Subscribe to WeChat Online Event





            _subscriptions.Add(_events.SubscribeToEvent<SCRM.SHARED.Models.Events.WeChatOnlineEvent>("OnWeChatOnline", (e) =>





            {





                _logger.LogInformation($"[CrmStore] WeChat Online: {e.nickName} ({e.weChatId})");

                ApplyWeChatOnlineSnapshot(e);

                OnNotification?.Invoke($"微信已上线: {e.nickName}", true);

                var accountId = FirstNonEmptyLocal(e.weChatId, e.accountId);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await LoadDevicesAsync().ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(accountId))
                        {
                            QueueContactsReload(accountId);
                            QueueConversationsReload(accountId);
                            ScheduleContactsReload(accountId, TimeSpan.FromSeconds(3));
                            ScheduleConversationsReload(accountId, TimeSpan.FromSeconds(3));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[CrmStore] 微信上线后刷新设备/联系人/会话失败：AccountId={AccountId}", accountId);
                    }
                });





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
                NormalizeRealtimePrivateMessageContent(msg);
                _logger.LogInformation($"[CrmStore] Message Received from {msg.senderWxid} to {msg.receiverWxid}: {msg.content}");
                var conversationWxid = ResolveConversationWxidFromMessage(msg);
                var isKnownMessageUpdate = CurrentMessages.Any(existing => IsSameMessageIdentity(existing, msg));
                UpsertRealtimeConversation(msg, conversationWxid, incrementMessageCount: !isKnownMessageUpdate);

                // 如果消息属于当前打开的会话/联系人，直接追加到当前消息列表。
                bool isRelevantToConversation = SelectedConversation != null
                    && IsMessageBelongsToConversation(msg, SelectedConversation.conversationWxid);

                bool isRelevantToContact = SelectedContact != null
                    && IsMessageBelongsToConversation(msg, SelectedContact.wxid);

                if (isRelevantToConversation || isRelevantToContact)
                {
                    var snapshot = CurrentMessages.ToList();
                    var existingIndex = snapshot.FindIndex(existing => IsSameMessageIdentity(existing, msg));
                    if (existingIndex >= 0)
                    {
                        snapshot[existingIndex] = MergeRealtimeMessage(snapshot[existingIndex], msg);
                    }
                    else
                    {
                        snapshot.Add(msg);
                    }

                    CurrentMessages = snapshot
                        .OrderBy(m => m.sentAt ?? m.receivedAt ?? m.createdAt)
                        .ThenBy(m => m.messageId)
                        .ToList();
                }

                // Generic message event for decoupled consumers
                MessageReceived?.Invoke(msg);
                NotifyStateChanged();
            }));





            _subscriptions.Add(_events.SubscribeToEvent<RealtimeDataChangedNoticeDto>("FinderResultChanged", HandleRealtimeDataChanged));





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
                await ReloadContactsNowAsync(accountId, token);
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





        /// <summary>
        /// 安排一次不取消前序请求的联系人延迟刷新。
        /// <para>好友通过验证、删除好友和多次 FriendPushNotice 会连续到达；这里不共用防抖 CTS，避免后来的通知取消前面的兜底刷新。</para>
        /// </summary>
        private void ScheduleContactsReload(string accountId, TimeSpan delay)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(accountId))
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                    if (_isDisposed)
                    {
                        return;
                    }

                    await ReloadContactsNowAsync(accountId, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[CrmStore] 延迟刷新联系人列表时出现可忽略异常，Account={AccountId}", accountId);
                }
            });
        }

        /// <summary>
        /// 立即从服务端读取联系人并回填当前设备。
        /// <para>联系人持久化仍由服务端 DbHelper.SaveContacts/MarkContactDeleted 负责；前端只读取并刷新内存展示。</para>
        /// </summary>
        private async Task ReloadContactsNowAsync(string accountId, CancellationToken token = default)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(accountId))
            {
                return;
            }

            var entered = false;
            try
            {
                await _contactsReloadLock.WaitAsync(token).ConfigureAwait(false);
                entered = true;

                if (_isDisposed || token.IsCancellationRequested)
                {
                    return;
                }

                token.ThrowIfCancellationRequested();
                if (!IsSelectedDeviceForAccount(accountId))
                {
                    _logger.LogInformation(
                        "[CrmStore] 跳过联系人刷新：当前设备账号 {CurrentAccountId} 与事件账号 {AccountId} 不一致",
                        SelectedDevice?.weChatId ?? string.Empty,
                        accountId);
                    return;
                }

                if (SelectedDevice == null)
                {
                    return;
                }

                if (SelectedDevice.wx == null)
                {
                    SelectedDevice.wx = new Wx { srClient = SelectedDevice };
                }

                token.ThrowIfCancellationRequested();
                var contacts = await ExecuteServiceCallAsync(() => _service.GetContactsAsync(accountId)).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                SelectedDevice.wx.contacts = contacts;
                Contacts = contacts;

                NotifyStateChanged();
            }
            finally
            {
                if (entered)
                {
                    _contactsReloadLock.Release();
                }
            }
        }

        /// <summary>
        /// 判断当前设备是否对应指定微信账号。
        /// </summary>
        private bool IsSelectedDeviceForAccount(string accountId)
        {
            if (SelectedDevice == null || string.IsNullOrWhiteSpace(accountId))
            {
                return false;
            }

            var deviceFromList = Devices.FirstOrDefault(item =>
                string.Equals(item.uuid, SelectedDevice.uuid, StringComparison.OrdinalIgnoreCase));

            var currentAccountId = FirstNonEmptyLocal(
                SelectedDevice.weChatId,
                SelectedDevice.wx?.wechatAccount?.wxid,
                deviceFromList?.weChatId,
                deviceFromList?.wx?.wechatAccount?.wxid);

            return string.Equals(currentAccountId, accountId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(SelectedDevice.uuid, accountId, StringComparison.OrdinalIgnoreCase);
        }

        public async Task InitializeAsync()





        {





             await LoadDevicesAsync();





        }
        public async Task LoadDevicesAsync()
        {
            var entered = false;
            try
            {
                entered = await _devicesReloadLock.WaitAsync(0);
                if (!entered)
                {
                    _logger.LogDebug("[CrmStore] 跳过设备列表刷新：已有刷新正在进行");
                    return;
                }

                try
                {
                    Devices = await ExecuteServiceCallAsync(() => _service.GetDevicesAsync());

                    // 刷新当前选中设备引用，避免上线事件后仍拿旧对象判断微信登录状态。
                    if (SelectedDevice != null)
                    {
                        var previousDevice = SelectedDevice;
                        var freshDevice = Devices.FirstOrDefault(d => d.uuid == previousDevice.uuid);
                        if (freshDevice != null)
                        {
                            PreserveWechatSnapshotIfMissing(freshDevice, previousDevice);
                            SelectedDevice = freshDevice;
                        }
                    }

                    // IM 首次进入或当前设备未就绪时，优先选中在线且已登录微信的设备，避免默认选到未登录设备后群聊/会话为空。
                    ResolveReadyWechatDevice(updateSelection: true);

                    _logger.LogInformation("[CrmStore] Loaded {Count} devices via ICrmService.", Devices.Count);
                    NotifyStateChanged();
                }
                finally
                {
                    if (entered)
                    {
                        _devicesReloadLock.Release();
                    }
                }
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogDebug(ex, "[CrmStore] LoadDevicesAsync canceled after disposal.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load devices.");
            }
        }

        /// <summary>
        /// 刷新设备列表时保留旧微信账号快照。
        /// <para>服务端上线事件和账号落库有时存在短暂时序差；如果新设备对象暂时没有 wx，会导致 UI 闪成“微信未登录”。</para>
        /// </summary>
        private static void PreserveWechatSnapshotIfMissing(SrClient freshDevice, SrClient previousDevice)
        {
            if (freshDevice.wx?.wechatAccount != null || previousDevice.wx?.wechatAccount == null)
            {
                return;
            }

            freshDevice.wx = new Wx
            {
                srClient = freshDevice,
                wechatAccount = previousDevice.wx.wechatAccount,
                contacts = previousDevice.wx.contacts
            };
        }

        /// <summary>
        /// 应用微信上线事件的前端内存快照。
        /// <para>这里只修正 Web 端当前设备展示和后续刷新判断，不直接写数据库；真实账号/设备持久化仍由服务端事件处理与 DbHelper 完成。</para>
        /// </summary>
        private void ApplyWeChatOnlineSnapshot(SCRM.SHARED.Models.Events.WeChatOnlineEvent e)
        {
            if (e == null)
            {
                return;
            }

            var accountId = FirstNonEmptyLocal(e.weChatId, e.accountId);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return;
            }

            var device = Devices.FirstOrDefault(item =>
                string.Equals(item.uuid, e.deviceUuid, StringComparison.OrdinalIgnoreCase));

            if (device == null
                && SelectedDevice != null
                && string.Equals(SelectedDevice.uuid, e.deviceUuid, StringComparison.OrdinalIgnoreCase))
            {
                device = SelectedDevice;
            }

            if (device == null)
            {
                return;
            }

            EnsureWechatSnapshot(device, accountId, e.nickName, e.deviceUuid, e.ownerId);

            if (SelectedDevice == null
                || string.Equals(SelectedDevice.uuid, device.uuid, StringComparison.OrdinalIgnoreCase))
            {
                SelectedDevice = device;
                RestoreFinderViewStateForSelectedDevice();
            }

            NotifyStateChanged();
        }

        /// <summary>
        /// 确保设备对象带有当前微信账号快照。
        /// </summary>
        private static void EnsureWechatSnapshot(SrClient device, string accountId, string? nickname, string? deviceUuid, string? ownerId)
        {
            device.wx ??= new Wx { srClient = device };
            device.wx.srClient ??= device;
            device.wx.wechatAccount ??= new WechatAccount();

            var account = device.wx.wechatAccount;
            account.wxid = accountId;
            account.clientUuid = FirstNonEmptyLocal(deviceUuid, device.uuid, account.clientUuid);
            account.ownerId = FirstNonEmptyLocal(ownerId, account.ownerId);
            account.nickname = FirstNonEmptyLocal(nickname, account.nickname, accountId);
            account.accountStatus = 1;
            account.isDeleted = false;
            account.lastOnlineAt = DateTime.UtcNow;
            account.updatedAt = DateTime.UtcNow;
            account.Client ??= device;

            device.isOnline = true;
            device.lastLoginAt = DateTime.UtcNow;
            device.updatedAt = DateTime.UtcNow;
            if (!device.loggedInWeChatIds.Any(id => string.Equals(id, accountId, StringComparison.OrdinalIgnoreCase)))
            {
                device.loggedInWeChatIds.Add(accountId);
            }
        }
        public async Task SelectDeviceAsync(SrClient device)





        {
            var previousDeviceUuid = SelectedDevice?.uuid ?? string.Empty;





            // 1. 设置当前选中设备





            SelectedDevice = device;

            if (!string.IsNullOrWhiteSpace(previousDeviceUuid)
                && !string.Equals(previousDeviceUuid, SelectedDevice?.uuid, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(LastScreenShotDeviceUuid)
                && !string.Equals(LastScreenShotDeviceUuid, SelectedDevice?.uuid, StringComparison.OrdinalIgnoreCase))
            {
                ClearScreenShotState();
            }

            if (!string.IsNullOrWhiteSpace(previousDeviceUuid)
                && !string.Equals(previousDeviceUuid, SelectedDevice?.uuid, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(LastMomentsSyncDeviceUuid)
                && !string.Equals(LastMomentsSyncDeviceUuid, SelectedDevice?.uuid, StringComparison.OrdinalIgnoreCase))
            {
                ClearMomentsSyncState();
            }

            if (!string.IsNullOrWhiteSpace(previousDeviceUuid)
                && !string.Equals(previousDeviceUuid, SelectedDevice?.uuid, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(LastMomentPostDeviceUuid)
                && !string.Equals(LastMomentPostDeviceUuid, SelectedDevice?.uuid, StringComparison.OrdinalIgnoreCase))
            {
                ClearMomentPostState();
            }





            // 2. 初始化 wx 对象 (如果为空)





            if (SelectedDevice.wx == null)





            {





                SelectedDevice.wx = new Wx();





            }











            // 3. 【关键】按需加载联系人





            // 如果联系人列表是空的，并且设备已登录(有WeChatId)，则去服务器拉取





            SelectedConversation = null;





            SelectedContact = null;





            CurrentMessages = new List<Message>();

            CurrentGroupMembers = new List<GroupMemberDto>();





            Conversations = new List<Conversation>();





            Contacts = new List<Contact>();






            var selectedAccountId = FirstNonEmptyLocal(
                device.weChatId,
                device.wx?.wechatAccount?.wxid,
                SelectedDevice.weChatId,
                SelectedDevice.wx?.wechatAccount?.wxid);

            RestoreFinderViewStateForSelectedDevice();











            if ((SelectedDevice.wx.contacts == null || !SelectedDevice.wx.contacts.Any())





                && !string.IsNullOrEmpty(selectedAccountId))





            {





                NotifyStateChanged(); // 先通知UI显示"加载中"状态（可选）











                try





                {





                    // 调用 Service 从 API 获取联系人





                    var contacts = await ExecuteServiceCallAsync(() => _service.GetContactsAsync(selectedAccountId));





                    SelectedDevice.wx.contacts = contacts;





                    Contacts = contacts;





                }





                catch (Exception ex)





                {





                    _logger.LogError(ex, "Failed to load contacts for device {Uuid}", device.uuid);





                }





            }











            await LoadFinderHistoryForSelectedDeviceAsync(device.uuid, selectedAccountId);

            if (!string.IsNullOrWhiteSpace(selectedAccountId))
            {
                try
                {
                    await LoadConversationsAsync(selectedAccountId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load conversations for device {Uuid}, account {AccountId}", device.uuid, selectedAccountId);
                }
            }





            // 4. 通知 UI 渲染数据





            NotifyStateChanged();





            //return Task.CompletedTask;





        }











        private void HandleRealtimeDataChanged(RealtimeDataChangedNoticeDto dto)
        {
            if (_isDisposed || dto == null)
            {
                return;
            }

            if (SelectedDevice == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(dto.deviceUuid))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(dto.deviceUuid)
                && !string.Equals(SelectedDevice.uuid, dto.deviceUuid, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var scope = dto.scope?.Trim() ?? string.Empty;
            if (string.Equals(scope, "moments", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("[CrmStore] 收到朋友圈安全变更通知，准备重拉设备 {DeviceUuid} 的脱敏朋友圈列表", SelectedDevice.uuid);
                _lastMomentRealtimePushAt = DateTimeOffset.UtcNow;
                QueueMomentsReload(dto.deviceUuid);
                NotifyStateChanged();
                return;
            }

            if (string.Equals(scope, "finder", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "[CrmStore] 收到视频号安全变更通知，准备重拉设备 {DeviceUuid} 的脱敏视频号历史。ChangeType={ChangeType}, Count={Count}",
                    SelectedDevice.uuid,
                    dto.changeType,
                    dto.itemCount);

                OnNotification?.Invoke(
                    string.IsNullOrWhiteSpace(dto.summary) ? "视频号结果已更新，正在刷新历史" : dto.summary,
                    dto.success);
                _ = ReloadFinderHistoryForSelectedDeviceAsync();
                NotifyStateChanged();
            }
        }

        /// <summary>
        /// 处理异步任务结果回执。
        /// 2026-05-12：补上 UI 侧消费，避免截图失败、朋友圈返回 0 条等结果只停留在日志里。
        /// </summary>
        private void HandleTaskResultReceived(TaskResultDto dto)
        {
            if (_isDisposed || dto == null)
            {
                return;
            }

            if (SelectedDevice == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(dto.deviceUuid)
                && !string.Equals(SelectedDevice.uuid, dto.deviceUuid, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var message = string.IsNullOrWhiteSpace(dto.message)
                ? (dto.success ? "任务已完成" : "任务执行失败")
                : dto.message.Trim();
            var hasMomentPostResult = TryReadMomentPostResult(dto.data, out var momentPostResult)
                || TryExtractMomentPostResultFromMessage(message, out momentPostResult);
            if (hasMomentPostResult && momentPostResult != null)
            {
                message = StripResultDataJson(message);
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = momentPostResult.success
                        ? "朋友圈发布成功，等待同步校准"
                        : "朋友圈发布失败";
                }
            }

            var contentHint = _pendingChatSendContentHints.Remove(dto.taskId, out var pendingHint)
                ? pendingHint
                : string.Empty;

            _logger.LogInformation(
                "[CrmStore] 收到异步任务结果: TaskId={TaskId}, Success={Success}, DeviceUuid={DeviceUuid}, Message={Message}",
                dto.taskId,
                dto.success,
                dto.deviceUuid,
                message);

            var shouldRefreshState = false;
            if (hasMomentPostResult && momentPostResult != null)
            {
                UpdateMomentPostStatus(
                    dto.deviceUuid,
                    dto.taskId,
                    momentPostResult.success,
                    message,
                    momentPostResult.status,
                    momentPostResult);

                if (momentPostResult.success || momentPostResult.needSync)
                {
                    ScheduleMomentPostRefresh(dto.deviceUuid, momentPostResult);
                }

                shouldRefreshState = true;
            }
            if (message.Contains("截图", StringComparison.OrdinalIgnoreCase))
            {
                UpdateScreenShotStatus(dto.deviceUuid, dto.success, message, null, clearPreviewOnFailure: !dto.success);
                shouldRefreshState = true;
            }
            if (message.Contains("朋友圈同步", StringComparison.OrdinalIgnoreCase))
            {
                UpdateMomentsSyncStatus(dto.deviceUuid, dto.taskId, dto.success, message);
                if (dto.success)
                {
                    QueueMomentsReload(dto.deviceUuid);
                    ScheduleMomentsReload(dto.deviceUuid, TimeSpan.FromSeconds(2));
                    ScheduleMomentsReload(dto.deviceUuid, TimeSpan.FromSeconds(8));
                    ScheduleMomentsReload(dto.deviceUuid, TimeSpan.FromSeconds(20));
                }
                shouldRefreshState = true;
            }
            if (IsMomentsInteractionTaskResult(message))
            {
                QueueMomentsReload(dto.deviceUuid);
                ScheduleMomentsReload(dto.deviceUuid, TimeSpan.FromSeconds(2));
                ScheduleMomentsReload(dto.deviceUuid, TimeSpan.FromSeconds(8));
                ScheduleMomentsReload(dto.deviceUuid, TimeSpan.FromSeconds(20));
                shouldRefreshState = true;
            }
            if (IsChatSendTaskResult(message, contentHint, dto.success))
            {
                _ = RefreshCurrentChatAfterTaskResultAsync(dto.deviceUuid, contentHint);
            }
            if (message.Contains("撤回", StringComparison.OrdinalIgnoreCase))
            {
                _ = RefreshCurrentChatAfterTaskResultAsync(dto.deviceUuid, "消息撤回");
            }

            dto.message = message;
            OnAsyncTaskResultReceived?.Invoke(dto);

            if (shouldRefreshState)
            {
                NotifyStateChanged();
            }

            if (!dto.success)
            {
                OnNotification?.Invoke(message, false);
                return;
            }

            if (ShouldSurfaceTaskResultNotification(message))
            {
                OnNotification?.Invoke(message, true);
            }
        }

        /// <summary>
        /// 处理好友请求列表更新信号。
        /// <para>当前 Store 不缓存 FriendRequests 明细，页面收到该事件后自行刷新；这里负责统一通知 UI 重新渲染。</para>
        /// </summary>
        private void HandleFriendRequestsUpdated(string accountId)
        {
            if (_isDisposed)
            {
                return;
            }

            _logger.LogInformation("[CrmStore] 收到好友请求列表更新事件: Account={AccountId}", accountId);
            OnFriendRequestsUpdated?.Invoke(accountId);
            NotifyStateChanged();
        }

        /// <summary>
        /// 处理群邀请列表更新信号。
        /// <para>Store 不缓存群邀请明细，页面收到该事件后自行调用 GetGroupInvitationsAsync 刷新。</para>
        /// </summary>
        private void HandleGroupInvitationsUpdated(string accountId)
        {
            if (_isDisposed)
            {
                return;
            }

            _logger.LogInformation("[CrmStore] 收到群邀请列表更新事件: Account={AccountId}", accountId);
            OnGroupInvitationsUpdated?.Invoke(accountId);
            NotifyStateChanged();
        }

        /// <summary>
        /// 处理联系人标签列表更新信号。
        /// <para>Store 不缓存标签明细，页面收到该事件后自行调用 GetContactLabelsAsync 刷新。</para>
        /// </summary>
        private void HandleContactLabelsUpdated(string accountId)
        {
            if (_isDisposed)
            {
                return;
            }

            _logger.LogInformation("[CrmStore] 收到联系人标签列表更新事件: Account={AccountId}", accountId);
            OnContactLabelsUpdated?.Invoke(accountId);
            NotifyStateChanged();
        }

        /// <summary>
        /// 处理手机短信记录更新信号。
        /// <para>Store 不缓存短信明细，页面收到该事件后自行调用 GetSmsRecordsAsync 刷新。</para>
        /// </summary>
        private void HandleSmsRecordsUpdated(string accountId)
        {
            if (_isDisposed)
            {
                return;
            }

            _logger.LogInformation("[CrmStore] 收到手机短信记录更新事件: Account={AccountId}", accountId);
            OnSmsRecordsUpdated?.Invoke(accountId);
            NotifyStateChanged();
        }

        /// <summary>
        /// 处理手机通话记录更新信号。
        /// <para>Store 不缓存通话明细，页面收到该事件后自行调用 GetCallLogRecordsAsync 刷新。</para>
        /// </summary>
        private void HandleCallLogRecordsUpdated(string accountId)
        {
            if (_isDisposed)
            {
                return;
            }

            _logger.LogInformation("[CrmStore] 收到手机通话记录更新事件: Account={AccountId}", accountId);
            OnCallLogRecordsUpdated?.Invoke(accountId);
            NotifyStateChanged();
        }

        private static bool ShouldSurfaceTaskResultNotification(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.Contains("朋友圈同步", StringComparison.OrdinalIgnoreCase)
                || message.Contains("截图", StringComparison.OrdinalIgnoreCase)
                || message.Contains("群发", StringComparison.OrdinalIgnoreCase)
                || message.Contains("视频号", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Finder", StringComparison.OrdinalIgnoreCase)
                || message.Contains("个人二维码", StringComparison.OrdinalIgnoreCase)
                || message.Contains("POI", StringComparison.OrdinalIgnoreCase)
                || message.Contains("表情信息", StringComparison.OrdinalIgnoreCase)
                || message.Contains("搜索联系人", StringComparison.OrdinalIgnoreCase)
                || message.Contains("微信位置", StringComparison.OrdinalIgnoreCase)
                || message.Contains("聊天消息 ID 快照", StringComparison.OrdinalIgnoreCase)
                || message.Contains("CDN下载", StringComparison.OrdinalIgnoreCase)
                || message.Contains("语音转文字", StringComparison.OrdinalIgnoreCase)
                || message.Contains("钱包余额", StringComparison.OrdinalIgnoreCase)
                || message.Contains("手机状态", StringComparison.OrdinalIgnoreCase)
                || message.Contains("短信", StringComparison.OrdinalIgnoreCase)
                || message.Contains("通话记录", StringComparison.OrdinalIgnoreCase)
                || message.Contains("红包", StringComparison.OrdinalIgnoreCase)
                || message.Contains("收钱结果", StringComparison.OrdinalIgnoreCase)
                || message.Contains("朋友圈评论", StringComparison.OrdinalIgnoreCase)
                || message.Contains("朋友圈点赞", StringComparison.OrdinalIgnoreCase)
                || message.Contains("好友检测", StringComparison.OrdinalIgnoreCase)
                || message.Contains("清粉", StringComparison.OrdinalIgnoreCase)
                || message.Contains("消息转发", StringComparison.OrdinalIgnoreCase)
                || message.Contains("朋友圈发布", StringComparison.OrdinalIgnoreCase)
                || message.Contains("PostMomentSuccess", StringComparison.OrdinalIgnoreCase)
                || message.Contains("CircleId=", StringComparison.OrdinalIgnoreCase)
                || message.Contains("CircleLike", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMomentsInteractionTaskResult(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.Contains("朋友圈评论", StringComparison.OrdinalIgnoreCase)
                || message.Contains("朋友圈点赞", StringComparison.OrdinalIgnoreCase)
                || message.Contains("朋友圈发布", StringComparison.OrdinalIgnoreCase)
                || message.Contains("PostMomentSuccess", StringComparison.OrdinalIgnoreCase)
                || message.Contains("CircleId=", StringComparison.OrdinalIgnoreCase)
                || message.Contains("CircleLike", StringComparison.OrdinalIgnoreCase)
                || message.Contains("CommentId=", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判断是否为聊天发送任务回执。
        /// <para>媒体发送常先返回 TaskResultNotice，再稍后由微信数据库 Hook 上报真实消息；这里补一次延迟刷新。</para>
        /// </summary>
        private static bool IsChatSendTaskResult(string message, string contentHint, bool success)
        {
            if (!success)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(contentHint))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(message)
                && (message.Contains("聊天", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("图片", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("视频", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("文件", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("转发", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("TalkToFriend", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 聊天任务回执后延迟刷新当前会话。
        /// <para>仅刷新当前选中设备和会话，不触碰数据库写入；持久化仍由 ChatMessageHandler + DbHelper.SaveMessages 完成。</para>
        /// </summary>
        private async Task RefreshCurrentChatAfterTaskResultAsync(string? deviceUuid, string contentHint)
        {
            try
            {
                if (SelectedDevice == null || SelectedConversation == null)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(deviceUuid)
                    && !string.Equals(SelectedDevice.uuid, deviceUuid, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var accountId = FirstNonEmptyLocal(SelectedConversation.wechatAccountId, SelectedDevice.weChatId, SelectedDevice.wx?.wechatAccount?.wxid);
                var conversationWxid = SelectedConversation.conversationWxid;
                if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(conversationWxid))
                {
                    return;
                }

                await Task.Delay(1200).ConfigureAwait(false);
                await ReloadCurrentMessagesAsync(accountId, conversationWxid).ConfigureAwait(false);
                await Task.Delay(3500).ConfigureAwait(false);
                await ReloadCurrentMessagesAsync(accountId, conversationWxid).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "聊天任务回执后刷新当前会话失败: {ContentHint}", contentHint);
            }
        }

        private static string FirstNonEmptyLocal(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 串行执行 CrmService 调用，避免同一个 scoped DbContext 被多个异步 UI 回调并发使用。
        /// </summary>
        private async Task<T> ExecuteServiceCallAsync<T>(Func<Task<T>> action)
        {
            await _serviceCallLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await action().ConfigureAwait(false);
            }
            finally
            {
                _serviceCallLock.Release();
            }
        }

        /// <summary>
        /// 串行执行无返回值的 CrmService 调用。
        /// </summary>
        private async Task ExecuteServiceCallAsync(Func<Task> action)
        {
            await _serviceCallLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await action().ConfigureAwait(false);
            }
            finally
            {
                _serviceCallLock.Release();
            }
        }

        /// <summary>
        /// 将服务端实时推送的朋友圈 DTO 合并到当前页面列表。
        /// <para>这样点赞/评论/详情推送到达后，网页不用等待防抖重拉就能立即看到变化。</para>
        /// </summary>
        private void MergeMomentDtoIntoCurrentList(MomentsTimelineDto dto)
        {
            var list = CurrentMoments?.ToList() ?? new List<MomentsTimeline>();
            var existingMoment = list.FirstOrDefault(item => item.snsId == dto.snsId);
            var incomingComments = ResolveMomentCommentDisplayNames(dto.comments ?? new List<MomentCommentDto>());
            var incomingLikes = ResolveMomentLikeDisplayNames(dto.likes ?? new List<MomentLikeDto>());
            if (existingMoment != null)
            {
                incomingComments = MergeMomentComments(
                    DeserializeMomentComments(existingMoment.commentsJson),
                    incomingComments);
                incomingLikes = MergeMomentLikes(
                    DeserializeMomentLikes(existingMoment.likesJson),
                    incomingLikes);
            }

            var moment = new MomentsTimeline
            {
                ownerWxid = FirstNonEmptyLocal(SelectedDevice?.weChatId, SelectedDevice?.wx?.wechatAccount?.wxid),
                snsId = dto.snsId,
                userName = dto.userName ?? string.Empty,
                nickName = ResolveMomentDisplayName(dto.userName, dto.nickName),
                content = MomentContentExtractor.FirstNonEmpty(
                    dto.content,
                    MomentContentExtractor.ExtractTextFromXml(dto.xmlContent)),
                createTime = dto.createTime,
                imagesJson = JsonSerializer.Serialize(dto.images ?? new List<string>()),
                commentsJson = JsonSerializer.Serialize(incomingComments),
                likesJson = JsonSerializer.Serialize(incomingLikes),
                videoUrl = dto.videoUrl ?? string.Empty,
                linkInfoJson = JsonSerializer.Serialize(dto.link ?? new MomentLinkDto()),
                xmlContent = dto.xmlContent ?? string.Empty,
                receivedAt = DateTime.UtcNow.Ticks
            };

            var index = list.FindIndex(item => item.snsId == dto.snsId);
            if (index >= 0)
            {
                list[index] = moment;
            }
            else
            {
                list.Insert(0, moment);
            }

            MergePendingMomentInteractions(moment);
            CurrentMoments = list
                .OrderByDescending(item => item.createTime)
                .ToList();
        }

        /// <summary>
        /// 朋友圈点赞成功后的前端乐观更新。
        /// <para>安卓端成功回执先于 CircleMsgPushNotice 到达时，先把当前账号写入本地 likesJson，后续重拉再校准。</para>
        /// </summary>
        private void ApplyMomentLikeOptimisticUpdate(long circleId, bool isCancel)
        {
            var moment = CurrentMoments.FirstOrDefault(item => item.snsId == circleId);
            if (moment == null)
            {
                return;
            }

            var wxid = SelectedDevice?.wx?.wechatAccount?.wxid ?? SelectedDevice?.weChatId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(wxid))
            {
                return;
            }

            var nickname = SelectedDevice?.wx?.wechatAccount?.nickname ?? wxid;
            var likes = DeserializeMomentLikes(moment.likesJson);
            likes.RemoveAll(item => string.Equals(item.userName, wxid, StringComparison.OrdinalIgnoreCase));
            if (!isCancel)
            {
                likes.Add(new MomentLikeDto
                {
                    userName = wxid,
                    nickName = ResolveContactDisplayNamePublic(wxid, nickname),
                    createTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                });
            }

            RecordPendingMomentLike(circleId, wxid, nickname, isCancel);
            moment.likesJson = JsonSerializer.Serialize(likes.OrderBy(item => item.createTime).ToList());
            NotifyStateChanged();
        }

        /// <summary>
        /// 朋友圈评论下发后的前端临时展示。
        /// <para>真正 commentId 仍以安卓端回包/后续 CircleMsgPushNotice 为准。</para>
        /// </summary>
        private long ApplyMomentCommentPendingUpdate(long circleId, string content, string toWeChatId, long replyCommentId)
        {
            if (circleId == 0 || string.IsNullOrWhiteSpace(content))
            {
                return 0;
            }

            var moment = CurrentMoments.FirstOrDefault(item => item.snsId == circleId);
            if (moment == null)
            {
                return 0;
            }

            var wxid = SelectedDevice?.wx?.wechatAccount?.wxid ?? SelectedDevice?.weChatId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(wxid))
            {
                return 0;
            }

            var nickname = SelectedDevice?.wx?.wechatAccount?.nickname ?? wxid;
            var comments = DeserializeMomentComments(moment.commentsJson);
            var pendingCommentId = -DateTime.UtcNow.Ticks;
            var pendingComment = new MomentCommentDto
            {
                commentId = pendingCommentId,
                userName = wxid,
                nickName = ResolveContactDisplayNamePublic(wxid, nickname),
                content = content.Trim(),
                createTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                replyUserName = toWeChatId ?? string.Empty,
                replyNickName = ResolveContactDisplayName(toWeChatId) ?? string.Empty,
                authorName = wxid
            };
            comments.Add(pendingComment);

            RecordPendingMomentComment(circleId, pendingComment);
            moment.commentsJson = JsonSerializer.Serialize(comments.OrderBy(item => item.createTime).ToList());
            NotifyStateChanged();
            return pendingCommentId;
        }

        /// <summary>
        /// 撤销未成功下发的朋友圈临时评论。
        /// <para>只有服务端明确返回下发失败时才撤销；如果微信侧已执行但回调超时，则保留临时评论等待后续详情回拉校准。</para>
        /// </summary>
        private void RemoveMomentCommentPendingUpdate(long circleId, long pendingCommentId)
        {
            if (circleId == 0 || pendingCommentId >= 0)
            {
                return;
            }

            var removed = false;
            var moment = CurrentMoments.FirstOrDefault(item => item.snsId == circleId);
            if (moment != null)
            {
                var comments = DeserializeMomentComments(moment.commentsJson);
                removed = comments.RemoveAll(item => item.commentId == pendingCommentId) > 0;
                if (removed)
                {
                    moment.commentsJson = JsonSerializer.Serialize(comments.OrderBy(item => item.createTime).ToList());
                }
            }

            lock (_pendingMomentInteractionSync)
            {
                if (_pendingMomentCommentsByCircleId.TryGetValue(circleId, out var pendingComments))
                {
                    removed = pendingComments.RemoveAll(item => item.Comment.commentId == pendingCommentId) > 0 || removed;
                    if (pendingComments.Count == 0)
                    {
                        _pendingMomentCommentsByCircleId.Remove(circleId);
                    }
                }
            }

            if (removed)
            {
                NotifyStateChanged();
            }
        }

        /// <summary>
        /// 记录本地点赞/取消点赞待确认状态。
        /// </summary>
        private void RecordPendingMomentLike(long circleId, string wxid, string? nickname, bool isCancel)
        {
            if (circleId == 0 || string.IsNullOrWhiteSpace(wxid))
            {
                return;
            }

            lock (_pendingMomentInteractionSync)
            {
                PruneExpiredPendingMomentInteractionsLocked(DateTimeOffset.UtcNow);
                if (!_pendingMomentLikesByCircleId.TryGetValue(circleId, out var likeMap))
                {
                    likeMap = new Dictionary<string, PendingMomentLikeUpdate>(StringComparer.OrdinalIgnoreCase);
                    _pendingMomentLikesByCircleId[circleId] = likeMap;
                }

                likeMap[wxid] = new PendingMomentLikeUpdate
                {
                    IsCancel = isCancel,
                    ExpiresAt = DateTimeOffset.UtcNow.Add(MomentInteractionPendingTtl),
                    Like = new MomentLikeDto
                    {
                        userName = wxid,
                        nickName = ResolveContactDisplayNamePublic(wxid, nickname),
                        createTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                    }
                };
            }
        }

        /// <summary>
        /// 记录本地评论待确认状态。
        /// </summary>
        private void RecordPendingMomentComment(long circleId, MomentCommentDto comment)
        {
            if (circleId == 0 || comment == null || string.IsNullOrWhiteSpace(comment.content))
            {
                return;
            }

            lock (_pendingMomentInteractionSync)
            {
                PruneExpiredPendingMomentInteractionsLocked(DateTimeOffset.UtcNow);
                if (!_pendingMomentCommentsByCircleId.TryGetValue(circleId, out var comments))
                {
                    comments = new List<PendingMomentCommentUpdate>();
                    _pendingMomentCommentsByCircleId[circleId] = comments;
                }

                comments.RemoveAll(item => IsSameMomentComment(item.Comment, comment));
                comments.Add(new PendingMomentCommentUpdate
                {
                    Comment = comment,
                    ExpiresAt = DateTimeOffset.UtcNow.Add(MomentInteractionPendingTtl)
                });
            }
        }

        /// <summary>
        /// 将服务端朋友圈列表与本地待确认互动合并后再展示。
        /// <para>该方法只修改前端内存对象，不写数据库；数据库持久化仍由安卓端 CirclePush/CircleMsgPush 进入服务端后处理。</para>
        /// </summary>
        private List<MomentsTimeline> MergePendingMomentInteractions(List<MomentsTimeline> serverMoments)
        {
            var moments = serverMoments ?? new List<MomentsTimeline>();
            lock (_pendingMomentInteractionSync)
            {
                var now = DateTimeOffset.UtcNow;
                PruneExpiredPendingMomentInteractionsLocked(now);
                if (_pendingMomentLikesByCircleId.Count == 0 && _pendingMomentCommentsByCircleId.Count == 0)
                {
                    return moments;
                }

                foreach (var moment in moments)
                {
                    if (moment == null || moment.snsId == 0)
                    {
                        continue;
                    }

                    MergePendingLikesForMomentLocked(moment, now);
                    MergePendingCommentsForMomentLocked(moment, now);
                }

                RemovePendingEntriesForMissingMomentsLocked(moments.Select(item => item.snsId).ToHashSet(), now);
                return moments;
            }
        }

        /// <summary>
        /// 将单条实时推送的朋友圈与本地待确认互动合并。
        /// </summary>
        private MomentsTimeline MergePendingMomentInteractions(MomentsTimeline moment)
        {
            lock (_pendingMomentInteractionSync)
            {
                var now = DateTimeOffset.UtcNow;
                PruneExpiredPendingMomentInteractionsLocked(now);
                MergePendingLikesForMomentLocked(moment, now);
                MergePendingCommentsForMomentLocked(moment, now);
                return moment;
            }
        }

        /// <summary>
        /// 合并单条朋友圈点赞待确认状态。
        /// </summary>
        private void MergePendingLikesForMomentLocked(MomentsTimeline moment, DateTimeOffset now)
        {
            if (!_pendingMomentLikesByCircleId.TryGetValue(moment.snsId, out var pendingMap) || pendingMap.Count == 0)
            {
                return;
            }

            var likes = DeserializeMomentLikes(moment.likesJson);
            foreach (var pending in pendingMap.Values.ToList())
            {
                if (pending.ExpiresAt <= now)
                {
                    pendingMap.Remove(pending.Like.userName);
                    continue;
                }

                likes.RemoveAll(item => string.Equals(item.userName, pending.Like.userName, StringComparison.OrdinalIgnoreCase));
                if (!pending.IsCancel)
                {
                    likes.Add(pending.Like);
                }
            }

            moment.likesJson = JsonSerializer.Serialize(ResolveMomentLikeDisplayNames(likes).OrderBy(item => item.createTime).ToList());
            if (pendingMap.Count == 0)
            {
                _pendingMomentLikesByCircleId.Remove(moment.snsId);
            }
        }

        /// <summary>
        /// 合并单条朋友圈评论待确认状态。
        /// </summary>
        private void MergePendingCommentsForMomentLocked(MomentsTimeline moment, DateTimeOffset now)
        {
            if (!_pendingMomentCommentsByCircleId.TryGetValue(moment.snsId, out var pendingComments) || pendingComments.Count == 0)
            {
                return;
            }

            var comments = DeserializeMomentComments(moment.commentsJson);
            foreach (var pending in pendingComments.ToList())
            {
                if (pending.ExpiresAt <= now)
                {
                    pendingComments.Remove(pending);
                    continue;
                }

                if (!comments.Any(item => IsSameMomentComment(item, pending.Comment)))
                {
                    comments.Add(pending.Comment);
                }
            }

            moment.commentsJson = JsonSerializer.Serialize(ResolveMomentCommentDisplayNames(comments).OrderBy(item => item.createTime).ToList());
            if (pendingComments.Count == 0)
            {
                _pendingMomentCommentsByCircleId.Remove(moment.snsId);
            }
        }

        /// <summary>
        /// 清理已过期的朋友圈待确认互动。
        /// </summary>
        private void PruneExpiredPendingMomentInteractionsLocked(DateTimeOffset now)
        {
            foreach (var pair in _pendingMomentLikesByCircleId.ToList())
            {
                foreach (var likePair in pair.Value.ToList())
                {
                    if (likePair.Value.ExpiresAt <= now)
                    {
                        pair.Value.Remove(likePair.Key);
                    }
                }

                if (pair.Value.Count == 0)
                {
                    _pendingMomentLikesByCircleId.Remove(pair.Key);
                }
            }

            foreach (var pair in _pendingMomentCommentsByCircleId.ToList())
            {
                pair.Value.RemoveAll(item => item.ExpiresAt <= now);
                if (pair.Value.Count == 0)
                {
                    _pendingMomentCommentsByCircleId.Remove(pair.Key);
                }
            }
        }

        /// <summary>
        /// 当前回拉列表已经不包含某条朋友圈时，保留短时间内的新互动，过期后再自然清理。
        /// </summary>
        private void RemovePendingEntriesForMissingMomentsLocked(HashSet<long> visibleCircleIds, DateTimeOffset now)
        {
            if (visibleCircleIds == null || visibleCircleIds.Count == 0)
            {
                return;
            }

            foreach (var pair in _pendingMomentLikesByCircleId.ToList())
            {
                if (!visibleCircleIds.Contains(pair.Key) && pair.Value.Values.All(item => item.ExpiresAt <= now))
                {
                    _pendingMomentLikesByCircleId.Remove(pair.Key);
                }
            }

            foreach (var pair in _pendingMomentCommentsByCircleId.ToList())
            {
                if (!visibleCircleIds.Contains(pair.Key) && pair.Value.All(item => item.ExpiresAt <= now))
                {
                    _pendingMomentCommentsByCircleId.Remove(pair.Key);
                }
            }
        }

        /// <summary>
        /// 判断两条朋友圈评论是否表示同一次评论。
        /// </summary>
        private static bool IsSameMomentComment(MomentCommentDto left, MomentCommentDto right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (left.commentId > 0 && right.commentId > 0 && left.commentId == right.commentId)
            {
                return true;
            }

            return string.Equals(left.userName?.Trim(), right.userName?.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.content?.Trim(), right.content?.Trim(), StringComparison.Ordinal)
                && string.Equals(left.replyUserName?.Trim() ?? string.Empty, right.replyUserName?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static List<MomentLikeDto> DeserializeMomentLikes(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<MomentLikeDto>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<MomentLikeDto>>(json) ?? new List<MomentLikeDto>();
            }
            catch
            {
                return new List<MomentLikeDto>();
            }
        }

        private static List<MomentCommentDto> DeserializeMomentComments(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<MomentCommentDto>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<MomentCommentDto>>(json) ?? new List<MomentCommentDto>();
            }
            catch
            {
                return new List<MomentCommentDto>();
            }
        }

        /// <summary>
        /// 合并朋友圈评论列表。
        /// <para>实时推送可能只带新增评论，也可能是旧详情快照；按 commentId/评论者/内容/回复对象去重后保留并集。</para>
        /// </summary>
        private static List<MomentCommentDto> MergeMomentComments(IEnumerable<MomentCommentDto> existing, IEnumerable<MomentCommentDto> incoming)
        {
            var map = new Dictionary<string, MomentCommentDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in (existing ?? Enumerable.Empty<MomentCommentDto>()).Concat(incoming ?? Enumerable.Empty<MomentCommentDto>()))
            {
                if (item == null)
                {
                    continue;
                }

                var key = item.commentId > 0
                    ? item.commentId.ToString()
                    : $"{item.userName}|{item.content}|{item.replyUserName}";
                map[key] = item;
            }

            return map.Values.OrderBy(item => item.createTime).ToList();
        }

        /// <summary>
        /// 合并朋友圈点赞列表。
        /// </summary>
        private static List<MomentLikeDto> MergeMomentLikes(IEnumerable<MomentLikeDto> existing, IEnumerable<MomentLikeDto> incoming)
        {
            var map = new Dictionary<string, MomentLikeDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in (existing ?? Enumerable.Empty<MomentLikeDto>()).Concat(incoming ?? Enumerable.Empty<MomentLikeDto>()))
            {
                if (item == null || string.IsNullOrWhiteSpace(item.userName))
                {
                    continue;
                }

                var key = item.userName.Trim();
                if (!map.TryGetValue(key, out var oldItem)
                    || item.createTime >= oldItem.createTime
                    || string.IsNullOrWhiteSpace(oldItem.nickName))
                {
                    map[key] = item;
                }
            }

            return map.Values.OrderBy(item => item.createTime).ToList();
        }

        public string ResolveContactDisplayNamePublic(string? wxid, string? currentName = null)
        {
            if (!string.IsNullOrWhiteSpace(currentName) && !LooksLikeRawWxid(currentName))
            {
                return currentName.Trim();
            }

            if (string.IsNullOrWhiteSpace(wxid))
            {
                return currentName ?? string.Empty;
            }

            var contact = Contacts.FirstOrDefault(item => string.Equals(item.wxid, wxid, StringComparison.OrdinalIgnoreCase))
                ?? Contacts.FirstOrDefault(item => string.Equals(item.friendNo, wxid, StringComparison.OrdinalIgnoreCase))
                ?? SelectedDevice?.wx?.contacts?.FirstOrDefault(item => string.Equals(item.wxid, wxid, StringComparison.OrdinalIgnoreCase))
                ?? SelectedDevice?.wx?.contacts?.FirstOrDefault(item => string.Equals(item.friendNo, wxid, StringComparison.OrdinalIgnoreCase));
            if (contact == null)
            {
                var account = Devices
                    .Select(item => item.wx?.wechatAccount)
                    .FirstOrDefault(item => item != null && string.Equals(item.wxid, wxid, StringComparison.OrdinalIgnoreCase));
                if (account != null && !string.IsNullOrWhiteSpace(account.nickname))
                {
                    return account.nickname;
                }

                if (!string.IsNullOrWhiteSpace(currentName) && !LooksLikeRawWxid(currentName))
                {
                    return currentName.Trim();
                }

                return wxid;
            }

            if (!string.IsNullOrWhiteSpace(contact.remarks)) return contact.remarks;
            if (!string.IsNullOrWhiteSpace(contact.nickname)) return contact.nickname;
            return wxid;
        }

        /// <summary>
        /// 当前操作微信的显示名称。
        /// <para>微信账号和设备不是硬绑定关系；这里用于 UI 统一展示“正在用哪个微信做事”。</para>
        /// </summary>
        public string CurrentWechatDisplayName
        {
            get
            {
                var device = SelectedDevice;
                if (device == null)
                {
                    return "未选择微信";
                }

                return FirstNonEmptyLocal(
                    device.wx?.wechatAccount?.nickname,
                    device.wx?.wechatAccount?.wechatNumber,
                    device.weChatId,
                    device.wx?.wechatAccount?.wxid,
                    "微信未登录");
            }
        }

        /// <summary>
        /// 当前执行设备的显示名称。
        /// <para>设备是当前微信任务的执行载体；微信换设备登录后应切换此对象。</para>
        /// </summary>
        public string CurrentExecutionDeviceDisplayName
        {
            get
            {
                var device = SelectedDevice;
                if (device == null)
                {
                    return "未选择设备";
                }

                return FirstNonEmptyLocal(
                    device.device?.PhoneModel,
                    device.uuid,
                    "未知设备");
            }
        }

        /// <summary>
        /// 当前执行对象标签。
        /// <para>用于所有会下发到安卓端的按钮确认文案，避免各页面重复拼接且口径不一致。</para>
        /// </summary>
        public string CurrentExecutionLabel
        {
            get
            {
                var device = SelectedDevice;
                if (device == null)
                {
                    return "未选择微信/设备";
                }

                return $"{CurrentWechatDisplayName} / {CurrentExecutionDeviceDisplayName}";
            }
        }

        private string ResolveMomentDisplayName(string? wxid, string? currentName)
        {
            return ResolveContactDisplayNamePublic(wxid, currentName);
        }

        private List<MomentCommentDto> ResolveMomentCommentDisplayNames(IEnumerable<MomentCommentDto> comments)
        {
            return comments.Select(comment =>
            {
                comment.nickName = ResolveContactDisplayNamePublic(comment.userName, comment.nickName);
                comment.replyNickName = ResolveContactDisplayNamePublic(comment.replyUserName, comment.replyNickName);
                return comment;
            }).ToList();
        }

        private List<MomentLikeDto> ResolveMomentLikeDisplayNames(IEnumerable<MomentLikeDto> likes)
        {
            return likes.Select(like =>
            {
                like.nickName = ResolveContactDisplayNamePublic(like.userName, like.nickName);
                return like;
            }).ToList();
        }

        private string? ResolveContactDisplayName(string? wxid)
        {
            var displayName = ResolveContactDisplayNamePublic(wxid);
            return string.IsNullOrWhiteSpace(displayName) ? null : displayName;
        }

        private static bool LooksLikeRawWxid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var text = value.Trim();
            return text.StartsWith("wxid_", StringComparison.OrdinalIgnoreCase)
                || text.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase)
                || (text.StartsWith("v3_", StringComparison.OrdinalIgnoreCase) && text.EndsWith("@stranger", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 更新截图状态。
        /// </summary>
        private void UpdateScreenShotStatus(string? deviceUuid, bool success, string? message, string? screenshotUrl = null, bool clearPreviewOnFailure = false)
        {
            LastScreenShotDeviceUuid = deviceUuid ?? string.Empty;
            LastScreenShotSuccess = success;
            LastScreenShotStatusMessage = string.IsNullOrWhiteSpace(message)
                ? (success ? "截图任务已完成" : "截图失败")
                : message.Trim();
            LastScreenShotUpdatedAt = DateTimeOffset.Now;

            if (success)
            {
                if (!string.IsNullOrWhiteSpace(screenshotUrl))
                {
                    LastScreenShotUrl = screenshotUrl.Trim();
                    ClearScreenShotAccessState();
                }
            }
            else if (clearPreviewOnFailure)
            {
                LastScreenShotUrl = null;
                ClearScreenShotAccessState();
            }
        }

        /// <summary>
        /// 清理截图短期预览状态。
        /// </summary>
        private void ClearScreenShotAccessState()
        {
            LastScreenShotAccessUrl = null;
            LastScreenShotAccessExpiresAt = null;
            IsPreparingScreenShotAccess = false;
        }

        /// <summary>
        /// 清理截图状态，避免跨设备误展示旧图。
        /// </summary>
        private void ClearScreenShotState()
        {
            LastScreenShotUrl = null;
            LastScreenShotDeviceUuid = null;
            LastScreenShotStatusMessage = null;
            LastScreenShotSuccess = null;
            LastScreenShotUpdatedAt = null;
            ClearScreenShotAccessState();
        }

        /// <summary>
        /// 更新朋友圈同步状态。
        /// </summary>
        private void UpdateMomentsSyncStatus(string? deviceUuid, long taskId, bool success, string? message)
        {
            LastMomentsSyncDeviceUuid = deviceUuid ?? string.Empty;
            LastMomentsSyncTaskId = taskId;
            LastMomentsSyncSuccess = success;
            LastMomentsSyncMessage = string.IsNullOrWhiteSpace(message)
                ? (success ? "朋友圈同步已完成" : "朋友圈同步失败")
                : message.Trim();
            LastMomentsSyncUpdatedAt = DateTimeOffset.Now;
        }

        /// <summary>
        /// 清理朋友圈同步状态。
        /// </summary>
        private void ClearMomentsSyncState()
        {
            LastMomentsSyncDeviceUuid = null;
            LastMomentsSyncTaskId = 0L;
            LastMomentsSyncMessage = null;
            LastMomentsSyncSuccess = null;
            LastMomentsSyncUpdatedAt = null;
        }

        /// <summary>
        /// 更新朋友圈发布状态。
        /// </summary>
        private void UpdateMomentPostStatus(
            string? deviceUuid,
            long taskId,
            bool? success,
            string? message,
            string status,
            MomentPostResultDto? result = null)
        {
            LastMomentPostDeviceUuid = deviceUuid ?? string.Empty;
            LastMomentPostTaskId = taskId;
            LastMomentPostClientRequestId = result?.clientRequestId ?? LastMomentPostClientRequestId;
            LastMomentPostStatus = string.IsNullOrWhiteSpace(status) ? result?.status : status;
            LastMomentPostMessage = string.IsNullOrWhiteSpace(message)
                ? (success == true ? "朋友圈发布成功" : success == false ? "朋友圈发布失败" : "朋友圈发布任务已下发，等待手机回包")
                : message.Trim();
            LastMomentPostSuccess = success;
            LastMomentPostCircleId = result?.circleId ?? LastMomentPostCircleId;
            LastMomentPostIsLate = result?.isLate ?? LastMomentPostIsLate;
            LastMomentPostNeedSync = result?.needSync ?? LastMomentPostNeedSync;
            LastMomentPostAttachmentType = result?.attachmentType ?? LastMomentPostAttachmentType;
            LastMomentPostAttachmentCount = result?.attachmentCount ?? LastMomentPostAttachmentCount;
            LastMomentPostUpdatedAt = DateTimeOffset.Now;
        }

        /// <summary>
        /// 清理朋友圈发布状态。
        /// </summary>
        private void ClearMomentPostState()
        {
            LastMomentPostDeviceUuid = null;
            LastMomentPostTaskId = 0L;
            LastMomentPostClientRequestId = null;
            LastMomentPostStatus = null;
            LastMomentPostMessage = null;
            LastMomentPostSuccess = null;
            LastMomentPostCircleId = 0L;
            LastMomentPostIsLate = false;
            LastMomentPostNeedSync = false;
            LastMomentPostAttachmentType = null;
            LastMomentPostAttachmentCount = 0;
            LastMomentPostUpdatedAt = null;
        }

        private void ScheduleMomentPostRefresh(string? deviceUuid, MomentPostResultDto? result)
        {
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return;
            }

            QueueMomentsReload(deviceUuid);
            ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(2));
            ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(8));
            ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(20));
            if (result?.isLate == true || result?.circleId == 0)
            {
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(45));
            }
        }

        private static bool TryReadMomentPostResult(object? data, out MomentPostResultDto? result)
        {
            result = null;
            switch (data)
            {
                case null:
                    return false;
                case MomentPostResultDto dto:
                    result = dto;
                    return IsMomentPostResult(result);
                case JsonElement element:
                    try
                    {
                        result = element.Deserialize<MomentPostResultDto>(MomentPostResultJsonOptions);
                        return IsMomentPostResult(result);
                    }
                    catch
                    {
                        return false;
                    }
                default:
                    try
                    {
                        var json = JsonSerializer.Serialize(data, MomentPostResultJsonOptions);
                        result = JsonSerializer.Deserialize<MomentPostResultDto>(json, MomentPostResultJsonOptions);
                        return IsMomentPostResult(result);
                    }
                    catch
                    {
                        return false;
                    }
            }
        }

        private static bool TryExtractMomentPostResultFromMessage(string? message, out MomentPostResultDto? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            var markerIndex = message.LastIndexOf("Data=", StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return false;
            }

            var json = message[(markerIndex + "Data=".Length)..].Trim().TrimEnd(';');
            if (string.IsNullOrWhiteSpace(json) || !json.Contains("clientRequestId", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                result = JsonSerializer.Deserialize<MomentPostResultDto>(json, MomentPostResultJsonOptions);
                return IsMomentPostResult(result);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsMomentPostResult(MomentPostResultDto? result)
        {
            if (result == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(result.clientRequestId)
                || !string.IsNullOrWhiteSpace(result.status)
                || !string.IsNullOrWhiteSpace(result.attachmentType)
                || result.circleId != 0
                || result.isLate
                || result.needSync;
        }

        private static string StripResultDataJson(string message)
        {
            var markerIndex = message.LastIndexOf("; Data=", StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                return message[..markerIndex].Trim();
            }

            markerIndex = message.LastIndexOf("Data=", StringComparison.OrdinalIgnoreCase);
            return markerIndex == 0 ? string.Empty : message.Trim();
        }

        private void QueueMomentsReload(string? expectedDeviceUuid)
        {
            if (_isDisposed)
            {
                return;
            }

            long reloadVersion;

            lock (_momentsReloadSync)
            {
                reloadVersion = ++_momentsReloadVersion;
            }

            _ = ReloadMomentsDebouncedAsync(expectedDeviceUuid ?? string.Empty, reloadVersion);
        }

        private void ScheduleMomentsReload(string? expectedDeviceUuid, TimeSpan delay)
        {
            if (_isDisposed)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                    if (_isDisposed)
                    {
                        return;
                    }

                    if (HasRecentMomentRealtimePush(TimeSpan.FromSeconds(3)))
                    {
                        _logger.LogDebug("[CrmStore] 跳过延迟朋友圈回拉：近期已收到服务端实时推送");
                        return;
                    }

                    QueueMomentsReload(expectedDeviceUuid);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[CrmStore] 延迟刷新朋友圈列表时出现可忽略异常");
                }
            });
        }

        private bool IsLatestMomentsReload(long reloadVersion)
        {
            lock (_momentsReloadSync)
            {
                return reloadVersion == _momentsReloadVersion;
            }
        }

        private async Task ReloadMomentsDebouncedAsync(string expectedDeviceUuid, long reloadVersion)
        {
            try
            {
                await Task.Delay(_momentsDebounceWindow).ConfigureAwait(false);
                if (_isDisposed || !IsLatestMomentsReload(reloadVersion))
                {
                    return;
                }

                await _momentsReloadLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (_isDisposed || !IsLatestMomentsReload(reloadVersion))
                    {
                        return;
                    }

                    var selectedDevice = SelectedDevice;
                    if (selectedDevice == null)
                    {
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(expectedDeviceUuid)
                        && !string.Equals(selectedDevice.uuid, expectedDeviceUuid, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation(
                            "[CrmStore] 跳过朋友圈回填：当前设备 {CurrentDeviceUuid} 与预期设备 {ExpectedDeviceUuid} 不一致",
                            selectedDevice.uuid,
                            expectedDeviceUuid);
                        return;
                    }

                    _logger.LogInformation("[CrmStore] 自动回填朋友圈列表，设备 {DeviceUuid}", selectedDevice.uuid);
                    await LoadMomentsAsync();
                }
                finally
                {
                    _momentsReloadLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // 页面释放或底层服务调用取消时忽略；朋友圈防抖本身不再主动抛取消异常。
            }
            catch (ObjectDisposedException) when (_isDisposed)
            {
                // 页面释放时串行锁可能已被 Dispose，属于正常收尾。
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CrmStore] 自动回填朋友圈列表失败，预期设备 {ExpectedDeviceUuid}", expectedDeviceUuid);
            }
        }

        private void NotifyStateChanged()
        {
            var handlers = OnChange;
            if (handlers == null)
            {
                return;
            }

            foreach (Action handler in handlers.GetInvocationList().Cast<Action>().ToList())
            {
                try
                {
                    handler();
                }
                catch (InvalidOperationException ex)
                    when (ex.Message.Contains("Collection was modified", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(ex, "[CrmStore] 状态通知时集合被并发修改，已忽略本次订阅者异常");
                }
            }
        }











        /// <summary>
        /// 重新加载当前选中设备的视频号历史。
        /// </summary>
        /// <remarks>
        /// 2026-05-12：FinderCenter 页面会直接调用此方法刷新筛选结果；
        /// 上一轮在重构内部加载链时误删了这个公共入口，这里补回，继续复用内部串行锁实现。
        /// </remarks>
        public async Task ReloadFinderHistoryForSelectedDeviceAsync(
            bool? success = null,
            long? taskId = null,
            DateTimeOffset? receivedFrom = null,
            DateTimeOffset? receivedTo = null)
        {
            if (SelectedDevice == null)
            {
                RestoreFinderViewStateForSelectedDevice();
                NotifyStateChanged();
                return;
            }

            await LoadFinderHistoryForSelectedDeviceAsync(
                SelectedDevice.uuid,
                SelectedDevice.weChatId,
                success,
                taskId,
                receivedFrom,
                receivedTo);

            NotifyStateChanged();
        }

        /// <summary>
        /// 服务端受控导出当前选中设备的视频号历史。
        /// <para>导出由 CrmService 负责权限校验、字段脱敏与审计；前端只负责把返回 JSON 交给浏览器下载。</para>
        /// </summary>
        public async Task<FinderHistoryExportDto> ExportFinderHistoryForSelectedDeviceAsync(
            int count,
            bool? success = null,
            long? taskId = null,
            DateTimeOffset? receivedFrom = null,
            DateTimeOffset? receivedTo = null)
        {
            if (SelectedDevice == null)
            {
                return new FinderHistoryExportDto
                {
                    success = false,
                    message = "请先选择设备。",
                    countPerType = count,
                    exportedAt = DateTimeOffset.UtcNow,
                    exportPermission = "finder.export",
                    masked = true,
                    rawPayloadIncluded = false,
                    successFilter = success,
                    taskIdFilter = taskId,
                    receivedFrom = receivedFrom,
                    receivedTo = receivedTo
                };
            }

            return await ExecuteServiceCallAsync(() => _service.ExportFinderHistoryAsync(
                SelectedDevice.uuid,
                count,
                success,
                taskId,
                receivedFrom,
                receivedTo));
        }

        private void RestoreFinderViewStateForSelectedDevice()





        {





            CurrentFinderMentions = GetSelectedFinderHistory(_finderMentionHistoryByDevice).FirstOrDefault();





            CurrentFinderUserPage = GetSelectedFinderHistory(_finderUserPageHistoryByDevice).FirstOrDefault();





            CurrentFinderComments = GetSelectedFinderHistory(_finderCommentHistoryByDevice).FirstOrDefault();





        }











        private bool IsFinderResultForSelectedDevice(string? deviceUuid, string? weChatId)





        {





            if (SelectedDevice == null)





            {





                return true;





            }











            if (!string.IsNullOrWhiteSpace(deviceUuid) &&





                string.Equals(SelectedDevice.uuid, deviceUuid, StringComparison.OrdinalIgnoreCase))





            {





                return true;





            }











            if (!string.IsNullOrWhiteSpace(weChatId) &&





                string.Equals(SelectedDevice.weChatId, weChatId, StringComparison.OrdinalIgnoreCase))





            {





                return true;





            }











            return false;





        }











        private static string NormalizeFinderMessage(string? value, string fallback)





        {





            return string.IsNullOrWhiteSpace(value) ? fallback : value;





        }











        private async Task LoadFinderHistoryForSelectedDeviceAsync(



            string? deviceUuid,



            string? weChatId,



            bool? success = null,



            long? taskId = null,



            DateTimeOffset? receivedFrom = null,



            DateTimeOffset? receivedTo = null)



        {



            await _finderHistoryLoadLock.WaitAsync();



            try



            {



                if (string.IsNullOrWhiteSpace(deviceUuid))



                {



                    RestoreFinderViewStateForSelectedDevice();



                    return;



                }



                try



                {



                    // 2026-05-12 修复：



                    // CrmService 当前为 scoped，底层共用同一个 ApplicationDbContext。



                    // 即使单次调用内部已改成串行 await，这个方法自身也可能被 UI 重入。



                    // 因此这里再加一层 store 级串行锁，彻底避免 Finder 历史并发查询。



                    var mentionItems = await ExecuteServiceCallAsync(() => _service.GetFinderMentionHistoryAsync(deviceUuid, FinderHistoryLimitPerDevice, success, taskId, receivedFrom, receivedTo));



                    var userPageItems = await ExecuteServiceCallAsync(() => _service.GetFinderUserPageHistoryAsync(deviceUuid, FinderHistoryLimitPerDevice, success, taskId, receivedFrom, receivedTo));



                    var commentItems = await ExecuteServiceCallAsync(() => _service.GetFinderCommentHistoryAsync(deviceUuid, FinderHistoryLimitPerDevice, success, taskId, receivedFrom, receivedTo));



                    ReplaceFinderHistoryItems(_finderMentionHistoryByDevice, deviceUuid, weChatId, mentionItems);



                    ReplaceFinderHistoryItems(_finderUserPageHistoryByDevice, deviceUuid, weChatId, userPageItems);



                    ReplaceFinderHistoryItems(_finderCommentHistoryByDevice, deviceUuid, weChatId, commentItems);



                }



                catch (Exception ex)



                {



                    _logger.LogWarning(ex, "[CrmStore] Load finder history failed for device {DeviceUuid}", deviceUuid);



                }



                RestoreFinderViewStateForSelectedDevice();



            }



            finally



            {



                _finderHistoryLoadLock.Release();



            }



        }



        private IReadOnlyList<T> GetSelectedFinderHistory<T>(Dictionary<string, List<T>> historyMap)





        {





            var deviceKey = ResolveSelectedFinderHistoryKey();





            if (string.IsNullOrWhiteSpace(deviceKey))





            {





                return Array.Empty<T>();





            }











            return historyMap.TryGetValue(deviceKey, out var list)





                ? list





                : Array.Empty<T>();





        }











        private string ResolveSelectedFinderHistoryKey()





        {





            if (SelectedDevice == null)





            {





                return string.Empty;





            }











            return ResolveFinderHistoryKey(SelectedDevice.uuid, SelectedDevice.weChatId);





        }











        private static string ResolveFinderHistoryKey(string? deviceUuid, string? weChatId)





        {





            if (!string.IsNullOrWhiteSpace(deviceUuid))





            {





                return $"device:{deviceUuid}";





            }











            if (!string.IsNullOrWhiteSpace(weChatId))





            {





                return $"wx:{weChatId}";





            }











            return string.Empty;





        }











        private void AddFinderMentionHistory(FinderMentionNoticeDto dto)





        {





            AddFinderHistoryItem(





                _finderMentionHistoryByDevice,





                dto.deviceUuid,





                dto.weChatId,





                dto,





                item => $"{item.taskId}|{item.receivedAt:O}");





        }











        private void AddFinderUserPageHistory(FinderUserPageDto dto)





        {





            AddFinderHistoryItem(





                _finderUserPageHistoryByDevice,





                dto.deviceUuid,





                dto.weChatId,





                dto,





                item => $"{item.taskId}|{item.receivedAt:O}");





        }











        private void AddFinderCommentHistory(FinderCommentListDto dto)





        {





            AddFinderHistoryItem(





                _finderCommentHistoryByDevice,





                dto.deviceUuid,





                dto.weChatId,





                dto,





                item => $"{item.taskId}|{item.receivedAt:O}");





        }











        private void AddFinderHistoryItem<T>(





            Dictionary<string, List<T>> historyMap,





            string? deviceUuid,





            string? weChatId,





            T item,





            Func<T, string> identityGetter)





        {





            var key = ResolveFinderHistoryKey(deviceUuid, weChatId);





            if (string.IsNullOrWhiteSpace(key))





            {





                return;





            }











            if (!historyMap.TryGetValue(key, out var list))





            {





                list = new List<T>();





                historyMap[key] = list;





            }











            var identity = identityGetter(item);





            list.RemoveAll(existing => string.Equals(identityGetter(existing), identity, StringComparison.Ordinal));





            list.Insert(0, item);











            if (list.Count > FinderHistoryLimitPerDevice)





            {





                list.RemoveRange(FinderHistoryLimitPerDevice, list.Count - FinderHistoryLimitPerDevice);





            }





        }











        private void ReplaceFinderHistoryItems<T>(





            Dictionary<string, List<T>> historyMap,





            string? deviceUuid,





            string? weChatId,





            IEnumerable<T> items)





        {





            var key = ResolveFinderHistoryKey(deviceUuid, weChatId);





            if (string.IsNullOrWhiteSpace(key))





            {





                return;





            }











            var newList = items





                .Take(FinderHistoryLimitPerDevice)





                .ToList();











            historyMap[key] = newList;





        }











        public void SelectFinderMentionResult(FinderMentionNoticeDto dto)





        {





            CurrentFinderMentions = dto;





            NotifyStateChanged();





        }











        public void SelectFinderUserPageResult(FinderUserPageDto dto)





        {





            CurrentFinderUserPage = dto;





            NotifyStateChanged();





        }











        public void SelectFinderCommentResult(FinderCommentListDto dto)





        {





            CurrentFinderComments = dto;





            NotifyStateChanged();





        }











        public void Dispose()
        {
            _isDisposed = true;

            CancellationTokenSource? contactsCts;
            lock (_contactsReloadSync)
            {
                contactsCts = _contactsReloadCts;
                _contactsReloadCts = null;
            }

            lock (_momentsReloadSync)
            {
                _momentsReloadVersion++;
            }

            if (_realTimeService != null)
            {
                _realTimeService.OnRealtimeDataChanged -= HandleRealtimeDataChanged;
                _realTimeService.OnTaskResultReceived -= HandleTaskResultReceived;
                _realTimeService.OnFriendRequestsUpdated -= HandleFriendRequestsUpdated;
                _realTimeService.OnGroupInvitationsUpdated -= HandleGroupInvitationsUpdated;
                _realTimeService.OnContactLabelsUpdated -= HandleContactLabelsUpdated;
                _realTimeService.OnSmsRecordsUpdated -= HandleSmsRecordsUpdated;
                _realTimeService.OnCallLogRecordsUpdated -= HandleCallLogRecordsUpdated;
            }

            if (contactsCts != null)
            {
                try { contactsCts.Cancel(); } catch { }
                try { contactsCts.Dispose(); } catch { }
            }

            _contactsReloadLock.Dispose();
            _momentsReloadLock.Dispose();
            _finderHistoryLoadLock.Dispose();
            _conversationsReloadLock.Dispose();
            _messagesReloadLock.Dispose();
            _devicesReloadLock.Dispose();
            _serviceCallLock.Dispose();

            foreach (var sub in _subscriptions)
            {
                sub.Dispose();
            }

            _subscriptions.Clear();
        }

        // --- Stubbed Methods to prevent compilation errors in Pages ---











        public async Task SelectConversationAsync(Conversation conversation) 





        { 





            SelectedConversation = conversation;





            if (conversation != null && !string.IsNullOrWhiteSpace(conversation.wechatAccountId))





            {





                await ReloadCurrentMessagesAsync(conversation.wechatAccountId, conversation.conversationWxid);

                if (IsChatRoomWxid(conversation.conversationWxid))
                {
                    CurrentGroupMembers = await ExecuteServiceCallAsync(() => _service.GetGroupMembersAsync(
                        conversation.wechatAccountId,
                        conversation.conversationWxid));
                }
                else
                {
                    CurrentGroupMembers = new List<GroupMemberDto>();
                }





            }





            else





            {





                CurrentMessages = new List<Message>();
                CurrentGroupMembers = new List<GroupMemberDto>();





            }





            NotifyStateChanged();





        }

        /// <summary>
        /// 串行刷新当前会话消息。
        /// <para>这里只读取服务端数据并更新前端内存态，不直接写数据库；服务端持久化仍由 DbHelper 负责。</para>
        /// </summary>
        private async Task ReloadCurrentMessagesAsync(string accountId, string conversationWxid, int count = 50)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(conversationWxid))
            {
                CurrentMessages = new List<Message>();
                return;
            }

            await _messagesReloadLock.WaitAsync();
            try
            {
                CurrentMessages = await ExecuteServiceCallAsync(() => _service.GetMessagesAsync(accountId, conversationWxid, count));
            }
            finally
            {
                _messagesReloadLock.Release();
            }
        }

        /// <summary>
        /// 判断消息是否属于指定会话。
        /// <para>群聊消息的 senderWxid 是 roomId，receiverWxid 是当前账号；普通单聊则任一端匹配即可。</para>
        /// </summary>
        private bool IsMessageBelongsToConversation(Message msg, string? conversationWxid)
        {
            if (msg == null || string.IsNullOrWhiteSpace(conversationWxid))
            {
                return false;
            }

            return string.Equals(msg.senderWxid, conversationWxid, StringComparison.OrdinalIgnoreCase)
                || string.Equals(msg.receiverWxid, conversationWxid, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ResolveConversationWxidFromMessage(msg), conversationWxid, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判断实时推送是否为同一条消息。
        /// <para>媒体补偿、CDN 回填、原消息详情补偿会再次推送同一条消息；前端应替换旧气泡，而不是追加重复气泡。</para>
        /// </summary>
        private static bool IsSameMessageIdentity(Message existing, Message incoming)
        {
            if (existing == null || incoming == null)
            {
                return false;
            }

            if (existing.messageId > 0 && incoming.messageId > 0)
            {
                return existing.messageId == incoming.messageId;
            }

            if (existing.msgSvrId.GetValueOrDefault() != 0
                && incoming.msgSvrId.GetValueOrDefault() != 0
                && existing.msgSvrId == incoming.msgSvrId
                && SameMessageAccount(existing, incoming))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(existing.localMessageId)
                && !string.IsNullOrWhiteSpace(incoming.localMessageId)
                && string.Equals(existing.localMessageId, incoming.localMessageId, StringComparison.Ordinal)
                && SameMessageAccount(existing, incoming);
        }

        private static bool SameMessageAccount(Message left, Message right)
        {
            if (string.IsNullOrWhiteSpace(left.accountId) || string.IsNullOrWhiteSpace(right.accountId))
            {
                return true;
            }

            return string.Equals(left.accountId, right.accountId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 合并实时消息展示字段。
        /// <para>当新推送未带媒体展示字段时，保留旧气泡已加载的附件/扩展，避免补偿信息短暂消失。</para>
        /// </summary>
        private static Message MergeRealtimeMessage(Message existing, Message incoming)
        {
            incoming.mediaAttachments = MergeRealtimeMediaAttachments(existing.mediaAttachments, incoming.mediaAttachments);
            incoming.messageExtensions = MergeRealtimeMessageExtensions(existing.messageExtensions, incoming.messageExtensions);

            incoming.voiceTransText ??= existing.voiceTransText;

            return incoming;
        }

        /// <summary>
        /// 合并实时媒体附件。
        /// <para>实时补偿可能只回传新增附件，不能因此丢掉旧气泡已加载的图片、语音、视频或文件附件。</para>
        /// </summary>
        private static List<MessageMediaAttachmentDto> MergeRealtimeMediaAttachments(
            IReadOnlyList<MessageMediaAttachmentDto>? existing,
            IReadOnlyList<MessageMediaAttachmentDto>? incoming)
        {
            return MergeRealtimeListByKey(existing, incoming, GetRealtimeMediaAttachmentKey);
        }

        /// <summary>
        /// 合并实时消息扩展。
        /// <para>按扩展 key 去重，incoming 中的新值优先；existing 中未被覆盖的 latest/history/kind 继续保留。</para>
        /// </summary>
        private static List<MessageExtensionViewDto> MergeRealtimeMessageExtensions(
            IReadOnlyList<MessageExtensionViewDto>? existing,
            IReadOnlyList<MessageExtensionViewDto>? incoming)
        {
            return MergeRealtimeListByKey(existing, incoming, item => item?.extensionKey ?? string.Empty);
        }

        private static List<T> MergeRealtimeListByKey<T>(
            IReadOnlyList<T>? existing,
            IReadOnlyList<T>? incoming,
            Func<T, string> keySelector)
            where T : class
        {
            var result = new List<T>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            AddRange(incoming);
            AddRange(existing);

            return result;

            void AddRange(IReadOnlyList<T>? source)
            {
                if (source == null || source.Count == 0)
                {
                    return;
                }

                foreach (var item in source)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    var key = keySelector(item);
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        result.Add(item);
                        continue;
                    }

                    if (seen.Add(key))
                    {
                        result.Add(item);
                    }
                }
            }
        }

        private static string GetRealtimeMediaAttachmentKey(MessageMediaAttachmentDto? media)
        {
            if (media == null)
            {
                return string.Empty;
            }

            if (media.id > 0)
            {
                return $"id:{media.id}";
            }

            if (!string.IsNullOrWhiteSpace(media.mediaHash))
            {
                return $"hash:{media.mediaHash}";
            }

            return string.IsNullOrWhiteSpace(media.mediaUrl)
                ? string.Empty
                : $"url:{media.mediaUrl}";
        }

        /// <summary>
        /// 从实时消息解析会话 ID。
        /// </summary>
        private string ResolveConversationWxidFromMessage(Message msg)
        {
            var ownerWxid = SelectedDevice?.weChatId
                ?? SelectedDevice?.wx?.wechatAccount?.wxid
                ?? msg.accountId
                ?? string.Empty;

            var sender = msg.senderWxid ?? string.Empty;
            var receiver = msg.receiverWxid ?? string.Empty;

            if (IsChatRoomWxid(sender))
            {
                return sender;
            }

            if (IsChatRoomWxid(receiver))
            {
                return receiver;
            }

            if (!string.Equals(sender, ownerWxid, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(sender))
            {
                return sender;
            }

            if (!string.Equals(receiver, ownerWxid, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(receiver))
            {
                return receiver;
            }

            return string.Empty;
        }

        /// <summary>
        /// 实时单聊消息正文兜底清洗。
        /// <para>旧安卓端可能把好友 wxid 作为 clientMsgId 前缀拼入正文；这里仅修正前端内存态，持久化清洗由服务端处理。</para>
        /// </summary>
        private void NormalizeRealtimePrivateMessageContent(Message msg)
        {
            if (msg == null || string.IsNullOrEmpty(msg.content))
            {
                return;
            }

            var conversationWxid = ResolveConversationWxidFromMessage(msg);
            if (IsChatRoomWxid(conversationWxid))
            {
                return;
            }

            foreach (var prefix in EnumerateRealtimePrivateMessagePrefixes(msg, conversationWxid))
            {
                if (!msg.content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                msg.content = msg.content[prefix.Length..].TrimStart(':', '：', ' ', '\t', '\r', '\n');
                msg.content = TrimAccidentalCoordinateSuffix(msg.content);
                return;
            }

            msg.content = TrimAccidentalCoordinateSuffix(msg.content);
        }

        /// <summary>
        /// 枚举实时单聊消息可能误拼到正文前的 wxid 前缀。
        /// </summary>
        private IEnumerable<string> EnumerateRealtimePrivateMessagePrefixes(Message msg, string conversationWxid)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in new[]
            {
                msg.senderWxid,
                msg.receiverWxid,
                conversationWxid,
                SelectedConversation?.conversationWxid,
                SelectedContact?.wxid,
                SelectedContact?.friendNo
            })
            {
                if (!string.IsNullOrWhiteSpace(value) && !IsChatRoomWxid(value) && seen.Add(value.Trim()))
                {
                    yield return value.Trim();
                }
            }

            foreach (var contact in Contacts.Concat(SelectedDevice?.wx?.contacts ?? Enumerable.Empty<Contact>()))
            {
                if (!string.IsNullOrWhiteSpace(contact.wxid) && seen.Add(contact.wxid.Trim()))
                {
                    yield return contact.wxid.Trim();
                }

                if (!string.IsNullOrWhiteSpace(contact.friendNo) && seen.Add(contact.friendNo.Trim()))
                {
                    yield return contact.friendNo.Trim();
                }
            }
        }

        /// <summary>
        /// 剥离单聊文本尾部误拼接的坐标片段。
        /// </summary>
        private static string TrimAccidentalCoordinateSuffix(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return body;
            }

            return System.Text.RegularExpressions.Regex.Replace(body, @"\s+\d{2,4}\.\d{2,4}\s*$", string.Empty, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// 实时消息到达时更新本地会话列表。
        /// <para>服务端会持久化会话；前端这里先做即时展示，避免群聊 tab 需要手动刷新才出现。</para>
        /// </summary>
        private void UpsertRealtimeConversation(Message msg, string conversationWxid, bool incrementMessageCount = true)
        {
            if (msg == null || string.IsNullOrWhiteSpace(conversationWxid))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var messageTime = msg.sentAt ?? msg.receivedAt ?? now;
            var snapshot = Conversations.ToList();
            var existing = snapshot.FirstOrDefault(c =>
                string.Equals(c.conversationWxid, conversationWxid, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                existing = new Conversation
                {
                    wechatAccountId = msg.accountId
                        ?? SelectedDevice?.weChatId
                        ?? SelectedDevice?.wx?.wechatAccount?.wxid
                        ?? string.Empty,
                    conversationWxid = conversationWxid,
                    conversationType = IsChatRoomWxid(conversationWxid) ? 2 : 1,
                    displayName = ResolveRealtimeConversationDisplayName(conversationWxid),
                    displayAvatar = "images/default-avatar.png",
                    unreadCount = 0,
                    messageCount = 0,
                    lastMessageTime = messageTime,
                    lastMessageContent = string.Empty,
                    createdAt = now,
                    updatedAt = now,
                    isDeleted = false
                };
            }

            existing.conversationType = IsChatRoomWxid(conversationWxid) ? 2 : 1;
            existing.lastMessageContent = msg.content ?? string.Empty;
            existing.lastMessageTime = messageTime;
            if (incrementMessageCount)
            {
                existing.messageCount = Math.Max(0, existing.messageCount) + 1;
            }
            existing.updatedAt = now;
            existing.isDeleted = false;
            if (string.IsNullOrWhiteSpace(existing.displayName)
                || string.Equals(existing.displayName, existing.conversationWxid, StringComparison.OrdinalIgnoreCase))
            {
                existing.displayName = ResolveRealtimeConversationDisplayName(conversationWxid);
            }

            Conversations = snapshot
                .Where(c => !string.Equals(c.conversationWxid, conversationWxid, StringComparison.OrdinalIgnoreCase))
                .Prepend(existing)
                .ToList();
        }

        /// <summary>
        /// 实时会话显示名。
        /// </summary>
        private string ResolveRealtimeConversationDisplayName(string conversationWxid)
        {
            if (IsChatRoomWxid(conversationWxid))
            {
                return conversationWxid;
            }

            var contact = Contacts.FirstOrDefault(c => string.Equals(c.wxid, conversationWxid, StringComparison.OrdinalIgnoreCase))
                ?? SelectedDevice?.wx?.contacts?.FirstOrDefault(c => string.Equals(c.wxid, conversationWxid, StringComparison.OrdinalIgnoreCase));

            if (contact == null)
            {
                return conversationWxid;
            }

            if (!string.IsNullOrWhiteSpace(contact.remarks))
            {
                return contact.remarks;
            }

            return string.IsNullOrWhiteSpace(contact.nickname) ? conversationWxid : contact.nickname;
        }

        /// <summary>
        /// 判断 wxid 是否为群聊 ID。
        /// </summary>
        private static bool IsChatRoomWxid(string? wxid)
        {
            return !string.IsNullOrWhiteSpace(wxid)
                && wxid.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase);
        }





        public Task SelectContactAsync(Contact? contact) 





        { 





            SelectedContact = contact;





            NotifyStateChanged();





            return Task.CompletedTask; 





        }





        public async Task<bool> SendMessageAsync(string content, int type = 1, string atIds = "") 





        { 





            // 支持直接向联系人发送消息；此时可能没有会话对象。





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





            





            var result = await ExecuteServiceCallAsync(() => _service.SendMessageAsync(deviceUuid, targetWxid, content, type, atIds));
            if (result.success && result.taskId > 0)
            {
                _pendingChatSendContentHints[result.taskId] = content;
            }
            var success = result.success;
            if (!success && !string.IsNullOrWhiteSpace(result.message))
            {
                OnNotification?.Invoke(result.message, false);
            }





            if (success)





            {





                // Optimistic UI Update or Wait for Event





                // For now, reload messages





                var accountId = SelectedConversation?.wechatAccountId ?? SelectedDevice?.weChatId ?? "";





                if (!string.IsNullOrWhiteSpace(accountId))





                {





                    await ReloadCurrentMessagesAsync(accountId, targetWxid);
                    _ = RefreshCurrentChatAfterTaskResultAsync(deviceUuid, content);





                }





                else





                {





                    CurrentMessages = new List<Message>();





                }





                NotifyStateChanged();





            }





            return success;





        }





        /// <summary>
        /// 发送多张图片到当前会话。
        /// <para>兼容旧入口：不再下发 SendMultiPictureTask，而是逐张复用 TalkToFriendTask 图片链，避免安卓端 SendImgProxyUI 假成功。</para>
        /// </summary>
        public async Task<bool> SendMultiPictureAsync(List<string> imageUrls)
        {
            if (imageUrls == null || !imageUrls.Any(url => !string.IsNullOrWhiteSpace(url)))
            {
                OnNotification?.Invoke("请选择要发送的图片", false);
                return false;
            }

            var normalizedUrls = imageUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sentCount = 0;
            foreach (var imageUrl in normalizedUrls)
            {
                if (await SendMessageAsync(imageUrl, type: 2))
                {
                    sentCount++;
                    await Task.Delay(350);
                }
            }

            var success = sentCount == normalizedUrls.Count;
            OnNotification?.Invoke(
                success ? $"图片已逐张下发 {sentCount}/{normalizedUrls.Count}" : $"图片仅成功下发 {sentCount}/{normalizedUrls.Count}",
                success);
            return success;
        }

        public async Task<TaskResult> RequestScreenShotAsync(string deviceUuid) 





        { 





             if (string.IsNullOrWhiteSpace(deviceUuid))
             {
                 return TaskResult.Fail("设备ID为空");
             }

             var result = await ExecuteServiceCallAsync(() => _service.RequestScreenShotAsync(deviceUuid));
             if (result.success)
             {
                 var screenshotUrl = result.data as string;
                 UpdateScreenShotStatus(deviceUuid, true, result.message ?? "截图任务已完成", screenshotUrl);
             }
             else
             {
                 UpdateScreenShotStatus(deviceUuid, false, result.message ?? "截图失败", null, clearPreviewOnFailure: true);
             }

             NotifyStateChanged();

             return result;





        }






        /// <summary>
        /// 拉取个人微信二维码。
        /// </summary>
        public async Task<TaskResult> PullWeChatQrCodeAsync(string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.PullWeChatQrCodeAsync(targetDeviceUuid));
            OnNotification?.Invoke(result.success ? "个人二维码拉取指令已下发" : $"个人二维码拉取失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 拉取指定微信群二维码。
        /// <para>二维码可能有有效期，只用于临时展示/复制，不在本地长期保存。</para>
        /// </summary>
        public async Task<TaskResult> PullChatRoomQrCodeAsync(string chatRoomId, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(chatRoomId))
            {
                OnNotification?.Invoke("群 wxid 为空，无法拉取群二维码", false);
                return TaskResult.Fail("群 wxid 为空，无法拉取群二维码");
            }

            var result = await ExecuteServiceCallAsync(() => _service.PullChatRoomQrCodeAsync(targetDeviceUuid, chatRoomId));
            OnNotification?.Invoke(result.success ? "群二维码拉取指令已下发" : $"群二维码拉取失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 执行手机/微信进程侧设备操作。
        /// <para>action 使用 PhoneActionTask.EnumPhoneAction 数值；PhoneCall 只表示已下发。</para>
        /// </summary>
        public async Task<TaskResult> ExecutePhoneActionAsync(int action, string strParam = "", int intParam = 0, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.ExecutePhoneActionAsync(targetDeviceUuid, action, strParam, intParam));
            OnNotification?.Invoke(result.success ? (result.message ?? "设备操作已下发") : $"设备操作失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 拉取附近 POI 列表。
        /// </summary>
        public async Task<TaskResult> GetPoiListAsync(double lat, double lng, string keyword = "", string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.GetPoiListAsync(targetDeviceUuid, lat, lng, keyword));
            OnNotification?.Invoke(result.success ? "POI 列表拉取指令已下发" : $"POI 列表拉取失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 拉取微信表情详情。
        /// </summary>
        public async Task<TaskResult> PullEmojiInfoAsync(string md5, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(md5))
            {
                OnNotification?.Invoke("表情 MD5 不能为空", false);
                return TaskResult.Fail("表情 MD5 不能为空");
            }

            var result = await ExecuteServiceCallAsync(() => _service.PullEmojiInfoAsync(targetDeviceUuid, md5));
            OnNotification?.Invoke(result.success ? "表情信息拉取指令已下发" : $"表情信息拉取失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 为当前聊天中的表情消息补图。
        /// <para>1272 返回表情 CDN 参数后，服务端自动下发 1269，最终等待 1271 回填 MessageMedias。</para>
        /// </summary>
        public async Task<TaskResult> PullEmojiInfoForMessageAsync(
            string md5,
            long msgSvrId,
            string friendId = "",
            string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(md5) || msgSvrId == 0)
            {
                OnNotification?.Invoke("表情 MD5 或 MsgSvrId 为空，无法补图", false);
                return TaskResult.Fail("表情 MD5 或 MsgSvrId 为空，无法补图");
            }

            var result = await ExecuteServiceCallAsync(() => _service.PullEmojiInfoForMessageAsync(targetDeviceUuid, md5, msgSvrId, friendId));
            OnNotification?.Invoke(result.success ? "表情补图指令已下发，等待 CDN 回填" : $"表情补图失败：{result.message}", result.success);
            if (result.success)
            {
                _ = RefreshCurrentChatAfterTaskResultAsync(targetDeviceUuid, "表情补图");
            }

            return result;
        }

        /// <summary>
        /// 搜索微信联系人。
        /// </summary>
        public async Task<TaskResult> FindContactAsync(string content, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                OnNotification?.Invoke("搜索内容不能为空", false);
                return TaskResult.Fail("搜索内容不能为空");
            }

            var result = await ExecuteServiceCallAsync(() => _service.FindContactAsync(targetDeviceUuid, content));
            OnNotification?.Invoke(result.success ? "搜索联系人指令已下发" : $"搜索联系人失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 查询微信定位。
        /// </summary>
        public async Task<TaskResult> GetWeChatLocationAsync(bool noCache = false, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.GetWeChatLocationAsync(targetDeviceUuid, noCache));
            OnNotification?.Invoke(result.success ? "微信定位查询指令已下发" : $"微信定位查询失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 查询微信零钱和银行卡摘要。
        /// </summary>
        public async Task<TaskResult> GetWalletBalanceAsync(int flag = 0, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.GetWalletBalanceAsync(targetDeviceUuid, flag));
            OnNotification?.Invoke(result.success ? "钱包余额查询指令已下发" : $"钱包余额查询失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 查询手机电量、网络和存储状态。
        /// </summary>
        public async Task<TaskResult> GetPhoneStateAsync(string imei = "", string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.GetPhoneStateAsync(targetDeviceUuid, imei));
            OnNotification?.Invoke(result.success ? "手机状态查询指令已下发" : $"手机状态查询失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 请求客户端同步企微用户列表。
        /// <para>结果由 QwUserPUshNotice 异步上报，服务端落库后联系人列表会刷新。</para>
        /// </summary>
        public async Task<TaskResult> SyncQwUsersAsync(string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SyncQwUsersAsync(targetDeviceUuid));
            OnNotification?.Invoke(result.success ? "企微用户同步指令已下发" : $"企微用户同步失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 请求客户端回传聊天消息 MsgSvrId 快照。
        /// </summary>
        public async Task<TaskResult> SyncChatMsgIdsAsync(long startTime, long endTime, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (startTime <= 0 || endTime <= 0 || startTime >= endTime)
            {
                OnNotification?.Invoke("消息 ID 快照时间范围无效", false);
                return TaskResult.Fail("消息 ID 快照时间范围无效");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SyncChatMsgIdsAsync(targetDeviceUuid, startTime, endTime));
            OnNotification?.Invoke(result.success ? "聊天消息 ID 快照同步指令已下发" : $"聊天消息 ID 快照同步失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 请求客户端同步历史聊天消息。
        /// </summary>
        public async Task<TaskResult> SyncHistoryMessagesAsync(string friendId = "", long startTime = 0, long endTime = 0, int flag = 0, int count = 50, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SyncHistoryMessagesAsync(targetDeviceUuid, friendId, startTime, endTime, flag, count));
            OnNotification?.Invoke(result.success ? "历史消息同步指令已下发" : $"历史消息同步失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 请求客户端同步指定会话已读状态。
        /// </summary>
        public async Task<TaskResult> SyncMessageReadAsync(string friendId, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(friendId))
            {
                OnNotification?.Invoke("会话 ID 为空，无法同步已读状态", false);
                return TaskResult.Fail("会话 ID 为空，无法同步已读状态");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SyncMessageReadAsync(targetDeviceUuid, friendId));
            OnNotification?.Invoke(result.success ? "会话已读同步指令已下发" : $"会话已读同步失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 请求客户端回传未读会话列表。
        /// </summary>
        public async Task<TaskResult> SyncUnreadListAsync(string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SyncUnreadListAsync(targetDeviceUuid));
            OnNotification?.Invoke(result.success ? "未读会话同步指令已下发" : $"未读会话同步失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 请求客户端同步单个会话未读状态。
        /// </summary>
        public async Task<TaskResult> SyncConversationUnreadAsync(string friendId, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(friendId))
            {
                OnNotification?.Invoke("会话 ID 为空，无法同步未读状态", false);
                return TaskResult.Fail("会话 ID 为空，无法同步未读状态");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SyncConversationUnreadAsync(targetDeviceUuid, friendId));
            OnNotification?.Invoke(result.success ? "单会话未读同步指令已下发" : $"单会话未读同步失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 请求客户端同步业务联系人列表。
        /// </summary>
        public async Task<TaskResult> SyncBizContactsAsync(string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SyncBizContactsAsync(targetDeviceUuid));
            OnNotification?.Invoke(result.success ? "业务联系人同步指令已下发" : $"业务联系人同步失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 请求客户端同步企微会话列表。
        /// </summary>
        public async Task<TaskResult> SyncQwConversationsAsync(long startTime = 0, long endTime = 0, int limit = 100, int offset = 0, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SyncQwConversationsAsync(targetDeviceUuid, startTime, endTime, limit, offset));
            OnNotification?.Invoke(result.success ? "企微会话同步指令已下发" : $"企微会话同步失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 请求客户端同步联系人标签列表。
        /// </summary>
        public async Task<TaskResult> SyncContactLabelsAsync(string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SyncContactLabelsAsync(targetDeviceUuid));
            OnNotification?.Invoke(result.success ? "联系人标签同步指令已下发" : $"联系人标签同步失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 读取当前微信账号或指定账号的联系人标签字典快照。
        /// <para>该方法只读已落库 ContactTags；同步手机端标签请调用 SyncContactLabelsAsync。</para>
        /// </summary>
        public async Task<List<ContactLabelDto>> GetContactLabelsAsync(string? accountId = null, bool includeDeleted = false)
        {
            var ownerWxid = string.IsNullOrWhiteSpace(accountId)
                ? FirstNonEmptyLocal(
                    SelectedDevice?.weChatId,
                    SelectedDevice?.wx?.wechatAccount?.wxid)
                : accountId.Trim();

            if (string.IsNullOrWhiteSpace(ownerWxid))
            {
                return new List<ContactLabelDto>();
            }

            return await ExecuteServiceCallAsync(() => _service.GetContactLabelsAsync(ownerWxid, includeDeleted));
        }

        /// <summary>
        /// 发送手机短信。
        /// </summary>
        public async Task<TaskResult> SendSmsAsync(string number, string content, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(content))
            {
                OnNotification?.Invoke("号码和短信内容不能为空", false);
                return TaskResult.Fail("号码和短信内容不能为空");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SendSmsAsync(targetDeviceUuid, number, content));
            OnNotification?.Invoke(result.success ? "短信发送任务已下发" : $"短信发送失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 拉取手机短信历史。
        /// </summary>
        public async Task<TaskResult> PullSmsAsync(long startTime, long endTime, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.PullSmsAsync(targetDeviceUuid, startTime, endTime));
            OnNotification?.Invoke(result.success ? "短信历史拉取任务已下发" : $"短信历史拉取失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 拉取手机通话记录。
        /// </summary>
        public async Task<TaskResult> PullCallLogsAsync(long startTime, long endTime, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.PullCallLogsAsync(targetDeviceUuid, startTime, endTime));
            OnNotification?.Invoke(result.success ? "通话记录拉取任务已下发" : $"通话记录拉取失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 读取当前微信账号或指定账号的短信记录快照。
        /// </summary>
        public async Task<List<SmsRecordDto>> GetSmsRecordsAsync(string? accountId = null, string imei = "", int count = 200)
        {
            var ownerWxid = string.IsNullOrWhiteSpace(accountId)
                ? FirstNonEmptyLocal(
                    SelectedDevice?.weChatId,
                    SelectedDevice?.wx?.wechatAccount?.wxid)
                : accountId.Trim();

            if (string.IsNullOrWhiteSpace(ownerWxid))
            {
                return new List<SmsRecordDto>();
            }

            return await ExecuteServiceCallAsync(() => _service.GetSmsRecordsAsync(ownerWxid, imei, count));
        }

        /// <summary>
        /// 读取当前微信账号或指定账号的通话记录快照。
        /// </summary>
        public async Task<List<CallLogRecordDto>> GetCallLogRecordsAsync(string? accountId = null, string imei = "", int count = 200)
        {
            var ownerWxid = string.IsNullOrWhiteSpace(accountId)
                ? FirstNonEmptyLocal(
                    SelectedDevice?.weChatId,
                    SelectedDevice?.wx?.wechatAccount?.wxid)
                : accountId.Trim();

            if (string.IsNullOrWhiteSpace(ownerWxid))
            {
                return new List<CallLogRecordDto>();
            }

            return await ExecuteServiceCallAsync(() => _service.GetCallLogRecordsAsync(ownerWxid, imei, count));
        }

        /// <summary>
        /// 为通话录音生成短期播放链接。
        /// <para>页面播放录音时必须走后端 token 代理，不直接打开数据库里的 recordUrl。</para>
        /// </summary>
        public async Task<MediaAccessTokenDto> CreateCallRecordingAccessTokenAsync(int callLogId, int expiresMinutes = 5)
        {
            return await ExecuteServiceCallAsync(() => _service.CreateCallRecordingAccessTokenAsync(callLogId, expiresMinutes));
        }

        /// <summary>
        /// 为设备截图生成短期预览链接。
        /// <para>设备管理页和设备控制弹窗预览截图时必须走后端 token 代理，不直接打开上传返回的截图 URL。</para>
        /// </summary>
        public async Task<MediaAccessTokenDto> CreateScreenshotAccessTokenAsync(string screenshotUrl, string deviceUuid = "", int expiresMinutes = 5)
        {
            return await ExecuteServiceCallAsync(() => _service.CreateScreenshotAccessTokenAsync(screenshotUrl, deviceUuid, expiresMinutes));
        }

        /// <summary>
        /// 为最近一次截图准备短期预览链接。
        /// </summary>
        public async Task<MediaAccessTokenDto> PrepareLastScreenShotAccessAsync(int expiresMinutes = 5)
        {
            if (string.IsNullOrWhiteSpace(LastScreenShotUrl))
            {
                return MediaAccessTokenDto.Fail("当前没有可预览的截图。");
            }

            if (string.IsNullOrWhiteSpace(LastScreenShotDeviceUuid))
            {
                return MediaAccessTokenDto.Fail("当前截图缺少设备归属。");
            }

            IsPreparingScreenShotAccess = true;
            NotifyStateChanged();

            try
            {
                var token = await CreateScreenshotAccessTokenAsync(LastScreenShotUrl, LastScreenShotDeviceUuid, expiresMinutes);
                if (!token.Success)
                {
                    return token;
                }

                LastScreenShotAccessUrl = token.Url;
                LastScreenShotAccessExpiresAt = token.ExpiresAt;
                return token;
            }
            finally
            {
                IsPreparingScreenShotAccess = false;
                NotifyStateChanged();
            }
        }

        /// <summary>
        /// 为聊天媒体生成短期访问链接。
        /// <para>IM 页面查看图片、视频、语音、文件时必须走后端 token 代理，不直接打开数据库里的 mediaUrl。</para>
        /// </summary>
        public async Task<MediaAccessTokenDto> CreateMediaAccessTokenAsync(string mediaUrl, string accountId = "", string deviceUuid = "", int expiresMinutes = 5)
        {
            return await ExecuteServiceCallAsync(() => _service.CreateMediaAccessTokenAsync(mediaUrl, accountId, deviceUuid, expiresMinutes));
        }

        /// <summary>
        /// 创建或重命名联系人标签。
        /// </summary>
        public async Task<TaskResult> SaveContactLabelAsync(string labelName, int labelId = 0, string addList = "", string delList = "", string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(labelName) && labelId <= 0)
            {
                OnNotification?.Invoke("标签名和标签 ID 不能同时为空", false);
                return TaskResult.Fail("标签名和标签 ID 不能同时为空");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SaveContactLabelAsync(targetDeviceUuid, labelName, labelId, addList, delList));
            OnNotification?.Invoke(result.success ? "联系人标签任务已下发" : $"联系人标签任务失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 删除联系人标签。
        /// </summary>
        public async Task<TaskResult> DeleteContactLabelAsync(int labelId, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (labelId <= 0)
            {
                OnNotification?.Invoke("标签 ID 无效，无法删除", false);
                return TaskResult.Fail("标签 ID 无效，无法删除");
            }

            var result = await ExecuteServiceCallAsync(() => _service.DeleteContactLabelAsync(targetDeviceUuid, labelId));
            OnNotification?.Invoke(result.success ? "联系人标签删除任务已下发" : $"联系人标签删除失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 设置单个好友的完整标签 ID 集合。
        /// </summary>
        public async Task<TaskResult> SetContactLabelsAsync(string friendId, IEnumerable<int>? labelIds, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(friendId))
            {
                OnNotification?.Invoke("好友 wxid 为空，无法设置标签", false);
                return TaskResult.Fail("好友 wxid 为空，无法设置标签");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SetContactLabelsAsync(targetDeviceUuid, friendId, labelIds));
            OnNotification?.Invoke(result.success ? "联系人标签设置任务已下发" : $"联系人标签设置失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 请求客户端回传当前本地配置快照。
        /// </summary>
        public async Task<TaskResult> TriggerConfigPushAsync(string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.TriggerConfigPushAsync(targetDeviceUuid));
            OnNotification?.Invoke(result.success ? "配置同步指令已下发" : $"配置同步失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 下发 Android 设备级配置。
        /// <para>只下发本次选择的配置键，不持久化到账号设置。</para>
        /// </summary>
        public async Task<TaskResult> SetDeviceConfigAsync(DeviceConfigDto config, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (config == null || config.IsEmpty)
            {
                OnNotification?.Invoke("没有选择任何需要下发的配置项", false);
                return TaskResult.Fail("没有选择任何需要下发的配置项");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SetDeviceConfigAsync(targetDeviceUuid, config));
            OnNotification?.Invoke(result.success ? "设备配置已下发" : $"设备配置下发失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 下发微信违禁词列表。
        /// <para>空列表会清空客户端本地违禁词。</para>
        /// </summary>
        public async Task<TaskResult> SetForbiddenWordAsync(IEnumerable<string>? words, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var normalizedWords = (words ?? Enumerable.Empty<string>())
                .Select(word => word?.Trim() ?? string.Empty)
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var result = await ExecuteServiceCallAsync(() => _service.SetForbiddenWordAsync(targetDeviceUuid, normalizedWords));
            OnNotification?.Invoke(result.success ? $"违禁词列表已下发：{normalizedWords.Length} 条" : $"违禁词下发失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 请求客户端拉取微信好友申请历史/补偿列表。
        /// <para>
        /// 下发成功后等待 FriendAddReqListNotice 异步回传；页面会通过 FriendRequestsUpdatedEvent 自动刷新。
        /// </para>
        /// </summary>
        public async Task<TaskResult> PullFriendAddReqListAsync(long startTime = 0, bool onlyNew = true, bool getAll = false, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.PullFriendAddReqListAsync(targetDeviceUuid, startTime, onlyNew, getAll));
            OnNotification?.Invoke(result.success ? "好友申请列表拉取指令已下发" : $"好友申请列表拉取失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 补偿单条聊天消息。
        /// <para>下发成功后等待 RequestTalkMsgTaskResultNotice 异步回填消息和会话。</para>
        /// </summary>
        public async Task<TaskResult> RequestTalkMsgAsync(long msgSvrId, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (msgSvrId == 0)
            {
                OnNotification?.Invoke("MsgSvrId 为空，无法补偿消息", false);
                return TaskResult.Fail("MsgSvrId 为空，无法补偿消息");
            }

            var result = await ExecuteServiceCallAsync(() => _service.RequestTalkMsgAsync(targetDeviceUuid, msgSvrId));
            OnNotification?.Invoke(result.success ? "聊天消息补偿指令已下发" : $"聊天消息补偿失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 补偿原始聊天正文/XML。
        /// </summary>
        public async Task<TaskResult> RequestTalkContentAsync(long msgSvrId, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (msgSvrId == 0)
            {
                OnNotification?.Invoke("MsgSvrId 为空，无法补偿原始正文", false);
                return TaskResult.Fail("MsgSvrId 为空，无法补偿原始正文");
            }

            var result = await ExecuteServiceCallAsync(() => _service.RequestTalkContentAsync(targetDeviceUuid, msgSvrId));
            OnNotification?.Invoke(result.success ? "原始消息正文补偿指令已下发" : $"原始消息正文补偿失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 补偿聊天消息详情。
        /// </summary>
        public async Task<TaskResult> RequestTalkDetailAsync(string friendId, long msgId, string msgSvrId = "", string md5 = "", bool getOriginal = false, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (msgId == 0 && string.IsNullOrWhiteSpace(msgSvrId))
            {
                OnNotification?.Invoke("消息标识为空，无法补偿详情", false);
                return TaskResult.Fail("消息标识为空，无法补偿详情");
            }

            var result = await ExecuteServiceCallAsync(() => _service.RequestTalkDetailAsync(targetDeviceUuid, friendId, msgId, msgSvrId, md5, getOriginal));
            OnNotification?.Invoke(result.success ? "消息详情补偿指令已下发" : $"消息详情补偿失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 请求语音转文字。
        /// <para>识别结果由服务端写入 VoiceToTextLogs，并通过任务回执提示。</para>
        /// </summary>
        public async Task<TaskResult> VoiceTransTextAsync(string friendId, long msgSvrId, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(friendId) || msgSvrId == 0)
            {
                OnNotification?.Invoke("会话 ID 或 MsgSvrId 为空，无法语音转文字", false);
                return TaskResult.Fail("会话 ID 或 MsgSvrId 为空，无法语音转文字");
            }

            var result = await ExecuteServiceCallAsync(() => _service.VoiceTransTextAsync(targetDeviceUuid, friendId, msgSvrId));
            var message = string.IsNullOrWhiteSpace(result.message)
                ? (result.success ? "语音转文字任务已完成" : "语音转文字失败")
                : result.message;
            OnNotification?.Invoke(result.success ? message : $"语音转文字失败：{message}", result.success);
            return result;
        }

        /// <summary>
        /// 请求客户端执行 GetA8Key。
        /// </summary>
        public async Task<TaskResult> GetA8KeyAsync(int type, string url, string userName = "", string msgSvrId = "", int reason = 0, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                OnNotification?.Invoke("URL 不能为空", false);
                return TaskResult.Fail("URL 不能为空");
            }

            var result = await ExecuteServiceCallAsync(() => _service.GetA8KeyAsync(targetDeviceUuid, type, url, userName, msgSvrId, reason));
            OnNotification?.Invoke(result.success ? "A8Key 获取任务已下发" : $"A8Key 获取失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 修改微信资料或隐私设置。
        /// </summary>
        public async Task<TaskResult> UpdateWechatSettingAsync(int action, string content = "", int intParam = 0, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.UpdateWechatSettingAsync(targetDeviceUuid, action, content, intParam));
            OnNotification?.Invoke(result.success ? "微信资料设置任务已下发" : $"微信资料设置失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 撤回聊天消息。
        /// </summary>
        public async Task<TaskResult> RevokeMessageAsync(string friendId, long msgSvrId, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(friendId) || msgSvrId == 0)
            {
                OnNotification?.Invoke("会话 ID 或 MsgSvrId 为空，无法撤回消息", false);
                return TaskResult.Fail("会话 ID 或 MsgSvrId 为空，无法撤回消息");
            }

            var result = await ExecuteServiceCallAsync(() => _service.RevokeMessageAsync(targetDeviceUuid, friendId, msgSvrId));
            OnNotification?.Invoke(result.success ? "消息撤回任务已下发" : $"消息撤回失败：{result.message}", result.success);
            if (result.success)
            {
                _ = RefreshCurrentChatAfterTaskResultAsync(targetDeviceUuid, "消息撤回");
            }
            return result;
        }

        /// <summary>
        /// 转发一条已有聊天消息。
        /// <para>talker 是原消息所在会话，friendIds 为目标接收人 wxid 列表，多个目标用逗号分隔。</para>
        /// </summary>
        public async Task<TaskResult> ForwardMessageAsync(string talker, long msgSvrId, string friendIds, string extMsg = "", string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var normalizedFriendIds = NormalizeForwardTargetIds(friendIds);
            if (string.IsNullOrWhiteSpace(talker) || msgSvrId == 0 || string.IsNullOrWhiteSpace(normalizedFriendIds))
            {
                OnNotification?.Invoke("原会话、MsgSvrId 或目标接收人为空，无法转发消息", false);
                return TaskResult.Fail("原会话、MsgSvrId 或目标接收人为空，无法转发消息");
            }

            var result = await ExecuteServiceCallAsync(() => _service.ForwardMessageAsync(targetDeviceUuid, talker, msgSvrId, normalizedFriendIds, extMsg));
            OnNotification?.Invoke(result.success ? "消息转发任务已下发" : $"消息转发失败：{result.message}", result.success);
            if (result.success)
            {
                _ = RefreshCurrentChatAfterTaskResultAsync(targetDeviceUuid, "消息转发");
            }
            return result;
        }

        /// <summary>
        /// 转发多条已有聊天消息。
        /// <para>talker 是原消息所在会话，msgIds 为待转发消息 ID，friendIds 为目标接收人 wxid 列表。</para>
        /// </summary>
        public async Task<TaskResult> ForwardMultiMessageAsync(string talker, IEnumerable<long>? msgIds, string friendIds, string extMsg = "", bool sendRecord = false, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var normalizedMsgIds = (msgIds ?? Enumerable.Empty<long>()).Where(id => id > 0).Distinct().ToArray();
            var normalizedFriendIds = NormalizeForwardTargetIds(friendIds);
            if (string.IsNullOrWhiteSpace(talker) || normalizedMsgIds.Length == 0 || string.IsNullOrWhiteSpace(normalizedFriendIds))
            {
                OnNotification?.Invoke("原会话、消息 ID 列表或目标接收人为空，无法转发多条消息", false);
                return TaskResult.Fail("原会话、消息 ID 列表或目标接收人为空，无法转发多条消息");
            }

            var result = await ExecuteServiceCallAsync(() => _service.ForwardMultiMessageAsync(targetDeviceUuid, talker, normalizedMsgIds, normalizedFriendIds, extMsg, sendRecord));
            OnNotification?.Invoke(result.success ? "多条消息转发任务已下发" : $"多条消息转发失败：{result.message}", result.success);
            if (result.success)
            {
                _ = RefreshCurrentChatAfterTaskResultAsync(targetDeviceUuid, "多条消息转发");
            }
            return result;
        }

        /// <summary>
        /// 按原始内容转发消息。
        /// </summary>
        public async Task<TaskResult> ForwardMessageByContentAsync(string friendIds, long msgSvrId, int msgType, string content, string thumb = "", string extMsg = "", string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var normalizedFriendIds = NormalizeForwardTargetIds(friendIds);
            if (string.IsNullOrWhiteSpace(normalizedFriendIds)
                || msgType <= 0
                || (msgSvrId == 0 && string.IsNullOrWhiteSpace(content)))
            {
                OnNotification?.Invoke("目标接收人、消息类型为空，或缺少 MsgSvrId 且内容为空，无法按内容转发", false);
                return TaskResult.Fail("目标接收人、消息类型为空，或缺少 MsgSvrId 且内容为空，无法按内容转发");
            }

            if (CountForwardTargetIds(normalizedFriendIds) != 1)
            {
                const string singleTargetMessage = "按内容转发当前只支持单个目标，请选择一位好友或群聊";
                OnNotification?.Invoke(singleTargetMessage, false);
                return TaskResult.Fail(singleTargetMessage);
            }

            var result = await ExecuteServiceCallAsync(() => _service.ForwardMessageByContentAsync(targetDeviceUuid, normalizedFriendIds, msgSvrId, msgType, content, thumb, extMsg));
            OnNotification?.Invoke(result.success ? "原始内容转发任务已下发" : $"原始内容转发失败：{result.message}", result.success);
            if (result.success)
            {
                _ = RefreshCurrentChatAfterTaskResultAsync(targetDeviceUuid, "原始内容转发");
            }
            return result;
        }

        private static string NormalizeForwardTargetIds(string friendIds)
        {
            return string.Join(",", SplitForwardTargetIds(friendIds));
        }

        private static int CountForwardTargetIds(string friendIds)
        {
            return SplitForwardTargetIds(friendIds).Length;
        }

        private static string[] SplitForwardTargetIds(string friendIds)
        {
            return (friendIds ?? string.Empty)
                .Split(new[] { ',', '，', ';', '；', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// 清空微信端聊天记录。
        /// <para>该操作不会删除 SCRM 服务端已落库消息。</para>
        /// </summary>
        public async Task<TaskResult> ClearAllChatMsgAsync(int flag = 0, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.ClearAllChatMsgAsync(targetDeviceUuid, flag));
            OnNotification?.Invoke(result.success ? "微信端聊天记录清空任务已下发" : $"微信端聊天记录清空失败：{result.message}", result.success);
            if (result.success)
            {
                var accountId = FirstNonEmptyLocal(SelectedDevice?.weChatId, SelectedDevice?.wx?.wechatAccount?.wxid);
                if (!string.IsNullOrWhiteSpace(accountId))
                {
                    ScheduleConversationsReload(accountId, TimeSpan.FromSeconds(2));
                    ScheduleConversationsReload(accountId, TimeSpan.FromSeconds(6));
                }
            }
            return result;
        }

        /// <summary>
        /// 查询红包详情。
        /// </summary>
        public async Task<TaskResult> QueryHbDetailAsync(string hbUrl, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(hbUrl))
            {
                OnNotification?.Invoke("红包链接为空", false);
                return TaskResult.Fail("红包链接为空");
            }

            var result = await ExecuteServiceCallAsync(() => _service.QueryHbDetailAsync(targetDeviceUuid, hbUrl));
            OnNotification?.Invoke(result.success ? "红包详情查询任务已下发" : $"红包详情查询失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 查询红包状态。
        /// </summary>
        public async Task<TaskResult> QueryHbStatusAsync(string hbUrl, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(hbUrl))
            {
                OnNotification?.Invoke("红包链接为空", false);
                return TaskResult.Fail("红包链接为空");
            }

            var result = await ExecuteServiceCallAsync(() => _service.QueryHbStatusAsync(targetDeviceUuid, hbUrl));
            OnNotification?.Invoke(result.success ? "红包状态查询任务已下发" : $"红包状态查询失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 发送微信红包。
        /// <para>金额单位为分；支付密码只随本次服务调用传输，不在 Store 中保存。</para>
        /// </summary>
        public async Task<TaskResult> SendLuckyMoneyAsync(string friendId, int money, int number, string passwd, string wish = "", string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(friendId))
            {
                OnNotification?.Invoke("红包接收人为空", false);
                return TaskResult.Fail("红包接收人为空");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SendLuckyMoneyAsync(targetDeviceUuid, friendId, money, number, passwd, wish));
            OnNotification?.Invoke(result.success ? "发红包任务已下发" : $"发红包失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 执行微信转账。
        /// <para>金额单位为分；支付密码只随本次服务调用传输，不在 Store 中保存。</para>
        /// </summary>
        public async Task<TaskResult> RemittanceAsync(string friendId, int money, string passwd, string memo = "", string roomId = "", string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(friendId))
            {
                OnNotification?.Invoke("转账收款人为空", false);
                return TaskResult.Fail("转账收款人为空");
            }

            var result = await ExecuteServiceCallAsync(() => _service.RemittanceAsync(targetDeviceUuid, friendId, money, passwd, memo, roomId));
            OnNotification?.Invoke(result.success ? "转账任务已下发" : $"转账失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 通过二维码 URL 或二维码原始内容加入群聊。
        /// <para>该方法只下发 JoinGroupByQrTask(1267)，不直接写入群或联系人数据库；最终状态以后续安卓端回传为准。</para>
        /// </summary>
        public async Task<TaskResult> JoinGroupByQrAsync(string qrUrl = "", string qrContent = "", string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(qrUrl) && string.IsNullOrWhiteSpace(qrContent))
            {
                OnNotification?.Invoke("二维码 URL 和二维码内容不能同时为空", false);
                return TaskResult.Fail("二维码 URL 和二维码内容不能同时为空");
            }

            var result = await ExecuteServiceCallAsync(() => _service.JoinGroupByQrAsync(targetDeviceUuid, qrUrl, qrContent));
            OnNotification?.Invoke(
                result.success
                    ? (result.message ?? "二维码入群任务已下发，等待安卓端回执。")
                    : $"二维码入群任务失败：{result.message}",
                result.success);
            return result;
        }

        /// <summary>
        /// 向指定微信群下发接龙任务。
        /// <para>协议字段 Chatoom 在 proto 中保持历史拼写，Store 对页面只暴露 chatRoomId 语义。</para>
        /// </summary>
        public async Task<TaskResult> SendJielongAsync(
            string chatRoomId,
            string content,
            string title = "",
            string sample = "",
            string memo = "",
            long msgSvrId = 0,
            string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(chatRoomId))
            {
                OnNotification?.Invoke("群聊 ID 为空", false);
                return TaskResult.Fail("群聊 ID 为空");
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                OnNotification?.Invoke("接龙内容不能为空", false);
                return TaskResult.Fail("接龙内容不能为空");
            }

            var result = await ExecuteServiceCallAsync(() => _service.SendJielongAsync(targetDeviceUuid, chatRoomId, content, title, sample, memo, msgSvrId));
            OnNotification?.Invoke(
                result.success
                    ? (result.message ?? "群接龙任务已下发，等待安卓端回执。")
                    : $"群接龙任务失败：{result.message}",
                result.success);
            return result;
        }

        /// <summary>
        /// 请求客户端执行微信账号登出。
        /// </summary>
        public async Task<TaskResult> WechatLogoutAsync(string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.WechatLogoutAsync(targetDeviceUuid));
            OnNotification?.Invoke(result.success ? "微信登出任务已下发" : $"微信登出失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 请求 Android 下载微信 CDN 文件并回填消息媒体 URL。
        /// </summary>
        public async Task<TaskResult> DownloadCdnFileAsync(string cdnUrl, string cdnKey, int fileType, string fileId = "", string fileFmt = "", int fileSize = 0, long msgSvrId = 0, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            if (string.IsNullOrWhiteSpace(cdnUrl) || msgSvrId == 0)
            {
                OnNotification?.Invoke("CDN URL 或 MsgSvrId 为空，无法下载文件", false);
                return TaskResult.Fail("CDN URL 或 MsgSvrId 为空，无法下载文件");
            }

            var result = await ExecuteServiceCallAsync(() => _service.DownloadCdnFileAsync(targetDeviceUuid, cdnUrl, cdnKey, fileType, fileId, fileFmt, fileSize, msgSvrId));
            OnNotification?.Invoke(result.success ? "CDN 文件下载指令已下发" : $"CDN 文件下载失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 启动好友检测/清粉任务。
        /// </summary>
        public async Task<TaskResult> StartFriendDetectAsync(string message, bool onlyCheck = true, int skipHour = 24, int mode = 0, int max = 0, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.StartFriendDetectAsync(targetDeviceUuid, message, onlyCheck, skipHour, mode, max));
            OnNotification?.Invoke(result.success ? "好友检测任务已启动" : $"好友检测启动失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 停止好友检测/清粉任务。
        /// </summary>
        public async Task<TaskResult> StopFriendDetectAsync(long taskId = 0, string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.StopFriendDetectAsync(targetDeviceUuid, taskId));
            OnNotification?.Invoke(result.success ? "好友检测停止指令已下发" : $"好友检测停止失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 拉取好友检测/清粉最终结果。
        /// </summary>
        public async Task<TaskResult> GetFriendDetectResultAsync(string? deviceUuid = null)
        {
            var targetDeviceUuid = string.IsNullOrWhiteSpace(deviceUuid) ? SelectedDevice?.uuid ?? string.Empty : deviceUuid;
            if (string.IsNullOrWhiteSpace(targetDeviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.GetFriendDetectResultAsync(targetDeviceUuid));
            OnNotification?.Invoke(result.success ? "好友检测结果拉取指令已下发" : $"好友检测结果拉取失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 加载当前账号会话列表。
        /// </summary>
        public async Task LoadConversationsAsync(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                Conversations = new List<Conversation>();
                NotifyStateChanged();
                return;
            }

            await _conversationsReloadLock.WaitAsync();
            try
            {
                if (_isDisposed)
                {
                    return;
                }

                var loaded = await ExecuteServiceCallAsync(() => _service.GetConversationsAsync(accountId));
                Conversations = MergeRealtimeConversations(Conversations, loaded);
                _logger.LogInformation(
                    "[CrmStore] 会话列表刷新完成：AccountId={AccountId}, Loaded={LoadedCount}, Total={TotalCount}, ChatRooms={ChatRoomCount}",
                    accountId,
                    loaded?.Count ?? 0,
                    Conversations.Count,
                    Conversations.Count(c => c.conversationType == 2 || IsChatRoomWxid(c.conversationWxid)));
                NotifyStateChanged();
            }
            finally
            {
                _conversationsReloadLock.Release();
            }
        }

        /// <summary>
        /// 合并服务端会话快照与本地实时占位会话。
        /// <para>
        /// 群消息先到而 ConversationPushNotice 仍未回包时，前端会先创建本地会话；
        /// 后台刷新如果直接覆盖列表，会把这个本地群会话短暂清掉，导致群聊页看起来“消失”。
        /// 这里按 conversationWxid 合并，服务端数据优先，本地占位兜底。
        /// </para>
        /// </summary>
        private static List<Conversation> MergeRealtimeConversations(
            IEnumerable<Conversation>? currentConversations,
            IEnumerable<Conversation>? loadedConversations)
        {
            var merged = new Dictionary<string, Conversation>(StringComparer.OrdinalIgnoreCase);
            var loadedSnapshot = loadedConversations?.ToList() ?? new List<Conversation>();
            var currentSnapshot = currentConversations?.ToList() ?? new List<Conversation>();

            foreach (var conversation in loadedSnapshot)
            {
                if (!string.IsNullOrWhiteSpace(conversation.conversationWxid))
                {
                    merged[conversation.conversationWxid] = conversation;
                }
            }

            foreach (var conversation in currentSnapshot)
            {
                if (conversation == null || string.IsNullOrWhiteSpace(conversation.conversationWxid))
                {
                    continue;
                }

                if (!merged.TryGetValue(conversation.conversationWxid, out var existing))
                {
                    merged[conversation.conversationWxid] = conversation;
                    continue;
                }

                // 服务端快照已存在时，只保留本地实时消息带来的更新鲜预览，避免刷新后最后一条消息倒退为空。
                if ((conversation.lastMessageTime > existing.lastMessageTime && !string.IsNullOrWhiteSpace(conversation.lastMessageContent))
                    || string.IsNullOrWhiteSpace(existing.lastMessageContent))
                {
                    existing.lastMessageContent = conversation.lastMessageContent;
                    existing.lastMessageTime = conversation.lastMessageTime;
                    existing.messageCount = Math.Max(existing.messageCount, conversation.messageCount);
                    existing.updatedAt = conversation.updatedAt > existing.updatedAt ? conversation.updatedAt : existing.updatedAt;
                }
            }

            return merged.Values
                .OrderByDescending(c => c.lastMessageTime == default ? c.updatedAt : c.lastMessageTime)
                .ToList();
        }

        /// <summary>
        /// 立即异步刷新会话列表。
        /// <para>事件回调不能直接 await 时使用，主要服务群聊资料/会话更新。</para>
        /// </summary>
        private void QueueConversationsReload(string accountId)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(accountId))
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadConversationsAsync(accountId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[CrmStore] 刷新会话列表失败：AccountId={AccountId}", accountId);
                }
            });
        }

        /// <summary>
        /// 兼容旧调用：早期页面把账号当作 long 传入。
        /// </summary>
        public Task LoadConversationsAsync(long accountId)
        {
            return LoadConversationsAsync(accountId.ToString());
        }





                /// <summary>
        /// 判断当前选中设备是否可以执行微信任务。
        /// </summary>
        private bool IsSelectedDeviceReadyForWechatTask(out string deviceUuid)
        {
            var device = ResolveReadyWechatDevice(updateSelection: true);
            deviceUuid = device?.uuid ?? string.Empty;
            return device != null;
        }

        /// <summary>
        /// 解析当前可执行微信任务的在线设备。
        /// <para>优先使用当前选中设备；如果它已经离线或微信未登录，则自动切换到第一台在线且已登录微信的设备，避免旧 SelectedDevice 导致“失败后又成功”的双提示。</para>
        /// </summary>
        private SrClient? ResolveReadyWechatDevice(bool updateSelection)
        {
            static bool IsReady(SrClient? device) => device != null
                && device.isOnline
                && !string.IsNullOrWhiteSpace(device.uuid)
                && !string.IsNullOrWhiteSpace(device.weChatId)
                && device.wx?.wechatAccount?.accountStatus == 1;

            var current = Devices.FirstOrDefault(item => string.Equals(item.uuid, SelectedDevice?.uuid, StringComparison.OrdinalIgnoreCase))
                ?? SelectedDevice;
            if (IsReady(current))
            {
                if (updateSelection && !ReferenceEquals(SelectedDevice, current))
                {
                    SelectedDevice = current;
                    if (SelectedDevice.wx == null)
                    {
                        SelectedDevice.wx = new Wx { srClient = SelectedDevice };
                    }
                }

                return current;
            }

            var fallback = Devices
                .Where(IsReady)
                .OrderByDescending(item => item.uuid == SelectedDevice?.uuid)
                .ThenByDescending(item => item.updatedAt)
                .FirstOrDefault();
            if (fallback != null && updateSelection)
            {
                SelectedDevice = fallback;
                if (SelectedDevice.wx == null)
                {
                    SelectedDevice.wx = new Wx { srClient = SelectedDevice };
                }
            }

            return fallback;
        }

        /// <summary>
        /// 强制选择指定设备执行微信任务。
        /// <para>添加好友页有自己的设备下拉框；如果 Store.SelectedDevice 仍停在旧离线设备，旧逻辑会先对离线设备下发失败。
        /// 这里用页面传入的 deviceUuid 同步 Store 选中态，并校验该设备在线且微信已登录。</para>
        /// </summary>
        private bool TryUseWechatDevice(string? requestedDeviceUuid, out string deviceUuid, out string accountId, out string errorMessage)
        {
            deviceUuid = string.Empty;
            accountId = string.Empty;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(requestedDeviceUuid))
            {
                var fallback = ResolveReadyWechatDevice(updateSelection: true);
                if (fallback == null)
                {
                    errorMessage = "没有在线且已登录微信的设备";
                    return false;
                }

                deviceUuid = fallback.uuid;
                accountId = fallback.weChatId ?? fallback.wx?.wechatAccount?.wxid ?? string.Empty;
                return true;
            }

            var device = Devices.FirstOrDefault(item => string.Equals(item.uuid, requestedDeviceUuid, StringComparison.OrdinalIgnoreCase));
            if (device == null)
            {
                errorMessage = "指定设备不存在或设备列表尚未同步";
                return false;
            }

            if (!device.isOnline
                || string.IsNullOrWhiteSpace(device.weChatId)
                || device.wx?.wechatAccount?.accountStatus != 1)
            {
                errorMessage = "指定设备未在线或微信未登录";
                return false;
            }

            SelectedDevice = device;
            if (SelectedDevice.wx == null)
            {
                SelectedDevice.wx = new Wx { srClient = SelectedDevice };
            }

            deviceUuid = device.uuid;
            accountId = device.weChatId ?? device.wx?.wechatAccount?.wxid ?? string.Empty;
            NotifyStateChanged();
            return true;
        }

        /// <summary>
        /// 下发按场景添加好友任务。
        /// 说明：这里只负责把任务发给安卓端，不直接写联系人表。
        /// 好友真正添加成功后，安卓端会回传联系人变更，服务端再由 ContactMessageHandler/DbHelper 体系同步数据库。
        /// </summary>
        public async Task<bool> AddFriendWithSceneAsync(
            string friendWxid,
            string message,
            string remark = "",
            string label = "",
            int scene = 3,
            int permission = 0,
            string verificationImagePath = "")
        {
            if (string.IsNullOrWhiteSpace(friendWxid))
            {
                OnNotification?.Invoke("好友微信号不能为空", false);
                return false;
            }

            await LoadDevicesAsync();
            if (!IsSelectedDeviceReadyForWechatTask(out var deviceUuid))
            {
                OnNotification?.Invoke("没有在线且已登录微信的设备，添加好友申请未下发", false);
                return false;
            }

            var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "你好" : message.Trim();
            var accountId = SelectedDevice?.weChatId ?? string.Empty;
            var result = await ExecuteServiceCallAsync(() => _service.AddFriendWithSceneAsync(
                deviceUuid,
                friendWxid.Trim(),
                normalizedMessage,
                remark?.Trim() ?? string.Empty,
                label?.Trim() ?? string.Empty,
                scene,
                permission,
                verificationImagePath?.Trim() ?? string.Empty));
            var success = result.success;

            OnNotification?.Invoke(success ? "添加好友申请已提交，等待对方验证和联系人同步" : $"添加好友申请提交失败：{result.message}", success);
            if (success && !string.IsNullOrWhiteSpace(accountId))
            {
                QueueContactsReload(accountId);
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(5));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(30));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(60));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(120));
            }

            return success;
        }

        /// <summary>
        /// 下发按场景添加好友任务，并使用页面指定的设备。
        /// </summary>
        public async Task<TaskResult> AddFriendWithSceneOnDeviceAsync(
            string requestedDeviceUuid,
            string friendWxid,
            string message,
            string remark = "",
            string label = "",
            int scene = 3,
            int permission = 0,
            string verificationImagePath = "")
        {
            if (string.IsNullOrWhiteSpace(friendWxid))
            {
                var emptyResult = TaskResult.Fail("好友微信号不能为空");
                return emptyResult;
            }

            await LoadDevicesAsync();
            if (!TryUseWechatDevice(requestedDeviceUuid, out var deviceUuid, out var accountId, out var errorMessage))
            {
                var fail = TaskResult.Fail(errorMessage);
                return fail;
            }

            var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "你好" : message.Trim();
            var result = await ExecuteServiceCallAsync(() => _service.AddFriendWithSceneAsync(
                deviceUuid,
                friendWxid.Trim(),
                normalizedMessage,
                remark?.Trim() ?? string.Empty,
                label?.Trim() ?? string.Empty,
                scene,
                permission,
                verificationImagePath?.Trim() ?? string.Empty));

            if (result.success && !string.IsNullOrWhiteSpace(accountId))
            {
                QueueContactsReload(accountId);
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(5));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(30));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(60));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(120));
            }

            return result;
        }

        public async Task<bool> AddFriendInChatRoomAsync(string chatRoomId, string friendId, string message, string remark = "", int permission = 0)
        {
            if (string.IsNullOrWhiteSpace(chatRoomId) || string.IsNullOrWhiteSpace(friendId))
            {
                OnNotification?.Invoke("群内添加好友参数不完整", false);
                return false;
            }

            await LoadDevicesAsync();
            if (!IsSelectedDeviceReadyForWechatTask(out var deviceUuid))
            {
                OnNotification?.Invoke("没有在线且已登录微信的设备，群内添加好友申请未下发", false);
                return false;
            }

            var accountId = SelectedDevice?.weChatId ?? string.Empty;
            var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "你好" : message.Trim();
            var result = await ExecuteServiceCallAsync(() => _service.AddFriendInChatRoomAsync(
                deviceUuid,
                chatRoomId.Trim(),
                friendId.Trim(),
                normalizedMessage,
                remark?.Trim() ?? string.Empty,
                permission));
            var success = result.success;

            OnNotification?.Invoke(success ? "群内添加好友申请已提交，等待对方验证和联系人同步" : $"群内添加好友申请提交失败：{result.message}", success);
            if (success && !string.IsNullOrWhiteSpace(accountId))
            {
                QueueContactsReload(accountId);
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(5));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(30));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(60));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(120));
            }

            return success;
        }

        /// <summary>
        /// 下发群内添加好友任务，并使用页面指定的设备。
        /// </summary>
        public async Task<TaskResult> AddFriendInChatRoomOnDeviceAsync(string requestedDeviceUuid, string chatRoomId, string friendId, string message, string remark = "", int permission = 0)
        {
            if (string.IsNullOrWhiteSpace(chatRoomId) || string.IsNullOrWhiteSpace(friendId))
            {
                var fail = TaskResult.Fail("群内添加好友参数不完整");
                return fail;
            }

            await LoadDevicesAsync();
            if (!TryUseWechatDevice(requestedDeviceUuid, out var deviceUuid, out var accountId, out var errorMessage))
            {
                var fail = TaskResult.Fail(errorMessage);
                return fail;
            }

            var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "你好" : message.Trim();
            var result = await ExecuteServiceCallAsync(() => _service.AddFriendInChatRoomAsync(
                deviceUuid,
                chatRoomId.Trim(),
                friendId.Trim(),
                normalizedMessage,
                remark?.Trim() ?? string.Empty,
                permission));

            if (result.success && !string.IsNullOrWhiteSpace(accountId))
            {
                QueueContactsReload(accountId);
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(5));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(30));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(60));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(120));
            }

            return result;
        }

        /// <summary>
        /// 下发手机号加好友任务，并使用页面指定的设备。
        /// </summary>
        public async Task<TaskResult> AddFriendsByPhoneOnDeviceAsync(string requestedDeviceUuid, IEnumerable<string> phones, string message, string remark = "", string label = "", int permission = 0)
        {
            var normalizedPhones = (phones ?? Enumerable.Empty<string>())
                .Where(phone => !string.IsNullOrWhiteSpace(phone))
                .Select(phone => phone.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!normalizedPhones.Any())
            {
                return TaskResult.Fail("手机号不能为空");
            }

            await LoadDevicesAsync();
            if (!TryUseWechatDevice(requestedDeviceUuid, out var deviceUuid, out var accountId, out var errorMessage))
            {
                return TaskResult.Fail(errorMessage);
            }

            var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "你好" : message.Trim();
            var result = await ExecuteServiceCallAsync(() => _service.AddFriendsByPhoneAsync(
                deviceUuid,
                normalizedPhones,
                normalizedMessage,
                remark?.Trim() ?? string.Empty,
                label?.Trim() ?? string.Empty,
                permission));

            if (result.success && !string.IsNullOrWhiteSpace(accountId))
            {
                QueueContactsReload(accountId);
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(5));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(30));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(60));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(120));
            }

            return result;
        }

        /// <summary>
        /// 下发通讯录加好友任务，并使用页面指定的设备。
        /// </summary>
        public async Task<TaskResult> AddFriendFromPhonebookOnDeviceAsync(string requestedDeviceUuid, string message, int count = 1, int index = 0, bool reset = false)
        {
            await LoadDevicesAsync();
            if (!TryUseWechatDevice(requestedDeviceUuid, out var deviceUuid, out var accountId, out var errorMessage))
            {
                return TaskResult.Fail(errorMessage);
            }

            var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "你好" : message.Trim();
            var result = await ExecuteServiceCallAsync(() => _service.AddFriendFromPhonebookAsync(
                deviceUuid,
                normalizedMessage,
                count,
                index,
                reset));

            if (result.success && !string.IsNullOrWhiteSpace(accountId))
            {
                QueueContactsReload(accountId);
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(30));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(120));
            }

            return result;
        }

        /// <summary>
        /// 下发名片加好友任务，并使用页面指定的设备。
        /// </summary>
        public async Task<TaskResult> AddFriendNameCardOnDeviceAsync(string requestedDeviceUuid, long msgSvrId, string message, string remark = "")
        {
            if (msgSvrId == 0)
            {
                return TaskResult.Fail("名片消息 MsgSvrId 不能为空");
            }

            await LoadDevicesAsync();
            if (!TryUseWechatDevice(requestedDeviceUuid, out var deviceUuid, out var accountId, out var errorMessage))
            {
                return TaskResult.Fail(errorMessage);
            }

            var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "你好" : message.Trim();
            var result = await ExecuteServiceCallAsync(() => _service.AddFriendNameCardAsync(
                deviceUuid,
                msgSvrId,
                normalizedMessage,
                remark?.Trim() ?? string.Empty));

            if (result.success && !string.IsNullOrWhiteSpace(accountId))
            {
                QueueContactsReload(accountId);
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(5));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(30));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(120));
            }

            return result;
        }

        /// <summary>
        /// 下发重新发送好友验证任务，并使用页面指定的设备。
        /// </summary>
        public async Task<TaskResult> SendFriendVerifyOnDeviceAsync(string requestedDeviceUuid, string friendId, string message)
        {
            if (string.IsNullOrWhiteSpace(friendId))
            {
                return TaskResult.Fail("好友 wxid 不能为空");
            }

            await LoadDevicesAsync();
            if (!TryUseWechatDevice(requestedDeviceUuid, out var deviceUuid, out var accountId, out var errorMessage))
            {
                return TaskResult.Fail(errorMessage);
            }

            var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "你好" : message.Trim();
            var result = await ExecuteServiceCallAsync(() => _service.SendFriendVerifyAsync(
                deviceUuid,
                friendId.Trim(),
                normalizedMessage));

            if (result.success && !string.IsNullOrWhiteSpace(accountId))
            {
                QueueContactsReload(accountId);
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(30));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(120));
            }

            return result;
        }





        public async Task<TaskResult> SendGroupMessageAsync(List<string> friendIds, string content, int contentType = 0, int duration = 0, bool original = false)





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(deviceUuid))





            {





                OnNotification?.Invoke("请先选择在线设备", false);





                return TaskResult.Fail("请先选择在线设备");





            }











            var result = await ExecuteServiceCallAsync(() => _service.SendGroupMessageAsync(deviceUuid, friendIds, content, contentType, duration, original));





            var successMessage = string.IsNullOrWhiteSpace(result.message)
                ? "群发消息指令已下发"
                : result.message;
            OnNotification?.Invoke(result.success ? successMessage : $"群发消息指令下发失败：{result.message}", result.success);





            return result;





        }

        /// <summary>
        /// 请求当前设备同步微信“群发助手”历史。
        /// </summary>
        public async Task<TaskResult> SyncMassSendHistoryAsync(long endTime = 0, string weChatId = "")
        {
            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var accountId = string.IsNullOrWhiteSpace(weChatId)
                ? (SelectedDevice?.weChatId ?? SelectedDevice?.wx?.wechatAccount?.wxid ?? string.Empty)
                : weChatId;
            var result = await ExecuteServiceCallAsync(() => _service.SyncMassSendHistoryAsync(deviceUuid, endTime, accountId));
            OnNotification?.Invoke(result.success
                ? (result.message ?? "群发历史同步指令已下发")
                : $"群发历史同步指令下发失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 读取当前微信账号的群发助手历史。
        /// <para>这是只读展示链路；历史写入由服务端 GroupSendHistoryPushNotice + DbHelper.SaveMassSendHistory 完成。</para>
        /// </summary>
        public async Task<List<MassSendHistoryDto>> GetSelectedMassSendHistoryAsync(int count = 50)
        {
            var accountId = SelectedDevice?.weChatId
                ?? SelectedDevice?.wx?.wechatAccount?.wxid
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(accountId))
            {
                return new List<MassSendHistoryDto>();
            }

            return await ExecuteServiceCallAsync(() => _service.GetMassSendHistoryAsync(accountId, count));
        }





        public async Task ExecuteGroupActionAsync(string chatRoomId, int action, string content, int intValue = 0)





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(deviceUuid))





            {





                OnNotification?.Invoke("请先选择在线设备", false);





                return;





            }











            var success = await ExecuteServiceCallAsync(() => _service.ExecuteGroupActionAsync(deviceUuid, chatRoomId, action, content, intValue));





            OnNotification?.Invoke(success ? "群操作指令已下发" : "群操作指令下发失败", success);





        }











        public async Task AgreeJoinGroupAsync(string talker, long msgSvrId, string content)





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(deviceUuid))





            {





                OnNotification?.Invoke("请先选择在线设备", false);





                return;





            }











            var success = await ExecuteServiceCallAsync(() => _service.AgreeJoinGroupAsync(deviceUuid, talker, msgSvrId, content));





            OnNotification?.Invoke(success ? "同意入群指令已下发" : "同意入群指令下发失败", success);





        }











        public async Task DeleteCurrentContactAsync()





        {





            var targetWxid = SelectedContact?.wxid ?? SelectedConversation?.conversationWxid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(targetWxid))





            {





                OnNotification?.Invoke("请先选择联系人或会话", false);





                return;





            }











            await DeleteFriendAsync(targetWxid);





        }











        /// <summary>
        /// 同步当前微信联系人。
        /// <para>
        /// 先读取服务端已落库联系人，保证页面立即刷新；随后下发 3056 请求手机端主动回传 FriendPushNotice。
        /// 联系人持久化仍由服务端 DbHelper.SaveContacts/MarkContactDeleted 负责，前端不直接写库。
        /// </para>
        /// </summary>
        public async Task SyncContactsAsync(bool requestPhoneRefresh = true)
        {
            if (SelectedDevice == null)
            {
                OnNotification?.Invoke("请先选择要同步联系人的设备", false);
                return;
            }

            var accountId = FirstNonEmptyLocal(
                SelectedDevice?.weChatId,
                SelectedDevice?.wx?.wechatAccount?.wxid);

            if (string.IsNullOrWhiteSpace(accountId))
            {
                Contacts = new List<Contact>();
                NotifyStateChanged();

                if (requestPhoneRefresh && SelectedDevice != null)
                {
                    var accountResult = await ExecuteServiceCallAsync(() => _service.RefreshWeChatAccountsAsync(SelectedDevice.uuid));
                    OnNotification?.Invoke(
                        accountResult.success ? "微信账号状态查询已下发，请稍后刷新联系人" : $"微信账号状态查询失败：{accountResult.message}",
                        accountResult.success);

                    if (accountResult.success)
                    {
                        ScheduleDevicesReload(TimeSpan.FromSeconds(3));
                        ScheduleDevicesReload(TimeSpan.FromSeconds(8));
                        ScheduleDevicesReload(TimeSpan.FromSeconds(20));
                    }
                }
                return;
            }

            Contacts = await ExecuteServiceCallAsync(() => _service.GetContactsAsync(accountId));
            if (SelectedDevice?.wx != null)
            {
                SelectedDevice.wx.contacts = Contacts;
            }
            NotifyStateChanged();

            if (!requestPhoneRefresh || SelectedDevice == null)
            {
                return;
            }

            var result = await ExecuteServiceCallAsync(() => _service.SyncFriendListAsync(SelectedDevice.uuid, accountId));
            OnNotification?.Invoke(
                result.success ? "好友同步指令已下发，等待手机端回传联系人" : $"好友同步指令下发失败：{result.message}",
                result.success);

            if (result.success)
            {
                QueueContactsReload(accountId);
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(3));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(10));
                ScheduleContactsReload(accountId, TimeSpan.FromSeconds(30));
            }
        }

        /// <summary>
        /// 请求当前设备回传微信账号状态。
        /// <para>对应 3050/3051，只校准设备账号展示，不直接写联系人。</para>
        /// </summary>
        public async Task<TaskResult> RefreshSelectedWeChatAccountsAsync()
        {
            if (SelectedDevice == null || string.IsNullOrWhiteSpace(SelectedDevice.uuid))
            {
                var emptyResult = TaskResult.Fail("请先选择设备");
                OnNotification?.Invoke(emptyResult.message ?? "请先选择设备", false);
                return emptyResult;
            }

            var result = await ExecuteServiceCallAsync(() => _service.RefreshWeChatAccountsAsync(SelectedDevice.uuid));
            OnNotification?.Invoke(
                result.success ? "微信账号状态查询已下发，等待手机端回传 3051" : $"微信账号状态查询失败：{result.message}",
                result.success);

            if (result.success)
            {
                ScheduleDevicesReload(TimeSpan.FromSeconds(3));
                ScheduleDevicesReload(TimeSpan.FromSeconds(8));
                ScheduleDevicesReload(TimeSpan.FromSeconds(20));
            }

            return result;
        }

        /// <summary>
        /// 延迟刷新设备列表。
        /// <para>用于 3050/3051 账号状态异步回传后的 UI 兜底刷新。</para>
        /// </summary>
        private void ScheduleDevicesReload(TimeSpan delay)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                    if (!_isDisposed)
                    {
                        await LoadDevicesAsync().ConfigureAwait(false);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // 页面已释放，无需处理。
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[CrmStore] 延迟刷新设备列表失败");
                }
            });
        }

        public async Task SyncChatRoomsAsync()





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(deviceUuid))





            {





                OnNotification?.Invoke("请先选择在线设备", false);





                return;





            }











            var accountId = FirstNonEmptyLocal(SelectedDevice?.weChatId, SelectedDevice?.wx?.wechatAccount?.wxid);
            var success = await ExecuteServiceCallAsync(() => _service.SyncChatRoomsAsync(deviceUuid, 0, accountId));
            OnNotification?.Invoke(success ? "群列表同步指令已下发" : "群列表同步指令下发失败", success);

            if (success && !string.IsNullOrWhiteSpace(accountId))
            {
                ScheduleConversationsReload(accountId, TimeSpan.FromSeconds(2));
                ScheduleConversationsReload(accountId, TimeSpan.FromSeconds(6));
                ScheduleConversationsReload(accountId, TimeSpan.FromSeconds(15));
            }
        }

















        /// <summary>
        /// 延迟刷新会话列表。
        /// <para>群列表同步和实时群消息落库存在异步窗口，延迟回查可以让网页自动看到新群聊。</para>
        /// </summary>
        private void ScheduleConversationsReload(string accountId, TimeSpan delay)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(accountId))
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                    if (_isDisposed)
                    {
                        return;
                    }

                    await LoadConversationsAsync(accountId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[CrmStore] 延迟刷新会话列表失败：AccountId={AccountId}", accountId);
                }
            });
        }


        /// <summary>





        /// 请求设备拉取群邀请列表。





        /// </summary>





        public async Task GetChatRoomInviteListAsync()





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(deviceUuid))





            {





                OnNotification?.Invoke("请先选择设备", false);





                return;





            }











            var success = await ExecuteServiceCallAsync(() => _service.GetChatRoomInviteListAsync(deviceUuid));





            OnNotification?.Invoke(success ? "群邀请列表同步指令已下发" : "群邀请列表同步指令下发失败", success);





        }











        /// <summary>
        /// 查询当前微信账号的群邀请审批列表。
        /// <para>不下发设备任务，只读取服务端已持久化的 62203 群邀请 Notice。</para>
        /// </summary>
        public async Task<List<GroupInvitationDto>> GetGroupInvitationsAsync(string? accountId = null, string chatRoomId = "", int count = 100, bool pendingOnly = false)
        {
            var ownerWxid = FirstNonEmptyLocal(
                accountId,
                SelectedDevice?.weChatId,
                SelectedDevice?.wx?.wechatAccount?.wxid);

            if (string.IsNullOrWhiteSpace(ownerWxid))
            {
                OnNotification?.Invoke("请先选择微信账号", false);
                return new List<GroupInvitationDto>();
            }

            return await ExecuteServiceCallAsync(() => _service.GetGroupInvitationsAsync(ownerWxid, chatRoomId, count, pendingOnly));
        }

        public async Task<TaskResult> PostMomentAsync(string content, List<string> imageUrls)





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(deviceUuid))





            {





                return TaskResult.Fail("请先选择在线设备");





            }











            var result = await ExecuteServiceCallAsync(() => _service.PostMomentAsync(deviceUuid, content, imageUrls));





            OnNotification?.Invoke(result.success ? "朋友圈发布指令已下发" : (result.message ?? "朋友圈发布失败"), result.success);





            return result;





        }

        /// <summary>
        /// 发布高级朋友圈协议任务。
        /// </summary>
        public async Task<TaskResult> PostMomentAdvancedAsync(MomentPostRequestDto request)
        {
            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return TaskResult.Fail("请先选择在线设备");
            }

            request ??= new MomentPostRequestDto();
            if (string.IsNullOrWhiteSpace(request.clientRequestId))
            {
                request.clientRequestId = Guid.NewGuid().ToString("N");
            }

            UpdateMomentPostStatus(
                deviceUuid,
                0L,
                null,
                "朋友圈发布任务正在下发，等待手机回包",
                "Sending",
                new MomentPostResultDto
                {
                    clientRequestId = request.clientRequestId,
                    attachmentType = request.attachment?.type.ToString() ?? string.Empty,
                    attachmentCount = request.attachment?.content?.Count ?? 0,
                    visibleType = request.visible?.type.ToString() ?? string.Empty,
                    labelCount = request.visible?.labels?.Count ?? 0,
                    friendCount = request.visible?.friends?.Count ?? 0,
                    notiUserCount = request.notiUsers?.Count ?? 0,
                    extCommentCount = request.extComment?.Count ?? 0,
                    hasComment = !string.IsNullOrWhiteSpace(request.comment),
                    hasPoi = request.poi != null
                        && (!string.IsNullOrWhiteSpace(request.poi.city)
                            || !string.IsNullOrWhiteSpace(request.poi.name)
                            || !string.IsNullOrWhiteSpace(request.poi.address)
                            || !string.IsNullOrWhiteSpace(request.poi.poiId)
                            || Math.Abs(request.poi.lat) > 0.000001f
                            || Math.Abs(request.poi.lng) > 0.000001f),
                    sendSlow = request.sendSlow
                });
            NotifyStateChanged();

            var result = await ExecuteServiceCallAsync(() => _service.PostMomentAdvancedAsync(deviceUuid, request));
            var message = string.IsNullOrWhiteSpace(result.message)
                ? (result.success ? "朋友圈发布任务已下发，等待手机回包或同步校准" : "朋友圈发布失败")
                : result.message.Trim();
            var hasMomentPostResult = TryReadMomentPostResult(result.data, out var momentPostResult)
                || TryExtractMomentPostResultFromMessage(message, out momentPostResult);
            if (hasMomentPostResult && momentPostResult != null)
            {
                message = StripResultDataJson(message);
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = momentPostResult.success
                        ? "朋友圈发布成功，等待同步校准"
                        : "朋友圈发布失败";
                }

                UpdateMomentPostStatus(
                    deviceUuid,
                    result.taskId,
                    momentPostResult.success,
                    message,
                    momentPostResult.status,
                    momentPostResult);

                if (momentPostResult.success || momentPostResult.needSync)
                {
                    ScheduleMomentPostRefresh(deviceUuid, momentPostResult);
                }
            }
            else
            {
                UpdateMomentPostStatus(
                    deviceUuid,
                    result.taskId,
                    result.success ? null : false,
                    message,
                    result.success ? "WaitingClient" : "Failed");
            }

            result.message = message;
            NotifyStateChanged();
            OnNotification?.Invoke(message, result.success);
            return result;
        }

















        public async Task<TaskResult> SphGetMentionAsync(long lastLikeId = 0, long lastCommentId = 0, long lastFollowId = 0)
        {
            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择设备", false); return TaskResult.Fail("请先选择设备"); }
            var result = await ExecuteServiceCallAsync(() => _service.SphGetMentionAsync(deviceUuid, lastLikeId, lastCommentId, lastFollowId));
            OnNotification?.Invoke(result.success ? "视频号提及拉取指令已下发" : $"视频号提及拉取指令失败：{result.message}", result.success);
            return result;
        }











        public async Task<TaskResult> SphGetCommentAsync(long feedId, string nonceId, string feedAuth, long refCommentId = 0, long replyCommentId = 0, int sortType = 0)
        {
            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择设备", false); return TaskResult.Fail("请先选择设备"); }
            var result = await ExecuteServiceCallAsync(() => _service.SphGetCommentAsync(deviceUuid, feedId, nonceId, feedAuth, refCommentId, replyCommentId, sortType));
            OnNotification?.Invoke(result.success ? "视频号评论列表拉取指令已下发" : $"视频号评论列表拉取指令失败：{result.message}", result.success);
            return result;
        }











        public async Task<TaskResult> SphUserPageAsync(string sphUserName)
        {
            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择设备", false); return TaskResult.Fail("请先选择设备"); }
            var result = await ExecuteServiceCallAsync(() => _service.SphUserPageAsync(deviceUuid, sphUserName));
            OnNotification?.Invoke(result.success ? "视频号用户页拉取指令已下发" : $"视频号用户页拉取指令失败：{result.message}", result.success);
            return result;
        }











        public async Task<TaskResult> SphPostAsync(string content, List<string> medias, int mediaType = 0, string cover = "")





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择设备", false); return TaskResult.Fail("请先选择设备"); }





            var result = await ExecuteServiceCallAsync(() => _service.SphPostAsync(deviceUuid, content, medias, mediaType, cover));





            OnNotification?.Invoke(result.success ? "视频号发布指令已下发" : $"视频号发布指令失败：{result.message}", result.success);





            return result;





        }











        public async Task<TaskResult> SphCommentAsync(long feedId, string nonceId, string feedAuth, int type, string content, string media = "", long replyCommentId = 0, string replyUsername = "")





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择设备", false); return TaskResult.Fail("请先选择设备"); }





            var result = await ExecuteServiceCallAsync(() => _service.SphCommentAsync(deviceUuid, feedId, nonceId, feedAuth, type, content, media, replyCommentId, replyUsername));





            OnNotification?.Invoke(result.success ? "视频号评论指令已下发" : $"视频号评论指令失败：{result.message}", result.success);





            return result;





        }











        public async Task<TaskResult> SphLikeAsync(long feedId, int type = 1, bool isCancel = false)





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择设备", false); return TaskResult.Fail("请先选择设备"); }





            var result = await ExecuteServiceCallAsync(() => _service.SphLikeAsync(deviceUuid, feedId, type, isCancel));





            OnNotification?.Invoke(result.success ? "视频号点赞指令已下发" : $"视频号点赞指令失败：{result.message}", result.success);





            return result;





        }











        public async Task<TaskResult> SphDelCommentAsync(long feedId, long commentId)





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择设备", false); return TaskResult.Fail("请先选择设备"); }





            var result = await ExecuteServiceCallAsync(() => _service.SphDelCommentAsync(deviceUuid, feedId, commentId));





            OnNotification?.Invoke(result.success ? "视频号删评指令已下发" : $"视频号删评指令失败：{result.message}", result.success);





            return result;





        }











        public async Task<TaskResult> SyncMomentsAsync(long startTime = 0, IEnumerable<long>? circleIds = null)
        {
            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var accountId = SelectedDevice?.weChatId ?? SelectedDevice?.wx?.wechatAccount?.wxid ?? string.Empty;
            var result = await ExecuteServiceCallAsync(() => _service.SyncMomentsAsync(deviceUuid, startTime, circleIds, accountId));
            UpdateMomentsSyncStatus(deviceUuid, result.taskId, result.success, result.message ?? (result.success ? "朋友圈同步指令已下发，等待客户端回执" : "朋友圈同步失败"));
            OnNotification?.Invoke(result.success ? "朋友圈同步指令已下发" : $"朋友圈同步失败：{result.message}", result.success);
            NotifyStateChanged();

            if (result.success)
            {
                // 朋友圈同步回包存在异步落库窗口：先短延迟回查一次，再补一轮兜底回查。
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(2));
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(6));
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(12));
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(30));
            }

            return result;
        }

        /// <summary>
        /// 下发朋友圈一键点赞任务。
        /// </summary>
        public async Task<TaskResult> OneKeyLikeMomentsAsync(int rate = 100, int num = 0, int endTime = 0, int timeOut = 0)
        {
            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            var result = await ExecuteServiceCallAsync(() => _service.OneKeyLikeMomentsAsync(deviceUuid, rate, num, endTime, timeOut));
            UpdateMomentsSyncStatus(deviceUuid, result.taskId, result.success, result.message ?? (result.success ? "朋友圈一键点赞指令已下发" : "朋友圈一键点赞失败"));
            OnNotification?.Invoke(result.success ? "朋友圈一键点赞指令已下发" : $"朋友圈一键点赞失败：{result.message}", result.success);

            if (result.success)
            {
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(2));
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(8));
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(20));
            }

            return result;
        }

        public async Task<TaskResult> DeleteMomentAsync(long circleId)





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择在线设备", false); return TaskResult.Fail("请先选择在线设备"); }

            var result = await ExecuteServiceCallAsync(() => _service.DeleteMomentAsync(deviceUuid, circleId));





            OnNotification?.Invoke(result.success ? "删除朋友圈指令已下发" : $"删除朋友圈指令失败：{result.message}", result.success);





            return result;





        }











        /// <summary>
        /// 普通朋友圈点赞或取消点赞。
        /// </summary>
        public async Task<TaskResult> LikeMomentAsync(long circleId, bool isCancel = false)
        {
            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                OnNotification?.Invoke("请先选择在线设备", false);
                return TaskResult.Fail("请先选择在线设备");
            }

            // 微信 snsId 以 long 传输时可能为负数，只有 0 才表示无效。
            if (circleId == 0)
            {
                OnNotification?.Invoke("朋友圈ID无效", false);
                return TaskResult.Fail("朋友圈ID无效");
            }

            var result = await ExecuteServiceCallAsync(() => _service.LikeMomentAsync(deviceUuid, circleId, isCancel));
            OnNotification?.Invoke(
                result.success ? (isCancel ? "取消朋友圈点赞指令已下发" : "朋友圈点赞指令已下发") : $"朋友圈点赞操作失败：{result.message}",
                result.success);

            if (result.success)
            {
                ApplyMomentLikeOptimisticUpdate(circleId, isCancel);
                _ = PullMomentDetailQuietAsync(deviceUuid, circleId);
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(2));
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(6));
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(12));
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(30));
            }

            return result;
        }



        public async Task<TaskResult> DeleteMomentCommentAsync(long circleId, long commentId, long publishTime)





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择在线设备", false); return TaskResult.Fail("请先选择在线设备"); }





            var result = await ExecuteServiceCallAsync(() => _service.DeleteMomentCommentAsync(deviceUuid, circleId, commentId, publishTime));





            OnNotification?.Invoke(result.success ? "删除朋友圈评论指令已下发" : $"删除朋友圈评论失败：{result.message}", result.success);





            return result;





        }











        public async Task<TaskResult> ReplyMomentCommentAsync(long circleId, string toWeChatId, string content, long replyCommentId, bool isResend = false)





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择在线设备", false); return TaskResult.Fail("请先选择在线设备"); }





            // 微信 snsId 以 long 传输时可能为负数，只有 0 才表示无效。
            if (circleId == 0) { OnNotification?.Invoke("朋友圈ID无效", false); return TaskResult.Fail("朋友圈ID无效"); }

            if (string.IsNullOrWhiteSpace(content)) { OnNotification?.Invoke("评论内容不能为空", false); return TaskResult.Fail("评论内容不能为空"); }

            var pendingCommentId = ApplyMomentCommentPendingUpdate(circleId, content, toWeChatId, replyCommentId);
            if (pendingCommentId != 0)
            {
                OnNotification?.Invoke("朋友圈评论已临时显示，等待微信回执校准。", true);
            }

            var result = await ExecuteServiceCallAsync(() => _service.ReplyMomentCommentAsync(deviceUuid, circleId, toWeChatId, content, replyCommentId, isResend));





            var shouldKeepPendingComment = result.success || IsMomentCommentTimeoutAfterClientExecution(result);
            OnNotification?.Invoke(shouldKeepPendingComment ? "回复朋友圈评论指令已下发" : $"回复朋友圈评论失败：{result.message}", shouldKeepPendingComment);

            if (shouldKeepPendingComment)
            {
                _ = PullMomentDetailQuietAsync(deviceUuid, circleId);
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(2));
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(6));
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(12));
                ScheduleMomentsReload(deviceUuid, TimeSpan.FromSeconds(30));
                if (!result.success)
                {
                    OnNotification?.Invoke("朋友圈评论已提交到微信界面，但客户端未捕获完成回执；已临时展示并安排多次回拉校准。", true);
                }
            }
            else if (pendingCommentId != 0)
            {
                RemoveMomentCommentPendingUpdate(circleId, pendingCommentId);
            }

            return result;





        }











        public async Task<TaskResult> PullFriendMomentsAsync(string friendId, long refSnsId = 0, int count = 20, long startTime = 0, long refTime = 0)
        {
            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择在线设备", false); return TaskResult.Fail("请先选择在线设备"); }
            if (string.IsNullOrWhiteSpace(friendId)) { OnNotification?.Invoke("好友微信ID不能为空", false); return TaskResult.Fail("好友微信ID不能为空"); }

            var result = await ExecuteServiceCallAsync(() => _service.PullFriendMomentsAsync(deviceUuid, friendId, refSnsId, count, startTime, refTime));
            OnNotification?.Invoke(result.success ? "好友朋友圈拉取指令已下发" : $"好友朋友圈拉取失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 判断朋友圈评论是否属于“客户端已执行但完成回调未捕获”的超时。
        /// <para>62203 上 Tsk50 进入 SnsCommentDetailUI 后可能没有命中完成回调；此时前端先临时展示，后续回拉再校准。</para>
        /// </summary>
        private static bool IsMomentCommentTimeoutAfterClientExecution(TaskResult? result)
        {
            if (result == null || result.success || string.IsNullOrWhiteSpace(result.message))
            {
                return false;
            }

            return result.message.Contains("TimeOut WAIT_TO_FINISHED", StringComparison.OrdinalIgnoreCase)
                || result.message.Contains("Timeout waiting for client response", StringComparison.OrdinalIgnoreCase);
        }











        public async Task<TaskResult> PullMomentDetailAsync(long circleId, bool getBigMap = false)
        {
            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择在线设备", false); return TaskResult.Fail("请先选择在线设备"); }
            // 微信 snsId 以 long 传输时可能为负数，只有 0 才表示无效。
            if (circleId == 0) { OnNotification?.Invoke("朋友圈ID无效", false); return TaskResult.Fail("朋友圈ID无效"); }

            var result = await ExecuteServiceCallAsync(() => _service.PullMomentDetailAsync(deviceUuid, circleId, getBigMap));
            OnNotification?.Invoke(result.success ? "朋友圈详情拉取指令已下发" : $"朋友圈详情拉取失败：{result.message}", result.success);
            return result;
        }

        /// <summary>
        /// 静默拉取朋友圈详情。
        /// <para>点赞/评论后主动拉一次详情，帮助服务端尽快收到最新评论与点赞；失败只写日志，不打扰页面操作。</para>
        /// </summary>
        private async Task PullMomentDetailQuietAsync(string deviceUuid, long circleId)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
                    if (HasRecentMomentRealtimePush(TimeSpan.FromSeconds(3)))
                    {
                        _logger.LogDebug(
                            "[CrmStore] 跳过朋友圈互动后静默拉详情：近期已收到服务端实时推送，DeviceUuid={DeviceUuid}, CircleId={CircleId}",
                            deviceUuid,
                            circleId);
                        return;
                    }

                    if (_isDisposed || string.IsNullOrWhiteSpace(deviceUuid) || circleId == 0)
                    {
                        return;
                    }

                var result = await ExecuteServiceCallAsync(() => _service.PullMomentDetailAsync(deviceUuid, circleId, false)).ConfigureAwait(false);
                if (!result.success)
                {
                    _logger.LogDebug(
                        "[CrmStore] 朋友圈互动后静默拉详情未成功：DeviceUuid={DeviceUuid}, CircleId={CircleId}, Message={Message}",
                        deviceUuid,
                        circleId,
                        result.message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[CrmStore] 朋友圈互动后静默拉详情异常：DeviceUuid={DeviceUuid}, CircleId={CircleId}", deviceUuid, circleId);
            }
        }











        public async Task<TaskResult> SyncMomentMessagesAsync(bool onlyComment = false, bool getAll = true)
        {
            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择在线设备", false); return TaskResult.Fail("请先选择在线设备"); }

            var result = await ExecuteServiceCallAsync(() => _service.SyncMomentMessagesAsync(deviceUuid, onlyComment, getAll));
            OnNotification?.Invoke(result.success ? "朋友圈互动消息同步指令已下发" : $"朋友圈互动消息同步失败：{result.message}", result.success);
            return result;
        }











        public async Task<TaskResult> MarkMomentMessageReadAsync(long circleId, int commentId = 0)
        {
            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择在线设备", false); return TaskResult.Fail("请先选择在线设备"); }
            // 微信 snsId 以 long 传输时可能为负数，只有 0 才表示无效。
            if (circleId == 0) { OnNotification?.Invoke("朋友圈ID无效", false); return TaskResult.Fail("朋友圈ID无效"); }

            var result = await ExecuteServiceCallAsync(() => _service.MarkMomentMessageReadAsync(deviceUuid, circleId, commentId));
            OnNotification?.Invoke(result.success ? "朋友圈互动消息已读指令已下发" : $"朋友圈互动消息已读失败：{result.message}", result.success);
            return result;
        }











        public async Task<TaskResult> ClearMomentMessageAsync(long circleId, int commentId = 0, bool isRead = true)
        {
            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid)) { OnNotification?.Invoke("请先选择在线设备", false); return TaskResult.Fail("请先选择在线设备"); }
            // 62203 中 CircleId=0 可能表示清空全部互动消息；单条清理仍传具体 snsId。

            var result = await ExecuteServiceCallAsync(() => _service.ClearMomentMessageAsync(deviceUuid, circleId, commentId, isRead));
            OnNotification?.Invoke(result.success ? "朋友圈互动消息清理指令已下发" : $"朋友圈互动消息清理失败：{result.message}", result.success);
            return result;
        }











        public Task DisconnectAsync() { return Task.CompletedTask; } 





        











        public async Task DeleteFriendAsync(string uuid, string wxid)





        {





            if (string.IsNullOrWhiteSpace(uuid) || string.IsNullOrWhiteSpace(wxid))





            {





                OnNotification?.Invoke("删除好友参数不完整", false);





                return;





            }











            var result = await ExecuteServiceCallAsync(() => _service.DeleteFriendAsync(uuid, wxid));
            var success = result.success;





            OnNotification?.Invoke(success ? "删除好友指令已下发" : $"删除好友指令下发失败：{result.message}", success);





            if (success)





            {





                Contacts.RemoveAll(c => c.wxid == wxid);
                var accountId = SelectedDevice?.weChatId ?? string.Empty;





                if (SelectedContact?.wxid == wxid) SelectedContact = null;





                NotifyStateChanged();
                if (!string.IsNullOrWhiteSpace(accountId))
                {
                    QueueContactsReload(accountId);
                    ScheduleContactsReload(accountId, TimeSpan.FromSeconds(3));
                    ScheduleContactsReload(accountId, TimeSpan.FromSeconds(10));
                    ScheduleContactsReload(accountId, TimeSpan.FromSeconds(30));
                }





            }





        }











        public async Task DeleteFriendAsync(string wxid)





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            await DeleteFriendAsync(deviceUuid, wxid);





        }











        public async Task SetFriendPermissionAsync(string uuid, string wxid, int permissionMask)





        {





            if (string.IsNullOrWhiteSpace(uuid) || string.IsNullOrWhiteSpace(wxid))





            {





                OnNotification?.Invoke("设置好友权限参数不完整", false);





                return;





            }











            var result = await ExecuteServiceCallAsync(() => _service.SetFriendPermissionAsync(uuid, wxid, permissionMask));





            OnNotification?.Invoke(result.success ? (result.message ?? "好友权限设置指令已下发") : (result.message ?? "好友权限设置指令下发失败"), result.success);





        }











        public async Task SetFriendPermissionAsync(string wxid, int permissionMask)





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            await SetFriendPermissionAsync(deviceUuid, wxid, permissionMask);





        }











        public async Task RequestMomentsSyncAsync()





        {





            await SyncMomentsAsync();





        }





        





        public async Task<bool> DeleteDeviceAsync(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
            {
                OnNotification?.Invoke("设备 UUID 为空，无法删除", false);
                return false;
            }

            var success = await ExecuteServiceCallAsync(() => _service.DeleteDeviceAsync(uuid));
            if (success)
            {
                Devices.RemoveAll(device => string.Equals(device.uuid, uuid, StringComparison.OrdinalIgnoreCase));
                if (SelectedDevice != null && string.Equals(SelectedDevice.uuid, uuid, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedDevice = null;
                    Contacts = new List<Contact>();
                    Conversations = new List<Conversation>();
                    CurrentMessages = new List<Message>();
                    CurrentGroupMembers = new List<GroupMemberDto>();
                }

                OnNotification?.Invoke("设备已删除；若设备在线，已先尝试通知 Android 端主动断开", true);
                NotifyStateChanged();
                return true;
            }

            OnNotification?.Invoke("设备删除失败：服务端未找到设备或删除未完成", false);
            return false;
        }

        /// <summary>
        /// 下发设备 App 升级通知。
        /// <para>该通知无结果回包；返回成功只代表服务端已写入在线 TCP 通道。</para>
        /// </summary>
        public async Task<TaskResult> UpgradeDeviceAppAsync(
            string deviceUuid,
            string packageName,
            string version,
            int versionCode,
            string packageUrl,
            string weChatId = "")
        {
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                OnNotification?.Invoke("设备 UUID 为空，无法下发升级通知", false);
                return TaskResult.Fail("设备 UUID 为空");
            }

            var result = await ExecuteServiceCallAsync(() => _service.UpgradeDeviceAppAsync(
                deviceUuid,
                packageName,
                version,
                versionCode,
                packageUrl,
                weChatId));

            OnNotification?.Invoke(
                result.success ? "升级通知已下发；安装结果需查看 Android 日志" : $"升级通知下发失败：{result.message}",
                result.success);

            return result;
        }











        public async Task LoadMomentsAsync()





        {





            var deviceUuid = SelectedDevice?.uuid ?? string.Empty;





            if (string.IsNullOrWhiteSpace(deviceUuid))





            {





                CurrentMoments = new List<MomentsTimeline>();





            }





            else





            {





                var accountId = FirstNonEmptyLocal(SelectedDevice?.weChatId, SelectedDevice?.wx?.wechatAccount?.wxid);
                var serverMoments = await ExecuteServiceCallAsync(() => _service.GetMomentsAsync(deviceUuid, 50, accountId));
                CurrentMoments = MergePendingMomentInteractions(serverMoments);
                _logger.LogInformation(
                    "[CrmStore] 朋友圈列表刷新完成：DeviceUuid={DeviceUuid}, AccountId={AccountId}, Loaded={LoadedCount}",
                    deviceUuid,
                    accountId,
                    CurrentMoments.Count);





            }





            NotifyStateChanged();





        }











        public Task DeleteWeChatAccountAsync(string wxid) { return Task.CompletedTask; }





        public Task<List<SrClient>> LoadAllDevicesAsync() { return ExecuteServiceCallAsync(() => _service.GetDevicesAsync()); }





        public Task<List<SrClient>> GetDevicesAsync() => ExecuteServiceCallAsync(() => _service.GetDevicesAsync());





        public Task<List<WechatAccount>> LoadAllWeChatAccountsAsync() { return Task.FromResult(new List<WechatAccount>()); }





    }





}


