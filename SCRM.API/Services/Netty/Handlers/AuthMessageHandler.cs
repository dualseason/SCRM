using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Models.Entities;
using SCRM.API.Services;
using SCRM.Services;
using SCRM.Services.Data;
using SCRM.SHARED.Models;
using System;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 处理鉴权、心跳等认证相关消息
    /// Scoped Service
    /// </summary>
    public class AuthMessageHandler
    {
        private readonly ILogger<AuthMessageHandler> _logger;
        private readonly AuthService _authService;
        private readonly ConnectionManager _connectionManager;
        private readonly ApplicationDbContext _dbContext;

        public AuthMessageHandler(
            ILogger<AuthMessageHandler> logger,
            AuthService authService,
            ConnectionManager connectionManager,
            ApplicationDbContext dbContext)
        {
            _logger = logger;
            _authService = authService;
            _connectionManager = connectionManager;
            _dbContext = dbContext;
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

                    // 1. 验证 Token (强校验)
                    System.Security.Claims.ClaimsPrincipal? principal = null;
                    try 
                    {
                        principal = _authService.ValidateToken(token);
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
                                    account.ownerId = userIdStr;
                                    await _dbContext.SaveChangesAsync();
                                    _logger.LogInformation("设备 {IMEI} 已自动归属给用户 {User}", imei, userName);
                                }
                                else if (account.ownerId != userIdStr)
                                {
                                    // 允许管理员或同一用户的不同设备？暂且严格检查
                                    // 如果只是单纯的 Warning，可能会导致 account 被赋值但无法连接？
                                    if (account.ownerId != userIdStr) 
                                    {
                                        _logger.LogWarning("设备归属不匹配: 设备归属 {Owner}, 尝试登录用户 {User}", account.ownerId, userIdStr);
                                        // 决定：不拒绝，或者拒绝？
                                        // 目前逻辑：如果不匹配，这里只是打印日志，Account 依然有效，后续会 RegisterConnection
                                        // 这意味着 "借用手机" 场景是被允许的？或者这只是 Token 验证层面的
                                    }
                                }
                            }
                        }
                    }
                }
            } // End InternalCode Check

            if (account != null)
            {
                // 3. 注册连接
                // 将 Netty Connection 映射到 DeviceUuid，并关联 ConnectionId (SignalR)
                var deviceUuid = account.wechatNumber; // 使用微信号/IMEI作为唯一标识
                
                // 注意：RegisterConnection 需要 ConnectionId (SignalR) 和 DeviceUuid 
                // 但这里是 Netty Handler，Context.Channel.Id 是 Netty 的 Id
                // 现在的架构通过 ConnectionManager 桥接了 SignalR ID 和 Netty Channel
                
                // 注册 Netty 通道
                _connectionManager.RegisterChannel(deviceUuid, context.Channel);
                _logger.LogInformation("设备 {DeviceUuid} 已注册 Netty 通道: {ChannelId}", deviceUuid, context.Channel.Id.AsLongText());

                // 4. 返回认证成功响应 (1011)
                var rsp = new DeviceAuthRspMessage
                {
                   // 可以根据需要填充
                };

                var responseMsg = new TransportMessage
                {
                    Id = message.Id, // 回应对应请求ID
                    MsgType = EnumMsgType.DeviceAuthRsp,
                    RefMessageId = message.Id,
                    Content = Any.Pack(rsp)
                };

                await context.WriteAndFlushAsync(responseMsg);
                _logger.LogInformation("已回复设备认证响应 (1011) 给 {DeviceUuid}", deviceUuid);

                // 5. 触发初始化流程 (可选: 发送好友列表、群列表等)
                // 这里可以是 EventBus 发布 DeviceConnectedEvent，由其他 Service 负责推送初始化任务
                // 保持本次重构范围最小化，暂不展开
            }
            else
            {
                 _logger.LogWarning("设备认证失败: 未能识别设备或 Token 无效. Credential: {Credential}", credential);
                 // 发送 Error Response?
            }
        }

        public async Task HandleHeartBeat(TransportMessage message, IChannelHandlerContext context)
        {
            var connId = context.Channel.Id.AsLongText();
            _logger.LogDebug("收到心跳包，来自 {RemoteAddress}，连接ID：{ConnectionId}", context.Channel.RemoteAddress, connId);
            
            // 检查连接是否已认证（是否映射到用户）
            bool isAuthenticated = await _connectionManager.IsConnectionAuthenticatedAsync(connId);

            if (!isAuthenticated)
            {
                _logger.LogWarning("收到未认证连接的心跳包 {ConnectionId}。发送强制下线通知。", connId);
                
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

        private async Task SendAckAsync(TransportMessage message, IChannelHandlerContext context)
        {
             var ack = new MsgReceivedAckMessage
             {
                 Id = message.Id
             };
             
             var response = new TransportMessage
             {
                 Id = 0,
                 MsgType = EnumMsgType.MsgReceivedAck,
                 RefMessageId = message.Id,
                 Content = Any.Pack(ack)
             };
             
             await context.WriteAndFlushAsync(response);
        }
    }
}
