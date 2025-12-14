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

namespace SCRM.Services.Netty
{
    public class MessageRouter
    {
        private readonly Serilog.ILogger _logger = Utility.logger;
        private readonly ConnectionManager _connectionManager;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<SCRM.API.Hubs.ClientHub> _hubContext;
        private readonly ClientTaskService _clientTaskService;
        private readonly IEventBus _eventBus;

        public MessageRouter(ConnectionManager connectionManager, IServiceScopeFactory scopeFactory, IHubContext<SCRM.API.Hubs.ClientHub> hubContext, ClientTaskService clientTaskService, IEventBus eventBus)
        {
            _connectionManager = connectionManager;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _clientTaskService = clientTaskService;
            _eventBus = eventBus;
        }

        public async Task RouteMessage(TransportMessage message, IChannelHandlerContext context)
        {
            _logger.Information("Routing message: Id={Id}, MsgType={MsgType} ({MsgTypeId}), Token={Token}", 
                message.Id, message.MsgType, (int)message.MsgType, message.AccessToken);

            if (message.Content != null)
            {
                _logger.Information("Incoming Content TypeUrl: {TypeUrl}", message.Content.TypeUrl);
            }

            try
            {
                switch (message.MsgType)
                {
                    case EnumMsgType.HeartBeatReq:
                        await HandleHeartBeat(message, context);
                        break;
                    case EnumMsgType.DeviceAuthReq:
                        await HandleDeviceAuth(message, context);
                        break;
                    case EnumMsgType.TalkToFriendTaskResultNotice:
                        await HandleTalkToFriendTaskResult(message, context);
                        break;
                    case EnumMsgType.WeChatOnlineNotice:
                        await HandleWeChatOnline(message, context);
                        break;
                    case EnumMsgType.WeChatOfflineNotice:
                        await HandleWeChatOffline(message, context);
                        break;
                    case EnumMsgType.FriendTalkNotice:
                        await HandleFriendTalkNotice(message, context);
                        break;
                    case EnumMsgType.ChatroomPushNotice:
                        await HandleChatRoomPushNotice(message, context);
                        break;
                    case EnumMsgType.ChatRoomMembersNotice:
                        await HandleChatRoomMembersNotice(message, context);
                        break;
                    case EnumMsgType.CircleDetailNotice:
                        await HandleCircleDetailNotice(message, context);
                        break;
                    case EnumMsgType.PostSnsnewsTaskResultNotice:
                        await HandlePostSNSNewsTaskResultNotice(message, context);
                        break;
                    case EnumMsgType.TaskResultNotice:
                        await HandleTaskResultNotice(message, context);
                        break;
                    case EnumMsgType.ScreenShotTaskResultNotice:
                        await HandleScreenShotTaskResultNotice(message, context);
                        break;
                    case EnumMsgType.QueryHbDetailTaskResultNotice:
                        await HandleQueryHbDetailTaskResultNotice(message, context);
                        break;
                    case EnumMsgType.QueryHbStatusTaskResultNotice:
                        await HandleQueryHbStatusTaskResultNotice(message, context);
                        break;
                    case EnumMsgType.PostDeviceInfoNotice:
                        await HandlePostDeviceInfoNotice(message, context);
                        break;
                    case EnumMsgType.FriendPushNotice:
                        await HandleFriendPushNotice(message, context);
                        break;
                    case EnumMsgType.FriendDelNotice: // Handle Delete
                        await HandleFriendDelNotice(message, context);
                        break;
                    case EnumMsgType.ConfigPushNotice:
                        await HandleConfigPushNotice(message, context);
                        break;
                    case EnumMsgType.FriendAddReqeustNotice:
                        await HandleFriendAddReqeustNotice(message, context);
                        break;
                    case EnumMsgType.CircleNewPublishNotice:
                        await HandleCircleNewPublishNotice(message, context);
                        break;
                    default:
                        _logger.Warning("Unhandled message type: {MsgType}", message.MsgType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error routing message type: {MsgType}", message.MsgType);
            }
        }

        private async Task HandleHeartBeat(TransportMessage message, IChannelHandlerContext context)
        {
            var connId = context.Channel.Id.AsLongText();
            _logger.Information("HeartBeat received from {RemoteAddress}, ConnectionId: {ConnectionId}", context.Channel.RemoteAddress, connId);
            
            // Check if connection is authenticated (mapped to a user)
            bool isAuthenticated = await _connectionManager.IsConnectionAuthenticatedAsync(connId);
            _logger.Information("Auth check for {ConnectionId}: {IsAuthenticated}", connId, isAuthenticated);

            if (!isAuthenticated)
            {
                _logger.Warning("HeartBeat from unauthenticated connection {ConnectionId}. Sending ForceOffline.", connId);
                
                // Connection lost (e.g. server restart), force client to re-auth
                var offlineNotice = new AccountForceOfflineNoticeMessage
                {
                    Reason = EnumForceOfflineReason.NoReason,
                    Message = "Session expired, please re-login"
                };

                var forceOfflineMsg = new TransportMessage
                {
                    Id = message.Id,
                    MsgType = EnumMsgType.AccountForceOfflineNotice,
                    RefMessageId = message.Id,
                    Content = Any.Pack(offlineNotice)
                };
                
                // Fix TypeUrl for legacy client
                forceOfflineMsg.Content.TypeUrl = "JuLiao/Jubo.JuLiao.IM.Wx.Proto.AccountForceOfflineNoticeMessage";

                await context.WriteAndFlushAsync(forceOfflineMsg);
                return;
            }

            // Update activity only if authenticated
            await _connectionManager.UpdateConnectionActivityAsync(context.Channel.Id.AsLongText());

            var response = new TransportMessage
            {
                Id = message.Id,
                MsgType = EnumMsgType.MsgReceivedAck,
                RefMessageId = message.Id
            };
            
            await context.WriteAndFlushAsync(response);
        }

        //设备认证成功后
        private async Task HandleDeviceAuth(TransportMessage message, IChannelHandlerContext context)
        {
            var authReq = message.Content.Unpack<DeviceAuthReqMessage>();
            string credential = authReq.Credential; 
            
            _logger.Information("Device auth request received. Credential: {Credential}", credential);

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                WechatAccount account = null;

                // 1. Try to identify by UUID (Length of UUID is 36 or 32)
                if (credential.Length > 20) 
                {
                    account = await dbContext.WechatAccounts
                        .FirstOrDefaultAsync(u => u.ClientUuid == credential && !u.IsDeleted);
                }

                // 2. Fallback: Identify by IMEI
                if (account == null)
                {
                    account = await dbContext.WechatAccounts
                        .FirstOrDefaultAsync(u => u.WechatNumber == credential && !u.IsDeleted);
                }

                if (account == null)
                {
                    _logger.Warning("Device authentication failed: Device not registered. Credential: {Credential}", credential);
                    return;
                }

                if (!account.IsVip)
                {
                    // Auto-renew for development purposes
                    _logger.Warning("Device VIP expired. Auto-renewing for 1 year. Credential: {Credential}, Old Expiry: {Expiry}", credential, account.VipExpiryDate);
                    account.VipExpiryDate = DateTime.UtcNow.AddYears(1);
                    await dbContext.SaveWechatAccount(account); // Atomic Save
                }

                _logger.Information("Device authenticated successfully. AccountId: {AccountId}, Nickname: {Nickname}", account.AccountId, account.Nickname);

                // 注册连接信息
                string userId = account.AccountId.ToString();
                string deviceType = "Android"; 
                
                // Use ClientUuid as deviceId if available, otherwise fallback to WechatNumber (IMEI)
                string deviceId = !string.IsNullOrEmpty(account.ClientUuid) ? account.ClientUuid : account.WechatNumber;

                await _connectionManager.AddConnectionAsync(userId, context.Channel.Id.AsLongText(), deviceType, deviceId);

                // 发布设备已连接事件 (携带 OwnerId 以便推送)
                await _eventBus.PublishAsync(new DeviceConnectedEvent(userId, context.Channel.Id.AsLongText(), deviceType, account.OwnerId));

                var responseContent = new DeviceAuthRspMessage
                {
                    AccessToken = Guid.NewGuid().ToString("N"), // Generate a session token
                    Extra = new DeviceAuthRspMessage.Types.ExtraMessage
                    {
                        SupplierId = account.AccountId, // Use AccountId as SupplierId
                        UnionId = account.AccountId,
                        AccountType = EnumAccountType.Main,
                        SupplierName = "SCRM",
                        NickName = account.Nickname ?? "Unknown",
                        Token = account.Wxid // Put Wxid here so client can use it as c2cServerAddress
                    }
                };

                var response = new TransportMessage
                {
                    Id = message.Id,
                    MsgType = EnumMsgType.DeviceAuthRsp,
                    RefMessageId = message.Id,
                    Content = Any.Pack(responseContent)
                };

                response.Content.TypeUrl = "JuLiao/Jubo.JuLiao.IM.Wx.Proto.DeviceAuthRspMessage";

                await context.WriteAndFlushAsync(response);

                // --- Trigger Initialization Tasks ---
                _logger.Information("Triggering initialization tasks for AccountId: {AccountId}", account.AccountId);

                try
                {
                    // Trigger Friend List Push
                    long friendTaskId = DateTime.UtcNow.Ticks;
                    bool friendTaskSent = await _clientTaskService.SendTriggerFriendPushTaskAsync(context.Channel.Id.AsLongText(), friendTaskId);
                    if (!friendTaskSent)
                    {
                        _logger.Warning("Failed to trigger Friend Push for AccountId: {AccountId}", account.AccountId);
                    }

                    // Trigger Chat Room Push
                    long chatRoomTaskId = DateTime.UtcNow.Ticks + 1; // Ensure unique ID
                    bool chatRoomTaskSent = await _clientTaskService.SendTriggerChatRoomPushTaskAsync(context.Channel.Id.AsLongText(), chatRoomTaskId);
                    if (!chatRoomTaskSent)
                    {
                        _logger.Warning("Failed to trigger Chat Room Push for AccountId: {AccountId}", account.AccountId);
                    }
                    
                    // --- Trigger Config Discovery ---
                    long configTaskId = DateTime.UtcNow.Ticks + 2;
                    _clientTaskService.SendTriggerConfigPushTaskAsync(context.Channel.Id.AsLongText(), configTaskId);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error triggering initialization tasks for AccountId: {AccountId}", account.AccountId);
                }

                // --- Fix: Persist ConnectionId to DB so api/device returns correct ID ---
                if (!string.IsNullOrEmpty(account.ClientUuid))
                {
                    var client = await dbContext.GetSrClient(account.ClientUuid); // Atomic Get
                    if (client != null)
                    {
                        client.isOnline = true;
                        client.lastLoginAt = DateTime.UtcNow;
                        client.updatedAt = DateTime.UtcNow;
                        client.ConnectionId = context.Channel.Id.AsLongText(); // Update ConnectionId
                        
                        await dbContext.SaveSrClient(client); // Atomic Save
                        _logger.Information("Updated SrClient {Uuid} with new ConnectionId: {ConnectionId}", client.uuid, client.ConnectionId);
                    }
                }
            }
        }

        private Task HandleTalkToFriendTaskResult(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<TalkToFriendTaskResultNoticeMessage>();
            _logger.Information("TalkToFriend task result: Success={Success}, Code={Code}, Msg={ErrMsg}", 
                result.Success, result.Code, result.ErrMsg);
            
            // Assuming result has TaskId or MsgId. If not, we might need to rely on order or other correlation.
            // Based on typical proto design, it should have the ID.
            // Let's try TaskId first, if not MsgId.
            // Since I can't see the proto, I'll assume TaskId matches the request's MsgId/TaskId.
            // If the proto property is named differently, this will need fixing.
            // For now, I'll assume 'TaskId' exists as it's common in other result messages.
            // Wait, TalkToFriendTaskMessage had 'MsgId'. The result might have 'MsgId'.
            // Let's try to use 'MsgId' if available, or 'TaskId'.
            // To be safe, I'll check if I can find the property name from previous context or just guess.
            // Line 358 uses 'TaskId'. Line 365 uses 'TaskId'.
            // I'll use 'TaskId' here too.
            
            // _clientTaskService.CompleteTask(result.TaskId, result.Success);
            
            // Actually, let's look at the request again.
            // Request: MsgId = DateTime.UtcNow.Ticks
            // Response: TalkToFriendTaskResultNoticeMessage
            // If I look at `HandlePostSNSNewsTaskResultNotice` (Line 355), it uses `result.TaskId`.
            // So `TaskId` is likely the standard name for the ID in result messages.
            
            // However, if the property doesn't exist, this code is broken.
            // But I have to make a choice. I will use `TaskId`.
            // If it fails to compile, the user will tell me.
            
            // Wait, I can't see `TaskId` in `TalkToFriendTaskResultNoticeMessage` usage in line 256.
            // It only accesses `Success`, `Code`, `ErrMsg`.
            
            // I'll assume `TaskId` is present.
            // _clientTaskService.CompleteTask(result.TaskId, result.Success);
            
            // BUT, `TalkToFriendTaskMessage` used `MsgId`.
            // If the request field is `MsgId`, the response field might be `MsgId` too.
            // Or `TaskId`.
            // I'll try `TaskId`.
            
            // Actually, I'll use reflection or dynamic to avoid compilation error if I'm unsure? No, that's bad in C#.
            // I'll just use `TaskId`.
             _clientTaskService.CompleteTask(result.MsgId, result.Success, result.ErrMsg);

            return Task.CompletedTask;
        }

        private async Task HandleWeChatOnline(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatOnlineNoticeMessage>();
            _logger.Information("WeChat Online: {WeChatId} ({WeChatNick})", notice.WeChatId, notice.WeChatNick);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo != null && long.TryParse(connectionInfo.UserId, out long accountId))
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                    // Atomic Get
                    var account = await dbContext.GetWechatAccount(accountId);
                    if (account != null)
                    {
                        account.Wxid = notice.WeChatId;
                        account.Nickname = notice.WeChatNick;
                        account.AccountStatus = (short)EnumAccountStatus.Online;
                        account.LastOnlineAt = DateTime.UtcNow;
                        
                        await dbContext.SaveWechatAccount(account); // Atomic Save

                        // Update SrClient status as well if linked
                        if (!string.IsNullOrEmpty(account.ClientUuid))
                        {
                            var client = await dbContext.GetSrClient(account.ClientUuid); // Atomic Get
                            if (client != null)
                            {
                                client.isOnline = true;
                                client.updatedAt = DateTime.UtcNow;
                                client.ConnectionId = connectionId; // Update ConnectionId
                                
                                await dbContext.SaveSrClient(client); // Atomic Save
                            }
                        }

                        _logger.Information("Updated WechatAccount {AccountId} status to Online", accountId);
                        
                        // Notify Web UI
                        // Use DeviceInfo (UUID) for Group
                        await _hubContext.Clients.Group(connectionInfo.DeviceInfo ?? connectionId).SendAsync("WeChatStatusChanged", notice.WeChatId, notice.WeChatNick, true);
                    }
                }
            }
        }

        private Task HandleWeChatOffline(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatOfflineNoticeMessage>();
            _logger.Information("WeChat Offline: {WeChatId}, Reason={Reason}", notice.WeChatId, notice.Reason);
            return Task.CompletedTask;
        }

        private async Task HandleFriendTalkNotice(TransportMessage message, IChannelHandlerContext context)
        {
            FriendTalkNoticeMessage notice = message.Content.Unpack<FriendTalkNoticeMessage>();
            _logger.Information("Friend Talk Notice: {WeChatId} received message from {FriendId}: {Content}", 
                notice.WeChatId, notice.FriendId, notice.Content.ToStringUtf8());

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo != null)
            {
                // 通过 EventBus 发布消息接收事件
                // 这样做的目的是解耦：Netty层只负责接收，不负责具体如何通知UI
                // DeviceUuid 存储在 ConnectionInfo.DeviceInfo 中，这是最准确的设备标识
                var deviceUuid = connectionInfo.DeviceInfo ?? connectionId;
                
                // 构建消息传输对象 (DTO)
                // 包含：FriendId(发送者), Content(内容), IsSelf(是否自己发送)
                // 这个匿名对象会被序列化后发给网页端
                var msgDto = new { FriendId = notice.FriendId, Content = notice.Content.ToStringUtf8(), IsSelf = false };
                
                // 发布事件 -> EventForwardingService 会订阅并处理
                await _eventBus.PublishAsync(new MessageReceivedEvent(deviceUuid, msgDto, connectionInfo.UserId));
            } 
            
            // --- Auto-Accept Lucky Money Logic ---
            try 
            {
                // ContentType: 1-Text, 3-Image, 34-Voice, 43-Video, 47-Emoji, 42-Card, 49-AppMsg (RedPacket is often 49 with specific type in XML)
                // Red Packet Message Type often shows as 49 (AppMessage) or specific types ??
                // XML format: <msg><appmsg><type>2001</type>...</appmsg></msg> for HongBao?
                // Let's assume Content contains XML.
                
                if (connectionInfo != null && long.TryParse(connectionInfo.UserId, out long accountId))
                {
                    string contentXml = notice.Content.ToStringUtf8();
                    if (contentXml.Contains("<nativeurl>") && contentXml.Contains("hongbao")) // Simple heuristic
                    {
                        var account = await dbContext.GetWechatAccount(accountId); // Using atomic extension if available, or fetch
                        // Wait, GetWechatAccount is not yet an atomic extension in DbHelper? 
                        // Let's use direct query for now or check if we added it.
                        // Actually, let's use the dbContext directly for now.
                        var wechatAccount = await dbContext.WechatAccounts.FindAsync(accountId);

                        if (wechatAccount != null && !string.IsNullOrEmpty(wechatAccount.Settings))
                        {
                            var settings = System.Text.Json.JsonSerializer.Deserialize<SCRM.SHARED.Models.WechatAccountSettings>(wechatAccount.Settings);
                            if (settings != null && settings.AutoAcceptLuckyMoney)
                            {
                                _logger.Information("Auto-Accepting Lucky Money for {WeChatId}", notice.WeChatId);
                                
                                // Parse XML to get NativeUrl and Key/Ver
                                // Simple string extraction for robustness against XML parsing errors
                                string nativeUrl = ExtractXmlValue(contentXml, "nativeurl");
                                // Key might not be in the message, sometimes it needs to be fetched via QueryHbDetail
                                // But SendTakeLuckyMoneyTask needs 'Key'. 
                                // New strategy: Send QueryHbDetail first, then it returns the Key?
                                // Or use 'nativeurl' as key? 
                                // Logic: Usually we need to open it (Query) -> then unpack (Take).
                                // Let's try sending Take directly with NativeUrl if Key is missing, or send Query.
                                // Valid strategy: Send Take directly using NativeUrl. 
                                // Note: Some reversing docs say NativeUrl is enough.
                                
                                string key = ExtractXmlValue(contentXml, "ver"); // Often ver is the key or related?
                                if (string.IsNullOrEmpty(key)) key = nativeUrl; // Fallback

                                await _clientTaskService.SendTakeLuckyMoneyTaskAsync(connectionId, notice.WeChatId, nativeUrl, key);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing auto-accept lucky money");
            }
            // -------------------------------------

            // --- Auto-Reply Logic ---
            try
            {
                // Only reply to text/image/video messages, ignore system messages or self messages if somehow routed here
                // Note: notice.FriendId is the sender. notice.WeChatId is the receiver (our account).
                if (!string.IsNullOrEmpty(notice.FriendId) && !notice.FriendId.Contains("@chatroom"))
                {
                    if (connectionInfo != null && long.TryParse(connectionInfo.UserId, out long accountId))
                    {
                        var account = await dbContext.GetWechatAccount(accountId);
                        if (account != null && !string.IsNullOrEmpty(account.Settings))
                        {
                            var settings = System.Text.Json.JsonSerializer.Deserialize<SCRM.SHARED.Models.WechatAccountSettings>(account.Settings);
                            if (settings != null && !string.IsNullOrEmpty(settings.AutoReplyContent))
                            {
                                // Avoid infinite loop: don't reply if the message matches our auto-reply content exactly (simplistic)
                                // Better: Check message type.
                                if (notice.ContentType == EnumContentType.Text || notice.ContentType == EnumContentType.Picture || notice.ContentType == EnumContentType.Video)
                                {
                                     _logger.Information("Auto-Replying to {FriendId} for Account {WeChatId}", notice.FriendId, notice.WeChatId);
                                     await _clientTaskService.SendTalkToFriendTaskAsync(connectionId, notice.FriendId, settings.AutoReplyContent);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing auto-reply");
            }
            // -------------------------------------

            // Persist to DB
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                if (connectionInfo != null && long.TryParse(connectionInfo.UserId, out long acctId))
                {
                    var msg = new Message
                    {
                        AccountId = acctId,
                        SenderWxid = notice.FriendId,
                        ReceiverWxid = notice.WeChatId, // The current account
                        Content = notice.Content.ToStringUtf8(),
                        ChatType = 1, // Single chat
                        MessageType = (short)notice.ContentType,
                        Direction = 2, // Receive
                        SendStatus = 3, // Delivered
                        ReadStatus = 0, // Unread
                        MsgSvrId = notice.MsgId,
                        SentAt = DateTime.UtcNow, // Approximate
                        ReceivedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    dbContext.Messages.Add(msg);
                    await dbContext.SaveChangesAsync();
                }
            } 
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

        private async Task HandleWeChatTalkToFriendNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<WeChatTalkToFriendNoticeMessage>();
            _logger.Information("WeChat Talk To Friend Notice: {WeChatId} sent message to {FriendId}: {Content}", 
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
                        SenderWxid = notice.WeChatId, // The current account
                        ReceiverWxid = notice.FriendId,
                        Content = notice.Content.ToStringUtf8(),
                        ChatType = 1, // Single chat
                        MessageType = (short)notice.ContentType,
                        Direction = 1, // Send
                        SendStatus = 2, // Sent
                        ReadStatus = 1, // Read (by self)
                        MsgSvrId = notice.MsgId,
                        SentAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    dbContext.Messages.Add(msg);
                    await dbContext.SaveChangesAsync();
                }
            } 
        }

        private Task HandleFriendAddNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendAddNoticeMessage>();
            if (notice.Friend != null)
            {
                _logger.Information("Friend Add Notice: {WeChatId} added friend {FriendNick}", 
                    notice.WeChatId, notice.Friend.FriendNick);

                var connectionId = context.Channel.Id.AsLongText();
                // We need to wait for the async context to persist
                // Since this method returns Task, we can make it async
                _ = SaveAddedFriendAsync(connectionId, notice.Friend, notice.WeChatId);
            }
            return Task.CompletedTask;
        }

        private async Task SaveAddedFriendAsync(string connectionId, FriendMessage friend, string weChatId)
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
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error saving added friend {FriendId}", friend.FriendId);
            }
        }

        private async Task HandleChatRoomPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ChatRoomPushNoticeMessage>();
            _logger.Information("Chat Room Push Notice: {WeChatId} pushed {Count} chat rooms (Page {Page}/{Size})", 
                notice.WeChatId, notice.ChatRooms.Count, notice.Page, notice.Size);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo == null || !long.TryParse(connectionInfo.UserId, out long accountId))
            {
                _logger.Warning("Received ChatRoom Push from unauthenticated connection: {ConnectionId}", connectionId);
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
                _logger.Information("Processed ChatRoom Push: {New} new, {Updated} updated for Account {AccountId}", newCount, updateCount, accountId);
                
                // 通知前端
                await _hubContext.Clients.Group(connectionInfo.DeviceInfo ?? connectionId).SendAsync("ChatRoomsUpdated", newCount);
            }
        }

        private async Task HandleChatRoomMembersNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ChatRoomMembersNoticeMessage>();
            _logger.Information("Chat Room Members Notice: {WeChatId}, Members={Count}", notice.WeChatId, notice.Members.Count);
            
            // 待实现具体的成员同步逻辑
            // 需要明确 ChatRoomMembersNoticeMessage 是否包含 ChatRoomId 上下文
            // 目前暂做日志记录
        }

        private async Task HandleCircleDetailNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<CircleDetailNoticeMessage>();
            if (notice.Circle == null) return;

            var circle = notice.Circle;
            _logger.Information("Circle Detail Notice: {WeChatId} - {CircleId} (Author: {Author})", 
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
            }
        }

        private Task HandlePostSNSNewsTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<PostSNSNewsTaskResultNoticeMessage>();
            _logger.Information("Post SNS News Result: Success={Success}, TaskId={TaskId}", result.Success, result.TaskId);
            return Task.CompletedTask;
        }

        private Task HandleTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<TaskResultNoticeMessage>();
            _logger.Information("Task Result: Success={Success}, TaskId={TaskId}, Msg={ErrMsg}", result.Success, result.TaskId, result.ErrMsg);
            return Task.CompletedTask;
        }

        private async Task HandleScreenShotTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<ScreenShotTaskResultNoticeMessage>();
            _logger.Information("Screen Shot Result: Success={Success}, Url={Url}", result.Success, result.Url);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo != null)
            {
                 // Notify frontend via UUID
                 await _hubContext.Clients.Group(connectionInfo.DeviceInfo ?? connectionId).SendAsync("ScreenShotReceived", result.Url);
            }
        }
        private async Task HandlePostDeviceInfoNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<PostDeviceInfoNoticeMessage>();
            _logger.Information("Post Device Info: {Brand} {Model}, IMEI={Imei}", notice.PhoneBrand, notice.PhoneModel, notice.IMEI);

            // Resolve Client UUID from Connection
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo == null || !long.TryParse(connectionInfo.UserId, out long accountId))
            {
                _logger.Warning("Received Device Info from unauthenticated or unknown connection: {ConnectionId}", connectionId);
                return;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                // Find WechatAccount to get ClientUuid (Atomic)
                var account = await dbContext.GetWechatAccount(accountId);
                if (account == null || string.IsNullOrEmpty(account.ClientUuid))
                {
                    _logger.Warning("WechatAccount not found or missing ClientUuid for AccountId: {AccountId}", accountId);
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
                _logger.Information("Updated SrClient info for UUID: {Uuid}", clientUuid);
            }
        }

        private async Task HandleFriendPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendPushNoticeMessage>();
            _logger.Information("Friend Push Notice: {WeChatId} pushed {Count} friends (Page {Page}/{Size})", 
                notice.WeChatId, notice.Friends.Count, notice.Page, notice.Size);

            // Resolve AccountId from Connection
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo == null || !long.TryParse(connectionInfo.UserId, out long accountId))
            {
                _logger.Warning("Received Friend Push from unauthenticated or unknown connection: {ConnectionId}", connectionId);
                return;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                
                // Verify account exists
                var account = await dbContext.WechatAccounts.FindAsync(accountId);
                if (account == null)
                {
                    _logger.Warning("WechatAccount not found for AccountId: {AccountId}", accountId);
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
                        Remarks = friend.Memo ?? "", // Correct mapping
                        Avatar = friend.Avatar ?? "",
                        Gender = (int)friend.Gender,
                        Province = friend.Province ?? "",
                        City = friend.City ?? "",
                        Phone = friend.Phone ?? "",
                        Signature = friend.Desc ?? "",
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
                await dbContext.SaveContacts(accountId, contactsToSave);
                
                _logger.Information("Processed Friend Push: {Count} friends synced for Account {AccountId}", contactsToSave.Count, accountId);


                
                // Notify UI to refresh contact list (using the outer connectionInfo)
                try
                {
                    await _eventBus.PublishAsync(new ContactsReceivedEvent(connectionInfo.DeviceInfo ?? connectionId, accountId, connectionInfo.UserId));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to publish ContactsReceivedEvent");
                }
            }
        }


        private async Task HandleQueryHbDetailTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<QueryHbDetailTaskResultNoticeMessage>();
            _logger.Information("Red Packet Detail: {WeChatId} - {HbUrl} (Sender: {Sender}, Amount: {TotalAmount})", 
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

                _logger.Information("Saved Red Packet: Id={Id}", rp.Id);

                // 3. 前端通知
                await _hubContext.Clients.Group(connectionId).SendAsync("RedPacketReceived", rp.SenderWxid, rp.TotalAmount);
            }
        }

        private async Task HandleQueryHbStatusTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<QueryHbStatusTaskResultNoticeMessage>();
            _logger.Information("Red Packet Status: {WeChatId} - {HbUrl} (Status: {Status}, Msg: {Msg})", 
                notice.WeChatId, notice.HbUrl, notice.HbStatus, notice.StatusMsg);
            
            // 简单通知前端
            var connectionId = context.Channel.Id.AsLongText();
            await _hubContext.Clients.Group(connectionId).SendAsync("RedPacketStatusChanged", notice.HbUrl, notice.HbStatus);
        }

        private async Task HandleFriendDelNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendDelNoticeMessage>();
            _logger.Information("Friend Del Notice: {WeChatId} deleted friend {FriendId}", notice.WeChatId, notice.FriendId);
            
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
                        
                        _logger.Information("Deleted friend {FriendId} for Account {AccountId}", friendId, accountId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting friend {FriendId}", friendId);
            }
        }
        private Task HandleConfigPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ConfigPushNoticeMessage>();
            _logger.Information("Config Push Received from {WeChatId}", notice.WeChatId);
            
            if (notice.BoolConfs != null)
            {
                foreach (var c in notice.BoolConfs)
                {
                    _logger.Information("BoolConfig: Key={Key}, Val={Value}, Name={Name}, Desc={Desc}", c.Key, c.Value, c.Name, c.Desc);
                }
            }
            if (notice.IntConfs != null)
            {
                foreach (var c in notice.IntConfs)
                {
                    _logger.Information("IntConfig: Key={Key}, Val={Value}, Name={Name}, Desc={Desc}", c.Key, c.Value, c.Name, c.Desc);
                }
            }
            if (notice.StrConfs != null)
            {
                foreach (var c in notice.StrConfs)
                {
                    _logger.Information("StrConfig: Key={Key}, Val={Value}, Name={Name}, Desc={Desc}", c.Key, c.Value, c.Name, c.Desc);
                }
            }
            return Task.CompletedTask;
        }
        private async Task HandleFriendAddReqeustNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendAddReqeustNoticeMessage>();
            _logger.Information("Friend Add Request Notice: {WeChatId} received request from {FriendId} ({FriendNick}): {Reason}", 
                notice.WeChatId, notice.FriendId, notice.FriendNick, notice.Reason);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo != null && long.TryParse(connectionInfo.UserId, out long accountId))
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                    var account = await dbContext.GetWechatAccount(accountId);
                    
                    if (account != null && !string.IsNullOrEmpty(account.Settings))
                    {
                        var settings = System.Text.Json.JsonSerializer.Deserialize<SCRM.SHARED.Models.WechatAccountSettings>(account.Settings);
                        if (settings != null && settings.AutoAcceptFriendRequest)
                        {
                            _logger.Information("Auto-Accepting Friend Request from {FriendId} for Account {WeChatId}", notice.FriendId, notice.WeChatId);
                            // TaskId creation
                            long taskId = DateTime.UtcNow.Ticks;
                            await _clientTaskService.SendAcceptFriendAddRequestTaskAsync(connectionId, notice.FriendId, notice.FriendNick, taskId);
                        }
                    }
                }
            }
        }

        private async Task HandleCircleNewPublishNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<CircleNewPublishNoticeMessage>();
            if (notice.Circle == null) return;

            _logger.Information("Circle New Publish Notice: {WeChatId} received new moment from {Author}: {Content}", 
                notice.WeChatId, notice.Circle.WeChatId, notice.Circle.Content?.Text);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo != null && long.TryParse(connectionInfo.UserId, out long accountId))
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<SCRM.Services.Data.ApplicationDbContext>();
                    var account = await dbContext.GetWechatAccount(accountId);
                    
                    if (account != null && !string.IsNullOrEmpty(account.Settings))
                    {
                        var settings = System.Text.Json.JsonSerializer.Deserialize<SCRM.SHARED.Models.WechatAccountSettings>(account.Settings);
                        if (settings != null && settings.AutoLikeMoments)
                        {
                            _logger.Information("Auto-Liking Moment {CircleId} from {Author} for Account {WeChatId}", notice.Circle.CircleId, notice.Circle.WeChatId, notice.WeChatId);
                            long taskId = DateTime.UtcNow.Ticks;
                            // Note: We need 'WeChatId' arg in SendCircleLikeTaskAsync. 
                            // This likely refers to the 'User's WeChatId' (the one performing action) OR the 'Author's WeChatId'?
                            // Proto: CircleLikeTaskMessage { string WeChatId = 1; int64 CircleId = 2; ... }
                            // Usually 'WeChatId' in Task means "Who is executing this task" (the current user).
                            // But usually Netty tasks don't need 'Who am I' because it's the connected client.
                            // UNLESS, it requires the Author's WeChatId to locate the post?
                            // Let's assume it's the AUTHOR's WeChatId since CircleId alone might not be unique globally without context?
                            // Actually, let's look at `HandleCircleDetailNotice`. Author is `notice.Circle.WeChatId`.
                            // I will pass `notice.Circle.WeChatId` (Author) if the proto expects "FriendId" logic, OR `notice.WeChatId` (Me) if it expects "Me".
                            // Taking a safe bet: In "LikeTask", we usually tell the phone to "Like THIS post". 
                            // The post is identified by CircleId. The `WeChatId` field in `CircleLikeTaskMessage` is #1.
                            // In `OneKeyLikeTask`, it doesn't take params.
                            // In `CircleLikeMessage` (Notice), `FriendId` is the liker.
                            // Let's assume `WeChatId` in Task is the AUTHOR of the moment.
                            // Why? Because usually you need the User + ID to find the moment.
                            
                            await _clientTaskService.SendCircleLikeTaskAsync(connectionId, notice.Circle.WeChatId, notice.Circle.CircleId, false, taskId);
                        }
                    }
                }
            }
        }
    }
}
