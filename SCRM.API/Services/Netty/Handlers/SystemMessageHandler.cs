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
                    case EnumMsgType.WeChatOfflineNotice:
                        await HandleWeChatOffline(message, context);
                        break;
                    case EnumMsgType.PostDeviceInfoNotice:
                        await HandlePostDeviceInfo(message, context);
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

        private async Task HandleWeChatOnline(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatOnlineNoticeMessage>();
            _logger.LogInformation("收到微信上线通知 (Raw): WxId={WxId}, Nick={Nick}", notice.WeChatId, notice.WeChatNick);
            
            var wechatId = notice.WeChatId;
            // Fix: Use GetConnectionAsync
            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            var deviceUuid = connInfo?.deviceUuid;

            _logger.LogInformation("WeChat Online: {WxId}, Device: {Uuid}", wechatId, deviceUuid);

            // 第一道门：单实例并发防抖拦截
            if (!string.IsNullOrEmpty(deviceUuid) && !string.IsNullOrEmpty(notice.WeChatId))
            {
                var debounceKey = $"wx_online:{deviceUuid}:{notice.WeChatId}";
                CleanupDebounceCacheIfNeeded();

                if (!TryEnterDebounceWindow(debounceKey, _debounceSeconds))
                {
                    _logger.LogWarning("[防抖拦截] Device={Device}, WxId={WxId}, Window={Sec}s",
                        deviceUuid, notice.WeChatId, _debounceSeconds);
                    await SendAckAsync(message, context); // 拦截后也要回复 ACK
                    return;
                }
            }


            if (!string.IsNullOrEmpty(deviceUuid))
            {
                var srClient = await DbHelper.GetSrClient(_db, deviceUuid);

                if (srClient != null)
                {
                    // Ensure Wx structure exists
                    if (srClient.wx == null) srClient.wx = new Wx();
                    
                    // Note: If srClient.wx.wechatAccount is null, we try to load it from DB or create new
                    if (srClient.wx.wechatAccount == null)
                    {
                        // Try to find existing account by WxId first to avoid duplicates
                        var existingAccount = await DbHelper.GetWechatAccount(_db, notice.WeChatId);
                        if (existingAccount != null)
                        {
                            srClient.wx.wechatAccount = existingAccount;
                        }
                        else
                        {
                            srClient.wx.wechatAccount = new WechatAccount();
                        }
                    }

                    var account = srClient.wx.wechatAccount;

                    // [Fix] 根据审核建议：记录旧状态，基于真实的状态翻转(Offline -> Online)来进行判定
                    bool wasOnline = account.accountStatus == 1;

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
                    account.accountStatus = 1; // 1 = Online
                    account.lastOnlineAt = DateTime.UtcNow;
                    account.updatedAt = DateTime.UtcNow;

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
                                account.ownerId
                            )),
                            "WeChatOnlineEvent", deviceUuid, account.wxid);

                        // [Fix] Trigger Contact Sync (Real-time) - 仅真首登/断线重连时下发拉取指令
                        try 
                        {
                            var syncMsg = new TransportMessage
                            {
                                MsgType = EnumMsgType.TriggerFriendPushTask,
                                Content = Any.Pack(new TriggerFriendPushTaskMessage
                                {
                                    WeChatId = account.wxid,
                                    TaskId = DateTime.Now.Ticks
                                })
                            };
                            await context.WriteAndFlushAsync(syncMsg);
                            _logger.LogInformation("[业务同步] 收到设备({Device})首次/重连微信上线通知({WxId})，已触发联系人同步指令。", deviceUuid, account.wxid);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to trigger friend sync after WeChat online notice.");
                        }

                        SafeFireAndForget(
                            _eventBus.PublishAsync(new DeviceConnectedEvent(deviceUuid, account.wxid, account.wxid, account.ownerId)
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

        private async Task HandleWeChatOffline(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatOfflineNoticeMessage>();
            // Logic to mark offline
            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            var deviceUuid = connInfo?.deviceUuid;

            _logger.LogInformation("微信未登录: Device: {Uuid} Reason: {Reason}", deviceUuid, notice.Reason);

            // Update DB
            if (!string.IsNullOrEmpty(deviceUuid))
            {
                var account = await _db.WechatAccounts.FirstOrDefaultAsync(a => a.clientUuid == deviceUuid);
                if (account != null)
                {
                    // [Fix] 离线时明确 accountStatus = 0 (Offline)，并通过 DbHelper 同步更新缓存
                    account.accountStatus = 0;
                    account.updatedAt = DateTime.UtcNow;
                    await DbHelper.SaveWechatAccount(_db, account);
                }
                // Publish Event: 只发布微信下线事件，设备的红点掉线走底层TCP断开逻辑
                SafeFireAndForget(
                    _eventBus.PublishAsync(new WeChatOfflineEvent(deviceUuid)),
                    "WeChatOfflineEvent", deviceUuid, "Offline");
            }

            await SendAckAsync(message, context);
        }

        private async Task HandlePostDeviceInfo(TransportMessage message, IChannelHandlerContext context)
        {
            // Update device basics
            await SendAckAsync(message, context);
        }
    }
}
