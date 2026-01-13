using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.AspNetCore.SignalR;
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
using SCRM.Models.Configurations;
using SCRM.API.Services; 
using SCRM.API.Models.DTOs;
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
            _logger.LogInformation("路由消息：Id={Id}, MsgType={MsgType} ({MsgTypeId}), Token={Token}", 
                message.Id, message.MsgType, (int)message.MsgType, message.AccessToken);

            if (message.Content != null)
            {
                _logger.LogInformation("传入内容 TypeUrl: {TypeUrl}", message.Content.TypeUrl);
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
                    case EnumMsgType.PostMessageReadNotice:
                        // 4.3 客户端上传消息已读状态
                        await HandlePostMessageReadNotice(message, context);
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
                        _logger.LogWarning("未处理的消息类型：{MsgType}", message.MsgType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "路由消息类型错误：{MsgType}", message.MsgType);
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
            _logger.LogInformation("收到心跳包，来自 {RemoteAddress}，连接ID：{ConnectionId}", context.Channel.RemoteAddress, connId);
            
            // 检查连接是否已认证（是否映射到用户）
            bool isAuthenticated = await _connectionManager.IsConnectionAuthenticatedAsync(connId);
            _logger.LogInformation("Auth check for {ConnectionId}: {IsAuthenticated}", connId, isAuthenticated);

            if (!isAuthenticated)
            {
                _logger.LogWarning("收到未认证连接的心跳包 {ConnectionId}。发送强制下线通知。", connId);
                
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
            
            _logger.LogInformation("[业务鉴权] 收到设备认证请求 - Type: {AuthType}, 凭证: {Credential}, ChannelId: {ChannelId}", authReq.AuthType, credential, context.Channel.Id.AsLongText());

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                var authService = scope.ServiceProvider.GetRequiredService<SCRM.Services.AuthService>();
                
                WechatAccount account = null;

                // === 新鉴权逻辑: Token | IMEI (AuthType = InternalCode) ===
                if (authReq.AuthType == DeviceAuthReqMessage.Types.EnumAuthType.InternalCode)
                {
                    // 格式约定: "JWT_TOKEN|IMEI"
                    var parts = credential.Split('|');
                    if (parts.Length == 2)
                    {
                        var token = parts[0];
                        var imei = parts[1];

                        // 1. 验证 Token (强校验)
                        System.Security.Claims.ClaimsPrincipal? principal = null;
                        try 
                        {
                            principal = authService.ValidateToken(token);
                        }
                        catch (Microsoft.IdentityModel.Tokens.SecurityTokenExpiredException)
                        {
                             _logger.LogWarning("Token 已过期。凭证: {Credential}", credential.Substring(0, Math.Min(20, credential.Length)) + "...");
                        }
                        catch (Microsoft.IdentityModel.Tokens.SecurityTokenMalformedException)
                        {
                             _logger.LogWarning("Token 格式错误 (非有效JWT)。凭证: {Credential}", credential.Substring(0, Math.Min(20, credential.Length)) + "...");
                        }
                        catch (Exception ex)
                        {
                             _logger.LogWarning("Token 验证异常: {Message}", ex.Message);
                        }

                        if (principal != null)
                        {
                            var userIdStr = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                            var userName = principal.Identity?.Name;
                            
                            // 修复：Identity User ID 是 GUID string，不是 long
                            if (!string.IsNullOrEmpty(userIdStr))
                            {
                                _logger.LogInformation("Token 验证成功。User: {User} ({Id})", userName, userIdStr);

                                // 2. 查找或绑定设备
                                // 策略：优先找已绑定的，没绑定的自动绑定到该用户
                                account = await dbContext.WechatAccounts
                                    .FirstOrDefaultAsync(u => u.wechatNumber == imei && !u.isDeleted);

                                if (account == null)
                                {
                                    // 自动注册/绑定 (Auto-Provisioning)
                                    _logger.LogInformation("新设备 {IMEI}，自动绑定给用户 {User}", imei, userName);
                                    account = new WechatAccount 
                                    { 
                                        wechatNumber = imei,
                                        ownerId = userIdStr, 
                                        isActive = true,
                                        createdAt = DateTime.UtcNow,
                                        vipExpiryDate = DateTime.UtcNow.AddSeconds(30)
                                    };
                                    await dbContext.WechatAccounts.AddAsync(account);
                                    await dbContext.SaveChangesAsync();
                                }

                                if (account != null)
                                {
                                    if (string.IsNullOrEmpty(account.ownerId))
                                    {
                                        account.ownerId = userIdStr;
                                        await dbContext.SaveWechatAccount(account);
                                        _logger.LogInformation("设备 {IMEI} 已自动归属给用户 {User}", imei, userName);
                                    }
                                    else if (account.ownerId != userIdStr)
                                    {
                                        // 允许管理员或同一用户的不同设备？暂且严格检查
                                        // 如果只是单纯的 Warning，可能会导致 account 被赋值但无法连接？
                                        // 现有逻辑 Account != null 就会继续。
                                        // 这里如果是归属权冲突，应该阻止？
                                        // 当前逻辑只是 Warning，然后继续连接。这意味着“借用”设备？
                                        // 为了安全，应该 return null 或者 throw？
                                        // 考虑到调试方便，先 LogWarning，允许连接，或者强制归属？
                                        // 暂时维持原状：LogWarning 但继续。
                                        _logger.LogWarning("设备 {IMEI} 归属权冲突！当前Owner: {Owner}, 请求User: {User}. (允许临时连接)", imei, account.ownerId, userIdStr);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Already logged in catch or explicitly null?
                            // If principal is null (and no exception caught?), it means invalid token logic.
                            if (principal == null) 
                            {
                                 _logger.LogWarning("Token 验证无效 (Principal is null)。");
                            }
                        }
                    }
                }

                if (account == null)
                {
                    _logger.LogWarning("[业务鉴权] 设备认证失败：未提供有效 Token 或 Token 验证失败。Type: {AuthType}, 凭证: {Credential}", authReq.AuthType, credential);
                    
                    // [Fix] 发送明确的失败响应，触发客户端 handleAuthFailed -> 自动刷新 Token
                    // 客户端逻辑：如果 AccessToken 为空，则视为认证失败，触发重试。
                    var failContent = new DeviceAuthRspMessage
                    {
                        AccessToken = "", // Empty indicates failure
                        Extra = new DeviceAuthRspMessage.Types.ExtraMessage 
                        { 
                            NickName = "Auth Failed" 
                        }
                    };
                    var failMsg = new TransportMessage
                    {
                        Id = 0,
                        MsgType = EnumMsgType.DeviceAuthRsp,
                        RefMessageId = message.Id,
                        Content = Any.Pack(failContent)
                    };
                    await context.WriteAndFlushAsync(failMsg);

                    return;
                }

                if (account == null)
                {
                    _logger.LogWarning("[业务鉴权] 设备认证失败：设备未注册。凭证：{Credential}", credential);
                    return;
                }

                if (!account.isVip)
                {
                    // 开发环境自动续期 VIP
                    _logger.LogWarning("设备 VIP 已过期。自动续期1年。凭证：{Credential}, 原过期时间：{Expiry}", credential, account.vipExpiryDate);
                    account.vipExpiryDate = DateTime.UtcNow.AddYears(1);
                    await dbContext.SaveWechatAccount(account); // 原子保存
                }

                _logger.LogInformation("[业务鉴权] 认证成功 - AccountId: {AccountId}, Nickname: {Nickname}", account.accountId, account.nickname);

                // 注册连接信息
                string userId = account.accountId.ToString();
                string deviceType = "Android"; 
                
                // 如果 WechatAccount 中有 ClientUuid 则用作 deviceId，否则使用 WechatNumber (IMEI)
                string deviceId = !string.IsNullOrEmpty(account.clientUuid) ? account.clientUuid : account.wechatNumber;

                await _connectionManager.AddConnectionAsync(userId, context.Channel.Id.AsLongText(), deviceType, deviceId);

                // 发布设备已连接事件 (携带 OwnerId 以便推送)
                await _eventBus.PublishAsync(new DeviceConnectedEvent(userId, context.Channel.Id.AsLongText(), deviceType, account.ownerId));

                var responseContent = new DeviceAuthRspMessage
                {
                    AccessToken = Guid.NewGuid().ToString("N"), // 生成会话 Token
                    Extra = new DeviceAuthRspMessage.Types.ExtraMessage
                    {
                        SupplierId = account.accountId, // 使用 AccountId 作为 SupplierId
                        UnionId = account.accountId,
                        AccountType = EnumAccountType.Main,
                        SupplierName = "SCRM",
                        NickName = account.nickname ?? "Unknown",
                        Token = account.wxid // 将 Wxid 放入 Token 字段，以便客户端将其用作 c2cServerAddress
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


                // --- 3. [Early Config Push] (TCP Based) ---
                try 
                {
                    _logger.LogInformation("[Config] Pushing early configuration via TCP...");
                    var nettySettings = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<NettySettings>>().Value;
                    
                    // 1. Fetch Global Configs from DB
                    var configs = await dbContext.SystemConfigs.ToListAsync();
                    var configMap = configs.ToDictionary(c => c.key, c => c.value);

                    // 2. Fetch Device Specific Configs (SrClient)
                    Dictionary<string, string> deviceConfigs = new Dictionary<string, string>();
                    if (!string.IsNullOrEmpty(account.clientUuid))
                    {
                        var srClient = await dbContext.SrClients.FirstOrDefaultAsync(c => c.uuid == account.clientUuid);
                        if (srClient != null && !string.IsNullOrEmpty(srClient.customConfigs))
                        {
                            try 
                            {
                                var temp = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(srClient.customConfigs);
                                if (temp != null) deviceConfigs = temp;
                                _logger.LogInformation("[Config] Loaded {Count} device specific configs for {Uuid}", deviceConfigs.Count, account.clientUuid);
                            }
                            catch(Exception ex) 
                            {
                                _logger.LogWarning("[Config] Failed to parse customConfigs for client {Uuid}: {Ex}", account.clientUuid, ex.Message);
                            }
                        }
                    }

                    string token = "";
                    if (authReq.AuthType == DeviceAuthReqMessage.Types.EnumAuthType.InternalCode && !string.IsNullOrEmpty(credential) && credential.Contains("|"))
                    {
                        token = credential.Split('|')[0];
                    }

                    // Prepare Dictionaries to Send
                    var strConfs = new Dictionary<string, string>();
                    var boolConfs = new Dictionary<string, bool>();
                    var intConfs = new Dictionary<string, int>();

                    // --- Helper: Get Config with Merge Logic (Device > Global > Default) ---
                    string GetConf(string key, string defVal) 
                    {
                        if (deviceConfigs.TryGetValue(key, out string? val) && !string.IsNullOrEmpty(val)) return val;
                        if (configMap.TryGetValue(key, out val) && !string.IsNullOrEmpty(val)) return val;
                        return defVal;
                    }

                    // 3. Map Core Network Configs (New Keys)
                    // Defaults
                    string defHost = nettySettings.Host;
                    string defPort = nettySettings.Port.ToString();
                    string defFileUp = $"http://{nettySettings.Host}:{nettySettings.HttpPort}/fileUpload?access_token={token}";
                    string defApiBase = $"http://{nettySettings.Host}:{nettySettings.HttpPort}/api";

                    // Host & Port
                    strConfs["tcpServerHost"] = GetConf("tcpServerHost", GetConf("host", defHost)); // Support legacy 'host' override
                    strConfs["tcpServerPort"] = GetConf("tcpServerPort", GetConf("server_port", defPort)); 

                    // HTTP URLs
                    strConfs["fileUploadUrl"] = GetConf("fileUploadUrl", GetConf("fileUpUrl", defFileUp));
                    strConfs["httpApiBaseUrl"] = GetConf("httpApiBaseUrl", GetConf("apiBaseUrl", defApiBase));

                    // 4. Map other keys (Auto-Merge)
                    // List of all other keys we support
                    var stringKeys = new[] { "clientConfigPath", "logLevel", "autoUpdateUrl" };
                    foreach(var key in stringKeys) strConfs[key] = GetConf(key, "");

                    var boolKeys = new[] { "autoLogin", "autoPic", "fastSend", "silentFunc", "forceRun" };
                    foreach(var key in boolKeys) 
                    {
                        string val = GetConf(key, "");
                        if (bool.TryParse(val, out bool bVal)) boolConfs[key] = bVal;
                    }

                    var intKeys = new[] { "keepWake" }; // server_port handled above
                    foreach(var key in intKeys) 
                    {
                         string val = GetConf(key, "");
                         if (int.TryParse(val, out int iVal)) intConfs[key] = iVal;
                    }
                    
                    // Uses existing SetConfigTask (1382) which handles key-value pairs
                    await _clientTaskService.SendSetConfigTaskAsync(context.Channel.Id.AsLongText(), boolConfs, intConfs, strConfs);
                    _logger.LogInformation("[Config] Pushed configuration. tcpHost={H}, tcpPort={P}, fileUp={F}", strConfs["tcpServerHost"], strConfs["tcpServerPort"], strConfs["fileUploadUrl"]);
                }


                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Config] Failed to push configuration.");
                }

                // --- 触发账号初始化任务 (Strict Verification Flow) ---
                _logger.LogInformation("触发严格身份验证流程，账号ID：{AccountId}", account.accountId);

                try
                {
                    // [Strict Verification]
                    // 无论新老设备，都不直接信任缓存的 Wxid。
                    // 必须先发送指令强制客户端上报当前真实的 Wxid (WeChatOnlineNotice)。
                    // 只有在 HandleWeChatOnline 中确认身份后，才会触发数据同步。
                    
                    var connId = context.Channel.Id.AsLongText();
                    _logger.LogInformation("[Strict] 发送 WeChatLocationTask(NoCache=true) 以获取实时 Wxid。");
                    
                    // 发送唤醒指令 (WeChatLocationTask 映射到客户端的 setDisturbModeEnabled(true) -> startWeChatIfNeeded(true))
                    var wakeUpTask = new WeChatLocationTaskMessage { NoCache = true };
                    var wakeUpMsg = new TransportMessage
                    {
                        Id = 0, // 无需跟踪 ID
                        MsgType = EnumMsgType.WeChatLocationTask,
                        Content = Any.Pack(wakeUpTask)
                    };
                    await context.WriteAndFlushAsync(wakeUpMsg);
                }

                catch (Exception ex)
                {
                    _logger.LogError(ex, "触发账号初始化任务出错，账号ID：{AccountId}", account.accountId);
                }

                // --- 修复：将 ConnectionId 持久化到数据库，以便 api/device 返回正确的 ID ---
                if (!string.IsNullOrEmpty(account.clientUuid))
                {
                    var client = await dbContext.GetSrClient(account.clientUuid); // 原子获取
                    if (client != null)
                    {
                        client.isOnline = true;
                        client.lastLoginAt = DateTime.UtcNow;
                        client.updatedAt = DateTime.UtcNow;
                        client.connectionId = context.Channel.Id.AsLongText(); // 更新 ConnectionId
                        
                        await dbContext.SaveSrClient(client); // 原子保存
                        _logger.LogInformation("已更新 SrClient {Uuid} 的新连接ID：{ConnectionId}", client.uuid, client.connectionId);
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
            _logger.LogInformation("给好友发消息任务结果：成功={Success}, 代码={Code}, 消息={ErrMsg}", 
                result.Success, result.Code, result.ErrMsg);
            
             // 标记任务完成
             long correlationId = result.MsgId;
             if (correlationId == 0 && message.RefMessageId != 0) correlationId = message.RefMessageId; // 回退到 Transport ID
             _clientTaskService.CompleteTask(correlationId, result.Success, result.ErrMsg);

            // Publish Event for UI
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            await _eventBus.PublishAsync(new TaskResultReceivedEvent(correlationId, result.Success, result.ErrMsg, connectionId, connectionInfo?.deviceInfo ?? connectionId));

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
            _logger.LogInformation("微信上线：{WeChatId} ({WeChatNick})", notice.WeChatId, notice.WeChatNick);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo != null && long.TryParse(connectionInfo.userId, out long currentAccountId))
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                    
                    // 1. 获取当前上下文绑定的账号 (可能是旧的)
                    var currentAccount = await dbContext.GetWechatAccount(currentAccountId);
                    WechatAccount targetAccount = currentAccount;
                    bool isAccountSwitched = false;

                    if (currentAccount != null)
                    {
                        // 2. 检查 Wxid 是否发生变更 (核心逻辑：严格分离)
                        if (currentAccount.wxid != notice.WeChatId && !string.IsNullOrEmpty(currentAccount.wxid))
                        {
                            _logger.LogWarning("账号身份变更检测: 设备原账号 {OldWxid} (ID:{OldId}) -> 新账号 {NewWxid}", 
                                currentAccount.wxid, currentAccount.accountId, notice.WeChatId);

                            // 3. 查找是否已存在目标账号
                            var existingTargetAccount = await dbContext.WechatAccounts
                                .FirstOrDefaultAsync(w => w.wxid == notice.WeChatId);

                            if (existingTargetAccount != null)
                            {
                                 _logger.LogInformation("找到现有目标账号 {Wxid} (ID:{Id})，切换 Session...", existingTargetAccount.wxid, existingTargetAccount.accountId);
                                targetAccount = existingTargetAccount;
                            }
                            else
                            {
                                _logger.LogInformation("目标账号 {Wxid} 不存在，创建新账号...", notice.WeChatId);
                                // 创建新账号
                                targetAccount = new WechatAccount
                                {
                                    wxid = notice.WeChatId,
                                    ownerId = currentAccount.ownerId, // 继承设备拥有者
                                    clientUuid = currentAccount.clientUuid,
                                    createdAt = DateTime.UtcNow
                                };
                                await dbContext.SaveWechatAccount(targetAccount); // 保存以获取 ID
                            }

                            // 4. 处理旧账号状态
                            currentAccount.accountStatus = (short)EnumAccountStatus.Offline;
                            // 解绑设备可以防止误操作，但为了历史记录暂时保留 clientUuid
                            await dbContext.SaveWechatAccount(currentAccount);

                            // 5. 更新连接映射 (关键：后续消息将路由到新 AccountID)
                            await _connectionManager.UpdateConnectionUserIdAsync(connectionId, targetAccount.accountId.ToString());
                            
                            isAccountSwitched = true;
                        }

                        // 6. 更新目标账号信息 (无论是新切换的还是原来的)
                        targetAccount.wxid = notice.WeChatId; // 再次确认
                        targetAccount.nickname = notice.WeChatNick;
                        targetAccount.accountStatus = (short)EnumAccountStatus.Online;
                        targetAccount.lastOnlineAt = DateTime.UtcNow;
                        // 确保 ownerId 和 clientUuid 正确 (如果是新建的或者接管的)
                        if (string.IsNullOrEmpty(targetAccount.ownerId)) targetAccount.ownerId = currentAccount.ownerId;
                        if (string.IsNullOrEmpty(targetAccount.clientUuid)) targetAccount.clientUuid = currentAccount.clientUuid;
                        
                        await dbContext.SaveWechatAccount(targetAccount);

                        // 7. 强制同步关联的 SrClient (确保设备指向正确的 AccountID)
                        if (!string.IsNullOrEmpty(targetAccount.clientUuid))
                        {
                            var client = await dbContext.GetSrClient(targetAccount.clientUuid);
                            if (client != null)
                            {
                                // 如果发生切换，或者 Client 的记录滞后
                                if (client.wechatAccountId != targetAccount.accountId || client.connectionId != connectionId)
                                {
                                    client.isOnline = true;
                                    client.updatedAt = DateTime.UtcNow;
                                    client.connectionId = connectionId;
                                    client.wechatAccountId = targetAccount.accountId; // 核心：指向新账号
                                    client.weChatId = notice.WeChatId;
                                    client.weChatNick = notice.WeChatNick;
                                    
                                    await dbContext.SaveSrClient(client);
                                    _logger.LogInformation("已更新 SrClient {Uuid} 绑定 -> Account {AccountId} ({Wxid})", client.uuid, targetAccount.accountId, targetAccount.wxid);
                                }
                            }
                        }

                        // 8. 通知与触发
                        // 如果切换了账号，通知 UI 刷新设备列表可能比单发 StatusChanged 更稳妥，但 StatusChanged 也会触发 LoadDevices
                        await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("WeChatStatusChanged", notice.WeChatId, notice.WeChatNick, true);
                        
                        // 触发同步任务 (针对 targetAccount)
                        _logger.LogInformation("触发后台同步: {Wxid} (ID:{Id})", targetAccount.wxid, targetAccount.accountId);
                        _ = _clientTaskService.SendTriggerFriendPushTaskAsync(connectionId, DateTime.UtcNow.Ticks);
                        _ = _clientTaskService.SendTriggerChatRoomPushTaskAsync(connectionId, DateTime.UtcNow.Ticks + 1);
                        _ = _clientTaskService.SendTriggerChatRoomPushTaskAsync(connectionId, DateTime.UtcNow.Ticks + 1);
                        //_ = _clientTaskService.SendTriggerConfigPushTaskAsync(connectionId, DateTime.UtcNow.Ticks + 2);
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
            var connectionId = context.Channel.Id.AsLongText();
            string weChatId = notice.WeChatId;

            _logger.LogInformation("微信下线：{WeChatId}, 原因={Reason}, 连接={ConnId}", weChatId, notice.Reason, connectionId);

            // [Race Condition Fix] 如果消息中 Wxid 为空，尝试通过连接查找
            if (string.IsNullOrEmpty(weChatId))
            {
                var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
                if (connectionInfo != null && !string.IsNullOrEmpty(connectionInfo.deviceInfo))
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                        var client = await dbContext.GetSrClient(connectionInfo.deviceInfo);

                        // 只要找到了 Client，无论是否有 Wxid，都意味着该设备上的微信离线了
                        if (client != null)
                        {
                            _logger.LogInformation("收到离线通知 (Wxid为空)，关联设备: {Uuid} ({Nick})", client.uuid, client.weChatNick);

                            // 如果 Client 知道 Wxid，补充上
                            if (string.IsNullOrEmpty(client.weChatId))
                            {
                                weChatId = client.weChatId;
                            }

                            if (!string.IsNullOrEmpty(client.weChatId))
                            {
                                // 更新 Account 状态
                                // 原子获取账号信息 (通过 ClientUuid 查找 Account 可能不可靠，最好有 Wxid)
                                // 这里我们暂时相信 client.WechatAccountId
                                if (client.wechatAccountId.HasValue)
                                {
                                    var account = await dbContext.GetWechatAccount(client.wechatAccountId.Value);
                                    if (account != null)
                                    {
                                        account.accountStatus = (short)EnumAccountStatus.Offline;
                                        await dbContext.SaveWechatAccount(account);
                                        _logger.LogInformation("已标记账号 {Wxid} 为离线", account.wxid);
                                    }
                                }
                            }

                            // 通知 UI
                            // 如果我们补充到了 Wxid，就发 WeChatStatusChanged
                            if (!string.IsNullOrEmpty(weChatId))
                            {
                                await _hubContext.Clients.All.SendAsync("WeChatStatusChanged", weChatId, "Unknown", false);
                            }
                        }
                    }
                }
            }
            else
            {
                // Wxid 已知，正常通知
                await _hubContext.Clients.All.SendAsync("WeChatStatusChanged", weChatId, "Unknown", false);
            }

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
            
            _logger.LogInformation("好友聊天通知：{WeChatId} 收到来自 {FriendId} 的消息: {Content}", 
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
            if (!long.TryParse(connectionInfo.userId, out long acctId)) return;

            // --- 消息去重 & 持久化 ---
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                // 1. 基于 MsgSvrId 的幂等检查
                var existingMsg = await dbContext.Messages.FirstOrDefaultAsync(m => m.msgSvrId == notice.MsgSvrId && m.accountId == acctId);
                
                if (existingMsg == null)
                {
                    // 1.5 确保会话存在
                    // 注意：WechatAccountId 在 Conversation 中定义为 int，这里强转 (int)acctId。
                    // 假设 AccountId 在 int 范围内。如果将来 AccountId 超过 int，需修改 Conversation 实体。
                    var conversation = await dbContext.Conversations
                        .FirstOrDefaultAsync(c => c.wechatAccountId == acctId && c.conversationWxid == notice.FriendId);

                    if (conversation == null)
                    {
                        conversation = new Conversation
                        {
                            wechatAccountId = acctId,
                            conversationWxid = notice.FriendId,
                            conversationType = isGroup ? 2 : 1,
                            displayName = notice.FriendId, // 暂用 Wxid 作为显示名称
                            displayAvatar = "",
                            unreadCount = 0,
                            messageCount = 0,
                            isPinned = 0,
                            isMuted = 0,
                            lastMessageTime = DateTime.UtcNow,
                            createdAt = DateTime.UtcNow,
                            updatedAt = DateTime.UtcNow,
                            isDeleted = false
                        };
                        dbContext.Conversations.Add(conversation);
                        await dbContext.SaveChangesAsync(); // 保存以获取 Id
                    }

                    var msg = new Message
                    {
                        conversationId = conversation.id,
                        accountId = acctId,
                        senderWxid = isSelf ? notice.WeChatId : notice.FriendId,
                        receiverWxid = isSelf ? notice.FriendId : notice.WeChatId,
                        content = contentUtf8,
                        chatType = (short)(isGroup ? 2 : 1), // 1=单聊, 2=群聊
                        messageType = (short)notice.ContentType,
                        direction = (short)direction,
                        sendStatus = 3, // 已送达/同步
                        readStatus = isSelf ? (short)1 : (short)0, // 自己发的设为已读
                        msgSvrId = notice.MsgSvrId,
                        sentAt = DateTime.UtcNow,
                        receivedAt = DateTime.UtcNow,
                        createdAt = DateTime.UtcNow,
                        updatedAt = DateTime.UtcNow
                    };
                    dbContext.Messages.Add(msg);
                    await dbContext.SaveChangesAsync();
                }
                else
                {
                     _logger.LogDebug("检测到重复消息，跳过保存。MsgSvrId: {MsgSvrId}", notice.MsgSvrId);
                }
            }

            // --- 发送给 UI 前端 (SignalR) ---
            var deviceInfo = connectionInfo.deviceInfo ?? connectionId;
            var msgDto = new 
            { 
                FriendId = notice.FriendId, // 会话窗口 ID
                Content = contentUtf8, 
                IsSelf = isSelf,
                MsgSvrId = notice.MsgSvrId,
                IsGroup = isGroup,
                TaskId = notice.MsgId // 透传任务关联 ID (即 MsgId)
            };
            
            await _hubContext.Clients.Group(deviceInfo).SendAsync("ReceiveMessage", msgDto);
            
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
                _logger.LogError(ex, "发布自动化事件出错");
            }

            // 发送 ACK
            await SendAckAsync(message, context);
        }

        private string ExtractXmlValue(string xml, string tagName)
        {
            try {
                // 简单的解析器
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
            _logger.LogInformation("微信回复好友通知：{WeChatId} 发送消息给 {FriendId}: {Content}", 
                notice.WeChatId, notice.FriendId, notice.Content.ToStringUtf8());

            // 推送到 SignalR 组 (UUID)
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo != null)
            {
                await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("ReceiveMessage", notice.FriendId, notice.Content.ToStringUtf8(), true); 
            } 

            // 持久化到数据库
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                // connectionId 和 connectionInfo 从外部作用域可用
                
                if (connectionInfo != null && long.TryParse(connectionInfo.userId, out long accountId))
                {
                    var msg = new Message
                    {
                        accountId = (int)accountId,
                        senderWxid = notice.WeChatId, // 当前账号
                        receiverWxid = notice.FriendId,
                        content = notice.Content.ToStringUtf8(),
                        chatType = 1, // 单聊
                        messageType = (short)notice.ContentType,
                        direction = 1, // 发送
                        sendStatus = 2, // 已发送
                        readStatus = 1, // 已读 (自己发的)
                        msgSvrId = notice.MsgId,
                        sentAt = DateTime.UtcNow,
                        createdAt = DateTime.UtcNow,
                        updatedAt = DateTime.UtcNow
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
                _logger.LogInformation("新增好友通知：{WeChatId} 添加了好友 {FriendNick}", 
                    notice.WeChatId, notice.Friend.FriendNick);

                var connectionId = context.Channel.Id.AsLongText();
                // 我们需要等待异步上下文持久化
                // 由于此方法返回 Task，我们可以将其异步化
                await SaveAddedFriendAsync(connectionId, notice.Friend, notice.WeChatId, message, context);
            }
            return;
        }

        /// <summary>
        /// 处理消息已读通知 (4.3)
        /// </summary>
        private async Task HandlePostMessageReadNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<PostMessageReadNoticeMessage>();
            _logger.LogInformation("消息已读通知：{WeChatId} 在会话 {FriendId} 中已读", 
                notice.WeChatId, notice.FriendId);
            
            // 发送 ACK
            await SendAckAsync(message, context);
        }

        private async Task SaveAddedFriendAsync(string connectionId, FriendMessage friend, string weChatId, TransportMessage message, IChannelHandlerContext context)
        {
            try
            {
                var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
                if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId)) return;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                    
                    var contact = new Contact
                    {
                        wechatAccountId = (int)accountId,
                        wxid = friend.FriendId,
                        nickname = friend.FriendNick ?? "",
                        remarks = friend.Memo ?? "",
                        avatar = friend.Avatar ?? "",
                        gender = (int)friend.Gender,
                        province = friend.Province ?? "",
                        city = friend.City ?? "",
                        phone = friend.Phone ?? "",
                        signature = friend.Desc ?? "",
                        email = "",
                        country = "",
                        contactType = 0,
                        isDeleted = false,
                        createdAt = DateTime.UtcNow,
                        updatedAt = DateTime.UtcNow
                    };

                    await dbContext.SaveContacts(accountId, new List<Contact> { contact });
                    
                    // Notify UI
                    await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("ContactsUpdated", accountId);

                    // 发送 ACK
                    await SendAckAsync(message, context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存新增好友失败 {FriendId}", friend.FriendId);
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
            _logger.LogInformation("群聊推送通知：{WeChatId} 推送了 {Count} 个群聊 (第 {Page}/{Size} 页)", 
                notice.WeChatId, notice.ChatRooms.Count, notice.Page, notice.Size);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId))
            {
                _logger.LogWarning("收到来自未认证连接的群聊推送：{ConnectionId}", connectionId);
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
                        .FirstOrDefaultAsync(g => g.wechatAccountId == accountId && g.groupWxid == room.UserName);

                    if (group == null)
                    {
                        group = new Group
                        {
                            wechatAccountId = (int)accountId,
                            groupWxid = room.UserName,
                            createdAt = DateTime.UtcNow,
                            isDeleted = false
                        };
                        dbContext.Groups.Add(group);
                        newCount++;
                    }
                    else
                    {
                        updateCount++;
                    }

                    // 更新字段
                    group.groupName = room.NickName ?? "";
                    group.ownerWxid = room.Owner ?? "";
                    group.groupNotice = room.Notice ?? "";
                    group.groupAvatar = room.Avatar ?? "";
                    group.groupDescription = ""; // 必须非空
                    group.memberCount = room.MemberList.Count; // 使用 MemberList 数量作为近似值
                    group.updatedAt = DateTime.UtcNow;

                    // 2. 简单的成员同步 (如果需要)
                    // 注意：这里暂不处理 ShowNameList 的全量 Member 同步，避免性能问题
                    // 将主要依赖专门的 Member 同步消息或按需获取
                }

                await dbContext.SaveChangesAsync();
                _logger.LogInformation("账号 {AccountId} 已保存 {New} 个新群聊，{Update} 个更新。", newCount, updateCount, accountId);
                
                // 通知 Web 端刷新
                await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("ChatRoomsUpdated", accountId);

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
            _logger.LogInformation("群成员通知：{WeChatId}, 成员数={Count}", notice.WeChatId, notice.Members.Count);
            
            // 待实现具体的成员同步逻辑
            // 需要明确 ChatRoomMembersNoticeMessage 是否包含 ChatRoomId 上下文
            // 目前暂做日志记录

            // 发送 ACK
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理好友检测统计通知
        /// 例如清粉任务的进度回报
        /// </summary>
        private async Task HandlePostFriendDetectCountNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<PostFriendDetectCountNoticeMessage>();
            _logger.LogInformation("好友检测计数通知：{WeChatId} 数量：{Count}", notice.WeChatId, notice.Count);
            
            // 发送 ACK
            await SendAckAsync(message, context);
        }



        /// <summary>
        /// 处理一键点赞任务结果通知 (4.22)
        /// </summary>
        private async Task HandleOneKeyLikeTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<OneKeyLikeTaskResultNoticeMessage>();
            _logger.LogInformation("一键点赞任务结果：TaskId={TaskId}, Count={Count}, EndType={EndType}", result.TaskId, result.Count, result.EndType);
            _clientTaskService.CompleteTask(result.TaskId, true, $"Count: {result.Count}, EndType: {result.EndType}");
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理联系人标签通知 (3.16)
        /// </summary>
        private async Task HandleContactLabelInfoNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ContactLabelInfoNoticeMessage>();
            _logger.LogInformation("收到联系人标签通知: WeChatId={WeChatId}, LabelCount={LabelCount}", notice.WeChatId, notice.Labels.Count);
            
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId)) return;

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                foreach (var label in notice.Labels)
                {
                    // 查找或更新标签
                    var existing = await dbContext.ContactTags
                        .FirstOrDefaultAsync(t => t.wechatAccountId == accountId && t.labelId == label.LabelId);
                    
                    if (existing != null)
                    {
                        existing.tagName = label.LabelName;
                        existing.updatedAt = DateTime.UtcNow;
                        existing.isDeleted = false; // 复活
                    }
                    else
                    {
                        var newTag = new ContactTag
                        {
                            wechatAccountId = accountId,
                            labelId = label.LabelId,
                            tagName = label.LabelName,
                            createdAt = DateTime.UtcNow,
                            updatedAt = DateTime.UtcNow,
                            isDeleted = false
                        };
                        dbContext.ContactTags.Add(newTag);
                    }
                }
                
                await dbContext.SaveChangesAsync();
            }
            
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
            // 修复：基于 proto 生成代码 'Conversations' -> 'Convers'
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
            // 修复：基于 proto 生成代码 'UserName' -> 'Username' (小写 'n')
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
            _logger.LogInformation("朋友圈详情通知：{WeChatId} - {CircleId} (作者：{Author})", 
                notice.WeChatId, circle.CircleId, circle.WeChatId);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId))
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
                    .FirstOrDefaultAsync(p => p.wechatAccountId == accountId && p.authorWxid == circle.WeChatId && p.publishTime == publishTime);

                if (post == null)
                {
                    post = new MomentsPost
                    {
                        wechatAccountId = (int)accountId,
                        authorWxid = circle.WeChatId,
                        publishTime = publishTime,
                        createdAt = DateTime.UtcNow,
                        isDeleted = false
                    };
                    dbContext.MomentsPosts.Add(post);
                }

                // 3. 更新内容字段
                if (circle.Content != null)
                {
                    post.postContent = circle.Content.Text ?? "";
                    
                    // 处理封面图 (取第一张图片或视频缩略图)
                    if (circle.Content.Images != null && circle.Content.Images.Count > 0)
                    {
                        post.postCover = circle.Content.Images[0].ThumbImg ?? circle.Content.Images[0].Url ?? "";
                        // 保存多图
                         post.imagesJson = System.Text.Json.JsonSerializer.Serialize(
                             circle.Content.Images.Select(i => i.Url).ToList()
                         );
                    }
                    else if (circle.Content.Video != null)
                    {
                        post.postCover = circle.Content.Video.ThumbImg ?? "";
                        post.videoUrl = circle.Content.Video.Url;
                    }
                     else if (circle.Content.Link != null) 
                    {
                        // 处理链接
                         post.linkInfoJson = System.Text.Json.JsonSerializer.Serialize(new {
                             Title = circle.Content.Link.Description,
                             Url = circle.Content.Link.Url,
                             Thumb = circle.Content.Link.ThumbImg
                         });
                    }
                }
                
                post.likeCount = circle.Likes.Count;
                post.commentCount = circle.Comments.Count;
                post.updatedAt = DateTime.UtcNow;

                // 先保存以获取 Post.Id
                await dbContext.SaveChangesAsync();

                // 4. 同步点赞 (先删后加，简化逻辑)
                var existingLikes = await dbContext.MomentsLikes.Where(l => l.postId == post.id).ToListAsync();
                dbContext.MomentsLikes.RemoveRange(existingLikes);

                foreach (var likeProto in circle.Likes)
                {
                    dbContext.MomentsLikes.Add(new MomentsLike
                    {
                        postId = post.id,
                        likerWxid = likeProto.FriendId,
                        likerNickname = likeProto.NickName ?? "",
                        likeTime = DateTimeOffset.FromUnixTimeSeconds(likeProto.PublishTime).UtcDateTime,
                        createdAt = DateTime.UtcNow
                    });
                }

                // 5. 同步评论 (先删后加)
                var existingComments = await dbContext.MomentsComments.Where(c => c.postId == post.id).ToListAsync();
                dbContext.MomentsComments.RemoveRange(existingComments);

                foreach (var cmtProto in circle.Comments)
                {
                    dbContext.MomentsComments.Add(new MomentsComment
                    {
                        postId = post.id,
                        commenterWxid = cmtProto.FromWeChatId,
                        weChatCommentId = cmtProto.CommentId, // 映射微信服务端的 CommentId
                        replyCommentId = cmtProto.ReplyCommentId, // 映射回复ID
                        commentContent = cmtProto.Content ?? "",
                        replyToWxid = cmtProto.ToWeChatId ?? "",
                        commentTime = DateTimeOffset.FromUnixTimeSeconds(cmtProto.PublishTime).UtcDateTime,
                        createdAt = DateTime.UtcNow
                    });
                }

                await dbContext.SaveChangesAsync();
                
                // 6. 前端通知
                await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("MomentReceived", post.authorWxid, post.postContent);
                await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("CircleDetailUpdated", notice.WeChatId, circle.CircleId);
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
            _logger.LogInformation("发朋友圈结果：成功={Success}, 任务Id={TaskId}", result.Success, result.TaskId);
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
            _logger.LogInformation("任务结果：成功={Success}, 任务Id={TaskId}, 关联Id={RefId}, 消息={ErrMsg}", result.Success, result.TaskId, message.RefMessageId, result.ErrMsg);
            _clientTaskService.CompleteTask(correlationId, result.Success, result.ErrMsg);

            // Publish Event for UI
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            await _eventBus.PublishAsync(new TaskResultReceivedEvent(correlationId, result.Success, result.ErrMsg, connectionId, connectionInfo?.deviceInfo ?? connectionId));

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理截屏任务结果
        /// </summary>
        private async Task HandleScreenShotTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<ScreenShotTaskResultNoticeMessage>();
            _logger.LogInformation("截屏结果：成功={Success}, Url={Url}", result.Success, result.Url);
            
             // 标记任务完成
            _clientTaskService.CompleteTask(result.TaskId, result.Success, result.Url);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo != null)
            {
                 // 如果需要，可以通过 UUID 通知前端 (为了兼容旧版)
                 await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("ScreenShotReceived", result.Url);
                 
                 // 发布 Reactive UI 事件
                 await _eventBus.PublishAsync(new TaskResultReceivedEvent(result.TaskId, result.Success, result.Url, connectionId, connectionInfo.deviceInfo ?? connectionId));
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
            try
            {
                var notice = message.Content.Unpack<PostDeviceInfoNoticeMessage>();
                _logger.LogInformation("上报设备信息：{Brand} {Model}, IMEI={Imei}", notice.PhoneBrand, notice.PhoneModel, notice.IMEI);

                // 从连接中解析客户端 UUID
                var connectionId = context.Channel.Id.AsLongText();
                var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
                
                if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId))
                {
                    _logger.LogWarning("收到来自未认证或未知连接的设备信息：{ConnectionId}", connectionId);
                    return;
                }

                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                    
                    // 查找 WechatAccount 以获取 ClientUuid (原子操作)
                    var account = await dbContext.GetWechatAccount(accountId);
                    if (account == null)
                    {
                        _logger.LogWarning("未找到 WechatAccount，Id：{AccountId}", accountId);
                        return;
                    }

                    // 1. Determine UUID logic (Prioritize existing, fallback to IMEI)
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
                            account.wechatNumber = clientUuid; // Optionally sync Number
                            needToBindAccount = true;
                        }
                    }

                    if (string.IsNullOrEmpty(clientUuid))
                    {
                        _logger.LogWarning("无法确定设备标识(UUID/IMEI)，AccountId：{AccountId}", account.accountId);
                        return;
                    }

                    // 2. Ensure SrClient Exists (Fix FK Constraint)
                    var client = await dbContext.GetSrClient(clientUuid);
                    if (client == null)
                    {
                        client = new SrClient
                        {
                            uuid = clientUuid,
                            createdAt = DateTime.UtcNow,
                            device = notice, // Init with data
                            tcpHost = "192.168.1.226", // TODO: Config
                            tcpPort = 8647
                        };
                        await dbContext.SaveSrClient(client); // Create Parent first
                        _logger.LogInformation("Created new SrClient {Uuid}", clientUuid);
                    }

                    // 3. Now Update Account Binding (if needed)
                    if (needToBindAccount)
                    {
                        account.clientUuid = clientUuid;
                        await dbContext.SaveWechatAccount(account);
                        _logger.LogInformation("Auto-binding Account {Id} to SrClient {Uuid}", account.accountId, clientUuid);
                    }

                    // 4. Update SrClient Details
                    client.device = notice;
                    client.ip = context.Channel.RemoteAddress.ToString();
                    client.lastLoginAt = DateTime.UtcNow;
                    client.isOnline = true;
                    client.updatedAt = DateTime.UtcNow;
                    client.connectionId = connectionId; 
                    
                    await dbContext.SaveSrClient(client);
                    _logger.LogInformation("更新 SrClient 信息，UUID：{Uuid}", clientUuid);

                    // 发送 ACK
                    await SendAckAsync(message, context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HandlePostDeviceInfoNotice 发生异常");
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
            _logger.LogInformation("好友列表推送：{WeChatId} 推送了 {Count} 个好友 (第 {Page}/{Size} 页)", 
                notice.WeChatId, notice.Friends.Count, notice.Page, notice.Size);

            // [DEBUG] 打印接收到的所有好友数据以进行验证
            for (int i = 0; i < notice.Friends.Count; i++)
            {
                var f = notice.Friends[i];
                _logger.LogInformation("[FriendSync] Index: {Index}, ID: {Id}, Nick: {Nick}, Remark: {Remark}, Memo: {Memo}, Desc: {Desc}",
                    i, f.FriendId, f.FriendNick, f.Remark, f.Memo, f.Desc);
            }
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId))
            {
                _logger.LogWarning("收到来自未认证或未知连接的好友推送：{ConnectionId}", connectionId);
                return;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                // 验证账号是否存在
                var account = await dbContext.WechatAccounts.FindAsync(accountId);
                if (account == null)
                {
                    _logger.LogWarning("未找到 AccountId 的 WechatAccount：{AccountId}", accountId);
                    return;
                }

                var contactsToSave = new List<Contact>();

                foreach (var friend in notice.Friends)
                {
                    // 将 Proto 映射到实体
                    var contact = new Contact
                    {
                        wechatAccountId = accountId,
                        wxid = friend.FriendId,
                        nickname = friend.FriendNick ?? "",
                        remarks = friend.Remark ?? "", // 映射 Remark(备注名) 到 Remarks
                        description = friend.Memo ?? "", // 映射 Memo(备注) 到 Description
                        avatar = friend.Avatar ?? "",
                        gender = (int)friend.Gender,
                        province = friend.Province ?? "",
                        city = friend.City ?? "",
                        phone = friend.Phone ?? "",
                        signature = friend.Desc ?? "",
                        source = friend.Source.ToString(),
                        labelIds = friend.LabelIds ?? "",
                        email = "",
                        country = "", // Proto 可能缺少 Country？如果是这样则留空
                        
                        contactType = 0, // 好友
                        isDeleted = false,
                        createdAt = DateTime.UtcNow,
                        updatedAt = DateTime.UtcNow
                    };
                    contactsToSave.Add(contact);
                }

                // [Fix] 1. 过滤无效 Wxid
                // [Fix] 2. 内存去重，防止 "ON CONFLICT DO UPDATE command cannot affect row a second time"
                contactsToSave = contactsToSave
                    .Where(c => !string.IsNullOrEmpty(c.wxid)) 
                    .GroupBy(c => c.wxid)
                    .Select(g => g.Last()) // 取最后一条（假设最后一条是最新的）
                    .ToList();

                if (contactsToSave.Count == 0)
                {
                    _logger.LogWarning("过滤后没有有效的联系人数据可保存 (Received: {Total}, Valid: 0)", notice.Friends.Count);
                }
                else
                {
                     _logger.LogInformation("[TRACE] 准备保存 {Count} 个联系人...", contactsToSave.Count);
                     await dbContext.SaveContacts(accountId, contactsToSave);
                     _logger.LogInformation("[TRACE] 保存联系人完成。");
                }
                
                _logger.LogInformation("处理好友推送完成：已同步 {Count} 个好友，账号 {AccountId}", contactsToSave.Count, accountId);

                // 通知 UI 刷新联系人列表 (使用外部 connectionInfo)
                try
                {
                    await _eventBus.PublishAsync(new ContactsReceivedEvent(connectionInfo.deviceInfo ?? connectionId, accountId, connectionInfo.userId));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "发布 ContactsReceivedEvent 失败");
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
            _logger.LogInformation("红包详情：{WeChatId} - {HbUrl} (发送者：{Sender}, 金额：{TotalAmount})", 
                notice.WeChatId, notice.HbUrl, notice.Sender, notice.TotalAmount);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId))
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
                    wechatAccountId = (int)accountId,
                    senderWxid = notice.Sender ?? "Unknown", // Proto Sender 可能是 Nickname? 暂存
                    totalAmount = notice.TotalAmount / 100m, // Proto 单位通常是分
                    totalCount = notice.TotalNum,
                    redPacketMessage = notice.Wishing ?? "", 
                    // 借用 TargetWxid 存储 HbUrl 以便未来可能的关联 (Hack)
                    targetWxid = notice.HbUrl, 
                    redPacketStatus = notice.HbStatus,
                    receivedCount = notice.RecNum,
                    receivedAmount = notice.RecAmount / 100m,
                    sendTime = DateTime.UtcNow, // 无法获取准确发送时间
                    createdAt = DateTime.UtcNow,
                    isDeleted = false
                };

                dbContext.RedPackets.Add(rp);
                await dbContext.SaveChangesAsync(); // 获取 id

                // 2. 存取领取记录 (RedPacketRecord)
                if (notice.Records != null)
                {
                    foreach (var rec in notice.Records)
                    {
                        var record = new RedPacketRecord
                        {
                            redPacketId = rp.id,
                            receiverWxid = rec.UserName,
                            receivedAmount = rec.Amount / 100m,
                            receiveTime = DateTime.TryParse(rec.Time, out var t) ? t : DateTime.UtcNow,
                            receiveStatus = 1, // 假设存在即已领
                            createdAt = DateTime.UtcNow
                        };
                        dbContext.RedPacketRecords.Add(record);
                    }
                    await dbContext.SaveChangesAsync();
                }

                _logger.LogInformation("保存红包：Id={Id}", rp.id);

                // 3. 前端通知
                await _hubContext.Clients.Group(connectionId).SendAsync("RedPacketReceived", rp.senderWxid, rp.totalAmount);
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
            _logger.LogInformation("红包状态：{WeChatId} - {HbUrl} (状态：{Status}, 消息：{Msg})", 
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
            _logger.LogInformation("删除好友通知：{WeChatId} 删除了好友 {FriendId}", notice.WeChatId, notice.FriendId);
            
            var connectionId = context.Channel.Id.AsLongText();
            await DeleteFriendAsync(connectionId, notice.FriendId, notice.WeChatId);
        }

        private async Task DeleteFriendAsync(string connectionId, string friendId, string weChatId)
        {
             try
            {
                var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
                if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId)) return;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                    
                    var contact = await dbContext.Contacts
                        .FirstOrDefaultAsync(c => c.wechatAccountId == accountId && c.wxid == friendId);
                        
                    if (contact != null)
                    {
                        contact.isDeleted = true;
                        contact.updatedAt = DateTime.UtcNow;
                        await dbContext.SaveChangesAsync();
                        
                        // Notify UI
                        await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("ContactsUpdated", accountId);
                        
                        _logger.LogInformation("账号 {AccountId} 已删除好友 {FriendId}", friendId, accountId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除好友失败 {FriendId}", friendId);
            }
        }
        
        /// <summary>
        /// 处理好友添加请求通知 (3.7)
        /// 保存请求详情到 FriendRequest 表
        /// </summary>
        private async Task HandleFriendAddReqeustNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendAddReqeustNoticeMessage>();
            _logger.LogInformation("收到好友验证请求：{WeChatId} 来自 {FriendNick} ({FriendId})", notice.WeChatId, notice.FriendNick, notice.FriendId);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId)) return;

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                // 1. 去重检查 (基于 RequestWxid + RequestMessage)
                var exists = await dbContext.FriendRequests
                    .AnyAsync(r => r.wechatAccountId == accountId && r.requestWxid == notice.FriendId && r.status == 0); // 0=未处理
                
                if (!exists)
                {
                    var request = new FriendRequest
                    {
                        wechatAccountId = accountId,
                        requestWxid = notice.FriendId,
                        nickname = notice.FriendNick,
                        avatar = notice.Avatar,
                        gender = (int)notice.Gender,
                        region = $"{notice.Province} {notice.City}".Trim(),
                        source = notice.Source.ToString(), // Source is int in proto
                        requestMessage = notice.Reason ?? "",
                        status = 0, // Pending
                        requestTime = DateTime.UtcNow,
                        createdAt = DateTime.UtcNow,
                        updatedAt = DateTime.UtcNow
                    };
                    
                    dbContext.FriendRequests.Add(request);
                    await dbContext.SaveChangesAsync();
                    
                    // 2. 通知前端
                    await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("FriendRequestReceived", request);
                }

                // 发布自动化事件 (兼容旧逻辑)
                var evt = new FriendRequestEvent(
                    notice.WeChatId, 
                    notice.FriendId, 
                    notice.FriendNick, 
                    notice.Reason ?? "", 
                    connectionId, 
                    accountId);
                await _eventBus.PublishAsync(evt);
            }

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理配置信息推送
        /// 打印日志
        /// </summary>
        private async Task HandleConfigPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ConfigPushNoticeMessage>();
            _logger.LogInformation("收到配置推送，来自 {WeChatId}", notice.WeChatId);
            
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
            _logger.LogInformation("发送 MsgReceivedAck，MsgType：{MsgType}，关联ID：{Id}", message.MsgType, message.Id);
        }



        /// <summary>
        /// 处理朋友圈新发布通知 (3.8)
        /// 1. 持久化到 MomentsTimeline
        /// 2. 通知 SignalR 前端
        /// 3. 发布 Autommation 事件
        /// </summary>
        private async Task HandleCircleNewPublishNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<CircleNewPublishNoticeMessage>();
            if (notice.Circle == null) return;

            _logger.LogInformation("朋友圈新发布通知：{WeChatId} 收到来自 {Author} 的新动态：{Content}", 
                notice.WeChatId, notice.Circle.WeChatId, notice.Circle.Content?.Text);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId))
            {
                return;
            }

            // 持久化 logic
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                // 1. 幂等性检查 (SnsId + OwnerWxid)
                var exists = await dbContext.MomentsTimelines
                    .AnyAsync(t => t.wechatAccountId == accountId && t.snsId == notice.Circle.CircleId);

                if (!exists)
                {
                    var timeline = new MomentsTimeline
                    {
                        wechatAccountId = accountId,
                        snsId = notice.Circle.CircleId,
                        userName = notice.Circle.WeChatId, // Author
                        nickName = "", // Proto 不带昵称，前端根据 UserName 查找联系人缓存
                        content = notice.Circle.Content?.Text ?? "",
                        createTime = notice.Circle.PublishTime,
                        receivedAt = DateTime.UtcNow.Ticks,
                        ownerWxid = notice.WeChatId,
                        imagesJson = "[]",
                        commentsJson = "[]",
                        likesJson = "[]"
                    };

                    // Map Media
                    if (notice.Circle.Content != null)
                    {
                        // Images
                        if (notice.Circle.Content.Images != null && notice.Circle.Content.Images.Count > 0)
                        {
                            timeline.imagesJson = System.Text.Json.JsonSerializer.Serialize(
                                notice.Circle.Content.Images.Select(i => i.Url).ToList()
                            );
                        }
                        
                        // Video
                        if (notice.Circle.Content.Video != null && !string.IsNullOrEmpty(notice.Circle.Content.Video.Url))
                        {
                            timeline.videoUrl = notice.Circle.Content.Video.Url;
                        }

                        // Link
                        if (notice.Circle.Content.Link != null && !string.IsNullOrEmpty(notice.Circle.Content.Link.Url))
                        {
                            timeline.linkInfoJson = System.Text.Json.JsonSerializer.Serialize(new {
                                Title = notice.Circle.Content.Link.Description,
                                Url = notice.Circle.Content.Link.Url,
                                Thumb = notice.Circle.Content.Link.ThumbImg
                            });
                        }
                        
                        // Ext (XML)
                        if (!string.IsNullOrEmpty(notice.Circle.Content.Ext))
                        {
                             timeline.xmlContent = notice.Circle.Content.Ext;
                        }
                    }

                    dbContext.MomentsTimelines.Add(timeline);
                    await dbContext.SaveChangesAsync();

                    // 2. 通知 SignalR 前端 (实时流)
                    var deviceGroup = connectionInfo.deviceInfo ?? connectionId;
                    var dto = new SCRM.SHARED.Models.Dtos.MomentsTimelineDto
                    {
                        snsId = timeline.snsId,
                        userName = timeline.userName,
                        nickName = timeline.nickName,
                        content = timeline.content,
                        createTime = timeline.createTime,
                        images = !string.IsNullOrEmpty(timeline.imagesJson) ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(timeline.imagesJson) ?? new List<string>() : new List<string>(),
                        videoUrl = timeline.videoUrl ?? string.Empty,
                        link = !string.IsNullOrEmpty(timeline.linkInfoJson) ? System.Text.Json.JsonSerializer.Deserialize<SCRM.SHARED.Models.Dtos.MomentLinkDto>(timeline.linkInfoJson) ?? new() : new(),
                        comments = !string.IsNullOrEmpty(timeline.commentsJson) ? System.Text.Json.JsonSerializer.Deserialize<List<SCRM.SHARED.Models.Dtos.MomentCommentDto>>(timeline.commentsJson) ?? new() : new(),
                        likes = !string.IsNullOrEmpty(timeline.likesJson) ? System.Text.Json.JsonSerializer.Deserialize<List<SCRM.SHARED.Models.Dtos.MomentLikeDto>>(timeline.likesJson) ?? new() : new()
                    };
                    await _hubContext.Clients.Group(deviceGroup).SendAsync("MomentTimelineReceived", dto);
                }
                
                // 3. 发布自动化事件 (Legacy support)
                var evt = new CircleNewPublishEvent(
                     notice.WeChatId,
                     notice.Circle.WeChatId,
                     notice.Circle.CircleId,
                     notice.Circle.Content?.Text ?? "",
                     connectionId,
                     accountId);
                await _eventBus.PublishAsync(evt);
            }

            await SendAckAsync(message, context);
        }

        // 已移除逻辑：此文件中不再需要 ExtractXmlValue。




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
                // 目前仅记录日志并 ACK 以抑制警告

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
            _logger.LogInformation("朋友圈推送通知：{WeChatId}，数量={Count}，页码={Page}", 
                notice.WeChatId, notice.Circles.Count, notice.Page);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId)) return;

            // 我们使用列表来收集 DTO，以便批量或单独发送
            // 理想情况下是批量，但客户端目前接收单个？我定义了 `OnMomentReceived` 接收单个项目。
            // 但 `GetMomentsTimelineAsync` 返回列表。
            // 让我们为实现实时 feed 效果而单独发送，或者修改客户端以接受列表。
            // 客户端：`OnMomentReceived` 接收 DTO。所以无效。
            // 如果数量多，我理想情况下应该发送列表。
            // 但 SignalR 允许在循环中调用客户端方法。
            
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                foreach (var circle in notice.Circles)
                {
                    long snsId = circle.CircleId;
                    
                    // 1. 检查是否存在
                    var exists = await dbContext.MomentsTimelines.AnyAsync(m => m.snsId == snsId);
                    if (!exists)
                    {
                        var entity = new MomentsTimeline
                        {
                            snsId = snsId,
                            wechatAccountId = accountId, // 确保赋值
                            userName = circle.WeChatId,
                            nickName = "", 
                            content = circle.Content?.Text ?? "",
                            createTime = circle.PublishTime,
                            receivedAt = DateTime.UtcNow.Ticks,
                            ownerWxid = notice.WeChatId,
                            
                            imagesJson = System.Text.Json.JsonSerializer.Serialize(
                                circle.Content?.Images.Select(i => i.ThumbImg).ToList() ?? new List<string>()
                            ),
                            commentsJson = System.Text.Json.JsonSerializer.Serialize(
                                circle.Comments.Select(c => new SCRM.SHARED.Models.Dtos.MomentCommentDto { authorName = c.FromName, content = c.Content }).ToList()
                            ),
                            likesJson = System.Text.Json.JsonSerializer.Serialize(
                                circle.Likes.Select(l => new SCRM.SHARED.Models.Dtos.MomentLikeDto { userName = l.FriendId, nickName = l.NickName }).ToList()
                            )
                        };
                        
                        dbContext.MomentsTimelines.Add(entity);
                    }
                    
                    // 2. 构建 DTO
                    var dto = new SCRM.SHARED.Models.Dtos.MomentsTimelineDto
                    {
                        snsId = snsId,
                        userName = circle.WeChatId,
                        nickName = "", // 占位符
                        content = circle.Content?.Text ?? "",
                        createTime = circle.PublishTime, // 秒
                        images = circle.Content?.Images.Select(i => i.ThumbImg).ToList() ?? new List<string>(),
                        comments = circle.Comments.Select(c => new SCRM.SHARED.Models.Dtos.MomentCommentDto { authorName = c.FromName, content = c.Content }).ToList(),
                        likes = circle.Likes.Select(l => new SCRM.SHARED.Models.Dtos.MomentLikeDto { userName = l.FriendId, nickName = l.NickName }).ToList()
                    };
                    
                    // 3. 发送给客户端
                    await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("MomentReceived", dto);
                }
                
                await dbContext.SaveChangesAsync();
            }
        }
    }
}



 