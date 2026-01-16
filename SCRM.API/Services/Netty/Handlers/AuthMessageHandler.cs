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

        public AuthMessageHandler(
            ILogger<AuthMessageHandler> logger,
            AuthService authService,
            ConnectionManager connectionManager,
            ApplicationDbContext dbContext,
            SCRM.UI.Services.ISystemConfigService configService) : base(logger)
        {
            _logger = logger;
            _authService = authService;
            _connectionManager = connectionManager;
            _dbContext = dbContext;
            _configService = configService;
        }

        public async Task HandleDeviceAuth(TransportMessage message, IChannelHandlerContext context)
        {
            var authReq = message.Content.Unpack<DeviceAuthReqMessage>();
            string credential = authReq.Credential;
            
            _logger.LogInformation("[业务鉴权] 收到设备认证请求 - Type: {AuthType}, 凭证: {Credential}, ChannelId: {ChannelId}", authReq.AuthType, credential, context.Channel.Id.AsLongText());

            // 因为是 Scoped Service，_db 已经是当前 Scope 所有的 Context
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

                    // 1. 验证 Token (优先强校验)
                    System.Security.Claims.ClaimsPrincipal? principal = _authService.ValidateToken(token);

                    // 如果强校验失败 (返回 null)，尝试忽略过期时间 (Emergency Fix)
                    if (principal == null)
                    {
                        // 注意：AuthService.ValidateToken 内部捕获了 SecurityTokenExpiredException 并返回 null
                        // 所以这里必须通过判空来触发重试
                        var expiredPrincipal = _authService.ValidateToken(token, validateLifetime: false);
                        if (expiredPrincipal != null)
                        {
                            _logger.LogWarning("Token 已过期但签名有效。允许登录 (AuthType=InternalCode)。");
                            principal = expiredPrincipal;
                        }
                    }

                    if (principal != null)
                    {
                        var userIdStr = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        var userName = principal.Identity?.Name;
                        
                        if (!string.IsNullOrEmpty(userIdStr))
                        {
                            _logger.LogInformation("Token 验证成功。User: {User} ({Id})", userName, userIdStr);

                            // 2. 查找或绑定设备
                            // 策略：优先找已绑定的，没绑定的自动绑定到该用户
                            account = await _dbContext.WechatAccounts
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
                                await _dbContext.WechatAccounts.AddAsync(account);
                                await _dbContext.SaveChangesAsync();
                            }

                            if (account != null)
                            {
                                if (string.IsNullOrEmpty(account.ownerId))
                                {
                                    account.ownerId = userIdStr; // Fix: Ensure ownerId is set if it was missing? (Though query above checked !isDeleted)
                                    // Actually original code Logic was weird here: if (string.IsNullOrEmpty(account.ownerId)) await SaveChangesAsync();
                                    // I'll keep it simple: if account found or created, we proceed.
                                    if (string.IsNullOrEmpty(account.ownerId))
                                    {
                                        account.ownerId = userIdStr;
                                        await _dbContext.SaveChangesAsync();
                                    }
                                }
                            }

                            // 3. Register Connection
                            if (account != null)
                            {
                                // public Task AddConnectionAsync(string userId, string connectionId, string deviceType, string deviceInfo = "")
                                await _connectionManager.AddConnectionAsync(account.ownerId, context.Channel.Id.AsLongText(), "WeChat", account.wechatNumber);
                            }

                            // 4. Send Response
                            var authResp = new TransportMessage
                            {
                                Id = 0,
                                MsgType = EnumMsgType.DeviceAuthRsp,
                                RefMessageId = message.Id,
                                Content = Any.Pack(new DeviceAuthRspMessage
                                {
                                    AccessToken = token // Assuming token is the AccessToken
                                })
                            };
                            await context.WriteAndFlushAsync(authResp);

                            // 5. [Fix] Push System Config immediately to ensure client has valid settings (e.g. keepWake)
                            await PushClientConfig(context);
                        }
                        else
                        {
                             base.Logger.LogWarning("Token 有效但 Identity 中缺少 NameIdentifier (UserId)");
                             await context.CloseAsync();
                        }
                    }
                    else
                    {
                        base.Logger.LogWarning("设备认证失败: Token 无效或过期且无法恢复。");
                        await context.CloseAsync();
                    }
                }
                else
                {
                    base.Logger.LogWarning("设备认证失败: 凭证格式错误 (期望 'Token|IMEI')");
                    await context.CloseAsync();
                }
            }
            // Add explicit handling for other AuthTypes if needed, or default fallback
            else
            {
                base.Logger.LogWarning("设备认证失败: 不支持的 AuthType {AuthType}", authReq.AuthType);
                await context.CloseAsync();
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

        public async Task HandlePostDeviceInfo(TransportMessage message, IChannelHandlerContext context)
        {
            // 设备信息上报 (简单处理，仅更新活动状态并回复ACK)
            // 实际业务逻辑可能需要解析 PostDeviceInfoNotice
            
            // 检查连接是否已认证
            if (!_connectionManager.IsConnected(context.Channel.Id.AsLongText())) 
            {
                return;
            }

            // 仅在已认证时更新活动状态
            await _connectionManager.UpdateConnectionActivityAsync(context.Channel.Id.AsLongText());

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
                var msg = new ConfigPushNoticeMessage();

                foreach (var config in configs)
                {
                    string key = config.key;
                    string value = config.value;

                    // 映射部分新旧Key兼容
                    if (key == "fileUploadUrl") key = "fileUpUrl"; // Client expects fileUpUrl?
                    if (key == "tcpServerPort") key = "server_port"; 
                    if (key == "httpApiBaseUrl") key = "apiBaseUrl";

                    // String Configs
                    if (key == "fileUpUrl" || key == "httpApiBaseUrl" || key == "apiBaseUrl" || key == "clientConfigPath" || 
                        key == "logLevel" || key == "autoUpdateUrl" || key == "fileUploadStorePath" || key == "fileUploadUrlPrefix" || key == "tcpServerHost" || key == "host")
                    {
                        msg.StrConfs.Add(new StrConfigMessage
                        {
                            Key = key,
                            Value = value,
                            Name = key,
                            Desc = config.description ?? "system config"
                        });
                    }
                    // Bool Configs
                    else if (key == "autoLogin" || key == "autoPic" || key == "silentFunc" || key == "forceRun")
                    {
                        if (bool.TryParse(value, out bool boolVal))
                        {
                            msg.BoolConfs.Add(new BoolConfigMessage
                            {
                                Key = key,
                                Value = boolVal,
                                Name = key,
                                Desc = config.description ?? "system config"
                            });
                        }
                    }
                    // Int Configs
                    else if (key == "keepWake" || key == "tcpServerPort" || key == "server_port" || key == "tokenExpiryMinutes")
                    {
                        if (int.TryParse(value, out int intVal))
                        {
                            msg.IntConfs.Add(new IntConfigMessage
                            {
                                Key = key,
                                Value = intVal,
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
                        MsgType = EnumMsgType.ConfigPushNotice,
                        Content = Any.Pack(msg)
                    };
                    await context.WriteAndFlushAsync(transMsg);
                    _logger.LogInformation("[配置推送] 初始化配置推送成功");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[配置推送] 初始化配置推送失败 {ChannelId}", context.Channel.Id.AsLongText());
            }
        }
    }
}
