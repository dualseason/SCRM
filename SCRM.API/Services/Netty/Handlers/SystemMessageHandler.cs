using DotNetty.Transport.Channels;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using SCRM.SHARED.Models;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Hubs;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models.Events;
using SCRM.API.Services.Core;
using SCRM.Services.Data;
using SCRM.Services.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Configuration;

using SCRM.API.Services.Netty.Handlers.Abstractions;
using SCRM.API.Services.Data;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 系统消息处理器
    /// <para>处理设备系统级通知与状态变更。</para>
    /// <para>核心功能：</para>
    /// <list type="bullet">
    /// <item>处理微信上线通知 (WeChatOnlineNotice)</item>
    /// <item>处理微信下线通知 (WeChatOfflineNotice)</item>
    /// <item>处理设备信息上报 (PostDeviceInfoNotice)</item>
    /// <item>维护设备在线状态与基本信息</item>
    /// </list>
    /// </summary>
    public class SystemMessageHandler : MessageHandlerBase
    {
        private readonly ILogger<SystemMessageHandler> _logger;
        private readonly ApplicationDbContext _db;
        private readonly ConnectionManager _connectionManager;
        private readonly IEventBus _eventBus;
        private readonly int _debounceSeconds;

        private const short WechatAccountStatusOffline = 0;
        private const short WechatAccountStatusOnline = 1;

        private static readonly ConcurrentDictionary<string, long> _onlineDebounceUntilTicks = new();
        private static int _debounceCleanupCounter = 0;

        public SystemMessageHandler(
            ILogger<SystemMessageHandler> logger,
            ApplicationDbContext db,
            ConnectionManager connectionManager,
            IEventBus eventBus,
            IConfiguration config) : base(logger)
        {
            _logger = logger;
            _db = db;
            _connectionManager = connectionManager;
            _eventBus = eventBus;
            _debounceSeconds = config.GetValue<int>("WeChatOnlineDebounceSeconds", 15);
        }

        private static bool TryEnterDebounceWindow(string key, int debounceSeconds)
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            var untilTicks = nowTicks + TimeSpan.FromSeconds(debounceSeconds).Ticks;

            while (true)
            {
                if (_onlineDebounceUntilTicks.TryGetValue(key, out var oldUntil))
                {
                    if (oldUntil > nowTicks) return false;

                    if (_onlineDebounceUntilTicks.TryUpdate(key, untilTicks, oldUntil))
                        return true;

                    continue;
                }

                if (_onlineDebounceUntilTicks.TryAdd(key, untilTicks))
                    return true;
            }
        }

        private static void CleanupDebounceCacheIfNeeded()
        {
            if ((Interlocked.Increment(ref _debounceCleanupCounter) & 0xFF) != 0) return;

            var nowTicks = DateTime.UtcNow.Ticks;
            foreach (var kv in _onlineDebounceUntilTicks)
            {
                if (kv.Value <= nowTicks)
                    _onlineDebounceUntilTicks.TryRemove(kv.Key, out _);
            }
        }

        /// <summary>
        /// 清理指定设备/微信号对应的在线防抖窗口。
        /// <para>
        /// 用途：
        /// 1. 微信明确下线后，下一条上线通知必须允许立即恢复状态；
        /// 2. 避免“先离线后重连”仍被旧窗口误拦截，导致网页持续显示“微信未登录”。
        /// </para>
        /// </summary>
        private static void ClearDebounceWindow(string? deviceUuid, string? weChatId)
        {
            if (string.IsNullOrWhiteSpace(deviceUuid) || string.IsNullOrWhiteSpace(weChatId))
            {
                return;
            }

            var debounceKey = $"wx_online:{deviceUuid}:{weChatId}";
            _onlineDebounceUntilTicks.TryRemove(debounceKey, out _);
        }

        private void SafeFireAndForget(Task task, string eventName, string deviceUuid, string wxid)
        {
            task.ContinueWith(t =>
            {
                try
                {
                    if (t.Exception != null)
                    {
                        _logger.LogError(t.Exception,
                            "[EventBus] 事件发布失败: {EventName}, Device={Device}, WxId={WxId}",
                            eventName, deviceUuid, wxid);
                    }
                }
                catch { }
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        public async Task HandleMessage(TransportMessage message, IChannelHandlerContext context)
        {
            try
            {
                switch (message.MsgType)
                {
                    case EnumMsgType.WeChatOnlineNotice:
                        await HandleWeChatOnline(message, context);
                        break;
                    case EnumMsgType.WeChatLoginNotice:
                        await HandleWeChatLoginNotice(message, context);
                        break;
                    case EnumMsgType.WeChatOfflineNotice:
                        await HandleWeChatOffline(message, context);
                        break;
                    case EnumMsgType.AccountLogoutNotice:
                        await HandleAccountLogoutNotice(message, context);
                        break;
                    case EnumMsgType.GetWeChatsRsp:
                        await HandleGetWeChatsRsp(message, context);
                        break;
                    case EnumMsgType.PostDeviceInfoNotice:
                        await HandlePostDeviceInfo(message, context);
                        break;
                    case EnumMsgType.ConfigPushNotice:
                        await HandleConfigPushNotice(message, context);
                        break;
                    case EnumMsgType.SetConfigTask:
                        await HandleSetConfigTaskSnapshotCompat(message, context);
                        break;
                    case EnumMsgType.PostFriendDetectCountNotice:
                        await HandlePostFriendDetectCount(message, context);
                        break;
                    case EnumMsgType.FriendDetectResultNotice:
                        await HandleFriendDetectResult(message, context);
                        break;
                    // Add other cases

                    default:
                        // Just ACK
                        await SendAckAsync(message, context);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling System Message: {MsgType}", message.MsgType);
            }
        }

        /// <summary>
        /// 处理清粉任务进度通知。
        /// <para>
        /// PostFriendDetectCountNotice(2028) 是过程进度；这里复用 FriendDetectionLogs 保存过程快照，
        /// 并通过 TaskResultReceivedEvent 推送给网页，避免清粉页面只能看到“任务已下发”。
        /// </para>
        /// </summary>
        private async Task HandlePostFriendDetectCount(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<PostFriendDetectCountNoticeMessage>();
            var connectionId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connectionId);
            var deviceUuid = connInfo?.deviceUuid ?? string.Empty;
            var ownerWxid = string.IsNullOrWhiteSpace(notice.WeChatId)
                ? connInfo?.wechatId ?? string.Empty
                : notice.WeChatId.Trim();

            _logger.LogInformation(
                "PostFriendDetectCountNotice handled: WeChatId={WeChatId}, TaskId={TaskId}, Count={Count}, DelCount={DelCount}, SkipCount={SkipCount}, IsFinished={IsFinished}, Zombies={ZombiesCount}",
                ownerWxid,
                notice.TaskId,
                notice.Count,
                notice.DelCount,
                notice.SkipCount,
                notice.IsFinished,
                notice.Zombies.Count);

            try
            {
                var savedCount = await _db.SaveFriendDetectProgress(
                    ownerWxid,
                    notice.TaskId,
                    notice.Count,
                    notice.DelCount,
                    notice.SkipCount,
                    notice.IsFinished,
                    notice.Zombies);

                var summary = $"清粉进度：已检测 {notice.Count}，疑似异常 {notice.Zombies.Count}，跳过 {notice.SkipCount}，删除 {notice.DelCount}，完成={notice.IsFinished}";
                _logger.LogInformation(
                    "清粉进度已落库: WeChatId={WeChatId}, TaskId={TaskId}, SavedLogs={SavedLogs}",
                    ownerWxid,
                    notice.TaskId,
                    savedCount);

                SafeFireAndForget(
                    _eventBus.PublishAsync(new TaskResultReceivedEvent(notice.TaskId, true, summary, connectionId, deviceUuid)),
                    "FriendDetectProgressResult",
                    deviceUuid,
                    ownerWxid);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "清粉进度落库或推送失败: WeChatId={WeChatId}, TaskId={TaskId}",
                    ownerWxid,
                    notice.TaskId);

                SafeFireAndForget(
                    _eventBus.PublishAsync(new TaskResultReceivedEvent(notice.TaskId, false, "清粉进度保存失败：" + ex.Message, connectionId, deviceUuid)),
                    "FriendDetectProgressResultFailed",
                    deviceUuid,
                    ownerWxid);
            }

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理清粉任务完整结果通知。
        /// <para>
        /// FriendDetectResultNotice(1280) 包含清粉结果明细；这里按“汇总 + 联系人明细”写入 FriendDetectionLogs，
        /// 并推送 TaskResultReceivedEvent 让前端获得最终结果提示。
        /// </para>
        /// </summary>
        private async Task HandleFriendDetectResult(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendDetectResultNoticeMessage>();
            var connectionId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connectionId);
            var deviceUuid = connInfo?.deviceUuid ?? string.Empty;
            var ownerWxid = string.IsNullOrWhiteSpace(notice.WeChatId)
                ? connInfo?.wechatId ?? string.Empty
                : notice.WeChatId.Trim();

            _logger.LogInformation(
                "FriendDetectResultNotice handled: WeChatId={WeChatId}, TaskId={TaskId}, IsFinished={IsFinished}, Count={Count}, DelCount={DelCount}, SkipCount={SkipCount}, Zombies={ZombiesCount}, Blocked={BlockedCount}, Banned={BannedCount}, Canceled={CanceledCount}",
                ownerWxid,
                notice.TaskId,
                notice.IsFinished,
                notice.Count,
                notice.DelCount,
                notice.SkipCount,
                notice.Zombies.Count,
                notice.BlockedList.Count,
                notice.BannedList.Count,
                notice.CanceledList.Count);

            try
            {
                var savedCount = await _db.SaveFriendDetectResult(
                    ownerWxid,
                    notice.TaskId,
                    notice.StartTime,
                    notice.EndTime,
                    notice.IsFinished,
                    notice.Count,
                    notice.SkipCount,
                    notice.DelCount,
                    notice.Zombies,
                    notice.BlockedList,
                    notice.BannedList,
                    notice.CanceledList);

                var summary = $"清粉结果：检测 {notice.Count}，僵尸 {notice.Zombies.Count}，拉黑 {notice.BlockedList.Count}，封禁 {notice.BannedList.Count}，取消 {notice.CanceledList.Count}，跳过 {notice.SkipCount}，删除 {notice.DelCount}";
                _logger.LogInformation(
                    "清粉结果已落库: WeChatId={WeChatId}, TaskId={TaskId}, SavedLogs={SavedLogs}",
                    ownerWxid,
                    notice.TaskId,
                    savedCount);

                SafeFireAndForget(
                    _eventBus.PublishAsync(new TaskResultReceivedEvent(notice.TaskId, true, summary, connectionId, deviceUuid)),
                    "FriendDetectFinalResult",
                    deviceUuid,
                    ownerWxid);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "清粉结果落库或推送失败: WeChatId={WeChatId}, TaskId={TaskId}",
                    ownerWxid,
                    notice.TaskId);

                SafeFireAndForget(
                    _eventBus.PublishAsync(new TaskResultReceivedEvent(notice.TaskId, false, "清粉结果保存失败：" + ex.Message, connectionId, deviceUuid)),
                    "FriendDetectFinalResultFailed",
                    deviceUuid,
                    ownerWxid);
            }

            await SendAckAsync(message, context);
        }

        private async Task HandleWeChatOnline(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatOnlineNoticeMessage>();
            _logger.LogInformation("收到微信上线通知 (Raw): WxId={WxId}, Nick={Nick}", notice.WeChatId, notice.WeChatNick);
            
            var wechatId = notice.WeChatId;
            var connId = context.Channel.Id.AsLongText();

            // 先把连接表里的微信信息与活跃时间同步起来。
            // 原因：
            // 1. SilentClientCheck 依赖 ConnectionManager._connections 中的 wechatId 判定“是否已上报微信号”。
            // 2. 旧实现虽然收到了 WeChatOnlineNotice，但没有把 wechatId 回写到连接表，
            //    导致连接实际上已经在线，SilentClientCheck 仍持续误判为 silent。
            // 3. 这里在进入业务防抖/落库前就先同步连接态，避免后续任一步骤异常时又回到误判状态。
            await _connectionManager.UpdateConnectionActivityAsync(connId);
            if (!string.IsNullOrEmpty(notice.WeChatId))
            {
                await _connectionManager.UpdateConnectionWeChatInfoAsync(connId, notice.WeChatId, notice.WeChatNick);
            }

            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            var deviceUuid = connInfo?.deviceUuid;

            if (string.IsNullOrWhiteSpace(notice.WeChatId))
            {
                _logger.LogWarning("收到微信上线通知但未携带 WeChatId，已跳过账号落库: Device={Device}, Nick={Nick}", deviceUuid, notice.WeChatNick);
                await SendAckAsync(message, context);
                return;
            }

            _logger.LogInformation("WeChat Online: {WxId}, Device: {Uuid}", wechatId, deviceUuid);

            // 先看当前设备在服务端视角下是否已处于“微信在线”。
            // 只有“已经在线”的重复通知才允许被防抖直接拦截；
            // 如果刚经历过离线/断线重连，即使仍在防抖窗口内，也必须放行恢复状态。
            SrClient? srClient = null;
            bool wasOnlineBeforeDebounce = false;
            if (!string.IsNullOrEmpty(deviceUuid))
            {
                srClient = await DbHelper.GetSrClient(_db, deviceUuid);
                wasOnlineBeforeDebounce = srClient?.wx?.wechatAccount?.accountStatus == WechatAccountStatusOnline;
            }

            // 第一道门：单实例并发防抖拦截
            if (!string.IsNullOrEmpty(deviceUuid) && !string.IsNullOrEmpty(notice.WeChatId))
            {
                var debounceKey = $"wx_online:{deviceUuid}:{notice.WeChatId}";
                CleanupDebounceCacheIfNeeded();

                if (!TryEnterDebounceWindow(debounceKey, _debounceSeconds))
                {
                    if (wasOnlineBeforeDebounce)
                    {
                        // 同一微信在线通知在短时间内重复到达是正常现象。
                        // 仅当服务端当前状态已经是“在线”时，才把它视为纯重复心跳并直接拦截。
                        _logger.LogInformation("[防抖拦截] Device={Device}, WxId={WxId}, Window={Sec}s",
                            deviceUuid, notice.WeChatId, _debounceSeconds);
                        await SendAckAsync(message, context); // 拦截后也要回复 ACK
                        return;
                    }

                    _logger.LogInformation(
                        "[防抖放行] Device={Device}, WxId={WxId}, Window={Sec}s, Reason=CurrentStateOffline",
                        deviceUuid, notice.WeChatId, _debounceSeconds);
                }
            }


            if (!string.IsNullOrEmpty(deviceUuid))
            {
                if (srClient != null)
                {
                    // Ensure Wx structure exists
                    if (srClient.wx == null) srClient.wx = new Wx();
                    
                    // 按 1020 notice.WeChatId 精确选中账号，避免多账号设备切号时改写旧账号主键。
                    if (srClient.wx.wechatAccount == null || !IsSameWxid(srClient.wx.wechatAccount.wxid, notice.WeChatId))
                    {
                        // Try to find existing account by WxId first to avoid duplicates
                        var existingAccount = await DbHelper.GetWechatAccount(_db, notice.WeChatId);
                        if (existingAccount != null)
                        {
                            srClient.wx.wechatAccount = existingAccount;
                        }
                        else
                        {
                            srClient.wx.wechatAccount = new WechatAccount
                            {
                                wxid = notice.WeChatId,
                                createdAt = DateTime.UtcNow
                            };
                        }
                    }

                    var account = srClient.wx.wechatAccount;

                    // [Fix] 根据审核建议：记录旧状态，基于真实的状态翻转(Offline -> Online)来进行判定
                    bool wasOnline = account.accountStatus == WechatAccountStatusOnline;

                    // Update Mapping
                    account.wxid = notice.WeChatId;
                    account.nickname = notice.WeChatNick;
                    account.wechatNumber = notice.WeChatNo;
                    account.avatarUrl = notice.Avatar;
                    account.mobilePhone = notice.Phone;
                    account.gender = (short)notice.Gender;
                    account.region = $"{notice.Country} {notice.Province} {notice.City}".Trim();
                    
                    // Link to this client
                    account.clientUuid = deviceUuid;
                    account.ownerId = srClient.ownerId; // Inherit ownership from device

                    // Explicitly set Online status
                    account.accountStatus = WechatAccountStatusOnline;
                    account.lastOnlineAt = DateTime.UtcNow;
                    account.updatedAt = DateTime.UtcNow;
                    AppendLoggedInWeChatId(srClient, account.wxid);

                    // Save using Atomic Helpers (This handles Cache Update too)
                    await DbHelper.SaveWechatAccount(_db, account);
                    await DbHelper.SaveSrClient(_db, srClient);

                    if (!wasOnline)
                    {
                        // 第二道门：仅真实状态翻转 (Offline -> Online) 时才发布核心事件与触发业务
                        // New Event: WeChat Online (Rich Data) - 仅状态翻转时发布，防止心跳周期引发的事件风暴
                        SafeFireAndForget(
                            _eventBus.PublishAsync(new WeChatOnlineEvent(
                                deviceUuid, 
                                account.wxid, 
                                account.nickname, 
                                account.wxid, // Use WxId as AccountId
                                account.ownerId ?? string.Empty
                            )),
                            "WeChatOnlineEvent", deviceUuid, account.wxid);

                        // [Fix] Trigger Contact/ChatRoom/Conversation Sync (Real-time)
                        // 仅真首登/断线重连时下发。联系人、群资料、会话列表分别覆盖 Web 端好友列表、群聊入口和 IM 会话入口。
                        try 
                        {
                            var baseTaskId = DateTime.Now.Ticks;
                            var syncMsg = new TransportMessage
                            {
                                Id = baseTaskId,
                                MsgType = EnumMsgType.TriggerFriendPushTask,
                                Content = Any.Pack(new TriggerFriendPushTaskMessage
                                {
                                    WeChatId = account.wxid,
                                    TaskId = baseTaskId
                                })
                            };
                            await context.WriteAndFlushAsync(syncMsg);

                            var roomMsg = new TransportMessage
                            {
                                Id = baseTaskId + 1,
                                MsgType = EnumMsgType.TriggerChatroomPushTask,
                                Content = Any.Pack(new TriggerChatRoomPushTaskMessage
                                {
                                    WeChatId = account.wxid,
                                    TaskId = baseTaskId + 1
                                })
                            };
                            await context.WriteAndFlushAsync(roomMsg);

                            var conversationMsg = new TransportMessage
                            {
                                Id = baseTaskId + 2,
                                MsgType = EnumMsgType.TriggerConversationPushTask,
                                Content = Any.Pack(new TriggerConversationPushTaskMessage
                                {
                                    WeChatId = account.wxid,
                                    WithName = true,
                                    Limit = 100,
                                    Offset = 0,
                                    TaskId = baseTaskId + 2
                                })
                            };
                            await context.WriteAndFlushAsync(conversationMsg);

                            _logger.LogInformation(
                                "[业务同步] 收到设备({Device})首次/重连微信上线通知({WxId})，已触发联系人/群聊/会话同步指令。",
                                deviceUuid,
                                account.wxid);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to trigger initial business sync after WeChat online notice.");
                        }

                        SafeFireAndForget(
                            _eventBus.PublishAsync(new DeviceConnectedEvent(deviceUuid, account.wxid, account.wxid, account.ownerId ?? string.Empty)
                            {
                                connectionId = connId
                            }),
                            "DeviceConnectedEvent", deviceUuid, account.wxid);
                    }
                    else
                    {
                        _logger.LogInformation("[业务同步] 设备({Device})微信({WxId})心跳上线通知，已跳过下发联系人同步与在线事件重复发布。", deviceUuid, account.wxid);
                    }
                }


                /* 
                // Legacy Logic Removed
                var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.clientUuid == deviceUuid && !w.isDeleted);
                if (account != null)
                {
                   // ...
                }
                */
            }

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理 3055 微信登录通知。
        /// <para>
        /// Android 端当前仍并行上报 1020 WeChatOnlineNotice；3055 作为 62203 账号体系补充。
        /// 只有通知明确为登录态时才补齐连接表并用 DbHelper 写入最小账号快照，
        /// 真实昵称、头像、联系人同步仍以 1020 后续主链为准。
        /// </para>
        /// </summary>
        private async Task HandleWeChatLoginNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatLoginNoticeMessage>();
            var connId = context.Channel.Id.AsLongText();
            await _connectionManager.UpdateConnectionActivityAsync(connId);
            var connInfo = await _connectionManager.GetConnectionAsync(connId);

            var login = notice.WeChats.FirstOrDefault(w => w.IsLogin && !string.IsNullOrWhiteSpace(w.WeChatId));

            if (login != null)
            {
                await _connectionManager.UpdateConnectionWeChatInfoAsync(connId, login.WeChatId);
                if (login.IsLogin)
                {
                    await UpsertWechatAccountSnapshotFromGetWeChatsAsync(
                        new WeChatRspMessage
                        {
                            WeChatId = login.WeChatId,
                            IsOnline = true,
                            IsLogined = true
                        },
                        connInfo?.deviceUuid);
                }

                _logger.LogInformation(
                    "收到 3055 微信登录通知: WxId={WxId}, IsLogin={IsLogin}, UnionId={UnionId}, AccountType={AccountType}",
                    login.WeChatId,
                    login.IsLogin,
                    notice.UnionId,
                    notice.AccountType);
            }
            else
            {
                var lastKnown = notice.WeChats.FirstOrDefault(w => !string.IsNullOrWhiteSpace(w.WeChatId));
                _logger.LogInformation(
                    "收到 3055 微信登录通知，但未包含 IsLogin=true 的账号，不更新连接当前微信: LastKnownWxId={LastKnownWxId}, UnionId={UnionId}, AccountType={AccountType}, Count={Count}",
                    lastKnown?.WeChatId,
                    notice.UnionId,
                    notice.AccountType,
                    notice.WeChats.Count);
            }

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理 3054 账号登出通知。
        /// <para>
        /// 3054 只携带 UnionId/AccountType，不携带 wxid；优先由 1021 WeChatOfflineNotice 精确离线。
        /// 若 1021 丢失，这里仅用当前连接 wxid 或当前设备最近在线账号做保守兜底，不能离线设备所有历史账号。
        /// </para>
        /// </summary>
        private async Task HandleAccountLogoutNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<AccountLogoutNoticeMessage>();
            var connId = context.Channel.Id.AsLongText();
            await _connectionManager.UpdateConnectionActivityAsync(connId);

            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            var account = await ResolveWechatAccountForOfflineAsync(
                connInfo?.deviceUuid,
                preferredWxid: null,
                connectionWxid: connInfo?.wechatId,
                source: "AccountLogoutNotice(3054)");

            if (account != null)
            {
                var marked = await MarkWechatAccountOfflineAsync(
                    account,
                    connInfo?.deviceUuid,
                    "AccountLogoutNotice(3054)",
                    $"UnionId={notice.UnionId}, AccountType={notice.AccountType}");

                if (marked && IsSameWxid(connInfo?.wechatId, account.wxid))
                {
                    await _connectionManager.UpdateConnectionWeChatInfoAsync(connId, string.Empty);
                }
            }
            else
            {
                _logger.LogInformation(
                    "收到 3054 账号登出通知，但未找到可保守离线的当前微信账号: Device={Device}, WxId={WxId}, UnionId={UnionId}, AccountType={AccountType}",
                    connInfo?.deviceUuid,
                    connInfo?.wechatId,
                    notice.UnionId,
                    notice.AccountType);
            }

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理 3051 微信账号列表查询响应。
        /// <para>
        /// 当前 3051 只作为账号状态查询/诊断通道；真实上线后的联系人同步和在线事件仍以 1020 WeChatOnlineNotice 为准。
        /// 这里更新连接表；若响应明确在线/已登录，则用 DbHelper 补齐账号快照，避免服务端重启后 Web 端短暂显示“微信未登录”。
        /// </para>
        /// </summary>
        private async Task HandleGetWeChatsRsp(TransportMessage message, IChannelHandlerContext context)
        {
            var rsp = message.Content.Unpack<GetWeChatsRspMessage>();
            var connId = context.Channel.Id.AsLongText();
            await _connectionManager.UpdateConnectionActivityAsync(connId);
            var connInfo = await _connectionManager.GetConnectionAsync(connId);

            var current = rsp.WeChats.FirstOrDefault(w => w.IsLogined && !string.IsNullOrWhiteSpace(w.WeChatId))
                ?? rsp.WeChats.FirstOrDefault(w => w.IsOnline && !string.IsNullOrWhiteSpace(w.WeChatId))
                ?? rsp.WeChats.FirstOrDefault(w => !string.IsNullOrWhiteSpace(w.WeChatId));

            if (current != null)
            {
                var isCurrentOnline = current.IsLogined || current.IsOnline;
                if (isCurrentOnline)
                {
                    await _connectionManager.UpdateConnectionWeChatInfoAsync(
                        connId,
                        current.WeChatId,
                        current.WeChatNick);

                    await UpsertWechatAccountSnapshotFromGetWeChatsAsync(current, connInfo?.deviceUuid);
                }
                else
                {
                    _logger.LogInformation(
                        "收到 3051 微信账号列表响应中的离线/lastKnown 快照，仅记录诊断，不更新连接当前微信: Conn={ConnectionId}, WxId={WxId}, Nick={Nick}, Online={Online}, Logined={Logined}, UnionId={UnionId}, AccountType={AccountType}, Count={Count}",
                        connId,
                        current.WeChatId,
                        current.WeChatNick,
                        current.IsOnline,
                        current.IsLogined,
                        rsp.UnionId,
                        rsp.AccountType,
                        rsp.WeChats.Count);
                }

                _logger.LogInformation(
                    "收到 3051 微信账号列表响应: Conn={ConnectionId}, WxId={WxId}, Nick={Nick}, Online={Online}, Logined={Logined}, UnionId={UnionId}, AccountType={AccountType}, Count={Count}",
                    connId,
                    current.WeChatId,
                    current.WeChatNick,
                    current.IsOnline,
                    current.IsLogined,
                    rsp.UnionId,
                    rsp.AccountType,
                    rsp.WeChats.Count);
            }
            else
            {
                _logger.LogInformation(
                    "收到 3051 微信账号列表响应但未携带账号: Conn={ConnectionId}, UnionId={UnionId}, AccountType={AccountType}, Count={Count}",
                    connId,
                    rsp.UnionId,
                    rsp.AccountType,
                    rsp.WeChats.Count);
            }

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 从 3051 账号查询响应补齐服务端账号快照。
        /// <para>
        /// 3051 不是主上线事件，不能替代 1020 的联系人同步/在线事件发布；
        /// 但它能在服务端重启、认证后上线通知尚未到达或被冷却窗口延后时，
        /// 先把 Web 端设备/微信展示所需的 wxid、昵称和在线状态写入 DbHelper 管理的缓存与数据库。
        /// </para>
        /// </summary>
        private async Task UpsertWechatAccountSnapshotFromGetWeChatsAsync(
            WeChatRspMessage current,
            string? deviceUuid)
        {
            if (current == null || string.IsNullOrWhiteSpace(current.WeChatId) || string.IsNullOrWhiteSpace(deviceUuid))
            {
                return;
            }

            var srClient = await DbHelper.GetSrClient(_db, deviceUuid);
            if (srClient == null)
            {
                _logger.LogDebug(
                    "3051账号快照跳过落库：设备不存在 Device={Device}, WxId={WxId}",
                    deviceUuid,
                    current.WeChatId);
                return;
            }

            srClient.wx ??= new Wx { srClient = srClient };

            var account = await DbHelper.GetWechatAccount(_db, current.WeChatId)
                ?? new WechatAccount
                {
                    wxid = current.WeChatId,
                    createdAt = DateTime.UtcNow
                };

            account.wxid = current.WeChatId;
            account.wechatNumber = string.IsNullOrWhiteSpace(current.WeChatNo) ? account.wechatNumber : current.WeChatNo;
            account.nickname = string.IsNullOrWhiteSpace(current.WeChatNick) ? account.nickname : current.WeChatNick;
            account.avatarUrl = string.IsNullOrWhiteSpace(current.Avatar) ? account.avatarUrl : current.Avatar;
            account.gender = current.Gender == EnumGender.UnknownGender ? account.gender : (short)current.Gender;
            account.region = BuildRegion(current.Country, current.Province, current.City, account.region);
            account.clientUuid = deviceUuid;
            account.ownerId = srClient.ownerId;
            account.accountStatus = (current.IsLogined || current.IsOnline) ? WechatAccountStatusOnline : account.accountStatus;
            account.lastOnlineAt = (current.IsLogined || current.IsOnline) ? DateTime.UtcNow : account.lastOnlineAt;
            account.isDeleted = false;
            account.updatedAt = DateTime.UtcNow;

            srClient.wx.wechatAccount = account;
            srClient.wx.srClient = srClient;
            srClient.isOnline = true;
            srClient.lastLoginAt ??= DateTime.UtcNow;
            AppendLoggedInWeChatId(srClient, current.WeChatId);

            await DbHelper.SaveWechatAccount(_db, account);
            await DbHelper.SaveSrClient(_db, srClient);
        }

        private static string? BuildRegion(string? country, string? province, string? city, string? fallback)
        {
            var parts = new[] { country, province, city }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return parts.Length > 0 ? string.Join(" ", parts) : fallback;
        }

        /// <summary>
        /// 处理 62203 标准配置快照上报。
        /// <para>
        /// ConfigPushNotice(1381) 是 Android 主动回传本地 SharedPreferences 配置的标准协议。
        /// 当前先做解析、结构化日志与 ACK，不自动覆盖全局 SystemConfigs，避免多设备旧配置反向污染服务端配置。
        /// </para>
        /// </summary>
        private async Task HandleConfigPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ConfigPushNoticeMessage>();
            await ApplyConfigSnapshotAsync(
                message,
                context,
                notice.IMEI,
                notice.WeChatId,
                notice.BoolConfs,
                notice.IntConfs,
                notice.StrConfs,
                "ConfigPushNotice");
        }

        /// <summary>
        /// 兼容处理当前 SmRun 使用 SetConfigTask(1382) 回传配置快照的口径。
        /// <para>
        /// SetConfigTask 原本是服务端下发配置；但当前 Android 端 uploadConfigInfo 也会以该消息号上报快照。
        /// SCRM 在接收方向只把它作为“客户端配置快照”解析并 ACK，不再仅默认 ACK。
        /// </para>
        /// </summary>
        private async Task HandleSetConfigTaskSnapshotCompat(TransportMessage message, IChannelHandlerContext context)
        {
            var snapshot = message.Content.Unpack<SetConfigTaskMessage>();
            await ApplyConfigSnapshotAsync(
                message,
                context,
                snapshot.IMEI,
                snapshot.WeChatId,
                snapshot.BoolConfs,
                snapshot.IntConfs,
                snapshot.StrConfs,
                "SetConfigTaskCompatSnapshot");
        }

        /// <summary>
        /// 统一解析 Android 配置快照。
        /// <para>
        /// 注意：这里不直接写 SystemConfigs。SystemConfigs 是服务端全局配置源，
        /// Android 上报可能是某台设备的旧缓存；若直接覆盖，会造成“旧设备反向回滚全局配置”。
        /// 后续如需持久化设备级快照，应新增设备维度表并通过 DbHelper 统一维护。
        /// </para>
        /// </summary>
        private async Task ApplyConfigSnapshotAsync(
            TransportMessage message,
            IChannelHandlerContext context,
            string? imei,
            string? weChatId,
            IEnumerable<BoolConfigMessage> boolConfs,
            IEnumerable<IntConfigMessage> intConfs,
            IEnumerable<StrConfigMessage> strConfs,
            string source)
        {
            var connectionId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connectionId);
            var deviceUuid = !string.IsNullOrWhiteSpace(connInfo?.deviceUuid)
                ? connInfo!.deviceUuid
                : imei ?? string.Empty;
            var ownerWxid = !string.IsNullOrWhiteSpace(weChatId)
                ? weChatId!.Trim()
                : connInfo?.wechatId ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(ownerWxid) && string.IsNullOrWhiteSpace(connInfo?.wechatId))
            {
                await _connectionManager.UpdateConnectionWeChatInfoAsync(connectionId, ownerWxid);
            }

            var boolItems = NormalizeBoolConfigs(boolConfs);
            var intItems = NormalizeIntConfigs(intConfs);
            var strItems = NormalizeStrConfigs(strConfs);

            _logger.LogInformation(
                "[配置快照] {Source} 已解析: Device={DeviceUuid}, IMEI={IMEI}, WeChatId={WeChatId}, Bool={BoolCount}, Int={IntCount}, Str={StrCount}, BoolKeys={BoolKeys}, IntKeys={IntKeys}, StrKeys={StrKeys}",
                source,
                deviceUuid,
                imei,
                ownerWxid,
                boolItems.Count,
                intItems.Count,
                strItems.Count,
                string.Join(",", boolItems.Select(item => $"{item.Key}={item.Value}")),
                string.Join(",", intItems.Select(item => $"{item.Key}={item.Value}")),
                string.Join(",", strItems.Select(item => item.Key)));

            await SendAckAsync(message, context);
        }

        private static List<(string Key, bool Value)> NormalizeBoolConfigs(IEnumerable<BoolConfigMessage> configs)
        {
            return configs
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .Select(item => (Key: item.Key.Trim(), item.Value))
                .ToList();
        }

        private static List<(string Key, int Value)> NormalizeIntConfigs(IEnumerable<IntConfigMessage> configs)
        {
            return configs
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .Select(item => (Key: item.Key.Trim(), item.Value))
                .ToList();
        }

        private static List<(string Key, string Value)> NormalizeStrConfigs(IEnumerable<StrConfigMessage> configs)
        {
            return configs
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .Select(item => (Key: item.Key.Trim(), item.Value ?? string.Empty))
                .ToList();
        }

        private async Task HandleWeChatOffline(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatOfflineNoticeMessage>();
            // Logic to mark offline
            var connId = context.Channel.Id.AsLongText();
            await _connectionManager.UpdateConnectionActivityAsync(connId);
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            var deviceUuid = connInfo?.deviceUuid;

            _logger.LogInformation("微信未登录: Device: {Uuid}, NoticeWxId={NoticeWxId}, ConnWxId={ConnWxId}, Reason: {Reason}",
                deviceUuid,
                notice.WeChatId,
                connInfo?.wechatId,
                notice.Reason);

            // 微信已明确下线时，立刻清掉该账号的在线防抖窗口，
            // 避免几秒内重新上线时仍被误判成重复在线通知。
            ClearDebounceWindow(deviceUuid, notice.WeChatId);

            // Update DB
            if (!string.IsNullOrEmpty(deviceUuid))
            {
                var account = await ResolveWechatAccountForOfflineAsync(
                    deviceUuid,
                    notice.WeChatId,
                    connInfo?.wechatId,
                    "WeChatOfflineNotice(1021)");

                if (account != null)
                {
                    await MarkWechatAccountOfflineAsync(
                        account,
                        deviceUuid,
                        "WeChatOfflineNotice(1021)",
                        notice.Reason.ToString());

                    ClearDebounceWindow(deviceUuid, account.wxid);
                    if (IsSameWxid(connInfo?.wechatId, account.wxid))
                    {
                        await _connectionManager.UpdateConnectionWeChatInfoAsync(connId, string.Empty);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "微信离线通知未匹配到账号，已跳过账号落库避免误标其他账号离线: Device={Device}, NoticeWxId={NoticeWxId}, ConnWxId={ConnWxId}, Reason={Reason}",
                        deviceUuid,
                        notice.WeChatId,
                        connInfo?.wechatId,
                        notice.Reason);
                }

                // Publish Event: 只发布微信下线事件，设备的红点掉线走底层TCP断开逻辑
                SafeFireAndForget(
                    _eventBus.PublishAsync(new WeChatOfflineEvent(deviceUuid)),
                    "WeChatOfflineEvent", deviceUuid, "Offline");
            }

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 追加设备登录过的微信号，保持大小写不敏感去重。
        /// <para>1020 富信息上线与 3051 账号快照都应维护该列表，避免纯 1020 路径遗漏历史登录 wxid。</para>
        /// </summary>
        private static bool AppendLoggedInWeChatId(SrClient client, string? wxid)
        {
            if (client == null || string.IsNullOrWhiteSpace(wxid))
            {
                return false;
            }

            client.loggedInWeChatIds ??= new List<string>();
            var normalizedWxid = wxid.Trim();
            if (client.loggedInWeChatIds.Any(id => string.Equals(id, normalizedWxid, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            client.loggedInWeChatIds.Add(normalizedWxid);
            return true;
        }

        /// <summary>
        /// 解析需要被标记离线的微信账号。
        /// <para>优先使用 1021 明确携带的 wxid；3054 不携带 wxid 时，只能保守使用当前连接 wxid 或当前设备最近在线账号。</para>
        /// </summary>
        private async Task<WechatAccount?> ResolveWechatAccountForOfflineAsync(
            string? deviceUuid,
            string? preferredWxid,
            string? connectionWxid,
            string source)
        {
            var normalizedDeviceUuid = deviceUuid?.Trim();
            var preferred = NormalizeWxid(preferredWxid);
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                var preferredAccount = await DbHelper.GetWechatAccount(_db, preferred);
                if (preferredAccount != null)
                {
                    if (!IsAccountBelongsToDeviceOrUnbound(preferredAccount, normalizedDeviceUuid))
                    {
                        _logger.LogWarning(
                            "{Source} 离线账号匹配冲突，已拒绝按 notice.WeChatId 标记离线: NoticeWxId={WxId}, AccountDevice={AccountDevice}, CurrentDevice={CurrentDevice}",
                            source,
                            preferred,
                            preferredAccount.clientUuid,
                            normalizedDeviceUuid);
                        return null;
                    }

                    if (string.IsNullOrWhiteSpace(preferredAccount.clientUuid) && !string.IsNullOrWhiteSpace(normalizedDeviceUuid))
                    {
                        preferredAccount.clientUuid = normalizedDeviceUuid;
                    }

                    return preferredAccount;
                }

                _logger.LogInformation(
                    "{Source} 未找到 notice.WeChatId 对应账号，将尝试当前连接或最近在线账号兜底: NoticeWxId={WxId}, Device={Device}",
                    source,
                    preferred,
                    normalizedDeviceUuid);
            }

            var connection = NormalizeWxid(connectionWxid);
            if (!string.IsNullOrWhiteSpace(connection)
                && !string.Equals(connection, preferred, StringComparison.OrdinalIgnoreCase))
            {
                var connectionAccount = await DbHelper.GetWechatAccount(_db, connection);
                if (connectionAccount != null && IsAccountBelongsToDeviceOrUnbound(connectionAccount, normalizedDeviceUuid))
                {
                    if (string.IsNullOrWhiteSpace(connectionAccount.clientUuid) && !string.IsNullOrWhiteSpace(normalizedDeviceUuid))
                    {
                        connectionAccount.clientUuid = normalizedDeviceUuid;
                    }

                    return connectionAccount;
                }

                if (connectionAccount != null)
                {
                    _logger.LogWarning(
                        "{Source} 当前连接 wxid 与设备归属冲突，已跳过连接兜底: ConnWxId={WxId}, AccountDevice={AccountDevice}, CurrentDevice={CurrentDevice}",
                        source,
                        connection,
                        connectionAccount.clientUuid,
                        normalizedDeviceUuid);
                }
            }

            if (string.IsNullOrWhiteSpace(normalizedDeviceUuid))
            {
                return null;
            }

            var fallbackWxid = await _db.WechatAccounts
                .Where(a => a.clientUuid == normalizedDeviceUuid
                    && !a.isDeleted
                    && a.accountStatus == WechatAccountStatusOnline)
                .OrderByDescending(a => a.lastOnlineAt)
                .ThenByDescending(a => a.updatedAt)
                .Select(a => a.wxid)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(fallbackWxid)
                ? null
                : await DbHelper.GetWechatAccount(_db, fallbackWxid);
        }

        /// <summary>
        /// 将指定微信账号标记为离线，并通过 DbHelper 同步数据库与 GlobalCache。
        /// </summary>
        private async Task<bool> MarkWechatAccountOfflineAsync(
            WechatAccount account,
            string? deviceUuid,
            string source,
            string? reason)
        {
            if (account == null || string.IsNullOrWhiteSpace(account.wxid))
            {
                return false;
            }

            var normalizedDeviceUuid = deviceUuid?.Trim();
            if (string.IsNullOrWhiteSpace(account.clientUuid) && !string.IsNullOrWhiteSpace(normalizedDeviceUuid))
            {
                account.clientUuid = normalizedDeviceUuid;
            }

            account.accountStatus = WechatAccountStatusOffline;
            account.updatedAt = DateTime.UtcNow;
            await DbHelper.SaveWechatAccount(_db, account);

            if (!string.IsNullOrWhiteSpace(normalizedDeviceUuid))
            {
                var srClient = await DbHelper.GetSrClient(_db, normalizedDeviceUuid);
                if (srClient?.wx?.wechatAccount != null && IsSameWxid(srClient.wx.wechatAccount.wxid, account.wxid))
                {
                    srClient.wx.wechatAccount = account;
                    await DbHelper.SaveSrClient(_db, srClient);
                }
            }

            _logger.LogInformation(
                "{Source} 已标记微信账号离线: Device={Device}, WxId={WxId}, Reason={Reason}",
                source,
                normalizedDeviceUuid,
                account.wxid,
                reason);

            return true;
        }

        private static bool IsAccountBelongsToDeviceOrUnbound(WechatAccount account, string? deviceUuid)
        {
            return account != null
                && (string.IsNullOrWhiteSpace(account.clientUuid)
                    || string.IsNullOrWhiteSpace(deviceUuid)
                    || string.Equals(account.clientUuid, deviceUuid, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSameWxid(string? left, string? right)
        {
            return !string.IsNullOrWhiteSpace(left)
                && !string.IsNullOrWhiteSpace(right)
                && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeWxid(string? wxid)
        {
            return wxid?.Trim() ?? string.Empty;
        }

        private async Task HandlePostDeviceInfo(TransportMessage message, IChannelHandlerContext context)
        {
            // Update device basics
            await SendAckAsync(message, context);
        }
    }
}
