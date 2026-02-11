using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models.Events;
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
                    case EnumMsgType.FriendChangeNotice: // 1017
                        await HandleFriendChange(message.Content.Unpack<FriendChangeNoticeMessage>(), context);
                        break;
                    case EnumMsgType.BizContactAddNotice: // 2038
                        await HandleBizContactAdd(message.Content.Unpack<BizContactAddNoticeMessage>(), context);
                        break;
                    case EnumMsgType.BizContactPushNotice: // 2071 (Existing but unhandled?)
                        _logger.LogInformation("Received BizContactPushNotice (2071). To be implemented.");
                        break;
                    default:
                        _logger.LogWarning("Unhandled Contact Message Type: {Type} ({Id})", message.MsgType, (int)message.MsgType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Contact Message");
            }
        }

        // [Fix] Handle Friend Change (Update Friend Info)
        private async Task HandleFriendChange(FriendChangeNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrEmpty(msg.WeChatId)) return;
            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null) return;

            // Typically FriendChangeNoticeMessage contains a single 'Friend' object or 'Friends' list.
            // Assuming standard pattern: single 'Friend' update. If 'Friends' list, adapted below.
            // Based on Proto naming, it likely has 'Friend' property.
            // Using a helper to process the update.
            if (msg.Friend != null) 
            {
                await ProcessFriendUpdate(account, msg.Friend);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Updated Friend Info: {Wxid} (ChangeNotice)", msg.Friend.FriendId);
                
                // Notify UI
                var connId = context.Channel.Id.AsLongText();
                var connInfo = await _connectionManager.GetConnectionAsync(connId);
                if (connInfo != null) await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
            }
        }

        // [Fix] Handle Biz Contact Add (New Business Friend)
        private async Task HandleBizContactAdd(BizContactAddNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrEmpty(msg.WeChatId)) return;
            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null) return;

            if (msg.Contact != null)
            {
                // BizContactMessage might have different fields than FriendMessage.
                // For MVP: manual mapping or overload.
                // Assuming basic fields exist: UserName, NickName, etc. 
                // Proto: BizContactMessage typically has UserName, NickName, Pinyin, Avatar, etc.
                
                var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.ownerWxid == account.wxid && c.wxid == msg.Contact.Username);
                if (contact == null)
                {
                    contact = new Contact
                    {
                        ownerWxid = account.wxid,
                        wxid = msg.Contact.Username, // BizContact often uses UserName
                        isFriend = 1,
                        createdAt = DateTime.UtcNow
                    };
                    _db.Contacts.Add(contact);
                }

                // Update fields
                contact.nickname = msg.Contact.Nickname ?? "";
                contact.remarks = ""; // Biz often has no remark initially
                contact.avatar = msg.Contact.Avatar ?? "";
                contact.gender = 0; // Often not available
                contact.signature = msg.Contact.Desc ?? ""; // Desc
                // type = 2? (Biz)
                contact.updatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                _logger.LogInformation("Added Biz Contact: {Wxid}", msg.Contact.Username);

                // Notify UI
                var connId = context.Channel.Id.AsLongText();
                var connInfo = await _connectionManager.GetConnectionAsync(connId);
                if (connInfo != null) await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
            }
        }

        // Helper to process a FriendMessage object (reusable logic)
        private async Task ProcessFriendUpdate(WechatAccount account, FriendMessage friendMsg)
        {
            var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.ownerWxid == account.wxid && c.wxid == friendMsg.FriendId);
            if (contact == null)
            {
                contact = new Contact
                {
                    ownerWxid = account.wxid,
                    wxid = friendMsg.FriendId,
                    isFriend = 1,
                    createdAt = DateTime.UtcNow
                };
                _db.Contacts.Add(contact);
            }

            // Update fields
            contact.nickname = friendMsg.FriendNick ?? "";
            contact.remarks = friendMsg.Remark ?? "";
            contact.avatar = friendMsg.Avatar ?? "";
            contact.gender = (int)friendMsg.Gender;
            contact.country = friendMsg.Country ?? "";
            contact.province = friendMsg.Province ?? "";
            contact.city = friendMsg.City ?? "";
            contact.signature = friendMsg.Desc ?? "";
            contact.isDeleted = false;
            contact.updatedAt = DateTime.UtcNow;
            
            // Biz Specific?
            // if (friendMsg.Type == ... ) contact.type = ...
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
                    var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.ownerWxid == account.wxid && c.wxid == friend.FriendId);
                    if (contact == null)
                    {
                        contact = new Contact
                        {
                            ownerWxid = account.wxid,
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
                await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
            }
        }

        private async Task HandleFriendDel(FriendDelNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrEmpty(msg.WeChatId)) return;

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null) return;

            var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.ownerWxid == account.wxid && c.wxid == msg.FriendId);
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
                    await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
                }
            }
        }
    }
}
