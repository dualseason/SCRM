using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Netty.Handlers.Abstractions;
using SCRM.API.Services.Core;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.Extensions.Logging;
using SCRM.API.Services;
using SCRM.Services.Data;
using SCRM.SHARED.Models;
using System;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using SCRM.API.Services.Netty.Handlers.Abstractions;
using SCRM.API.Services.Core;
using SCRM.Services.Events;
using SCRM.API.Models.Events;
using SCRM.API.Services.Data;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 认证消息处理器
    /// <para>负责处理客户端连接的建立、鉴权与心跳维持。</para>
    /// <para>核心功能：</para>
    /// <list type="bullet">
    /// <item>设备鉴权 (HandleDeviceAuth): 验证 Token 或 IMEI，建立 ConnectionId 映射</item>
    /// <item>心跳维持 (HandleHeartBeat): 更新连接活跃时间</item>
    /// <item>设备信息上报 (HandlePostDeviceInfo): 补充设备基础信息 (Deprecated)</item>
    /// </list>
    /// <para>Scoped Service: 每个 Channel 请求可能会创建新的 Scope (如果 MessageRouter 也是 Scoped 或 Transient)</para>
    /// </summary>
    public class AuthMessageHandler : MessageHandlerBase
    {
        private readonly ILogger<AuthMessageHandler> _logger;
        private readonly AuthService _authService;
        private readonly ConnectionManager _connectionManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly SCRM.UI.Services.ISystemConfigService _configService;
        private readonly IEventBus _eventBus;

        public AuthMessageHandler(
            ILogger<AuthMessageHandler> logger,
            AuthService authService,
            ConnectionManager connectionManager,
            ApplicationDbContext dbContext,
            SCRM.UI.Services.ISystemConfigService configService,
            IEventBus eventBus) : base(logger)
        {
            _logger = logger;
            _authService = authService;
            _connectionManager = connectionManager;
            _dbContext = dbContext;
            _configService = configService;
            _eventBus = eventBus;
        }

        public async Task HandleDeviceAuth(TransportMessage message, IChannelHandlerContext context)
        {
            var authReq = message.Content.Unpack<DeviceAuthReqMessage>();
            string credential = authReq.Credential?.Trim();

            _logger.LogInformation("[业务鉴权] 收到设备认证请求 - Type: {AuthType}, ChannelId: {ChannelId}", authReq.AuthType, context.Channel.Id.AsLongText());

            if (authReq.AuthType != DeviceAuthReqMessage.Types.EnumAuthType.InternalCode)
            {
                _logger.LogWarning("鉴权失败: 不支持的 AuthType {AuthType}", authReq.AuthType);
                await context.CloseAsync();
                return;
            }

            if (string.IsNullOrEmpty(credential))
            {
                _logger.LogWarning("鉴权失败: 凭证为空。");
                await context.CloseAsync();
                return;
            }

            // 1. 验证 JWT 令牌
            System.Security.Claims.ClaimsPrincipal? principal = null;
            try
            {
                principal = _authService.ValidateToken(credential);
                if (principal == null)
                {
                    // 尝试过期的令牌但签名必须有效
                    principal = _authService.ValidateToken(credential, validateLifetime: false);
                    if (principal != null)
                    {
                        _logger.LogWarning("令牌已过期但签名有效。允许登录以刷新连接。");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("令牌校验过程中发生异常: {Message}。凭据预览: {Preview}", ex.Message, 
                    credential.Length > 20 ? credential.Substring(0, 20) + "..." : credential);
            }

            if (principal == null)
            {
                _logger.LogWarning("[安全审计] 无效、过期或破坏的令牌尝试连接。已拒绝。");
                await context.CloseAsync();
                return;
            }

            // 2. 提取身份信息
            var userIdStr = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = principal.Identity?.Name ?? "DeviceUser";
            var deviceUuid = principal.FindFirst("device_uuid")?.Value;

            if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(deviceUuid))
            {
                _logger.LogWarning("[安全审计] 令牌缺失关键声明 (User: {User}, Device: {Device})。已拒绝。", userIdStr, deviceUuid);
                await context.CloseAsync();
                return;
            }

            _logger.LogInformation("鉴权成功。用户: {User} ({Id}), 设备: {Device}", userName, userIdStr, deviceUuid);

                // 3. 确保账号记录存在
            var account = await _dbContext.WechatAccounts
                .FirstOrDefaultAsync(u => u.clientUuid == deviceUuid && !u.isDeleted);

            if (account == null)
            {
                _logger.LogInformation("首次连接: 为设备 {Device} 创建影子账号声明并绑定至用户 {User}", deviceUuid, userName);
                account = new WechatAccount 
                { 
                    wechatNumber = deviceUuid, // 默认使用 UUID 作为内部识别码
                    clientUuid = deviceUuid,
                    ownerId = userIdStr, 
                    isActive = true,
                    createdAt = DateTime.UtcNow,
                    vipExpiryDate = DateTime.UtcNow.AddYears(1)
                };
                // await _dbContext.WechatAccounts.AddAsync(account);
                // Fix: 立即持久化并同步缓存
                await DbHelper.SaveWechatAccount(_dbContext, account);
            }

            // 4. 确保 SrClient 记录并更新连接状态
            //var srClient = await _dbContext.SrClients.FirstOrDefaultAsync(c => c.uuid == deviceUuid);
            var srClient = await DbHelper.GetSrClient(_dbContext, deviceUuid);

            if (srClient == null)
            {
                srClient = new SrClient { uuid = deviceUuid, ownerId = userIdStr, createdAt = DateTime.UtcNow, isOnline = true };
                //await _dbContext.SrClients.AddAsync(srClient);
                // await DbHelper.SaveSrClient(_dbContext,srClient);

            }
            else
            {
                srClient.isOnline = true;
                // 注意：由于是严格 JWT，令牌中的归属关系具有最高优先级，更新数据库以保持同步
                srClient.ownerId = userIdStr; 
            }

            // [Optimization] 统一更新 SrClient 属性 (在线状态 + 归属人 + 登录记录)
            if (account != null && !string.IsNullOrEmpty(account.wxid))
            {
                if (!srClient.loggedInWeChatIds.Contains(account.wxid))
                {
                    srClient.loggedInWeChatIds.Add(account.wxid);
                }
                srClient.wx = new Wx { wechatAccount = account, srClient = srClient };
            }

            // Fix: 无论后续逻辑如何，这里必须先保存一次 SrClient 的基础状态
            // 这样可以确保 "在线" 状态被持久化
            await DbHelper.SaveSrClient(_dbContext,srClient);

            //await _dbContext.SaveChangesAsync();
            await _connectionManager.AddConnectionAsync(userIdStr, context.Channel.Id.AsLongText(), "WeChat", deviceUuid);

            // 5. 发送认证成功响应及初始化配置
            // Extra.Token 用于初始化客户端的 currentWeChatId。
            // 优先使用 wxid，若为空（首次连接且未登录微信）则使用 deviceUuid 作为占位符，以通过客户端的“已登录”拦截检查。
            var extraMsg = new DeviceAuthRspMessage.Types.ExtraMessage
            {
                Token = !string.IsNullOrEmpty(account?.wxid) ? account.wxid : deviceUuid 
            };

            var authResp = new TransportMessage
            {
                Id = 0,
                MsgType = EnumMsgType.DeviceAuthRsp,
                RefMessageId = message.Id,
                Content = Any.Pack(new DeviceAuthRspMessage 
                { 
                    AccessToken = credential,
                    Extra = extraMsg
                }) // 返回原始 Token 及识别码
            };
            await context.WriteAndFlushAsync(authResp);
            await PushClientConfig(context);    //下发初始化配置

            // 下发 联系人同步 (如果微信已登录)
            if (account != null && !string.IsNullOrEmpty(account.wxid))
            {
                _logger.LogInformation("[业务同步] 微信已登录 ({WxId})，自动触发联系人全量同步指令...", account.wxid);
                try 
                {
                    var syncMsg = new TransportMessage
                    {
                        Id = 0,
                        MsgType = EnumMsgType.TriggerFriendPushTask,
                        RefMessageId = 0,
                        Content = Any.Pack(new TriggerFriendPushTaskMessage
                        {
                            WeChatId = account.wxid,
                            TaskId = DateTime.Now.Ticks
                        })
                    };
                    await context.WriteAndFlushAsync(syncMsg);

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "自动触发联系人同步失败");
                }
            }
        }

        public async Task HandleHeartBeat(TransportMessage message, IChannelHandlerContext context)
        {
            // 心跳处理逻辑
            // Logger.LogDebug("收到心跳: {ConnId}", context.Channel.Id); // 减少日志噪音
            
            await _connectionManager.UpdateConnectionActivityAsync(context.Channel.Id.AsLongText());

            var pong = new TransportMessage
            {
                Id = 0,
                MsgType = EnumMsgType.HeartBeatReq, // 保持与客户端协议一致 (通常是用 Req 作为 Pong 或者有专门的 Pong 类型，这里沿用旧逻辑)
                RefMessageId = message.Id
            };
            await context.WriteAndFlushAsync(pong);
        }

        public async Task HandlePhoneStateWarning(TransportMessage message, IChannelHandlerContext context)
        {
            try
            {
                var warningMsg = message.Content.Unpack<PhoneStateWarningNoticeMessage>(); // 1053 uses PhoneStateWarningNoticeMessage
                var connId = context.Channel.Id.AsLongText();
                if (!string.IsNullOrEmpty(warningMsg.WeChatId))
                {
                    _logger.LogInformation("收到设备状态告警/上报: WeChatId={WeChatId}, IMEI={IMEI}, Net={Net}", warningMsg.WeChatId, warningMsg.Imei, warningMsg.NetType);
                    _logger.LogInformation("PhoneStateWarning Detail: {Detail}", System.Text.Json.JsonSerializer.Serialize(warningMsg));
                    
                    // 尝试从数据库获取昵称
                    string nickName = "";
                    var account = await _dbContext.WechatAccounts.FirstOrDefaultAsync(u => u.wxid == warningMsg.WeChatId);
                    if (account != null)
                    {
                        nickName = account.nickname ?? "";
                    }

                    await _connectionManager.UpdateConnectionWeChatInfoAsync(connId, warningMsg.WeChatId, nickName);
                    
                    var connInfo = await _connectionManager.GetConnectionAsync(connId);
                    if (connInfo != null)
                    {
                        await _eventBus.PublishAsync(new DeviceStatusChangedEvent(connInfo.deviceUuid, true));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解析 PhoneStateWarningNotice 失败");
            }
        }

        public async Task HandlePostDeviceInfo(TransportMessage message, IChannelHandlerContext context)
        {
             // 检查连接是否已认证
            var connId = context.Channel.Id.AsLongText();
            if (!_connectionManager.IsConnected(connId)) 
            {
                return;
            }

            try 
            {
                var infoMsg = message.Content.Unpack<PostDeviceInfoNoticeMessage>();
                if (!string.IsNullOrEmpty(infoMsg.WeChatId))
                {
                    _logger.LogInformation("收到设备信息上报: WeChatId={WeChatId}", infoMsg.WeChatId);
                    _logger.LogInformation("PostDeviceInfo Detail: {Detail}", System.Text.Json.JsonSerializer.Serialize(infoMsg));
                    
                    // 尝试从数据库获取昵称
                    string nickName = "";
                    var account = await _dbContext.WechatAccounts.FirstOrDefaultAsync(u => u.wxid == infoMsg.WeChatId);
                    if (account != null)
                    {
                        nickName = account.nickname ?? "";
                    }

                    await _connectionManager.UpdateConnectionWeChatInfoAsync(connId, infoMsg.WeChatId, nickName);
                    
                    // 获取 DeviceUuid 并发布状态变更事件以刷新 UI
                    var connInfo = await _connectionManager.GetConnectionAsync(connId);
                    if (connInfo != null)
                    {
                        await _eventBus.PublishAsync(new DeviceStatusChangedEvent(connInfo.deviceUuid, true));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解析 PostDeviceInfoNotice 失败");
            }

            // 仅在已认证时更新活动状态
            await _connectionManager.UpdateConnectionActivityAsync(connId);

            // 发送 ACK
            var response = new TransportMessage
            {
                Id = 0,
                MsgType = EnumMsgType.MsgReceivedAck,
                RefMessageId = message.Id
            };
            
            await context.WriteAndFlushAsync(response);
        }

        private async Task PushClientConfig(IChannelHandlerContext context)
        {
            try
            {
                // 获取当前最新配置
                var configs = await _configService.GetConfigsAsync();
                var msg = new SetConfigTaskMessage();

                foreach (var config in configs)
                {
                    string key = config.key;
                    string value = config.value;

                    // 1. Key Mapping (Retro-compatibility)
                    // fileUploadUrl -> fileUpUrl for client
                    if (key == "fileUploadUrl") key = "fileUpUrl";
                    if (key == "tcpServerPort") key = "server_port"; // map port
                    if (key == "httpApiBaseUrl") key = "apiBaseUrl";

                    // 2. Type Inference & Grouping
                    // Boolean Configs
                    if (bool.TryParse(value, out bool boolVal))
                    {
                        msg.BoolConfs.Add(new BoolConfigMessage
                        {
                            Key = key,
                            Value = boolVal,
                            Name = key, // Or map to friendly name if needed
                            Desc = config.description ?? "system config"
                        });
                    }
                    // Integer Configs
                    else if (int.TryParse(value, out int intVal))
                    {
                        msg.IntConfs.Add(new IntConfigMessage
                        {
                            Key = key,
                            Value = intVal,
                            Name = key,
                            Desc = config.description ?? "system config"
                        });
                    }
                    // String Configs (Default)
                    else
                    {
                        // Exclude sensitive or internal keys if necessary (e.g. jwt keys), but user asked for "All"
                        // Filtering out JWT keys just in case, though they are safe on server
                        if (!key.StartsWith("jwt")) 
                        {
                            msg.StrConfs.Add(new StrConfigMessage
                            {
                                Key = key,
                                Value = value,
                                Name = key,
                                Desc = config.description ?? "system config"
                            });
                        }
                    }
                }

                if (msg.StrConfs.Count > 0 || msg.BoolConfs.Count > 0 || msg.IntConfs.Count > 0)
                {
                    _logger.LogInformation("[配置推送] 正在向终端 {ChannelId} 推送初始化配置 (共 {Count} 项)...", context.Channel.Id.AsLongText(), msg.StrConfs.Count + msg.BoolConfs.Count + msg.IntConfs.Count);
                    
                    
                    var transMsg = new TransportMessage 
                    {
                        Id = 0,
                        MsgType = EnumMsgType.SetConfigTask, // Changed from ConfigPushNotice(1381) to SetConfigTask(1382)
                        Content = Any.Pack(msg)
                    };
                    await context.WriteAndFlushAsync(transMsg);
                    _logger.LogInformation("[配置推送] 初始化配置推送成功 (指令: SetConfigTask)");
                    
                    // 主动请求设备上报信息 (TriggerDeviceInfo)
                    var triggerMsg = new TransportMessage
                    {
                        Id = 0,
                        MsgType = EnumMsgType.TriggerDeviceInfo,
                        Content = Any.Pack(new Empty())
                    };
                    await context.WriteAndFlushAsync(triggerMsg);
                    _logger.LogInformation("[业务同步] 已发送 TriggerDeviceInfo 指令，请求设备上报状态");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[配置推送] 初始化配置推送失败 {ChannelId}", context.Channel.Id.AsLongText());
            }
        }
    }
}
