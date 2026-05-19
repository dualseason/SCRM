using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Netty.Handlers.Abstractions;
using SCRM.API.Services.Core;
using SCRM.API.Services;
using SCRM.Services.Data;
using SCRM.SHARED.Models;
using System;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using SCRM.Services.Events;
using SCRM.SHARED.Models.Events;
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
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public AuthMessageHandler(
            ILogger<AuthMessageHandler> logger,
            AuthService authService,
            ConnectionManager connectionManager,
            ApplicationDbContext dbContext,
            SCRM.UI.Services.ISystemConfigService configService,
            IEventBus eventBus,
            Microsoft.Extensions.Configuration.IConfiguration configuration) : base(logger)
        {
            _logger = logger;
            _authService = authService;
            _connectionManager = connectionManager;
            _dbContext = dbContext;
            _configService = configService;
            _eventBus = eventBus;
            _configuration = configuration;
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

            // 3. [Fix] 彻底移除历史账号查找逻辑
            // 之前的逻辑会查找并自动关联历史 WechatAccount，导致"假在线"。
            // 现在我们完全忽略历史状态，一切以客户端实时上报为准。
            // WechatAccount account = null; // Do not fetch from DB

            // 4. 确保 SrClient 记录并更新连接状态
            var srClient = await DbHelper.GetSrClient(_dbContext, deviceUuid);

            if (srClient == null)
            {
                srClient = new SrClient { uuid = deviceUuid, ownerId = userIdStr, createdAt = DateTime.UtcNow, isOnline = true };
            }
            else
            {
                srClient.isOnline = true;
                srClient.ownerId = userIdStr;
                // [Fix] 清空历史关联，等待客户端上报
                // srClient.loggedInWeChatIds.Clear(); // Optional: Do we want to clear history? Maybe not.
                srClient.wx = null; // Ensure no current wx session is assumed
            }

            // Fix: 无论后续逻辑如何，这里必须先保存一次 SrClient 的基础状态
            await DbHelper.SaveSrClient(_dbContext, srClient);

            await _connectionManager.AddConnectionAsync(userIdStr, context.Channel.Id.AsLongText(), "WeChat", deviceUuid);

            // 5. 发送认证成功响应及初始化配置
            // [Fix] 由于不查库，直接使用 deviceUuid 作为 Token
            // 客户端会使用此 Token 进行后续通信，只要非空即可。
            var extraMsg = new DeviceAuthRspMessage.Types.ExtraMessage
            {
                Token = deviceUuid 
            };

            var authResp = new TransportMessage
            {
                Id = DateTime.UtcNow.Ticks,
                MsgType = EnumMsgType.DeviceAuthRsp,
                RefMessageId = message.Id,
                Content = Any.Pack(new DeviceAuthRspMessage
                {
                    AccessToken = credential,
                    Extra = extraMsg
                }) 
            };
            await context.WriteAndFlushAsync(authResp);

            // 6. 轻量查询当前微信账号状态。
            // 说明：
            // - TriggerWechatPushTask 仍保留，用于触发安卓端/微信侧主链上报 WeChatOnlineNotice。
            // - GetWeChatsReq(3050) 是 62203 账号状态查询口径，安卓端无需拉起 UI 即可返回当前内存态账号。
            // - 两者并行可以降低服务端重启或微信侧冷却窗口内 Web 端短暂显示“微信未登录”的概率。
            try
            {
                var getWeChatsMsg = new TransportMessage
                {
                    Id = DateTime.UtcNow.Ticks,
                    MsgType = EnumMsgType.GetWeChatsReq,
                    Content = Any.Pack(new GetWeChatsReqMessage
                    {
                        UnionId = 0,
                        AccountType = EnumAccountType.Main
                    })
                };
                await context.WriteAndFlushAsync(getWeChatsMsg);
                _logger.LogInformation("下发微信账号状态查询指令(GetWeChatsReq 3050)...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下发微信账号状态查询指令失败");
            }
            
            // 7. 下发 获取wx信息 (强制查询)
            // [Fix] 不再依赖 account.wxid，直接下发空 ID，要求客户端汇报当前状态
            _logger.LogInformation("下发获取微信信息指令(Blank ID)...");
            try
            {
                var syncMsg = new TransportMessage
                {
                    Id = DateTime.UtcNow.Ticks,
                    MsgType = EnumMsgType.TriggerWechatPushTask,
                    Content = Any.Pack(new  TriggerWechatPushTaskMessage
                    {
                        WeChatId = "" // Empty ID force client to report self
                    })
                };
                await context.WriteAndFlushAsync(syncMsg);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下发获取wx信息失败");
            }

            // [Fix] 移除 TriggerFriendPushTask
            // 现在由 SystemMessageHandler 在收到 WeChatOnlineNotice 后触发
        }

        public async Task HandleHeartBeat(TransportMessage message, IChannelHandlerContext context)
        {
            // 心跳处理逻辑
            // 这里只更新连接活跃时间，不再把客户端 HeartBeatReq 原样回发给客户端。
            // 原因：
            // 1. 客户端收到 HeartBeatReq 会立即调用 sendHeartbeat(true) 再发一个 HeartBeatReq。
            // 2. 如果服务端也把客户端心跳回成 HeartBeatReq，就会形成 Req -> Req 的 ping-pong 死循环。
            // 3. Web/控制面主动下发 HeartBeatReq 时，客户端仍会回一个 HeartBeatReq 作为 ACK，
            //    服务端在这里更新活跃时间即可，不需要继续回包。
            await _connectionManager.UpdateConnectionActivityAsync(context.Channel.Id.AsLongText());
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
                var connInfo = await _connectionManager.GetConnectionAsync(connId);
                if (!string.IsNullOrEmpty(infoMsg.WeChatId))
                {
                    _logger.LogInformation("收到设备信息上报: WeChatId={WeChatId}", infoMsg.WeChatId);
                    _logger.LogInformation("PostDeviceInfo Detail: {Detail}", System.Text.Json.JsonSerializer.Serialize(infoMsg));

                    // PostDeviceInfoNotice 不包含昵称信息，此处仅更新设备在线状态 (传递 null 以保持原昵称不变)
                    await _connectionManager.UpdateConnectionWeChatInfoAsync(connId, infoMsg.WeChatId);
                }

                if (connInfo == null || string.IsNullOrWhiteSpace(connInfo.deviceUuid))
                {
                    _logger.LogWarning(
                        "PostDeviceInfoNotice 已收到但连接缺少 deviceUuid，无法落库。ConnectionId={ConnectionId}, WeChatId={WeChatId}",
                        connId,
                        infoMsg.WeChatId);
                }
                else
                {
                    var srClient = await DbHelper.GetSrClient(_dbContext, connInfo.deviceUuid);
                    if (srClient == null)
                    {
                        srClient = new SrClient
                        {
                            uuid = connInfo.deviceUuid,
                            ownerId = connInfo.userId,
                            createdAt = DateTime.UtcNow,
                            status = 1
                        };
                    }

                    // PostDeviceInfoNotice 是安卓端完整设备快照，直接落到 SrClient.device，
                    // 供 Web 端展示机型、系统版本、IMEI、应用列表、Hook/WxSupport 等信息。
                    srClient.device = infoMsg.Clone();
                    srClient.isOnline = true;
                    srClient.status = srClient.status == 0 ? 1 : srClient.status;
                    srClient.connectionId = connId;
                    srClient.ownerId = string.IsNullOrWhiteSpace(srClient.ownerId) ? connInfo.userId : srClient.ownerId;
                    srClient.lastLoginAt = DateTime.UtcNow;
                    srClient.updatedAt = DateTime.UtcNow;
                    srClient.ip = context.Channel.RemoteAddress?.ToString() ?? srClient.ip;

                    await DbHelper.SaveSrClient(_dbContext, srClient);

                    _logger.LogInformation(
                        "PostDeviceInfoNotice 已落库: DeviceUuid={DeviceUuid}, WeChatId={WeChatId}, Brand={PhoneBrand}, Model={PhoneModel}, OS={OSVerNumber}, IMEI={IMEI}, AppCount={AppCount}, IsHook={IsHook}, WxSupport={WxSupport}",
                        srClient.uuid,
                        infoMsg.WeChatId,
                        infoMsg.PhoneBrand,
                        infoMsg.PhoneModel,
                        infoMsg.OSVerNumber,
                        infoMsg.IMEI,
                        infoMsg.AppInfos.Count,
                        infoMsg.IsHook,
                        infoMsg.WxSupport);

                    await _eventBus.PublishAsync(new DeviceStatusChangedEvent(connInfo.deviceUuid, true));
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

                // --- Configs (Source: Database) ---
                // All client behavior, including where to connect (Host/Port), is managed via the UI (Database).
                // This allows flexibility (e.g. mapping external IPs, ports) without restarting the server.
                
                foreach (var config in configs)
                {
                    string key = config.key;
                    string value = config.value;

                    // MAPPING: TCP Connection Configs (DB -> Proto)
                    // These are "Android Settings" as requested, allowing dynamic modification via UI.
                    if (key == "tcpServerHost")
                    {
                        msg.StrConfs.Add(new StrConfigMessage 
                        { 
                            Key = "host", // Client expects "host"
                            Value = value, // STRICTLY use DB value
                            Name = key, 
                            Desc = config.description ?? "TCP Host" 
                        });
                        continue;
                    }

                    if (key == "tcpServerPort")
                    {
                        // Use default 8647 if parsing fails, but try DB value first
                        int portVal = 42719;
                        int.TryParse(value, out portVal);

                        msg.IntConfs.Add(new IntConfigMessage 
                        { 
                            Key = "port", // Client expects "port"
                            Value = portVal,
                            Name = key, 
                            Desc = config.description ?? "TCP Port" 
                        });
                        continue;
                    }

                    // MAPPING: Legacy Key Support
                    if (key == "fileUploadUrl") key = "fileUpUrl";
                    if (key == "httpApiBaseUrl") key = "apiBaseUrl";

                    // PARSING: Type Inference
                    if (bool.TryParse(value, out bool boolVal))
                    {
                        msg.BoolConfs.Add(new BoolConfigMessage
                        {
                            Key = key,
                            Value = boolVal,
                            Name = key,
                            Desc = config.description ?? ""
                        });
                    }
                    else if (int.TryParse(value, out int intVal))
                    {
                        msg.IntConfs.Add(new IntConfigMessage
                        {
                            Key = key,
                            Value = intVal,
                            Name = key,
                            Desc = config.description ?? ""
                        });
                    }
                    else
                    {
                        // Default to String
                        if (!key.StartsWith("jwt")) // Safety check
                        {
                            msg.StrConfs.Add(new StrConfigMessage
                            {
                                Key = key,
                                Value = value,
                                Name = key,
                                Desc = config.description ?? ""
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
                }

                // 主动请求设备上报信息 (TriggerDeviceInfo)。
                // 设备快照上报不应依赖配置项是否存在；即使初始化配置为空，
                // 也要让 Android 端补发 PostDeviceInfoNotice，刷新 SrClient.device。
                var triggerMsg = new TransportMessage
                {
                    Id = 0,
                    MsgType = EnumMsgType.TriggerDeviceInfo,
                    Content = Any.Pack(new Empty())
                };
                await context.WriteAndFlushAsync(triggerMsg);
                _logger.LogInformation("[业务同步] 已发送 TriggerDeviceInfo 指令，请求设备上报状态");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[配置推送] 初始化配置推送失败 {ChannelId}", context.Channel.Id.AsLongText());
            }
        }
    }
}
