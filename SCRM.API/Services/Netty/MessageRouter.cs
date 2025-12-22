using SCRM.SHARED.Proto;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Google.Protobuf;
using SCRM.Shared.Core;
using SCRM.Services;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;
using SCRM.Services.Events;
using SCRM.API.Services.Data;
using SCRM.API.Models.Entities;
using SCRM.API.Models.Events;

namespace SCRM.Services.Netty
{

    /// <summary>
    /// 消息路由服务
    /// 负责接收从 NettyMessageHandler 传来的 TransportMessage，并将其分发到对应的 Handle 方法。
    /// 包含大部分业务逻辑的处理入口。
    /// </summary>
    public class MessageRouter
    {
        private readonly Microsoft.Extensions.Logging.ILogger<MessageRouter> _logger;
        
        /// <summary>
        /// 连接管理器，用于追踪客户端连接状态
        /// </summary>
        private readonly ConnectionManager _connectionManager;

        /// <summary>
        /// 作用域工厂，用于在异步处理中创建临时的 DBContext
        /// </summary>
        private readonly IServiceScopeFactory _scopeFactory;

        /// <summary>
        /// SignalR 上下文，用于向 Web 前端发送实时通知
        /// </summary>
        private readonly IHubContext<SCRM.API.Hubs.ClientHub> _hubContext;

        /// <summary>
        /// 任务服务，用于管理发给客户端的任务（如发消息、发朋友圈）的状态
        /// </summary>
        private readonly ClientTaskService _clientTaskService;

        /// <summary>
        /// 事件总线，用于解耦消息接收与后续业务处理（如自动回复）
        /// </summary>
        private readonly IEventBus _eventBus;

        public MessageRouter(ConnectionManager connectionManager, IServiceScopeFactory scopeFactory, IHubContext<SCRM.API.Hubs.ClientHub> hubContext, ClientTaskService clientTaskService, IEventBus eventBus, Microsoft.Extensions.Logging.ILogger<MessageRouter> logger)
        {
            _connectionManager = connectionManager;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _clientTaskService = clientTaskService;
            _eventBus = eventBus;
            _logger = logger;
        }

        public async Task RouteMessage(TransportMessage message, IChannelHandlerContext context)
        {
            _logger.LogInformation("Routing message: Id={Id}, MsgType={MsgType} ({MsgTypeId}), Token={Token}", 
                message.Id, message.MsgType, (int)message.MsgType, message.AccessToken);

            if (message.Content != null)
            {
                _logger.LogInformation("Incoming Content TypeUrl: {TypeUrl}", message.Content.TypeUrl);
            }

            try
            {
                switch (message.MsgType)
                {
                    case EnumMsgType.HeartBeatReq:
                        // 1. 客户端发送的心跳包
                        await HandleHeartBeat(message, context);
                        break;
                    case EnumMsgType.DeviceAuthReq:
                        // 2.1 设备(手机客户端、客服客户端)获取通信token请求
                        await HandleDeviceAuth(message, context);
                        break;
                    case EnumMsgType.PostDeviceInfoNotice:
                        // 上报设备信息（IMEI、系统版本等）
                        await HandlePostDeviceInfoNotice(message, context);
                        break;
                    case EnumMsgType.WeChatOnlineNotice:
                        // 3.1 手机客户端微信上线通知
                        await HandleWeChatOnline(message, context);
                        break;
                    case EnumMsgType.WeChatOfflineNotice:
                        // 3.2 手机客户端微信下线通知
                        await HandleWeChatOffline(message, context);
                        break;
                    case EnumMsgType.FriendAddNotice:
                        // 3.3 微信个人号新增好友通知（已废弃/旧版？）
                        // 注意：协议文档中3.3标题是新增好友通知，但实际可能使用 FriendPushNotice
                        break; 
                    case EnumMsgType.FriendChangeNotice:
                        // 3.3.1 好友变更通知
                        await HandleFriendChangeNotice(message, context);
                        break;
                    case EnumMsgType.FriendDelNotice: 
                        // 3.4 微信个人号移除好友通知
                        await HandleFriendDelNotice(message, context);
                        break;
                    case EnumMsgType.FriendTalkNotice:
                        // 3.5 微信好友发来聊天消息通知
                        await HandleFriendTalkNotice(message, context);
                        break;
                    case EnumMsgType.ChatroomPushNotice:
                        // 3.13 群聊新增通知 / 3.29 群聊列表任务推送
                        // 注意：这里可能是群列表推送
                        await HandleChatRoomPushNotice(message, context);
                        break;
                    case EnumMsgType.ChatRoomMembersNotice:
                        // 群成员变更/列表通知
                        await HandleChatRoomMembersNotice(message, context);
                        break;
                    case EnumMsgType.CircleDetailNotice:
                        // 3.26 获取朋友圈的图片/详情返回
                        await HandleCircleDetailNotice(message, context);
                        break;
                    case EnumMsgType.PostSnsnewsTaskResultNotice:
                        // 4.2 发送朋友圈任务及结果返回
                        await HandlePostSNSNewsTaskResultNotice(message, context);
                        break;
                    case EnumMsgType.TaskResultNotice:
                        // 1.4 通用任务执行结果通知
                        await HandleTaskResultNotice(message, context);
                        break;
                    case EnumMsgType.TalkToFriendTaskResultNotice:
                        // 4.1 给好友发消息任务结果返回
                        await HandleTalkToFriendTaskResult(message, context);
                        break;
                    case EnumMsgType.ScreenShotTaskResultNotice:
                        // 截屏任务结果
                        await HandleScreenShotTaskResultNotice(message, context);
                        break;
                    case EnumMsgType.QueryHbDetailTaskResultNotice:
                        // 4.25 领取红包或转账收钱任务及返回 - 详情
                        await HandleQueryHbDetailTaskResultNotice(message, context);
                        break;
                    case EnumMsgType.QueryHbStatusTaskResultNotice:
                        // 4.25 领取红包详情状态
                        await HandleQueryHbStatusTaskResultNotice(message, context);
                        break;
                    case EnumMsgType.FriendPushNotice:
                        // 3.3 微信个人号新增好友通知 (推送完整好友信息)
                        await HandleFriendPushNotice(message, context);
                        break;
                    case EnumMsgType.ConfigPushNotice:
                        // 推送配置信息
                        await HandleConfigPushNotice(message, context);
                        break;
                    case EnumMsgType.FriendAddReqeustNotice:
                        // 3.7 有好友请求添加好友的通知
                        await HandleFriendAddReqeustNotice(message, context);
                        break;
                    case EnumMsgType.CircleNewPublishNotice:
                        // 3.8 手机上发送了朋友圈通知
                        await HandleCircleNewPublishNotice(message, context);
                        break;
                    case EnumMsgType.PostFriendDetectCountNotice:
                        // 上报好友检测计数
                        await HandlePostFriendDetectCountNotice(message, context);
                        break;
                    case EnumMsgType.CirclePushNotice:
                        // 3.13 手机触发的推送朋友圈列表
                        await HandleCirclePushNotice(message, context);
                        break;
                    case EnumMsgType.OneKeyLikeTaskResultNotice:
                        // 4.22 朋友圈点赞任务及结果返回 (原 PostMomentsPraiseCountNotice)
                        await HandleOneKeyLikeTaskResultNotice(message, context);
                        break;
                    case EnumMsgType.ContactLabelInfoNotice:
                        // 3.16 联系人标签新增，修改通知 / 列表
                        await HandleContactLabelInfoNotice(message, context);
                        break;
                    case EnumMsgType.HistoryMsgPushNotice:
                        // 4.20 通知手机推送历史聊天记录任务及返回
                        await HandleHistoryMsgPushNotice(message, context);
                        break;
                    case EnumMsgType.ConversationPushNotice:
                        // 4.47 获取会话列表任务及返回
                        await HandleConversationPushNotice(message, context);
                        break;
                    case EnumMsgType.FriendAddReqListNotice:
                        // 4.49 获取加好友请求列表任务及返回结果
                        await HandleFriendAddReqListNotice(message, context);
                        break;
                    case EnumMsgType.BizContactPushNotice:
                        // 4.50 获取公众号列表任务及结果返回
                        await HandleBizContactPushNotice(message, context);
                        break;
                    case EnumMsgType.BizContactAddNotice:
                        // 3.18 新增公众号通知
                        await HandleBizContactAddNotice(message, context);
                        break;
                    default:
                        _logger.LogWarning("Unhandled message type: {MsgType}", message.MsgType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error routing message type: {MsgType}", message.MsgType);
            }
        }

        /// <summary>
        /// 处理心跳包
        /// 1. 检查连接是否已认证
        /// 2. 如果未认证，发送强制下线通知
        /// 3. 如果已认证，更新活动时间并回复 MsgReceivedAck (1002)
        /// </summary>
        private async Task HandleHeartBeat(TransportMessage message, IChannelHandlerContext context)
        {
            var connId = context.Channel.Id.AsLongText();
            _logger.LogInformation("HeartBeat received from {RemoteAddress}, ConnectionId: {ConnectionId}", context.Channel.RemoteAddress, connId);
            
            // 检查连接是否已认证（是否映射到用户）
            bool isAuthenticated = await _connectionManager.IsConnectionAuthenticatedAsync(connId);
            _logger.LogInformation("Auth check for {ConnectionId}: {IsAuthenticated}", connId, isAuthenticated);

            if (!isAuthenticated)
            {
                _logger.LogWarning("HeartBeat from unauthenticated connection {ConnectionId}. Sending ForceOffline.", connId);
                
                // 连接丢失（例如服务器重启），强制客户端重新认证
                var offlineNotice = new AccountForceOfflineNoticeMessage
                {
                    Reason = EnumForceOfflineReason.NoReason,
                    Message = "Session expired, please re-login"
                };

                var forceOfflineMsg = new TransportMessage
                {
                    Id = 0,
                    MsgType = EnumMsgType.AccountForceOfflineNotice,
                    RefMessageId = message.Id,
                    Content = Any.Pack(offlineNotice)
                };
                
                await context.WriteAndFlushAsync(forceOfflineMsg);
                return;
            }

            // 仅在已认证时更新活动状态
            await _connectionManager.UpdateConnectionActivityAsync(context.Channel.Id.AsLongText());

            // 发送 ACK
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理设备认证请求 (2.1)
        /// 1. 根据 Credential (UUID 或 IMEI) 查找 WechatAccount
        /// 2. 验证账号状态（VIP等）
        /// 3. 注册连接 (ConnectionManager)
        /// 4. 返回 DeviceAuthRsp (1011)
        /// 5. 触发初始化任务（推送好友、群聊、配置等）
        /// </summary>
        private async Task HandleDeviceAuth(TransportMessage message, IChannelHandlerContext context)
        {
            var authReq = message.Content.Unpack<DeviceAuthReqMessage>();
            string credential = authReq.Credential; 
            
            _logger.LogInformation("Device auth request received. Credential: {Credential}", credential);

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                WechatAccount account = null;

                // 1. 尝试通过 UUID 识别 (UUID 长度通常为 36 或 32)
                if (credential.Length > 20) 
                {
                    account = await dbContext.WechatAccounts
                        .FirstOrDefaultAsync(u => u.ClientUuid == credential && !u.IsDeleted);
                }

                // 2. 降级：通过 IMEI 识别
                if (account == null)
                {
                    account = await dbContext.WechatAccounts
                        .FirstOrDefaultAsync(u => u.WechatNumber == credential && !u.IsDeleted);
                }

                if (account == null)
                {
                    _logger.LogWarning("Device authentication failed: Device not registered. Credential: {Credential}", credential);
                    return;
                }

                if (!account.IsVip)
                {
                    // 开发环境自动续期 VIP
                    _logger.LogWarning("Device VIP expired. Auto-renewing for 1 year. Credential: {Credential}, Old Expiry: {Expiry}", credential, account.VipExpiryDate);
                    account.VipExpiryDate = DateTime.UtcNow.AddYears(1);
                    await dbContext.SaveWechatAccount(account); // 原子保存
                }

                _logger.LogInformation("Device authenticated successfully. AccountId: {AccountId}, Nickname: {Nickname}", account.AccountId, account.Nickname);

                // 注册连接信息
                string userId = account.AccountId.ToString();
                string deviceType = "Android"; 
                
                // 如果 WechatAccount 中有 ClientUuid 则用作 deviceId，否则使用 WechatNumber (IMEI)
                string deviceId = !string.IsNullOrEmpty(account.ClientUuid) ? account.ClientUuid : account.WechatNumber;

                await _connectionManager.AddConnectionAsync(userId, context.Channel.Id.AsLongText(), deviceType, deviceId);

                // 发布设备已连接事件 (携带 OwnerId 以便推送)
                await _eventBus.PublishAsync(new DeviceConnectedEvent(userId, context.Channel.Id.AsLongText(), deviceType, account.OwnerId));

                var responseContent = new DeviceAuthRspMessage
                {
                    AccessToken = Guid.NewGuid().ToString("N"), // 生成会话 Token
                    Extra = new DeviceAuthRspMessage.Types.ExtraMessage
                    {
                        SupplierId = account.AccountId, // 使用 AccountId 作为 SupplierId
                        UnionId = account.AccountId,
                        AccountType = EnumAccountType.Main,
                        SupplierName = "SCRM",
                        NickName = account.Nickname ?? "Unknown",
                        Token = account.Wxid // 将 Wxid 放入 Token 字段，以便客户端将其用作 c2cServerAddress
                    }
                };

                var response = new TransportMessage
                {
                    Id = 0,
                    MsgType = EnumMsgType.DeviceAuthRsp,
                    RefMessageId = message.Id,
                    Content = Any.Pack(responseContent)
                };

                await context.WriteAndFlushAsync(response);

                // --- 触发初始化任务 ---
                _logger.LogInformation("Triggering initialization tasks for AccountId: {AccountId}", account.AccountId);

                try
                {
                    var connId = context.Channel.Id.AsLongText();
                    // 触发推送好友列表
                    long friendTaskId = DateTime.UtcNow.Ticks;
                    await _clientTaskService.SendTriggerFriendPushTaskAsync(connId, friendTaskId);

                    // 触发推送群聊列表
                    long chatRoomTaskId = DateTime.UtcNow.Ticks + 1; // 确保 ID 唯一
                    await _clientTaskService.SendTriggerChatRoomPushTaskAsync(connId, chatRoomTaskId);
                    
                    // --- 触发配置发现 ---
                    long configTaskId = DateTime.UtcNow.Ticks + 2;
                    await _clientTaskService.SendTriggerConfigPushTaskAsync(connId, configTaskId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error triggering initialization tasks for AccountId: {AccountId}", account.AccountId);
                }

                // --- 修复：将 ConnectionId 持久化到数据库，以便 api/device 返回正确的 ID ---
                if (!string.IsNullOrEmpty(account.ClientUuid))
                {
                    var client = await dbContext.GetSrClient(account.ClientUuid); // 原子获取
                    if (client != null)
                    {
                        client.isOnline = true;
                        client.lastLoginAt = DateTime.UtcNow;
                        client.updatedAt = DateTime.UtcNow;
                        client.ConnectionId = context.Channel.Id.AsLongText(); // 更新 ConnectionId
                        
                        await dbContext.SaveSrClient(client); // 原子保存
                        _logger.LogInformation("Updated SrClient {Uuid} with new ConnectionId: {ConnectionId}", client.uuid, client.ConnectionId);
                    }
                }
            }
        }

        /// <summary>
        /// 处理给好友发消息任务结果返回 (4.1)
        /// 告诉服务端消息是否发送成功
        /// </summary>
        private async Task HandleTalkToFriendTaskResult(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<TalkToFriendTaskResultNoticeMessage>();
            _logger.LogInformation("TalkToFriend task result: Success={Success}, Code={Code}, Msg={ErrMsg}", 
                result.Success, result.Code, result.ErrMsg);
            
             // 标记任务完成
             long correlationId = result.MsgId;
             if (correlationId == 0 && message.RefMessageId != 0) correlationId = message.RefMessageId; // Fallback to Transport ID
             _clientTaskService.CompleteTask(correlationId, result.Success, result.ErrMsg);

            // Publish Event for UI
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            await _eventBus.PublishAsync(new TaskResultReceivedEvent(correlationId, result.Success, result.ErrMsg, connectionId, connectionInfo?.DeviceInfo ?? connectionId));

            // 发送 ACK
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理微信上线通知 (3.1)
        /// 1. 更新数据库中 WechatAccount 的状态为 Online
        /// 2. 更新 SrClient 的在线状态和 ConnectionId
        /// 3. 是通过 SignalR 通知前端页面
        /// </summary>
        private async Task HandleWeChatOnline(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatOnlineNoticeMessage>();
            _logger.LogInformation("WeChat Online: {WeChatId} ({WeChatNick})", notice.WeChatId, notice.WeChatNick);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo != null && long.TryParse(connectionInfo.UserId, out long accountId))
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                    // 原子获取账号信息
                    var account = await dbContext.GetWechatAccount(accountId);
                    if (account != null)
                    {
                        account.Wxid = notice.WeChatId;
                        account.Nickname = notice.WeChatNick;
                        account.AccountStatus = (short)EnumAccountStatus.Online;
                        account.LastOnlineAt = DateTime.UtcNow;
                        
                        await dbContext.SaveWechatAccount(account); // 原子保存

                        // 关键修复：主动同步关联的 SrClient，确保其 WechatAccountId 正确绑定
                        if (!string.IsNullOrEmpty(account.ClientUuid))
                        {
                            var client = await dbContext.GetSrClient(account.ClientUuid); // 原子获取
                            if (client != null)
                            {
                                client.isOnline = true;
                                client.updatedAt = DateTime.UtcNow;
                                client.ConnectionId = connectionId; // 更新 ConnectionId
                                client.WechatAccountId = account.AccountId; // 强制绑定当前在线账号
                                client.WeChatNick = account.Nickname; // 同步昵称
                                
                                await dbContext.SaveSrClient(client); // 原子保存
                                _logger.LogInformation("Proactively bound SrClient {Uuid} to WechatAccount {AccountId}", client.uuid, account.AccountId);
                            }
                        }

                        // 通知 Web 端页面状态变更
                        await _hubContext.Clients.Group(connectionInfo.DeviceInfo ?? connectionId).SendAsync("WeChatStatusChanged", notice.WeChatId, notice.WeChatNick, true);
                        
                        // 主动触发全量同步指令
                        _logger.LogInformation("Proactively triggering background sync for {WeChatId}", notice.WeChatId);
                        _ = _clientTaskService.SendTriggerFriendPushTaskAsync(connectionId, DateTime.UtcNow.Ticks);
                        _ = _clientTaskService.SendTriggerChatRoomPushTaskAsync(connectionId, DateTime.UtcNow.Ticks + 1);
                    }
                }
            }

            // 发送 ACK
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理微信下线通知 (3.2)
        /// </summary>
        private async Task HandleWeChatOffline(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatOfflineNoticeMessage>();
            _logger.LogInformation("WeChat Offline: {WeChatId}, Reason={Reason}", notice.WeChatId, notice.Reason);
            // Send ACK
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理好友发来聊天消息通知 (3.5)
        /// 1. 解析消息内容
        /// 2. 分发 MessageReceivedEvent，供前端显示
        /// 3. 分发 AutomationMessageEvent，供自动回复服务处理
        /// 4. 将消息持久化到数据库
        /// </summary>
        private async Task HandleFriendTalkNotice(TransportMessage message, IChannelHandlerContext context)
        {
            FriendTalkNoticeMessage notice = message.Content.Unpack<FriendTalkNoticeMessage>();
            string contentUtf8 = notice.Content.ToStringUtf8();
            
            _logger.LogInformation("Friend Talk Notice: {WeChatId} 收到来自 {FriendId} 的消息: {Content}", 
                notice.WeChatId, notice.FriendId, contentUtf8);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo == null) return;

            // --- 核心逻辑修复: IsSelf, ChatType, Direction ---
            
            // 判定是否为“我”发送的消息 (同步消息)
            // 1. FriendId 与 WeChatId 一直说明是同步自发消息
            // 2. XML 内容中可能包含 <is_self>1</is_self>
            bool isSelf = notice.FriendId == notice.WeChatId || contentUtf8.Contains("<is_self>1</is_self>");
            
            // 判定是否为群聊
            bool isGroup = notice.FriendId.EndsWith("@chatroom");
            
            // 尝试从 XML 中提取群聊真实发送者 (如果是群聊的话)
            string senderWxid = notice.FriendId;
            if (isGroup && !isSelf)
            {
                // 简单的 XML 提取逻辑 (通常在 <from_nickname> 附近或特定标签)
                // 暂时保留 notice.FriendId 作为会话 ID，后续可根据需要解析实际发言人
            }

            // 方向判定：1=发送, 2=接收
            int direction = isSelf ? 1 : 2;
            
            // 归属账号ID
            if (!long.TryParse(connectionInfo.UserId, out long acctId)) return;

            // --- 消息去重 & 持久化 ---
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                // 1. 基于 MsgSvrId 的幂等检查
                var existingMsg = await dbContext.Messages.FirstOrDefaultAsync(m => m.MsgSvrId == notice.MsgSvrId && m.AccountId == acctId);
                
                if (existingMsg == null)
                {
                    var msg = new Message
                    {
                        AccountId = acctId,
                        SenderWxid = isSelf ? notice.WeChatId : notice.FriendId,
                        ReceiverWxid = isSelf ? notice.FriendId : notice.WeChatId,
                        Content = contentUtf8,
                        ChatType = (short)(isGroup ? 2 : 1), // 1=单聊, 2=群聊
                        MessageType = (short)notice.ContentType,
                        Direction = (short)direction,
                        SendStatus = 3, // 已送达/同步
                        ReadStatus = isSelf ? (short)1 : (short)0, // 自己发的设为已读
                        MsgSvrId = notice.MsgSvrId,
                        SentAt = DateTime.UtcNow,
                        ReceivedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    dbContext.Messages.Add(msg);
                    await dbContext.SaveChangesAsync();
                }
                else
                {
                     _logger.LogDebug("Duplicate message detected, skipping DB save. MsgSvrId: {MsgSvrId}", notice.MsgSvrId);
                }
            }

            // --- 发送给 UI 前端 (SignalR) ---
            var deviceUuid = connectionInfo.DeviceInfo ?? connectionId;
            var msgDto = new 
            { 
                FriendId = notice.FriendId, // 会话窗口 ID
                Content = contentUtf8, 
                IsSelf = isSelf,
                MsgSvrId = notice.MsgSvrId,
                IsGroup = isGroup,
                TaskId = notice.MsgId // 透传任务关联 ID (即 MsgId)
            };
            
            await _hubContext.Clients.Group(deviceUuid).SendAsync("ReceiveMessage", msgDto);
            
            // --- 自动化逻辑转发 ---
            try 
            {
                 var automationEvent = new AutomationMessageEvent(
                     acctId, 
                     connectionId, 
                     notice.WeChatId, 
                     notice.FriendId, 
                     contentUtf8, 
                     (int)notice.ContentType
                 );
                 _eventBus.PublishAsync(automationEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing automation event");
            }

            // 发送 ACK
            await SendAckAsync(message, context);
        }

        private string ExtractXmlValue(string xml, string tagName)
        {
            try {
                // simple parser
                string startTag = $"<{tagName}>";
                string endTag = $"</{tagName}>";
                int start = xml.IndexOf(startTag);
                if (start == -1) 
                {
                    // Try CDATA
                    startTag = $"<{tagName}><![CDATA[";
                    endTag = $"]]></{tagName}>";
                    start = xml.IndexOf(startTag);
                }

                if (start != -1)
                {
                    start += startTag.Length;
                    int end = xml.IndexOf(endTag, start);
                    if (end != -1)
                    {
                        return xml.Substring(start, end - start);
                    }
                }
            } catch {}
            return string.Empty;
        }

        /// <summary>
        /// 处理手机上回复好友的聊天消息通知 (3.6)
        /// 1. 推送到 SignalR 前端
        /// 2. 持久化到数据库 (Direction = 1 发送)
        /// </summary>
        private async Task HandleWeChatTalkToFriendNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatTalkToFriendNoticeMessage>();
            _logger.LogInformation("WeChat Talk To Friend Notice: {WeChatId} sent message to {FriendId}: {Content}", 
                notice.WeChatId, notice.FriendId, notice.Content.ToStringUtf8());

            // Push to SignalR Group (UUID)
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo != null)
            {
                await _hubContext.Clients.Group(connectionInfo.DeviceInfo ?? connectionId).SendAsync("ReceiveMessage", notice.FriendId, notice.Content.ToStringUtf8(), true); 
            } 

            // Persist to DB
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                // connectionId and connectionInfo are available from outer scope
                
                if (connectionInfo != null && long.TryParse(connectionInfo.UserId, out long accountId))
                {
                    var msg = new Message
                    {
                        AccountId = accountId,
                        SenderWxid = notice.WeChatId, // 当前账号
                        ReceiverWxid = notice.FriendId,
                        Content = notice.Content.ToStringUtf8(),
                        ChatType = 1, // 单聊
                        MessageType = (short)notice.ContentType,
                        Direction = 1, // 发送
                        SendStatus = 2, // 已发送
                        ReadStatus = 1, // 已读 (自己发的)
                        MsgSvrId = notice.MsgId,
                        SentAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    dbContext.Messages.Add(msg);
                    await dbContext.SaveChangesAsync();
                }
            } 
            // 发送 ACK
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理新增好友通知 (3.3 - 可能是旧版)
        /// 保存好友信息到数据库
        /// </summary>
        private async Task HandleFriendAddNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendAddNoticeMessage>();
            if (notice.Friend != null)
            {
                _logger.LogInformation("Friend Add Notice: {WeChatId} added friend {FriendNick}", 
                    notice.WeChatId, notice.Friend.FriendNick);

                var connectionId = context.Channel.Id.AsLongText();
                // We need to wait for the async context to persist
                // Since this method returns Task, we can make it async
                _ = SaveAddedFriendAsync(connectionId, notice.Friend, notice.WeChatId, message, context);
            }
            return;
        }

        private async Task SaveAddedFriendAsync(string connectionId, FriendMessage friend, string weChatId, TransportMessage message, IChannelHandlerContext context)
        {
            try
            {
                var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
                if (connectionInfo == null || !long.TryParse(connectionInfo.UserId, out long accountId)) return;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                    
                    var contact = new Contact
                    {
                        WechatAccountId = (int)accountId,
                        Wxid = friend.FriendId,
                        Nickname = friend.FriendNick ?? "",
                        Remarks = friend.Memo ?? "",
                        Avatar = friend.Avatar ?? "",
                        Gender = (int)friend.Gender,
                        Province = friend.Province ?? "",
                        City = friend.City ?? "",
                        Phone = friend.Phone ?? "",
                        Signature = friend.Desc ?? "",
                        Email = "",
                        Country = "",
                        ContactType = 0,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await dbContext.SaveContacts(accountId, new List<Contact> { contact });
                    
                    // Notify UI
                    await _hubContext.Clients.Group(connectionInfo.DeviceInfo ?? connectionId).SendAsync("ContactsUpdated", accountId);

                    // 发送 ACK
                    await SendAckAsync(message, context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving added friend {FriendId}", friend.FriendId);
            }
        }

        /// <summary>
        /// 处理群聊列表推送通知 (3.29/3.13)
        /// 1. 同步群组信息到数据库 (Group 表)
        /// 2. 通知 SignalR 前端刷新
        /// </summary>
        private async Task HandleChatRoomPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ChatRoomPushNoticeMessage>();
            _logger.LogInformation("Chat Room Push Notice: {WeChatId} pushed {Count} chat rooms (Page {Page}/{Size})", 
                notice.WeChatId, notice.ChatRooms.Count, notice.Page, notice.Size);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo == null || !long.TryParse(connectionInfo.UserId, out long accountId))
            {
                _logger.LogWarning("Received ChatRoom Push from unauthenticated connection: {ConnectionId}", connectionId);
                return;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                int newCount = 0;
                int updateCount = 0;

                foreach (var room in notice.ChatRooms)
                {
                    // 1. 同步群组基本信息
                    var group = await dbContext.Groups
                        .FirstOrDefaultAsync(g => g.WechatAccountId == accountId && g.GroupWxid == room.UserName);

                    if (group == null)
                    {
                        group = new Group
                        {
                            WechatAccountId = (int)accountId,
                            GroupWxid = room.UserName,
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = false
                        };
                        dbContext.Groups.Add(group);
                        newCount++;
                    }
                    else
                    {
                        updateCount++;
                    }

                    // 更新字段
                    group.GroupName = room.NickName ?? "";
                    group.OwnerWxid = room.Owner ?? "";
                    group.GroupNotice = room.Notice ?? "";
                    group.GroupAvatar = room.Avatar ?? "";
                    group.MemberCount = room.MemberList.Count; // 使用 MemberList 数量作为近似值
                    group.UpdatedAt = DateTime.UtcNow;

                    // 2. 简单的成员同步 (如果需要)
                    // 注意：这里暂不处理 ShowNameList 的全量 Member 同步，避免性能问题
                    // 将主要依赖专门的 Member 同步消息或按需获取
                }

                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Saved {New} new and {Update} updated chat rooms for AccountId: {AccountId}", newCount, updateCount, accountId);
                
                // 通知 Web 端刷新
                await _hubContext.Clients.Group(connectionInfo.DeviceInfo ?? connectionId).SendAsync("ChatRoomsUpdated", accountId);

                // 发送 ACK
                await SendAckAsync(message, context);
            }
        }

        /// <summary>
        /// 处理群成员列表通知
        /// </summary>
        private async Task HandleChatRoomMembersNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ChatRoomMembersNoticeMessage>();
            _logger.LogInformation("Chat Room Members Notice: {WeChatId}, Members={Count}", notice.WeChatId, notice.Members.Count);
            
            // 待实现具体的成员同步逻辑
            // 需要明确 ChatRoomMembersNoticeMessage 是否包含 ChatRoomId 上下文
            // 目前暂做日志记录

            // Send ACK
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理好友检测统计通知
        /// 例如清粉任务的进度回报
        /// </summary>
        private async Task HandlePostFriendDetectCountNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<PostFriendDetectCountNoticeMessage>();
            _logger.LogInformation("Friend Detect Count Notice: {WeChatId} count: {Count}", notice.WeChatId, notice.Count);
            
            // Send ACK
            await SendAckAsync(message, context);
        }



        /// <summary>
        /// 处理一键点赞任务结果通知 (4.22)
        /// </summary>
        private async Task HandleOneKeyLikeTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<OneKeyLikeTaskResultNoticeMessage>();
            _logger.LogInformation("OneKeyLike Task Result: TaskId={TaskId}, Count={Count}, EndType={EndType}", result.TaskId, result.Count, result.EndType);
            _clientTaskService.CompleteTask(result.TaskId, true, $"Count: {result.Count}, EndType: {result.EndType}");
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理联系人标签通知 (3.16)
        /// 此处仅接收并记录日志
        /// </summary>
        private async Task HandleContactLabelInfoNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ContactLabelInfoNoticeMessage>();
            _logger.LogInformation("收到联系人标签通知: WeChatId={WeChatId}, LabelCount={LabelCount}", notice.WeChatId, notice.Labels.Count);
            await SendMsgReceivedAck(context, message);
        }

        /// <summary>
        /// 处理历史聊天记录推送 (4.20 结果)
        /// 此处仅接收并记录日志
        /// </summary>
        private async Task HandleHistoryMsgPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<HistoryMsgPushNoticeMessage>();
            _logger.LogInformation("收到历史消息推送: WeChatId={WeChatId}, MsgCount={MsgCount}", notice.WeChatId, notice.Messages.Count);
            await SendMsgReceivedAck(context, message);
        }

        /// <summary>
        /// 处理会话列表推送 (4.47 结果)
        /// 此处仅接收并记录日志
        /// </summary>
        private async Task HandleConversationPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ConversationPushNoticeMessage>();
            // Fix: 'Conversations' -> 'Convers' based on proto generated code
            _logger.LogInformation("收到会话列表推送: WeChatId={WeChatId}, ConversCount={ConversCount}", notice.WeChatId, notice.Convers.Count);
            await SendMsgReceivedAck(context, message);
        }

        /// <summary>
        /// 处理好友添加请求列表推送 (4.49 结果)
        /// 此处仅接收并记录日志
        /// </summary>
        private async Task HandleFriendAddReqListNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendAddReqListNoticeMessage>();
            _logger.LogInformation("收到好友请求列表通知: WeChatId={WeChatId}, RequestCount={RequestCount}", notice.WeChatId, notice.Requests.Count);
            await SendMsgReceivedAck(context, message);
        }

        /// <summary>
        /// 处理企业微信联系人推送 (4.50 结果)
        /// 此处仅接收并记录日志
        /// </summary>
        private async Task HandleBizContactPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<BizContactPushNoticeMessage>();
            _logger.LogInformation("收到企业微信联系人推送: WeChatId={WeChatId}, ContactCount={ContactCount}", notice.WeChatId, notice.Contacts.Count);
            await SendMsgReceivedAck(context, message);
        }

        /// <summary>
        /// 处理新增公众号/企业微信联系人通知 (3.18)
        /// 此处仅接收并记录日志
        /// </summary>
        private async Task HandleBizContactAddNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<BizContactAddNoticeMessage>();
            // Fix: 'UserName' -> 'Username' based on proto generated code (lowercase 'n')
            _logger.LogInformation("收到企业微信添加通知: WeChatId={WeChatId}, AddedUser={AddedUser}", notice.WeChatId, notice.Contact?.Username ?? "Unknown");
            await SendMsgReceivedAck(context, message);
        }


        /// <summary>
        /// 处理朋友圈详情通知 (3.26 结果)
        /// 1. 保存朋友圈内容到 MomentsPost
        /// 2. 保存图片/视频链接
        /// 3. 保存点赞和评论
        /// 4. 通知前端显示
        /// </summary>
        private async Task HandleCircleDetailNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<CircleDetailNoticeMessage>();
            if (notice.Circle == null) return;

            var circle = notice.Circle;
            _logger.LogInformation("Circle Detail Notice: {WeChatId} - {CircleId} (Author: {Author})", 
                notice.WeChatId, circle.CircleId, circle.WeChatId);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo == null || !long.TryParse(connectionInfo.UserId, out long accountId))
            {
                return;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();

                // 1. 转换时间
                var publishTime = DateTimeOffset.FromUnixTimeSeconds(circle.PublishTime).UtcDateTime;

                // 2. 查找或创建 MomentsPost (由于缺少 SnsId 字段，使用 Author + Time 判定)
                var post = await dbContext.MomentsPosts
                    .FirstOrDefaultAsync(p => p.WechatAccountId == accountId && p.AuthorWxid == circle.WeChatId && p.PublishTime == publishTime);

                if (post == null)
                {
                    post = new MomentsPost
                    {
                        WechatAccountId = (int)accountId,
                        AuthorWxid = circle.WeChatId,
                        PublishTime = publishTime,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };
                    dbContext.MomentsPosts.Add(post);
                }

                // 3. 更新内容字段
                if (circle.Content != null)
                {
                    post.PostContent = circle.Content.Text ?? "";
                    
                    // 处理封面图 (取第一张图片或视频缩略图)
                    if (circle.Content.Images != null && circle.Content.Images.Count > 0)
                    {
                        post.PostCover = circle.Content.Images[0].ThumbImg ?? circle.Content.Images[0].Url ?? "";
                    }
                    else if (circle.Content.Video != null)
                    {
                        post.PostCover = circle.Content.Video.ThumbImg ?? "";
                    }
                }
                
                post.LikeCount = circle.Likes.Count;
                post.CommentCount = circle.Comments.Count;
                post.UpdatedAt = DateTime.UtcNow;

                // 先保存以获取 Post.Id
                await dbContext.SaveChangesAsync();

                // 4. 同步点赞 (先删后加，简化逻辑)
                var existingLikes = await dbContext.MomentsLikes.Where(l => l.PostId == post.Id).ToListAsync();
                dbContext.MomentsLikes.RemoveRange(existingLikes);

                foreach (var likeProto in circle.Likes)
                {
                    dbContext.MomentsLikes.Add(new MomentsLike
                    {
                        PostId = post.Id,
                        LikerWxid = likeProto.FriendId,
                        LikerNickname = likeProto.NickName ?? "",
                        LikeTime = DateTimeOffset.FromUnixTimeSeconds(likeProto.PublishTime).UtcDateTime,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // 5. 同步评论 (先删后加)
                var existingComments = await dbContext.MomentsComments.Where(c => c.PostId == post.Id).ToListAsync();
                dbContext.MomentsComments.RemoveRange(existingComments);

                foreach (var cmtProto in circle.Comments)
                {
                    dbContext.MomentsComments.Add(new MomentsComment
                    {
                        PostId = post.Id,
                        CommenterWxid = cmtProto.FromWeChatId,
                        CommentContent = cmtProto.Content ?? "",
                        ReplyToWxid = cmtProto.ToWeChatId ?? "",
                        CommentTime = DateTimeOffset.FromUnixTimeSeconds(cmtProto.PublishTime).UtcDateTime,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await dbContext.SaveChangesAsync();
                
                // 6. 前端通知
                await _hubContext.Clients.Group(connectionInfo.DeviceInfo ?? connectionId).SendAsync("MomentReceived", post.AuthorWxid, post.PostContent);
                await _hubContext.Clients.Group(connectionInfo.DeviceInfo ?? connectionId).SendAsync("CircleDetailUpdated", notice.WeChatId, circle.CircleId);
            }

            // 发送 ACK
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理发送朋友圈任务结果 (4.2)
        /// </summary>
        private async Task HandlePostSNSNewsTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<PostSNSNewsTaskResultNoticeMessage>();
            _logger.LogInformation("Post SNS News Result: Success={Success}, TaskId={TaskId}", result.Success, result.TaskId);
            _clientTaskService.CompleteTask(result.TaskId, result.Success);
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理通用任务结果 (1.4)
        /// </summary>
        private async Task HandleTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<TaskResultNoticeMessage>();

            long correlationId = result.TaskId;
            if (correlationId == 0 && message.RefMessageId != 0) correlationId = message.RefMessageId; // Fallback
            _logger.LogInformation("Task Result: Success={Success}, TaskId={TaskId}, RefId={RefId}, Msg={ErrMsg}", result.Success, result.TaskId, message.RefMessageId, result.ErrMsg);
            _clientTaskService.CompleteTask(correlationId, result.Success, result.ErrMsg);

            // Publish Event for UI
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            await _eventBus.PublishAsync(new TaskResultReceivedEvent(correlationId, result.Success, result.ErrMsg, connectionId, connectionInfo?.DeviceInfo ?? connectionId));

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理截屏任务结果
        /// </summary>
        private async Task HandleScreenShotTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<ScreenShotTaskResultNoticeMessage>();
            _logger.LogInformation("Screen Shot Result: Success={Success}, Url={Url}", result.Success, result.Url);
            
             // 标记任务完成
            _clientTaskService.CompleteTask(result.TaskId, result.Success, result.Url);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo != null)
            {
                 // Notify frontend via UUID (Legacy Hub Call - can potentially remove later)
                 await _hubContext.Clients.Group(connectionInfo.DeviceInfo ?? connectionId).SendAsync("ScreenShotReceived", result.Url);
                 
                 // Publish Event for Reactive UI
                 await _eventBus.PublishAsync(new TaskResultReceivedEvent(result.TaskId, result.Success, result.Url, connectionId, connectionInfo.DeviceInfo ?? connectionId));
            }
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理设备信息上报 (2027)
        /// 1. 解析设备信息 (IMEI, Brand等)
        /// 2. 绑定 ClientUuid 到 WechatAccount
        /// 3. 创建或更新 SrClient 记录
        /// 4. 发送 Ack
        /// </summary>
        private async Task HandlePostDeviceInfoNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<PostDeviceInfoNoticeMessage>();
            _logger.LogInformation("Post Device Info: {Brand} {Model}, IMEI={Imei}", notice.PhoneBrand, notice.PhoneModel, notice.IMEI);

            // Resolve Client UUID from Connection
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo == null || !long.TryParse(connectionInfo.UserId, out long accountId))
            {
                _logger.LogWarning("Received Device Info from unauthenticated or unknown connection: {ConnectionId}", connectionId);
                return;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                // Find WechatAccount to get ClientUuid (Atomic)
                var account = await dbContext.GetWechatAccount(accountId);
                if (account == null || string.IsNullOrEmpty(account.ClientUuid))
                {
                    _logger.LogWarning("WechatAccount not found or missing ClientUuid for AccountId: {AccountId}", accountId);
                    return;
                }

                var clientUuid = account.ClientUuid;
                var client = await dbContext.GetSrClient(clientUuid);
                
                if (client == null)
                {
                    client = new SrClient
                    {
                        uuid = clientUuid,
                        createdAt = DateTime.UtcNow
                    };
                    // Note: Atomic Save will handle Add
                }

                // Map properties to Device DTO
                client.device = new SCRM.API.Models.DTOs.Device
                {
                    hsman = notice.PhoneBrand,
                    hstype = notice.PhoneModel,
                    androidApi = notice.OSVerNumber.ToString(),
                    imei = notice.IMEI,
                    
                    packageName = notice.AppInfos.FirstOrDefault()?.PackageName ?? "",
                    versionCode = notice.AppInfos.FirstOrDefault()?.VerNumber ?? 0
                };

                client.ip = context.Channel.RemoteAddress.ToString();
                client.lastLoginAt = DateTime.UtcNow;
                client.isOnline = true;
                client.updatedAt = DateTime.UtcNow;
                client.ConnectionId = connectionId; // Update ConnectionId
                
                await dbContext.SaveSrClient(client);
                _logger.LogInformation("Updated SrClient info for UUID: {Uuid}", clientUuid);

                // 发送 ACK
                await SendAckAsync(message, context);
            }
        }

        /// <summary>
        /// 处理好友信息推送 (3.3 / 3.1 结果) (完整列表)
        /// 1. 批量保存好友到 Contacts 表
        /// 2. 更新关系链
        /// 3. 通知 SignalR 前端
        /// </summary>
        private async Task HandleFriendPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendPushNoticeMessage>();
            _logger.LogInformation("Friend Push Notice: {WeChatId} pushed {Count} friends (Page {Page}/{Size})", 
                notice.WeChatId, notice.Friends.Count, notice.Page, notice.Size);

            // Resolve AccountId from Connection
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo == null || !long.TryParse(connectionInfo.UserId, out long accountId))
            {
                _logger.LogWarning("Received Friend Push from unauthenticated or unknown connection: {ConnectionId}", connectionId);
                return;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                // Verify account exists
                var account = await dbContext.WechatAccounts.FindAsync(accountId);
                if (account == null)
                {
                    _logger.LogWarning("WechatAccount not found for AccountId: {AccountId}", accountId);
                    return;
                }

                var contactsToSave = new List<Contact>();

                foreach (var friend in notice.Friends)
                {
                    // Map Proto to Entity
                    var contact = new Contact
                    {
                        WechatAccountId = (int)accountId,
                        Wxid = friend.FriendId,
                        Nickname = friend.FriendNick ?? "",
                        Remarks = friend.Remark ?? "", // Map Remark(备注名) to Remarks
                        Description = friend.Memo ?? "", // Map Memo(备注) to Description
                        Avatar = friend.Avatar ?? "",
                        Gender = (int)friend.Gender,
                        Province = friend.Province ?? "",
                        City = friend.City ?? "",
                        Phone = friend.Phone ?? "",
                        Signature = friend.Desc ?? "",
                        Source = friend.Source.ToString(),
                        LabelIds = friend.LabelIds ?? "",
                        Email = "",
                        Country = "", // Proto might lack Country? Check if available or default empty
                        
                        ContactType = 0, // Friend
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    contactsToSave.Add(contact);
                }

                // Atomic Save (DB + Cache Invalidation)
                _logger.LogInformation("[TRACE] About to SaveContacts for {Count} contacts...", contactsToSave.Count);
                await dbContext.SaveContacts(accountId, contactsToSave);
                _logger.LogInformation("[TRACE] SaveContacts finished.");
                
                _logger.LogInformation("Processed Friend Push: {Count} friends synced for Account {AccountId}", contactsToSave.Count, accountId);


                
                // Notify UI to refresh contact list (using the outer connectionInfo)
                try
                {
                    await _eventBus.PublishAsync(new ContactsReceivedEvent(connectionInfo.DeviceInfo ?? connectionId, accountId, connectionInfo.UserId));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish ContactsReceivedEvent");
                }
            }
            await SendAckAsync(message, context);
        }


        /// <summary>
        /// 处理红包详情结果 (4.25 结果)
        /// 1. 记录红包信息 (RedPacket)
        /// 2. 记录领取详情 (RedPacketRecord)
        /// 3. 通知前端
        /// </summary>
        private async Task HandleQueryHbDetailTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<QueryHbDetailTaskResultNoticeMessage>();
            _logger.LogInformation("Red Packet Detail: {WeChatId} - {HbUrl} (Sender: {Sender}, Amount: {TotalAmount})", 
                notice.WeChatId, notice.HbUrl, notice.Sender, notice.TotalAmount);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo == null || !long.TryParse(connectionInfo.UserId, out long accountId))
            {
                return;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();

                // 1. 创建红包记录 (RedPacket)
                // 由于数据库缺少 HbUrl 唯一键，我们每次 Detail 通知都视为一次记录或者尝试去重
                // 为防止重复，可以使用 Sender + Amount + Time (Approx) 判断，但这里简单起见，作为日志式插入
                // 或尝试用 RedPacketMessage 存储 HbUrl 用于去重
                
                var rp = new RedPacket
                {
                    WechatAccountId = (int)accountId,
                    SenderWxid = notice.Sender ?? "Unknown", // Proto Sender 可能是 Nickname? 暂存
                    TotalAmount = notice.TotalAmount / 100m, // Proto 单位通常是分
                    TotalCount = notice.TotalNum,
                    RedPacketMessage = notice.Wishing ?? "", 
                    // 借用 TargetWxid 存储 HbUrl 以便未来可能的关联 (Hack)
                    TargetWxid = notice.HbUrl, 
                    RedPacketStatus = notice.HbStatus,
                    ReceivedCount = notice.RecNum,
                    ReceivedAmount = notice.RecAmount / 100m,
                    SendTime = DateTime.UtcNow, // 无法获取准确发送时间
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                dbContext.RedPackets.Add(rp);
                await dbContext.SaveChangesAsync(); // 获取 Id

                // 2. 存取领取记录 (RedPacketRecord)
                if (notice.Records != null)
                {
                    foreach (var rec in notice.Records)
                    {
                        var record = new RedPacketRecord
                        {
                            RedPacketId = rp.Id,
                            ReceiverWxid = rec.UserName,
                            ReceivedAmount = rec.Amount / 100m,
                            ReceiveTime = DateTime.TryParse(rec.Time, out var t) ? t : DateTime.UtcNow,
                            ReceiveStatus = 1, // 假设存在即已领
                            CreatedAt = DateTime.UtcNow
                        };
                        dbContext.RedPacketRecords.Add(record);
                    }
                    await dbContext.SaveChangesAsync();
                }

                _logger.LogInformation("Saved Red Packet: Id={Id}", rp.Id);

                // 3. 前端通知
                await _hubContext.Clients.Group(connectionId).SendAsync("RedPacketReceived", rp.SenderWxid, rp.TotalAmount);
            }
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理红包状态结果 (4.25 结果)
        /// 通知前端变更
        /// </summary>
        private async Task HandleQueryHbStatusTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<QueryHbStatusTaskResultNoticeMessage>();
            _logger.LogInformation("Red Packet Status: {WeChatId} - {HbUrl} (Status: {Status}, Msg: {Msg})", 
                notice.WeChatId, notice.HbUrl, notice.HbStatus, notice.StatusMsg);
            
            // 简单通知前端
            var connectionId = context.Channel.Id.AsLongText();
            await _hubContext.Clients.Group(connectionId).SendAsync("RedPacketStatusChanged", notice.HbUrl, notice.HbStatus);
        }

        /// <summary>
        /// 处理删除好友通知 (3.4)
        /// 1. 软删除本地 Contact
        /// 2. 通知 SignalR
        /// </summary>
        private async Task HandleFriendDelNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendDelNoticeMessage>();
            _logger.LogInformation("Friend Del Notice: {WeChatId} deleted friend {FriendId}", notice.WeChatId, notice.FriendId);
            
            var connectionId = context.Channel.Id.AsLongText();
            _ = DeleteFriendAsync(connectionId, notice.FriendId, notice.WeChatId);
        }

        private async Task DeleteFriendAsync(string connectionId, string friendId, string weChatId)
        {
             try
            {
                var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
                if (connectionInfo == null || !long.TryParse(connectionInfo.UserId, out long accountId)) return;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                    
                    var contact = await dbContext.Contacts
                        .FirstOrDefaultAsync(c => c.WechatAccountId == accountId && c.Wxid == friendId);
                        
                    if (contact != null)
                    {
                        contact.IsDeleted = true;
                        contact.UpdatedAt = DateTime.UtcNow;
                        await dbContext.SaveChangesAsync();
                        
                        // Notify UI
                        await _hubContext.Clients.Group(connectionInfo.DeviceInfo ?? connectionId).SendAsync("ContactsUpdated", accountId);
                        
                        _logger.LogInformation("Deleted friend {FriendId} for Account {AccountId}", friendId, accountId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting friend {FriendId}", friendId);
            }
        }

        /// <summary>
        /// 处理配置信息推送
        /// 打印日志
        /// </summary>
        private async Task HandleConfigPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ConfigPushNoticeMessage>();
            _logger.LogInformation("Config Push Received from {WeChatId}", notice.WeChatId);
            
            if (notice.BoolConfs != null)
            {
                foreach (var c in notice.BoolConfs)
                {
                    _logger.LogInformation("BoolConfig: Key={Key}, Val={Value}, Name={Name}, Desc={Desc}", c.Key, c.Value, c.Name, c.Desc);
                }
            }
            if (notice.IntConfs != null)
            {
                foreach (var c in notice.IntConfs)
                {
                    _logger.LogInformation("IntConfig: Key={Key}, Val={Value}, Name={Name}, Desc={Desc}", c.Key, c.Value, c.Name, c.Desc);
                }
            }
            if (notice.StrConfs != null)
            {
                foreach (var c in notice.StrConfs)
                {
                    _logger.LogInformation("StrConfig: Key={Key}, Val={Value}, Name={Name}, Desc={Desc}", c.Key, c.Value, c.Name, c.Desc);
                }
            }

            // 发送 ACK
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 发送通用确认消息 (MsgReceivedAck - 1002)
        /// </summary>
        private async Task SendAckAsync(TransportMessage message, IChannelHandlerContext context)
        {
            var response = new TransportMessage
            {
                Id = 0, // 协议规范：ACK 消息的 Id 通常为 0，关键在于 RefMessageId
                MsgType = EnumMsgType.MsgReceivedAck,
                RefMessageId = message.Id
            };

            // 如果原始消息有令牌，ACK 必须携带
            if (!string.IsNullOrEmpty(message.AccessToken))
            {
                response.AccessToken = message.AccessToken;
            }

            await context.WriteAndFlushAsync(response);
            _logger.LogInformation("Sent MsgReceivedAck for MsgType {MsgType}, RefId {Id}", message.MsgType, message.Id);
        }

        /// <summary>
        /// 处理好友请求通知 (3.7)
        /// 发布 FriendRequestEvent 供自动化逻辑处理
        /// </summary>
        private async Task HandleFriendAddReqeustNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendAddReqeustNoticeMessage>();
            _logger.LogInformation("Friend Add Request Notice: {WeChatId} received request from {FriendId} ({FriendNick}): {Reason}", 
                notice.WeChatId, notice.FriendId, notice.FriendNick, notice.Reason);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo != null && long.TryParse(connectionInfo.UserId, out long accountId))
            {
                // Publish Event for Automation
                var evt = new FriendRequestEvent(
                    notice.WeChatId, 
                    notice.FriendId, 
                    notice.FriendNick, 
                    notice.Reason, 
                    connectionId, 
                    accountId);
                await _eventBus.PublishAsync(evt);
            }
        }

        /// <summary>
        /// 处理朋友圈新发通知 (3.8)
        /// 发布 CircleNewPublishEvent 供自动化处理
        /// </summary>
        private async Task HandleCircleNewPublishNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<CircleNewPublishNoticeMessage>();
            if (notice.Circle == null) return;

            _logger.LogInformation("Circle New Publish Notice: {WeChatId} received new moment from {Author}: {Content}", 
                notice.WeChatId, notice.Circle.WeChatId, notice.Circle.Content?.Text);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo != null && long.TryParse(connectionInfo.UserId, out long accountId))
            {
                 // Publish Event for Automation
                 var evt = new CircleNewPublishEvent(
                     notice.WeChatId,
                     notice.Circle.WeChatId,
                     notice.Circle.CircleId,
                     notice.Circle.Content?.Text,
                     connectionId,
                     accountId);
                 await _eventBus.PublishAsync(evt);
            }
        }

        // Removed logic: ExtractXmlValue is no longer needed in this file.




        /// <summary>
        /// 处理好友变更通知 (1052)
        /// Handle FriendChangeNotice (1052)
        /// </summary>
        private async Task HandleFriendChangeNotice(TransportMessage message, IChannelHandlerContext context)
        {
            try
            {
                var notice = message.Content.Unpack<FriendChangeNoticeMessage>();
                _logger.LogInformation("收到好友变更通知 FriendChangeNotice: WeChatId={WeChatId}, FriendNick={FriendNick}", 
                    notice.WeChatId, notice.Friend?.FriendNick);

                // 可以在此处添加业务逻辑，例如更新好友列表缓存或通知前端
                // Currently just logging and ACKing to suppress warnings

                await SendMsgReceivedAck(context, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HandleFriendChangeNotice 异常");
            }
        }

        /// <summary>
        /// 发送消息接收确认 (1002)
        /// 告知客户端已收到消息，防止客户端重试或超时
        /// </summary>
        private async Task SendMsgReceivedAck(IChannelHandlerContext context, TransportMessage refMessage)
        {
            var response = new TransportMessage
            {
                Id = refMessage.Id,
                MsgType = EnumMsgType.MsgReceivedAck,
                RefMessageId = refMessage.Id
            };
            await context.WriteAndFlushAsync(response);
        }

        /// <summary>
        /// 处理朋友圈推送通知 (3.13)
        /// 1. 保存到数据库
        /// 2. 推送到 SignalR 前端
        /// </summary>
        private async Task HandleCirclePushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<CirclePushNoticeMessage>();
            _logger.LogInformation("Circle Push Notice: {WeChatId}, Count={Count}, Page={Page}", 
                notice.WeChatId, notice.Circles.Count, notice.Page);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo == null || !long.TryParse(connectionInfo.UserId, out long accountId)) return;

            // We use a list to collect DTOs to send in batch or individually
            // Ideally batch, but client expects single? I defined `OnMomentReceived` for single item.
            // But `GetMomentsTimelineAsync` returns list.
            // Let's send individually for real-time feed effect, or modify client to accept list.
            // Client: `OnMomentReceived` takes DTO. So invalid.
            // I should ideally send list if many.
            // But SignalR allows calling client method in loop.
            
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                foreach (var circle in notice.Circles)
                {
                    string snsId = circle.CircleId.ToString();
                    
                    // 1. Check if exists
                    var exists = await dbContext.MomentsTimelines.AnyAsync(m => m.SnsId == snsId);
                    if (!exists)
                    {
                        var entity = new MomentsTimeline
                        {
                            SnsId = snsId,
                            UserName = circle.WeChatId,
                            NickName = "", // Nickname not in CircleInfo? Wait. Author Nickname is usually separate. 
                                           // CircleLike has NickName. CircleInfo doesn't? 
                                           // We might need to look up Contact? 
                                           // Or it might be empty.
                            Content = circle.Content?.Text ?? "",
                            CreateTime = circle.PublishTime,
                            ReceivedAt = DateTime.UtcNow.Ticks,
                            OwnerWxid = notice.WeChatId, // The device owner seeing this
                            
                            ImagesJson = System.Text.Json.JsonSerializer.Serialize(
                                circle.Content?.Images.Select(i => i.ThumbImg).ToList() ?? new List<string>()
                            ),
                            CommentsJson = System.Text.Json.JsonSerializer.Serialize(
                                circle.Comments.Select(c => new SCRM.SHARED.Models.Dtos.MomentCommentDto { AuthorName = c.FromName, Content = c.Content }).ToList()
                            ),
                            LikesJson = System.Text.Json.JsonSerializer.Serialize(
                                circle.Likes.Select(l => new SCRM.SHARED.Models.Dtos.MomentLikeDto { UserName = l.FriendId, NickName = l.NickName }).ToList()
                            )
                        };
                        
                        dbContext.MomentsTimelines.Add(entity);
                    }
                    
                    // 2. Build DTO
                    var dto = new SCRM.SHARED.Models.Dtos.MomentsTimelineDto
                    {
                        SnsId = snsId,
                        UserName = circle.WeChatId,
                        NickName = "", // Placeholder
                        Content = circle.Content?.Text ?? "",
                        CreateTime = circle.PublishTime, // Seconds
                        Images = circle.Content?.Images.Select(i => i.ThumbImg).ToList() ?? new List<string>(),
                        Comments = circle.Comments.Select(c => new SCRM.SHARED.Models.Dtos.MomentCommentDto { AuthorName = c.FromName, Content = c.Content }).ToList(),
                        Likes = circle.Likes.Select(l => new SCRM.SHARED.Models.Dtos.MomentLikeDto { UserName = l.FriendId, NickName = l.NickName }).ToList()
                    };
                    
                    // 3. Send to Client
                    await _hubContext.Clients.Group(connectionInfo.DeviceInfo ?? connectionId).SendAsync("MomentReceived", dto);
                }
                
                await dbContext.SaveChangesAsync();
            }
        }
    }
}



