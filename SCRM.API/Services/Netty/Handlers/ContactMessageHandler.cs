using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Models.Entities;
using SCRM.API.Models.Events;
using SCRM.API.Services.Core;
using SCRM.API.Services.Netty.Handlers.Abstractions;
using SCRM.Services.Data;
using SCRM.Services.Events;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 联系人消息处理器
    /// <para>处理好友列表变更、好友请求等联系人相关事件。</para>
    /// </summary>
    public class ContactMessageHandler : MessageHandlerBase
    {
        private readonly ILogger<ContactMessageHandler> _logger;
        private readonly ApplicationDbContext _db;
        private readonly ConnectionManager _connectionManager;
        private readonly IEventBus _eventBus;

        public ContactMessageHandler(
            ILogger<ContactMessageHandler> logger,
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
            await SendAckAsync(message, context);
            
            try 
            {
                switch (message.MsgType)
                {
                    case EnumMsgType.FriendPushNotice:
                        await HandleFriendPush(message.Content.Unpack<FriendPushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.FriendDelNotice:
                        await HandleFriendDel(message.Content.Unpack<FriendDelNoticeMessage>(), context);
                        break;
                    default:
                        _logger.LogWarning("Unhandled Contact Message Type: {Type}", message.MsgType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Contact Message");
            }
        }

        private async Task HandleFriendPush(FriendPushNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrEmpty(msg.WeChatId)) return;

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for WeChatId: {WeChatId}", msg.WeChatId);
                return;
            }

            int count = 0;
            foreach (var friend in msg.Friends)
            {
                try 
                {
                    var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.wechatAccountId == account.accountId && c.wxid == friend.FriendId);
                    if (contact == null)
                    {
                        contact = new Contact
                        {
                            wechatAccountId = account.accountId,
                            wxid = friend.FriendId,
                            isFriend = 1,
                            createdAt = DateTime.UtcNow
                        };
                        _db.Contacts.Add(contact);
                    }

                    // Populate new fields
                    contact.ownerWxid = msg.WeChatId ?? "";
                    contact.friendNo = friend.FriendNo ?? "";
                    contact.sourceExt = friend.SourceExt ?? "";

                    // Update fields
                    contact.nickname = friend.FriendNick ?? "";
                    contact.remarks = friend.Remark ?? "";
                    contact.avatar = friend.Avatar ?? "";
                    contact.gender = (int)friend.Gender; 
                    contact.country = friend.Country ?? "";
                    contact.province = friend.Province ?? "";
                    contact.city = friend.City ?? "";
                    contact.signature = friend.Desc ?? "";
                    contact.isDeleted = false;
                    contact.updatedAt = DateTime.UtcNow;

                    count++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing friend {Wxid}", friend.FriendId);
                }
            }

            await _db.SaveChangesAsync();
            
            var names = string.Join(", ", msg.Friends.Select(f => f.FriendNick).Take(10));
            if (msg.Friends.Count > 10) names += "...";
            
            _logger.LogInformation("收到好友上报信息: {Names} 等共 {Count} 个好友 (WxId: {WeChatId})", names, count, msg.WeChatId);

            // Notify UI
            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            if (connInfo != null)
            {
                await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, account.accountId, connInfo.userId));
            }
        }

        private async Task HandleFriendDel(FriendDelNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrEmpty(msg.WeChatId)) return;

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null) return;

            var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.wechatAccountId == account.accountId && c.wxid == msg.FriendId);
            if (contact != null)
            {
                contact.isDeleted = true;
                contact.updatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                
                _logger.LogInformation("Marked contact {Wxid} as deleted for {WeChatId}", msg.FriendId, msg.WeChatId);

                // Notify UI (Re-triggering load)
                var connId = context.Channel.Id.AsLongText();
                var connInfo = await _connectionManager.GetConnectionAsync(connId);
                if (connInfo != null)
                {
                    // Typically might want a specific 'ContactDeletedEvent', but reusing list reload is safe KISS
                    await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, account.accountId, connInfo.userId));
                }
            }
        }
    }
}
