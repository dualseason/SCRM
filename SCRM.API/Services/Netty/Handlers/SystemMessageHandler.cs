using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using SCRM.API.Hubs;
using SCRM.Services;
using SCRM.Services.Data;
using SCRM.API.Services.Data;
using SCRM.API.Services;
using SCRM.SHARED.Models;
using SCRM.API.Models.Entities;


namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 系统消息处理器
    /// 负责处理微信上下线通知、配置推送、设备信息上报等系统级消息
    /// Scoped Service
    /// </summary>
    public class SystemMessageHandler
    {
        private readonly ILogger<SystemMessageHandler> _logger;
        private readonly ConnectionManager _connectionManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHubContext<ClientHub> _hubContext;
        private readonly ClientTaskService _clientTaskService;

        public SystemMessageHandler(
            ILogger<SystemMessageHandler> logger,
            ConnectionManager connectionManager,
            ApplicationDbContext dbContext,
            IHubContext<ClientHub> hubContext,
            ClientTaskService clientTaskService)
        {
            _logger = logger;
            _connectionManager = connectionManager;
            _dbContext = dbContext;
            _hubContext = hubContext;
            _clientTaskService = clientTaskService;
        }

        /// <summary>
        /// 处理微信上线通知 (MsgType: 300)
        /// 1. 更新数据库中 WechatAccount 的状态为 Online
        /// 2. 更新 SrClient 的在线状态和 ConnectionId
        /// 3. 通过 SignalR 通知前端页面
        /// 4. 触发初始化任务（推送好友、群聊等）
        /// </summary>
        public async Task HandleWeChatOnline(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatOnlineNoticeMessage>();
            _logger.LogInformation("微信上线：{WeChatId} ({WeChatNick})", notice.WeChatId, notice.WeChatNick);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo != null && long.TryParse(connectionInfo.userId, out long currentAccountId))
            {
                // 1. 获取当前上下文绑定的账号
                var currentAccount = await _dbContext.GetWechatAccount(currentAccountId);
                WechatAccount targetAccount = currentAccount;

                if (currentAccount != null)
                {
                    // 2. 检查 Wxid 是否发生变更
                    if (currentAccount.wxid != notice.WeChatId && !string.IsNullOrEmpty(currentAccount.wxid))
                    {
                        _logger.LogWarning("账号身份变更检测: 设备原账号 {OldWxid} (ID:{OldId}) -> 新账号 {NewWxid}", 
                            currentAccount.wxid, currentAccount.accountId, notice.WeChatId);

                        // 3. 查找是否已存在目标账号
                        var existingTargetAccount = await _dbContext.WechatAccounts
                            .FirstOrDefaultAsync(w => w.wxid == notice.WeChatId);

                        if (existingTargetAccount != null)
                        {
                             _logger.LogInformation("找到现有目标账号 {Wxid} (ID:{Id})，切换 Session...", existingTargetAccount.wxid, existingTargetAccount.accountId);
                            targetAccount = existingTargetAccount;
                        }
                        else
                        {
                            _logger.LogInformation("目标账号 {Wxid} 不存在，创建新账号...", notice.WeChatId);
                            targetAccount = new WechatAccount
                            {
                                wxid = notice.WeChatId,
                                ownerId = currentAccount.ownerId,
                                clientUuid = currentAccount.clientUuid,
                                createdAt = DateTime.UtcNow
                            };
                            await _dbContext.SaveWechatAccount(targetAccount);
                        }

                        // 4. 处理旧账号状态
                        currentAccount.accountStatus = (short)EnumAccountStatus.Offline;
                        await _dbContext.SaveWechatAccount(currentAccount);

                        // 5. 更新连接映射
                        await _connectionManager.UpdateConnectionUserIdAsync(connectionId, targetAccount.accountId.ToString());
                    }

                    // 6. 更新目标账号信息
                    targetAccount.wxid = notice.WeChatId; 
                    targetAccount.nickname = notice.WeChatNick;
                    targetAccount.accountStatus = (short)EnumAccountStatus.Online;
                    targetAccount.lastOnlineAt = DateTime.UtcNow;
                    if (string.IsNullOrEmpty(targetAccount.ownerId)) targetAccount.ownerId = currentAccount.ownerId;
                    if (string.IsNullOrEmpty(targetAccount.clientUuid)) targetAccount.clientUuid = currentAccount.clientUuid;
                    
                    await _dbContext.SaveWechatAccount(targetAccount);

                    // 7. 强制同步关联的 SrClient
                    if (!string.IsNullOrEmpty(targetAccount.clientUuid))
                    {
                        var client = await _dbContext.GetSrClient(targetAccount.clientUuid);
                        if (client != null)
                        {
                            if (client.wechatAccountId != targetAccount.accountId || client.connectionId != connectionId)
                            {
                                client.isOnline = true;
                                client.updatedAt = DateTime.UtcNow;
                                client.connectionId = connectionId;
                                client.wechatAccountId = targetAccount.accountId;
                                client.weChatId = notice.WeChatId;
                                client.weChatNick = notice.WeChatNick;
                                
                                await _dbContext.SaveSrClient(client);
                                _logger.LogInformation("已更新 SrClient {Uuid} 绑定 -> Account {AccountId} ({Wxid})", client.uuid, targetAccount.accountId, targetAccount.wxid);
                            }
                        }
                    }

                    // 8. 通知与触发
                    await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("WeChatStatusChanged", notice.WeChatId, notice.WeChatNick, true);
                    
                    _logger.LogInformation("触发后台同步: {Wxid} (ID:{Id})", targetAccount.wxid, targetAccount.accountId);
                    _ = _clientTaskService.SendTriggerFriendPushTaskAsync(connectionId, DateTime.UtcNow.Ticks);
                    _ = _clientTaskService.SendTriggerChatRoomPushTaskAsync(connectionId, DateTime.UtcNow.Ticks + 1);
                }
            }

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理微信下线通知 (MsgType: 301)
        /// 1. 更新数据库中 WechatAccount 的状态为 Offline
        /// 2. 更新 SrClient 状态
        /// 3. 通知 SignalR 前端
        /// </summary>
        public async Task HandleWeChatOffline(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatOfflineNoticeMessage>();
            var connectionId = context.Channel.Id.AsLongText();
            string weChatId = notice.WeChatId;

            _logger.LogInformation("微信下线：{WeChatId}, 原因={Reason}, 连接={ConnId}", weChatId, notice.Reason, connectionId);

            if (string.IsNullOrEmpty(weChatId))
            {
                var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
                if (connectionInfo != null && !string.IsNullOrEmpty(connectionInfo.deviceInfo))
                {
                    var client = await _dbContext.GetSrClient(connectionInfo.deviceInfo);

                    if (client != null)
                    {
                        if (string.IsNullOrEmpty(client.weChatId)) weChatId = client.weChatId;

                        if (!string.IsNullOrEmpty(client.weChatId))
                        {
                            if (client.wechatAccountId.HasValue)
                            {
                                var account = await _dbContext.GetWechatAccount(client.wechatAccountId.Value);
                                if (account != null)
                                {
                                    account.accountStatus = (short)EnumAccountStatus.Offline;
                                    await _dbContext.SaveWechatAccount(account);
                                    _logger.LogInformation("已标记账号 {Wxid} 为离线", account.wxid);
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(weChatId))
                        {
                            await _hubContext.Clients.All.SendAsync("WeChatStatusChanged", weChatId, "Unknown", false);
                        }
                    }
                }
            }
            else
            {
                await _hubContext.Clients.All.SendAsync("WeChatStatusChanged", weChatId, "Unknown", false);
            }

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理设备信息上报 (2027)
        /// 1. 解析设备信息并绑定 Uuid
        /// 2. 创建或更新 SrClient 记录
        /// </summary>
        public async Task HandlePostDeviceInfoNotice(TransportMessage message, IChannelHandlerContext context)
        {
            try
            {
                var notice = message.Content.Unpack<PostDeviceInfoNoticeMessage>();
                _logger.LogInformation("上报设备信息：{Brand} {Model}, IMEI={Imei}", notice.PhoneBrand, notice.PhoneModel, notice.IMEI);

                var connectionId = context.Channel.Id.AsLongText();
                var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
                
                if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId))
                {
                    _logger.LogWarning("收到来自未认证或未知连接的设备信息：{ConnectionId}", connectionId);
                    return;
                }

                var account = await _dbContext.GetWechatAccount(accountId);
                if (account == null) return;

                // 1. Determine UUID logic
                string? clientUuid = account.clientUuid;
                bool needToBindAccount = false;

                if (string.IsNullOrEmpty(clientUuid))
                {
                    if (!string.IsNullOrEmpty(account.wechatNumber))
                    {
                        clientUuid = account.wechatNumber;
                        needToBindAccount = true;
                    }
                    else if (!string.IsNullOrEmpty(notice.IMEI))
                    {
                        clientUuid = notice.IMEI;
                        account.wechatNumber = clientUuid; 
                        needToBindAccount = true;
                    }
                }

                if (string.IsNullOrEmpty(clientUuid))
                {
                    _logger.LogWarning("无法确定设备标识(UUID/IMEI)，AccountId：{AccountId}", account.accountId);
                    return;
                }

                // 2. Ensure SrClient Exists
                var client = await _dbContext.GetSrClient(clientUuid);
                if (client == null)
                {
                    client = new SrClient
                    {
                        uuid = clientUuid,
                        createdAt = DateTime.UtcNow,
                        device = notice,
                        tcpHost = "192.168.1.226", // TODO: Config
                        tcpPort = 8647
                    };
                    await _dbContext.SaveSrClient(client);
                    _logger.LogInformation("Created new SrClient {Uuid}", clientUuid);
                }

                // 3. Update Account Binding
                if (needToBindAccount)
                {
                    account.clientUuid = clientUuid;
                    await _dbContext.SaveWechatAccount(account);
                    _logger.LogInformation("Auto-binding Account {Id} to SrClient {Uuid}", account.accountId, clientUuid);
                }

                // 4. Update SrClient Details
                client.device = notice;
                client.ip = context.Channel.RemoteAddress.ToString();
                client.lastLoginAt = DateTime.UtcNow;
                client.isOnline = true;
                client.updatedAt = DateTime.UtcNow;
                client.connectionId = connectionId; 
                
                await _dbContext.SaveSrClient(client);
                _logger.LogInformation("更新 SrClient 信息，UUID：{Uuid}", clientUuid);

                await SendAckAsync(message, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HandlePostDeviceInfoNotice 发生异常");
            }
        }

        /// <summary>
        /// 处理配置推送
        /// 通常用于客户端同步配置信息
        /// </summary>
        public async Task HandleConfigPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
             var notice = message.Content.Unpack<ConfigPushNoticeMessage>();
             _logger.LogInformation("收到配置推送通知");
             // 暂无持久化需求，仅 ACK
             await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理好友检测计数通知 (4.6)
        /// 例如清粉任务进度
        /// </summary>
        public async Task HandlePostFriendDetectCountNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<PostFriendDetectCountNoticeMessage>();
            _logger.LogInformation("好友检测计数通知：{WeChatId} 数量：{Count}", notice.WeChatId, notice.Count);
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 发送通用 ACK 回执
        /// </summary>
        private async Task SendAckAsync(TransportMessage message, IChannelHandlerContext context)
        {
             var response = new TransportMessage
             {
                 Id = 0,
                 MsgType = EnumMsgType.MsgReceivedAck,
                 RefMessageId = message.Id
             };
             
             await context.WriteAndFlushAsync(response);
        }
    }
}
