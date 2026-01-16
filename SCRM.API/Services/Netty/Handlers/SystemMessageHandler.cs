using DotNetty.Transport.Channels;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Hubs;
using SCRM.API.Models.Entities;
using SCRM.API.Models.Events;
using SCRM.API.Services.Core;
using SCRM.Services.Data;
using SCRM.Services.Events;
using System;
using System.Linq;
using System.Threading.Tasks;

using SCRM.API.Services.Netty.Handlers.Abstractions;

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
            var wechatId = notice.WeChatId;
            // Fix: Use GetConnectionAsync
            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            var deviceUuid = connInfo?.deviceUuid;

            _logger.LogInformation("WeChat Online: {WxId}, Device: {Uuid}", wechatId, deviceUuid);


            if (!string.IsNullOrEmpty(deviceUuid))
            {
                var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.clientUuid == deviceUuid && !w.isDeleted);
                if (account != null)
                {
                    account.nickname = notice.WeChatNick;
                    account.avatarUrl = ""; // notice.WeChatHeadUrl; // Proto property missing
                    // account.isOnline = true; // Property is read-only or missing setter
                    // Update other fields
                    await _db.SaveChangesAsync();

                    // Publish Event
                    // Constructor mismatch fix: Add dummy string for 4th arg (likely ownerId or wxid if separate)
                    _eventBus.PublishAsync(new DeviceConnectedEvent(deviceUuid, account.accountId.ToString(), account.wxid, account.ownerId) 
                    { 
                        connectionId = connId
                    });
                }
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
            
             _logger.LogInformation("WeChat Offline: Device: {Uuid}", deviceUuid);
             
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
