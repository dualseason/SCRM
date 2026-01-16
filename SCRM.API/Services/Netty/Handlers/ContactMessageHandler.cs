using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Google.Protobuf;
using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using SCRM.API.Hubs;
using SCRM.Services;
using SCRM.Services.Data;
using SCRM.API.Services.Data;
using SCRM.SHARED.Models;
using SCRM.API.Services.Data; // For SaveContacts helper extension if exists, or use DbContext logic directly
using SCRM.API.Models.Entities;
using System.Collections.Generic;
using System.Linq;
using SCRM.API.Models.Events;
using SCRM.Services.Events;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 联系人消息处理器
    /// 负责处理好友增删改、好友列表同步、企业微信联系人等
    /// Scoped Service
    /// </summary>
    public class ContactMessageHandler
    {
        private readonly ILogger<ContactMessageHandler> _logger;
        private readonly ConnectionManager _connectionManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHubContext<ClientHub> _hubContext;
        private readonly IEventBus _eventBus;

        public ContactMessageHandler(
            ILogger<ContactMessageHandler> logger,
            ConnectionManager connectionManager,
            ApplicationDbContext dbContext,
            IHubContext<ClientHub> hubContext,
            IEventBus eventBus)
        {
            _logger = logger;
            _connectionManager = connectionManager;
            _dbContext = dbContext;
            _hubContext = hubContext;
            _eventBus = eventBus;
        }

        /// <summary>
        /// 处理新增好友通知 (3.3 - 可能是旧版)
        /// 保存好友信息到数据库
        /// </summary>
        public async Task HandleFriendAddNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendAddNoticeMessage>();
            if (notice.Friend != null)
            {
                _logger.LogInformation("新增好友通知：{WeChatId} 添加了好友 {FriendNick}", 
                    notice.WeChatId, notice.Friend.FriendNick);

                var connectionId = context.Channel.Id.AsLongText();
                await SaveAddedFriendAsync(connectionId, notice.Friend, notice.WeChatId);
            }
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理好友变更通知 (1052)
        /// </summary>
        public async Task HandleFriendChangeNotice(TransportMessage message, IChannelHandlerContext context)
        {
            try
            {
                var notice = message.Content.Unpack<FriendChangeNoticeMessage>();
                _logger.LogInformation("收到好友变更通知 FriendChangeNotice: WeChatId={WeChatId}, FriendNick={FriendNick}", 
                    notice.WeChatId, notice.Friend?.FriendNick);

                // TODO: 实现具体的变更同步逻辑
                await SendAckAsync(message, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HandleFriendChangeNotice 异常");
            }
        }

        /// <summary>
        /// 处理删除好友通知 (3.4)
        /// 1. 软删除本地 Contact
        /// 2. 通知 SignalR
        /// </summary>
        public async Task HandleFriendDelNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendDelNoticeMessage>();
            _logger.LogInformation("删除好友通知：{WeChatId} 删除了好友 {FriendId}", notice.WeChatId, notice.FriendId);
            
            var connectionId = context.Channel.Id.AsLongText();
            await DeleteFriendAsync(connectionId, notice.FriendId, notice.WeChatId);
            
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理好友信息推送 (3.3 / 3.1 结果) (完整列表)
        /// </summary>
        public async Task HandleFriendPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendPushNoticeMessage>();
            _logger.LogInformation("好友列表推送：{WeChatId} 推送了 {Count} 个好友 (第 {Page}/{Size} 页)", 
                notice.WeChatId, notice.Friends.Count, notice.Page, notice.Size);

            // [DEBUG]
            /*
            for (int i = 0; i < notice.Friends.Count; i++)
            {
                var f = notice.Friends[i];
                _logger.LogInformation("[FriendSync] Index: {Index}, ID: {Id}, Nick: {Nick}", i, f.FriendId, f.FriendNick);
            }
            */

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId))
            {
                _logger.LogWarning("收到来自未认证或未知连接的好友推送：{ConnectionId}", connectionId);
                return;
            }

            // 验证账号是否存在
            var account = await _dbContext.WechatAccounts.FindAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("未找到 AccountId 的 WechatAccount：{AccountId}", accountId);
                return;
            }

            var contactsToSave = new List<Contact>();

            foreach (var friend in notice.Friends)
            {
                var contact = new Contact
                {
                    wechatAccountId = (int)accountId,
                    wxid = friend.FriendId,
                    nickname = friend.FriendNick ?? "",
                    remarks = friend.Remark ?? "", 
                    description = friend.Memo ?? "", 
                    avatar = friend.Avatar ?? "",
                    gender = (int)friend.Gender,
                    province = friend.Province ?? "",
                    city = friend.City ?? "",
                    phone = friend.Phone ?? "",
                    signature = friend.Desc ?? "",
                    source = friend.Source.ToString(),
                    labelIds = friend.LabelIds ?? "",
                    email = "",
                    country = "",
                    contactType = 0,
                    isDeleted = false,
                    createdAt = DateTime.UtcNow,
                    updatedAt = DateTime.UtcNow
                };
                contactsToSave.Add(contact);
            }

             contactsToSave = contactsToSave
                .Where(c => !string.IsNullOrEmpty(c.wxid)) 
                .GroupBy(c => c.wxid)
                .Select(g => g.Last()) 
                .ToList();

            if (contactsToSave.Count > 0)
            {
                 _logger.LogInformation("[TRACE] 准备保存 {Count} 个联系人...", contactsToSave.Count);
                 await _dbContext.SaveContacts(accountId, contactsToSave);
                 _logger.LogInformation("处理好友推送完成：已同步 {Count} 个好友，账号 {AccountId}", contactsToSave.Count, accountId);
            }

            // 通知 UI (ContactsReceivedEvent)
            try
            {
                await _eventBus.PublishAsync(new ContactsReceivedEvent(connectionInfo.deviceInfo ?? connectionId, accountId, connectionInfo.userId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发布 ContactsReceivedEvent 失败");
            }

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理好友添加请求通知 (3.7) (请求添加我)
        /// </summary>
        public async Task HandleFriendAddReqeustNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendAddReqeustNoticeMessage>();
            _logger.LogInformation("收到好友验证请求：{WeChatId} 来自 {FriendNick} ({FriendId})", notice.WeChatId, notice.FriendNick, notice.FriendId);
            // TODO: 实现请求持久化到 FriendRequest 表
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理好友添加请求列表推送 (4.49 结果)
        /// </summary>
        public async Task HandleFriendAddReqListNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<FriendAddReqListNoticeMessage>();
            _logger.LogInformation("收到好友请求列表通知: WeChatId={WeChatId}, RequestCount={RequestCount}", notice.WeChatId, notice.Requests.Count);
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理联系人标签通知 (3.16)
        /// </summary>
        public async Task HandleContactLabelInfoNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ContactLabelInfoNoticeMessage>();
            _logger.LogInformation("收到联系人标签通知: WeChatId={WeChatId}, LabelCount={LabelCount}", notice.WeChatId, notice.Labels.Count);
            
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId)) return;

            foreach (var label in notice.Labels)
            {
                var existing = await _dbContext.ContactTags
                    .FirstOrDefaultAsync(t => t.wechatAccountId == accountId && t.labelId == label.LabelId);
                
                if (existing != null)
                {
                    existing.tagName = label.LabelName;
                    existing.updatedAt = DateTime.UtcNow;
                    existing.isDeleted = false; 
                }
                else
                {
                    var newTag = new ContactTag
                    {
                        wechatAccountId = (int)accountId,
                        labelId = label.LabelId,
                        tagName = label.LabelName,
                        createdAt = DateTime.UtcNow,
                        updatedAt = DateTime.UtcNow,
                        isDeleted = false
                    };
                    _dbContext.ContactTags.Add(newTag);
                }
            }
            
            await _dbContext.SaveChangesAsync();
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理企业微信联系人推送 (4.50 结果)
        /// </summary>
        public async Task HandleBizContactPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<BizContactPushNoticeMessage>();
            _logger.LogInformation("收到企业微信联系人推送: WeChatId={WeChatId}, ContactCount={ContactCount}", notice.WeChatId, notice.Contacts.Count);
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理新增公众号/企业微信联系人通知 (3.18)
        /// </summary>
        public async Task HandleBizContactAddNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<BizContactAddNoticeMessage>();
            _logger.LogInformation("收到企业微信添加通知: WeChatId={WeChatId}, AddedUser={AddedUser}", notice.WeChatId, notice.Contact?.Username ?? "Unknown");
            await SendAckAsync(message, context);
        }

        // --- Helpers ---

        private async Task SaveAddedFriendAsync(string connectionId, FriendMessage friend, string weChatId)
        {
            try
            {
                var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
                if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId)) return;

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

                await _dbContext.SaveContacts(accountId, new List<Contact> { contact });
                
                await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("ContactsUpdated", accountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存新增好友失败 {FriendId}", friend.FriendId);
            }
        }

        private async Task DeleteFriendAsync(string connectionId, string friendId, string weChatId)
        {
             try
            {
                var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
                if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId)) return;

                var contact = await _dbContext.Contacts
                    .FirstOrDefaultAsync(c => c.wechatAccountId == accountId && c.wxid == friendId);
                    
                if (contact != null)
                {
                    contact.isDeleted = true;
                    contact.updatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    
                    // Notify UI
                    await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("ContactsUpdated", accountId);
                    
                    _logger.LogInformation("账号 {AccountId} 已删除好友 {FriendId}", friendId, accountId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除好友失败 {FriendId}", friendId);
            }
        }

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
