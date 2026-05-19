using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Data;
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
                    case EnumMsgType.FriendAddNotice:
                        await HandleFriendAdd(message.Content.Unpack<FriendAddNoticeMessage>(), context);
                        break;
                    case EnumMsgType.AddFriendNotice:
                        await HandleOutgoingAddFriendNotice(message.Content.Unpack<AddFriendNoticeMessage>(), context);
                        break;
                    case EnumMsgType.FriendAddReqeustNotice:
                        await HandleFriendAddRequest(message.Content.Unpack<FriendAddReqeustNoticeMessage>(), context);
                        break;
                    case EnumMsgType.FriendAddReqListNotice:
                        await HandleFriendAddReqList(message.Content.Unpack<FriendAddReqListNoticeMessage>(), context);
                        break;
                    case EnumMsgType.FriendPushNotice:
                        await HandleFriendPush(message.Content.Unpack<FriendPushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.SyncFriendListAsyncRsp:
                        await HandleSyncFriendListAsyncRsp(message.Content.Unpack<SyncFriendListAsyncRspMessage>(), context);
                        break;
                    case EnumMsgType.ContactInfoNotice:
                        await HandleContactInfo(message.Content.Unpack<ContactInfoNoticeMessage>(), context);
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
                    case EnumMsgType.BizContactPushNotice:
                        await HandleBizContactPush(message.Content.Unpack<BizContactPushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.QwUserPushNotice:
                        await HandleQwUserPush(message.Content.Unpack<QwUserPushNoticeMessage>(), context);
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

        /// <summary>
        /// 处理 3057 好友异步同步响应。
        /// <para>
        /// 3057 只表示 Android 已经触发或拒绝本次同步；完整联系人数据仍由 FriendPushNotice 负责落库。
        /// 如果响应中附带 FriendPush，也按现有 FriendPushNotice 口径写入，避免新增第二套持久化路径。
        /// </para>
        /// </summary>
        private async Task HandleSyncFriendListAsyncRsp(SyncFriendListAsyncRspMessage msg, IChannelHandlerContext context)
        {
            _logger.LogInformation(
                "收到好友异步同步响应 3057: WeChatId={WeChatId}, TaskId={TaskId}, Success={Success}, Code={Code}, ErrMsg={ErrMsg}",
                msg.WeChatId,
                msg.TaskId,
                msg.Success,
                msg.Code,
                msg.ErrMsg);

            if (msg.FriendPush != null && msg.FriendPush.Friends.Count > 0)
            {
                await HandleFriendPush(msg.FriendPush, context);
            }

            var resultMessage = msg.Success
                ? $"好友异步同步已触发：Code={msg.Code}，附带联系人 {msg.FriendPush?.Friends.Count ?? 0} 条"
                : $"好友异步同步失败：Code={msg.Code}，{(string.IsNullOrWhiteSpace(msg.ErrMsg) ? "未返回错误信息" : msg.ErrMsg)}";
            await PublishTaskResultAsync(context, msg.TaskId, msg.Success, resultMessage);
        }

        /// <summary>
        /// 处理手机端主动发起加好友请求通知。
        /// <para>
        /// AddFriendNotice(1264) 只表示“我向别人发出了验证请求”，不代表对方已经通过。
        /// 当前版本先消费日志，避免误写 FriendRequests 的“别人请求我”语义；真正成为好友仍由
        /// FriendAddNotice/FriendPushNotice/好友验证系统消息确认并落联系人。
        /// </para>
        /// </summary>
        private async Task HandleOutgoingAddFriendNotice(AddFriendNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId) || string.IsNullOrWhiteSpace(msg.FriendId))
            {
                _logger.LogWarning(
                    "AddFriendNotice ignored because required fields are empty. WeChatId={WeChatId}, FriendId={FriendId}",
                    msg.WeChatId,
                    msg.FriendId);
                return;
            }

            _logger.LogInformation(
                "AddFriendNotice handled: WeChatId={WeChatId}, FriendId={FriendId}, FriendNo={FriendNo}, Nick={Nick}, Reason={Reason}, Source={Source}, SourceUser={SourceUser}",
                msg.WeChatId,
                msg.FriendId,
                msg.FriendNo,
                msg.FriendNick,
                msg.Reason,
                msg.Source,
                msg.SourceUser);

            await Task.CompletedTask;
        }

        /// <summary>
        /// 处理好友添加请求通知。
        /// <para>
        /// 安卓端上报的 proto 名称为 FriendAddReqeustNotice（原始拼写如此）。
        /// 这里将请求落库到 FriendRequests，并发布 FriendRequestEvent，供自动通过好友请求逻辑继续消费。
        /// </para>
        /// </summary>
        private async Task HandleFriendAddRequest(FriendAddReqeustNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning("FriendAddReqeustNotice ignored because WeChatId is empty.");
                return;
            }

            if (string.IsNullOrWhiteSpace(msg.FriendId))
            {
                _logger.LogWarning("FriendAddReqeustNotice ignored because FriendId is empty. WeChatId={WeChatId}", msg.WeChatId);
                return;
            }

            var account = await _db.GetWechatAccount(msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for FriendAddReqeustNotice WeChatId: {WeChatId}", msg.WeChatId);
                return;
            }

            var now = DateTime.UtcNow;
            var request = new FriendRequest
            {
                requestWxid = msg.FriendId ?? string.Empty,
                nickname = msg.FriendNick ?? string.Empty,
                avatar = msg.Avatar ?? string.Empty,
                gender = (int)msg.Gender,
                region = BuildRegion(msg.Province, msg.City),
                source = BuildFriendRequestSource(msg.Source, msg.SourceUser, msg.FriendNo, msg.Type),
                requestMessage = msg.Reason ?? string.Empty,
                status = 0,
                requestTime = now,
                responseMessage = string.Empty,
                createdAt = now,
                updatedAt = now
            };

            await _db.SaveFriendRequestFromNotice(account.wxid, request);

            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            if (connInfo != null)
            {
                await _eventBus.PublishAsync(new FriendRequestEvent(
                    account.wxid,
                    msg.FriendId ?? string.Empty,
                    msg.FriendNick ?? string.Empty,
                    msg.Reason ?? string.Empty,
                    connId,
                    account.wxid));
                await _eventBus.PublishAsync(new FriendRequestsUpdatedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
            }

            _logger.LogInformation(
                "收到好友添加请求: FriendId={FriendId}, Nick={Nick}, Reason={Reason}, Source={Source}, Type={Type}, WeChatId={WeChatId}",
                msg.FriendId, msg.FriendNick, msg.Reason, msg.Source, msg.Type, msg.WeChatId);
        }

        /// <summary>
        /// 处理好友申请列表推送。
        /// <para>
        /// FriendAddReqListNotice(2036) 是 PullFriendAddReqListTask(1234) 的历史/补偿列表回包。
        /// 第一版只复用 FriendRequests 表落库并刷新 UI，不触发自动通过，避免旧申请被重复处理。
        /// </para>
        /// </summary>
        private async Task HandleFriendAddReqList(FriendAddReqListNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning("FriendAddReqListNotice ignored because WeChatId is empty.");
                return;
            }

            var account = await _db.GetWechatAccount(msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for FriendAddReqListNotice WeChatId: {WeChatId}", msg.WeChatId);
                return;
            }

            var saved = 0;
            foreach (var item in msg.Requests)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.FriendId))
                {
                    continue;
                }

                var now = DateTime.UtcNow;
                var request = new FriendRequest
                {
                    requestWxid = item.FriendId ?? string.Empty,
                    nickname = item.FriendNick ?? string.Empty,
                    avatar = item.Avatar ?? string.Empty,
                    gender = (int)item.Gender,
                    region = BuildRegion(item.Province, item.City),
                    source = BuildFriendReqListSource(item),
                    requestMessage = item.Reason ?? string.Empty,
                    // FriendReqMessage.State 的具体语义未完全确认；列表回包不直接改通过/拒绝状态。
                    status = 0,
                    requestTime = NormalizeWeChatTimestamp(item.ReqTime) ?? now,
                    responseMessage = string.Empty,
                    createdAt = now,
                    updatedAt = now
                };

                if (await _db.SaveFriendRequestFromNotice(account.wxid, request) != null)
                {
                    saved++;
                }
            }

            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            if (connInfo != null)
            {
                await _eventBus.PublishAsync(new FriendRequestsUpdatedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
            }

            _logger.LogInformation(
                "FriendAddReqListNotice handled: WeChatId={WeChatId}, Count={Count}, Saved={Saved}",
                msg.WeChatId,
                msg.Requests.Count,
                saved);
        }

        /// <summary>
        /// 拼接好友请求地区字段。
        /// </summary>
        private static string BuildRegion(string? province, string? city)
        {
            province = province?.Trim() ?? string.Empty;
            city = city?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(province)) return city;
            if (string.IsNullOrWhiteSpace(city)) return province;
            return $"{province} {city}";
        }

        /// <summary>
        /// 规整好友请求来源，保留 SourceUser 与 FriendNo，方便后续排查来源。
        /// </summary>
        private static string BuildFriendRequestSource(int source, string? sourceUser, string? friendNo, int type = 0)
        {
            var parts = new System.Collections.Generic.List<string> { source.ToString() };
            if (type != 0)
            {
                parts.Add($"type={type}");
            }
            if (!string.IsNullOrWhiteSpace(sourceUser))
            {
                parts.Add($"sourceUser={sourceUser}");
            }
            if (!string.IsNullOrWhiteSpace(friendNo))
            {
                parts.Add($"friendNo={friendNo}");
            }
            return string.Join(";", parts);
        }

        /// <summary>
        /// 构造好友申请列表来源字段，保留微信列表回包中的状态证据但不直接改业务状态。
        /// </summary>
        private static string BuildFriendReqListSource(FriendReqMessage item)
        {
            var parts = new System.Collections.Generic.List<string>
            {
                BuildFriendRequestSource(item.Source, item.SourceUser, item.FriendNo)
            };

            parts.Add($"state={item.State}");
            if (item.FirstReq > 0)
            {
                parts.Add($"firstReq={item.FirstReq}");
            }

            return string.Join(";", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        /// <summary>
        /// 兼容微信秒级/毫秒级时间戳。
        /// </summary>
        private static DateTime? NormalizeWeChatTimestamp(long timestamp)
        {
            if (timestamp <= 0)
            {
                return null;
            }

            try
            {
                if (timestamp > 10_000_000_000L)
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
                }

                if (timestamp > 1_000_000_000L)
                {
                    return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// 处理单个新增好友通知。
        /// <para>
        /// 安卓端部分链路会先发 FriendAddNotice，再延迟发 FriendPushNotice 全量列表。
        /// 这里先把单个好友按 DbHelper.SaveContacts 口径落库，避免 Web 等不到全量列表时看不到新好友。
        /// </para>
        /// </summary>
        private async Task HandleFriendAdd(FriendAddNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrEmpty(msg.WeChatId))
            {
                return;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for FriendAddNotice WeChatId: {WeChatId}", msg.WeChatId);
                return;
            }

            if (msg.Friend == null || string.IsNullOrWhiteSpace(msg.Friend.FriendId))
            {
                _logger.LogWarning("FriendAddNotice ignored because Friend is empty. WeChatId={WeChatId}", msg.WeChatId);
                return;
            }

            var contact = BuildContactFromFriendMessage(account.wxid, msg.Friend);
            await _db.SaveContacts(account.wxid, new System.Collections.Generic.List<Contact> { contact });
            var markedAccepted = await _db.MarkFriendRequestAccepted(account.wxid, contact.wxid, "新增好友通知已确认通过");

            _logger.LogInformation("收到新增好友通知: {Nick} ({Wxid}) (WxId: {WeChatId})",
                msg.Friend.FriendNick, msg.Friend.FriendId, msg.WeChatId);

            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            if (connInfo != null)
            {
                await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
                if (markedAccepted)
                {
                    await _eventBus.PublishAsync(new FriendRequestsUpdatedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
                }
            }
        }

        /// <summary>
        /// 将 protobuf 好友消息转换成联系人实体，统一新增好友与全量列表的字段口径。
        /// </summary>
        private static Contact BuildContactFromFriendMessage(string ownerWxid, FriendMessage friend)
        {
            return new Contact
            {
                ownerWxid = ownerWxid,
                wxid = friend.FriendId ?? string.Empty,
                friendNo = friend.FriendNo ?? string.Empty,
                sourceExt = friend.SourceExt ?? string.Empty,
                nickname = friend.FriendNick ?? string.Empty,
                remarks = !string.IsNullOrWhiteSpace(friend.Remark) ? friend.Remark : (friend.Memo ?? string.Empty),
                avatar = friend.Avatar ?? string.Empty,
                gender = (int)friend.Gender,
                country = friend.Country ?? string.Empty,
                province = friend.Province ?? string.Empty,
                city = friend.City ?? string.Empty,
                signature = friend.Desc ?? string.Empty,
                description = friend.Desc ?? string.Empty,
                phone = friend.Phone ?? string.Empty,
                source = friend.Source.ToString(),
                labelIds = friend.LabelIds ?? string.Empty,
                contactType = friend.Type,
                isFriend = 1,
                isDeleted = false,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow
            };
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
                var contact = BuildContactFromFriendMessage(account.wxid, msg.Friend);
                await _db.SaveContacts(account.wxid, new System.Collections.Generic.List<Contact> { contact });
                var markedAccepted = await _db.MarkFriendRequestAccepted(account.wxid, contact.wxid, "好友资料变更通知已确认通过");
                _logger.LogInformation("Updated Friend Info: {Wxid} (ChangeNotice)", msg.Friend.FriendId);
                
                // Notify UI
                var connId = context.Channel.Id.AsLongText();
                var connInfo = await _connectionManager.GetConnectionAsync(connId);
                if (connInfo != null)
                {
                    await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
                    if (markedAccepted)
                    {
                        await _eventBus.PublishAsync(new FriendRequestsUpdatedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
                    }
                }
            }
        }

        /// <summary>
        /// 处理单条业务联系人新增通知。
        /// <para>2038 与 2037 使用相同的 BizContactMessage 字段，单条新增必须复用批量同步的转换口径，避免 Alias/Icon/Company/source/contactType 丢失。</para>
        /// </summary>
        private async Task HandleBizContactAdd(BizContactAddNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrEmpty(msg.WeChatId)) return;
            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null) return;

            if (msg.Contact != null && !string.IsNullOrWhiteSpace(msg.Contact.Username))
            {
                var contact = BuildContactFromBizContactMessage(account.wxid, msg.Contact);
                await _db.SaveContacts(account.wxid, new System.Collections.Generic.List<Contact> { contact });
                _logger.LogInformation(
                    "Added Biz Contact: {Wxid}, Alias={Alias}, Nickname={Nickname}, HasAvatar={HasAvatar}, HasIcon={HasIcon}",
                    msg.Contact.Username,
                    msg.Contact.Alias,
                    msg.Contact.Nickname,
                    !string.IsNullOrWhiteSpace(msg.Contact.Avatar),
                    !string.IsNullOrWhiteSpace(msg.Contact.Icon));

                // Notify UI
                var connId = context.Channel.Id.AsLongText();
                var connInfo = await _connectionManager.GetConnectionAsync(connId);
                if (connInfo != null) await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
            }
        }

        /// <summary>
        /// 处理企业微信联系人列表推送。
        /// <para>2071 与 2038 使用相同的 BizContactMessage 字段，这里按联系人 upsert 口径落库并通知前端刷新。</para>
        /// </summary>
        private async Task HandleBizContactPush(BizContactPushNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning("BizContactPushNotice ignored because WeChatId is empty. TaskId={TaskId}", msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, "企业联系人同步失败：WeChatId 为空");
                return;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for BizContactPushNotice WeChatId: {WeChatId}", msg.WeChatId);
                await PublishTaskResultAsync(context, msg.TaskId, false, $"企业联系人同步失败：账号不存在 {msg.WeChatId}");
                return;
            }

            var contacts = msg.Contacts
                .Where(contact => contact != null && !string.IsNullOrWhiteSpace(contact.Username))
                .Select(contact => BuildContactFromBizContactMessage(account.wxid, contact))
                .ToList();

            if (contacts.Any())
            {
                await _db.SaveContacts(account.wxid, contacts);
            }

            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            if (connInfo != null)
            {
                await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
            }

            _logger.LogInformation(
                "收到企业微信联系人列表推送: Count={Count}, WxId={WeChatId}, TaskId={TaskId}",
                contacts.Count,
                msg.WeChatId,
                msg.TaskId);
            await PublishTaskResultAsync(context, msg.TaskId, true, $"企业联系人同步完成：保存 {contacts.Count}/{msg.Contacts.Count} 条");
        }

        /// <summary>
        /// 将企业微信联系人消息转换为联系人实体。
        /// </summary>
        private static Contact BuildContactFromBizContactMessage(string ownerWxid, BizContactMessage contact)
        {
            return new Contact
            {
                ownerWxid = ownerWxid,
                wxid = contact.Username ?? string.Empty,
                friendNo = contact.Alias ?? string.Empty,
                sourceExt = contact.Company ?? string.Empty,
                nickname = contact.Nickname ?? string.Empty,
                remarks = string.Empty,
                avatar = !string.IsNullOrWhiteSpace(contact.Avatar) ? contact.Avatar : (contact.Icon ?? string.Empty),
                gender = 0,
                signature = contact.Desc ?? string.Empty,
                description = contact.Desc ?? string.Empty,
                source = "biz",
                contactType = 2,
                isFriend = 1,
                isDeleted = false,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 处理企微用户列表推送。
        /// <para>
        /// Android 62203 链路为 TriggerQwUserPush(1285) -> QwUserPUshNotice(1286)。
        /// 注意协议枚举名原样为 QwUserPUshNotice；落库必须复用 DbHelper.SaveContacts 的 ownerWxid + wxid 幂等口径。
        /// </para>
        /// </summary>
        private async Task HandleQwUserPush(QwUserPushNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning("QwUserPUshNotice ignored because WeChatId is empty. TaskId={TaskId}", msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, "企微用户同步失败：WeChatId 为空");
                return;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for QwUserPUshNotice WeChatId: {WeChatId}", msg.WeChatId);
                await PublishTaskResultAsync(context, msg.TaskId, false, $"企微用户同步失败：账号不存在 {msg.WeChatId}");
                return;
            }

            var contacts = msg.Users
                .Where(user => user != null && !string.IsNullOrWhiteSpace(user.UserId))
                .Select(user => BuildContactFromQwUserMessage(account.wxid, user))
                .ToList();

            if (contacts.Any())
            {
                await _db.SaveContacts(account.wxid, contacts);
            }

            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            if (connInfo != null)
            {
                await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
            }

            _logger.LogInformation(
                "QwUserPUshNotice handled: WeChatId={WeChatId}, Page={Page}, Size={Size}, Count={Count}, Saved={Saved}, TaskId={TaskId}",
                msg.WeChatId,
                msg.Page,
                msg.Size,
                msg.Count,
                contacts.Count,
                msg.TaskId);
            await PublishTaskResultAsync(context, msg.TaskId, true, $"企微用户同步完成：保存 {contacts.Count}/{msg.Users.Count} 条，Count={msg.Count}，Page={msg.Page}");
        }

        /// <summary>
        /// 将企微用户消息转换为联系人实体。
        /// <para>source 使用 qw，contactType 暂复用 2，与现有业务/企业联系人筛选口径保持兼容。</para>
        /// </summary>
        private static Contact BuildContactFromQwUserMessage(string ownerWxid, QwUserMessage user)
        {
            return new Contact
            {
                ownerWxid = ownerWxid,
                wxid = user.UserId ?? string.Empty,
                friendNo = user.UserId ?? string.Empty,
                nickname = user.Name ?? string.Empty,
                remarks = string.Empty,
                avatar = user.Avatar ?? string.Empty,
                gender = 0,
                signature = string.Empty,
                description = string.Empty,
                source = "qw",
                contactType = 2,
                isFriend = 1,
                isDeleted = false,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow
            };
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

            var contacts = new System.Collections.Generic.List<Contact>();
            foreach (var friend in msg.Friends)
            {
                try 
                {
                    if (string.IsNullOrWhiteSpace(friend.FriendId))
                    {
                        _logger.LogDebug("FriendPushNotice ignored empty friend id. WeChatId={WeChatId}", msg.WeChatId);
                        continue;
                    }

                    contacts.Add(BuildContactFromFriendMessage(account.wxid, friend));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing friend {Wxid}", friend.FriendId);
                }
            }

            var acceptedCount = 0;
            if (contacts.Any())
            {
                await _db.SaveContacts(account.wxid, contacts);
                acceptedCount = await _db.MarkFriendRequestsAccepted(
                    account.wxid,
                    contacts.Select(contact => contact.wxid),
                    "好友列表同步已确认通过");
                if (acceptedCount > 0)
                {
                    _logger.LogInformation(
                        "好友列表同步确认 {Count} 条好友请求已通过 (WxId: {WeChatId})",
                        acceptedCount,
                        msg.WeChatId);
                }
            }
            
            var names = string.Join(", ", msg.Friends.Select(f => f.FriendNick).Take(10));
            if (msg.Friends.Count > 10) names += "...";
            
            _logger.LogInformation("收到好友上报信息: {Names} 等共 {Count} 个好友 (WxId: {WeChatId})", names, contacts.Count, msg.WeChatId);

            // Notify UI
            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            if (connInfo != null)
            {
                await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
                if (acceptedCount > 0)
                {
                    await _eventBus.PublishAsync(new FriendRequestsUpdatedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
                }
            }
        }

        private async Task HandleFriendDel(FriendDelNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrEmpty(msg.WeChatId)) return;

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null) return;

            var deleted = await _db.MarkContactDeleted(account.wxid, msg.FriendId);
            if (deleted)
            {
                _logger.LogInformation("收到删除好友通知，已标记联系人删除: {Wxid} (WxId: {WeChatId}, Reason={Reason})",
                    msg.FriendId, msg.WeChatId, msg.Reason);
            }
            else
            {
                // 安卓端 62203 删除链路可能连续发两次 FriendDelNotice。
                // 第一次已完成软删除后，第二次没有可删除记录是幂等结果，不应作为 warning 暴露给用户。
                _logger.LogInformation("收到重复删除好友通知，本地已无可删除联系人: {Wxid} (WxId: {WeChatId}, Reason={Reason})",
                    msg.FriendId, msg.WeChatId, msg.Reason);
            }

            // 无论本地是否找到记录，都通知前端重载一次联系人列表，避免旧页面缓存停留。
            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            if (connInfo != null)
            {
                await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
            }
        }

        /// <summary>
        /// 处理单个联系人资料回包。
        /// <para>GetContactInfoTask/ContactInfoNotice 常用于好友通过验证后的补资料；落库仍统一走 DbHelper.SaveContacts。</para>
        /// </summary>
        private async Task HandleContactInfo(ContactInfoNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning("ContactInfoNotice ignored because WeChatId is empty. TaskId={TaskId}", msg.TaskId);
                await PublishTaskResultAsync(context, msg.TaskId, false, "联系人详情同步失败：WeChatId 为空");
                return;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == msg.WeChatId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for ContactInfoNotice WeChatId: {WeChatId}", msg.WeChatId);
                await PublishTaskResultAsync(context, msg.TaskId, false, $"联系人详情同步失败：账号不存在 {msg.WeChatId}");
                return;
            }

            if (!msg.Success || msg.Contact == null || string.IsNullOrWhiteSpace(msg.Contact.Wxid))
            {
                _logger.LogWarning(
                    "ContactInfoNotice failed or empty: WeChatId={WeChatId}, TaskId={TaskId}, Success={Success}, ErrMsg={ErrMsg}",
                    msg.WeChatId,
                    msg.TaskId,
                    msg.Success,
                    msg.ErrMsg);
                var failMessage = string.IsNullOrWhiteSpace(msg.ErrMsg) ? "联系人详情为空或查询失败" : msg.ErrMsg;
                await PublishTaskResultAsync(context, msg.TaskId, false, $"联系人详情同步失败：{failMessage}");
                return;
            }

            var contact = BuildContactFromStrangerMessage(account.wxid, msg.Contact);
            await _db.SaveContacts(account.wxid, new System.Collections.Generic.List<Contact> { contact });

            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            if (connInfo != null)
            {
                await _eventBus.PublishAsync(new ContactsReceivedEvent(connInfo.deviceUuid, account.wxid, connInfo.userId));
            }

            _logger.LogInformation(
                "ContactInfoNotice handled: WeChatId={WeChatId}, Friend={FriendId}, Nick={Nick}, TaskId={TaskId}",
                msg.WeChatId,
                msg.Contact.Wxid,
                msg.Contact.Nickname,
                msg.TaskId);
            await PublishTaskResultAsync(context, msg.TaskId, true, $"联系人详情同步完成：{msg.Contact.Nickname}({msg.Contact.Wxid})");
        }

        /// <summary>
        /// 将陌生人/联系人资料转换为联系人实体。
        /// </summary>
        private static Contact BuildContactFromStrangerMessage(string ownerWxid, StrangerMessage stranger)
        {
            return new Contact
            {
                ownerWxid = ownerWxid,
                wxid = stranger.Wxid ?? string.Empty,
                friendNo = stranger.Alias ?? string.Empty,
                nickname = stranger.Nickname ?? string.Empty,
                remarks = stranger.Memo ?? string.Empty,
                avatar = stranger.Avatar ?? string.Empty,
                gender = (int)stranger.Gender,
                country = stranger.Country ?? string.Empty,
                province = stranger.Province ?? string.Empty,
                city = stranger.City ?? string.Empty,
                signature = stranger.Signature ?? string.Empty,
                description = stranger.Signature ?? string.Empty,
                contactType = stranger.Type,
                isFriend = 1,
                isDeleted = false,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 发布联系人异步任务结果。
        /// <para>联系人/企微/好友同步部分回包是数据型 Notice，不一定再发送通用 TaskResultNotice，因此这里补齐前端任务状态。</para>
        /// </summary>
        private async Task PublishTaskResultAsync(IChannelHandlerContext context, long taskId, bool success, string message)
        {
            if (taskId <= 0)
            {
                return;
            }

            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            if (connInfo == null)
            {
                return;
            }

            await _eventBus.PublishAsync(new TaskResultReceivedEvent(taskId, success, message, connId, connInfo.deviceUuid));
        }
    }
}
