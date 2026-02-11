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

        public SystemMessageHandler(
            ILogger<SystemMessageHandler> logger,
            ApplicationDbContext db,
            ConnectionManager connectionManager,
            IEventBus eventBus) : base(logger)
        {
            _logger = logger;
            _db = db;
            _connectionManager = connectionManager;
            _eventBus = eventBus;
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

                    // New Event: WeChat Online (Rich Data)
                    _eventBus.PublishAsync(new WeChatOnlineEvent(
                        deviceUuid, 
                        account.wxid, 
                        account.nickname, 
                        account.wxid, // Use WxId as AccountId
                        account.ownerId
                    ));

                    // [Fix] Trigger Contact Sync (Real-time)
                    // Configured SystemHandler to trigger this because AuthHandler no longer assumes login status.
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
                        _logger.LogInformation("[业务同步] 收到设备({Device})微信上线通知({WxId})，已触发联系人同步指令。", deviceUuid, account.wxid);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to trigger friend sync after WeChat online notice.");
                    }

                    _eventBus.PublishAsync(new DeviceConnectedEvent(deviceUuid, account.wxid, account.wxid, account.ownerId)
                    {
                        connectionId = connId
                    });
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
                    // account.isOnline = false; // Read-only
                    account.lastOnlineAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
                // Publish Event
                _eventBus.PublishAsync(new DeviceStatusChangedEvent(deviceUuid, false));
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
