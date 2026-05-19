using System.Security.Claims;
using System.Text.RegularExpressions;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Core;
using SCRM.Models.Constants;
using SCRM.SHARED.Models;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.API.Services.Security;

/// <summary>
/// 敏感数据脱敏服务。
/// <para>当前阶段保持 ClientHub 既有返回类型不变，只在出站前复制对象并隐藏手机号、微信号、聊天正文、短信正文和录音 URL。</para>
/// </summary>
public class SensitiveMaskingService
{
    private const string HiddenMessageContent = "[消息内容已隐藏]";
    private const string HiddenSmsContent = "[短信内容已隐藏]";
    private const string HiddenText = "[已隐藏]";

    private static readonly Regex MainlandPhoneRegex = new(@"(?<!\d)(1[3-9]\d{9})(?!\d)", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WxidRegex = new(@"\bwxid_[A-Za-z0-9_\-]{4,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AuthService _authService;

    public SensitiveMaskingService(AuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// 根据当前用户构建一次请求内可复用的脱敏权限画像。
    /// </summary>
    public async Task<SensitiveAccessProfile> BuildProfileAsync(ClaimsPrincipal? user)
    {
        if (AccountAccessGuard.IsAdmin(user))
        {
            return SensitiveAccessProfile.FullAccess;
        }

        var userId = AccountAccessGuard.GetUserIdCandidates(user).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return SensitiveAccessProfile.NoAccess;
        }

        var canViewPhone = await HasAnyPermissionAsync(userId,
            Permissions.Customer.ViewPhone,
            Permissions.Customer.Export);
        var canViewWechatId = await HasAnyPermissionAsync(userId,
            Permissions.Customer.ViewWechatId,
            Permissions.Customer.Export);
        var canViewCustomerSensitive = await HasAnyPermissionAsync(userId,
            Permissions.Customer.ViewSensitive,
            Permissions.Customer.Export);
        var canViewMessageContent = await HasAnyPermissionAsync(userId,
            Permissions.Message.ViewContent,
            Permissions.Message.Receive);
        var canViewMessageRaw = await HasAnyPermissionAsync(userId,
            Permissions.Message.ViewRaw);
        var canViewPhoneNumber = await HasAnyPermissionAsync(userId,
            Permissions.PhoneRecord.ViewNumber,
            Permissions.Customer.ViewPhone,
            Permissions.Customer.Export);
        var canViewSmsContent = await HasAnyPermissionAsync(userId,
            Permissions.PhoneRecord.ViewSmsContent,
            Permissions.Message.Receive,
            Permissions.Message.ViewContent);
        var canViewCallRecordUrl = await HasAnyPermissionAsync(userId,
            Permissions.PhoneRecord.ViewCallRecordUrl,
            Permissions.Customer.Export);

        return new SensitiveAccessProfile(
            canViewPhone,
            canViewWechatId,
            canViewCustomerSensitive,
            canViewMessageContent,
            canViewMessageRaw,
            canViewPhoneNumber,
            canViewSmsContent,
            canViewCallRecordUrl);
    }

    /// <summary>
    /// 脱敏联系人列表。
    /// </summary>
    public async Task<List<Contact>> MaskContactsAsync(ClaimsPrincipal? user, IEnumerable<Contact> contacts)
    {
        var profile = await BuildProfileAsync(user);
        return contacts.Select(contact => MaskContact(contact, profile)).ToList();
    }

    /// <summary>
    /// 脱敏设备列表。
    /// <para>uuid 保留为操作路由键；非管理员隐藏 TCP 地址、连接 ID、Token 和设备专属配置。</para>
    /// </summary>
    public async Task<List<SrClient>> MaskDevicesAsync(ClaimsPrincipal? user, IEnumerable<SrClient> devices)
    {
        var profile = await BuildProfileAsync(user);
        var isAdmin = AccountAccessGuard.IsAdmin(user);
        return devices.Select(device => MaskDevice(device, profile, isAdmin)).ToList();
    }

    /// <summary>
    /// 脱敏微信账号列表。
    /// <para>wxid 保留为账号操作路由键；微信号、手机号、二维码、签名和 settings 按权限隐藏。</para>
    /// </summary>
    public async Task<List<WechatAccount>> MaskWechatAccountsAsync(ClaimsPrincipal? user, IEnumerable<WechatAccount> accounts)
    {
        var profile = await BuildProfileAsync(user);
        return accounts.Select(account => MaskWechatAccount(account, profile)).ToList();
    }

    /// <summary>
    /// 脱敏聊天消息列表。
    /// </summary>
    public async Task<List<Message>> MaskMessagesAsync(ClaimsPrincipal? user, IEnumerable<Message> messages)
    {
        var profile = await BuildProfileAsync(user);
        return messages.Select(message => MaskMessage(message, profile)).ToList();
    }

    /// <summary>
    /// 脱敏会话列表。
    /// </summary>
    public async Task<List<Conversation>> MaskConversationsAsync(ClaimsPrincipal? user, IEnumerable<Conversation> conversations)
    {
        var profile = await BuildProfileAsync(user);
        return conversations.Select(conversation => MaskConversation(conversation, profile)).ToList();
    }

    /// <summary>
    /// 脱敏短信记录列表。
    /// </summary>
    public async Task<List<SmsRecordDto>> MaskSmsRecordsAsync(ClaimsPrincipal? user, IEnumerable<SmsRecordDto> records)
    {
        var profile = await BuildProfileAsync(user);
        return records.Select(record => MaskSmsRecord(record, profile)).ToList();
    }

    /// <summary>
    /// 脱敏通话记录列表。
    /// </summary>
    public async Task<List<CallLogRecordDto>> MaskCallLogRecordsAsync(ClaimsPrincipal? user, IEnumerable<CallLogRecordDto> records)
    {
        var profile = await BuildProfileAsync(user);
        return records.Select(record => MaskCallLogRecord(record, profile)).ToList();
    }

    /// <summary>
    /// 脱敏群成员列表。
    /// <para>memberWxid 当前仍作为发起群内支付、加好友等操作的路由键保留；展示字段先做文本级脱敏。</para>
    /// </summary>
    public async Task<List<GroupMemberDto>> MaskGroupMembersAsync(ClaimsPrincipal? user, IEnumerable<GroupMemberDto> members)
    {
        var profile = await BuildProfileAsync(user);
        return members.Select(member => MaskGroupMember(member, profile)).ToList();
    }

    /// <summary>
    /// 脱敏好友请求列表。
    /// <para>requestWxid 当前仍作为通过/拒绝好友请求的操作键保留；验证消息、来源和响应留言先按权限隐藏或打码。</para>
    /// </summary>
    public async Task<List<FriendRequestDto>> MaskFriendRequestsAsync(ClaimsPrincipal? user, IEnumerable<FriendRequestDto> requests)
    {
        var profile = await BuildProfileAsync(user);
        return requests.Select(request => MaskFriendRequest(request, profile)).ToList();
    }

    /// <summary>
    /// 脱敏群邀请列表。
    /// </summary>
    public async Task<List<GroupInvitationDto>> MaskGroupInvitationsAsync(ClaimsPrincipal? user, IEnumerable<GroupInvitationDto> invitations)
    {
        var profile = await BuildProfileAsync(user);
        return invitations.Select(invitation => MaskGroupInvitation(invitation, profile)).ToList();
    }

    /// <summary>
    /// 脱敏群发助手历史。
    /// </summary>
    public async Task<List<MassSendHistoryDto>> MaskMassSendHistoryAsync(ClaimsPrincipal? user, IEnumerable<MassSendHistoryDto> histories)
    {
        var profile = await BuildProfileAsync(user);
        return histories.Select(history => MaskMassSendHistory(history, profile)).ToList();
    }

    /// <summary>
    /// 脱敏朋友圈时间轴。
    /// </summary>
    public async Task<List<MomentsTimeline>> MaskMomentsAsync(ClaimsPrincipal? user, IEnumerable<MomentsTimeline> moments)
    {
        var profile = await BuildProfileAsync(user);
        return moments.Select(moment => MaskMoment(moment, profile)).ToList();
    }

    /// <summary>
    /// 判断当前用户是否具备视频号历史读取权限。
    /// </summary>
    public async Task<bool> CanReadFinderAsync(ClaimsPrincipal? user)
    {
        var profile = await BuildFinderProfileAsync(user);
        return profile.CanReadFinder;
    }

    /// <summary>
    /// 判断当前用户是否具备视频号历史导出权限。
    /// <para>导出权限只代表允许导出；导出内容仍会继续按正文、微信号、raw、媒体和指标权限脱敏。</para>
    /// </summary>
    public async Task<bool> CanExportFinderAsync(ClaimsPrincipal? user)
    {
        var profile = await BuildFinderProfileAsync(user);
        return profile.CanReadFinder && profile.CanExportFinder;
    }

    /// <summary>
    /// 脱敏视频号提及历史。
    /// </summary>
    public async Task<List<FinderMentionNoticeDto>> MaskFinderMentionsAsync(
        ClaimsPrincipal? user,
        IEnumerable<FinderMentionNoticeDto> items,
        bool forExport = false)
    {
        var profile = await BuildFinderProfileAsync(user);
        if (!profile.CanReadFinder || (forExport && !profile.CanExportFinder))
        {
            return new List<FinderMentionNoticeDto>();
        }

        return items.Select(item => MaskFinderMention(item, profile)).ToList();
    }

    /// <summary>
    /// 脱敏视频号用户页历史。
    /// </summary>
    public async Task<List<FinderUserPageDto>> MaskFinderUserPagesAsync(
        ClaimsPrincipal? user,
        IEnumerable<FinderUserPageDto> items,
        bool forExport = false)
    {
        var profile = await BuildFinderProfileAsync(user);
        if (!profile.CanReadFinder || (forExport && !profile.CanExportFinder))
        {
            return new List<FinderUserPageDto>();
        }

        return items.Select(item => MaskFinderUserPage(item, profile)).ToList();
    }

    /// <summary>
    /// 脱敏视频号评论历史。
    /// </summary>
    public async Task<List<FinderCommentListDto>> MaskFinderCommentsAsync(
        ClaimsPrincipal? user,
        IEnumerable<FinderCommentListDto> items,
        bool forExport = false)
    {
        var profile = await BuildFinderProfileAsync(user);
        if (!profile.CanReadFinder || (forExport && !profile.CanExportFinder))
        {
            return new List<FinderCommentListDto>();
        }

        return items.Select(item => MaskFinderCommentList(item, profile)).ToList();
    }

    /// <summary>
    /// 脱敏联系人。
    /// <para>当前仍保留 wxid 作为 UI 操作路由键；后续改 DTO 后再拆分“操作 ID”和“展示 ID”。</para>
    /// </summary>
    public static Contact MaskContact(Contact contact, SensitiveAccessProfile profile)
    {
        return new Contact
        {
            id = contact.id,
            wxid = contact.wxid,
            ownerWxid = contact.ownerWxid,
            friendNo = profile.CanViewWechatId ? contact.friendNo : MaskWechatId(contact.friendNo),
            sourceExt = profile.CanViewCustomerSensitive ? contact.sourceExt : string.Empty,
            nickname = contact.nickname,
            remarks = profile.CanViewCustomerSensitive ? MaskFreeText(contact.remarks, profile) ?? string.Empty : string.Empty,
            avatar = contact.avatar,
            gender = contact.gender,
            signature = profile.CanViewCustomerSensitive ? MaskFreeText(contact.signature, profile) ?? string.Empty : HiddenIfNotEmpty(contact.signature),
            phone = profile.CanViewPhone ? contact.phone : MaskPhone(contact.phone),
            email = profile.CanViewPhone ? contact.email : MaskEmail(contact.email),
            country = contact.country,
            province = contact.province,
            city = contact.city,
            description = profile.CanViewCustomerSensitive ? MaskFreeText(contact.description, profile) ?? string.Empty : HiddenIfNotEmpty(contact.description),
            source = contact.source,
            labelIds = contact.labelIds,
            contactType = contact.contactType,
            isFriend = contact.isFriend,
            isBlocked = contact.isBlocked,
            isStarred = contact.isStarred,
            lastInteractionTime = contact.lastInteractionTime,
            createdAt = contact.createdAt,
            updatedAt = contact.updatedAt,
            isDeleted = contact.isDeleted,
            Account = contact.Account == null ? null : MaskWechatAccount(contact.Account, profile, includeClient: true)
        };
    }

    /// <summary>
    /// 脱敏设备。
    /// </summary>
    public static SrClient MaskDevice(SrClient device, SensitiveAccessProfile profile, bool isAdmin = false, bool includeWx = true)
    {
        return new SrClient
        {
            uuid = device.uuid,
            tcpHost = isAdmin ? device.tcpHost : string.Empty,
            tcpPort = isAdmin ? device.tcpPort : 0,
            device = device.device,
            ip = isAdmin ? device.ip : string.Empty,
            lastLoginAt = device.lastLoginAt,
            isOnline = device.isOnline,
            customConfigs = isAdmin ? device.customConfigs : null,
            status = device.status,
            ownerId = device.ownerId,
            owner = null,
            connectionId = isAdmin ? device.connectionId : null,
            createdAt = device.createdAt,
            updatedAt = device.updatedAt,
            token = null,
            loggedInWeChatIds = profile.CanViewWechatId
                ? device.loggedInWeChatIds?.ToList() ?? new List<string>()
                : device.loggedInWeChatIds?.Select(MaskWechatId).ToList() ?? new List<string>(),
            wx = includeWx && device.wx != null
                ? new Wx
                {
                    srClient = null,
                    wechatAccount = device.wx.wechatAccount == null
                        ? null
                        : MaskWechatAccount(device.wx.wechatAccount, profile, includeClient: false),
                    contacts = null
                }
                : null
        };
    }

    /// <summary>
    /// 脱敏微信账号。
    /// </summary>
    public static WechatAccount MaskWechatAccount(WechatAccount account, SensitiveAccessProfile profile, bool includeClient = false)
    {
        return new WechatAccount
        {
            ownerId = account.ownerId,
            owner = null,
            wxid = account.wxid,
            wechatNumber = profile.CanViewWechatId ? account.wechatNumber : MaskWechatId(account.wechatNumber),
            clientUuid = account.clientUuid,
            Client = includeClient && account.Client != null
                ? MaskDevice(account.Client, profile, isAdmin: false, includeWx: false)
                : null,
            nickname = account.nickname,
            mobilePhone = profile.CanViewPhone ? account.mobilePhone : MaskPhone(account.mobilePhone),
            gender = account.gender,
            avatarUrl = account.avatarUrl,
            signature = profile.CanViewCustomerSensitive ? MaskFreeText(account.signature, profile) : HiddenIfNotEmpty(account.signature),
            qrCodeUrl = profile.CanViewWechatId ? account.qrCodeUrl : string.Empty,
            region = profile.CanViewCustomerSensitive ? MaskFreeText(account.region, profile) : string.Empty,
            accountStatus = account.accountStatus,
            lastOnlineAt = account.lastOnlineAt,
            isDeleted = account.isDeleted,
            createdAt = account.createdAt,
            updatedAt = account.updatedAt,
            deletedAt = account.deletedAt,
            vipExpiryDate = account.vipExpiryDate,
            settings = null
        };
    }

    /// <summary>
    /// 脱敏消息。
    /// </summary>
    public static Message MaskMessage(Message message, SensitiveAccessProfile profile)
    {
        var canViewContent = profile.CanViewMessageContent;
        var canViewRaw = profile.CanViewMessageRaw;

        return new Message
        {
            messageId = message.messageId,
            accountId = message.accountId,
            msgSvrId = message.msgSvrId,
            conversationId = message.conversationId,
            senderId = message.senderId,
            senderWxid = message.senderWxid,
            receiverId = message.receiverId,
            receiverWxid = message.receiverWxid,
            chatType = message.chatType,
            messageType = message.messageType,
            content = canViewContent ? MaskFreeText(message.content, profile) : HiddenMessageContent,
            contentXml = canViewRaw ? MaskFreeText(message.contentXml, profile) : string.Empty,
            direction = message.direction,
            sendStatus = message.sendStatus,
            readStatus = message.readStatus,
            isRevoked = message.isRevoked,
            isDeleted = message.isDeleted,
            localMessageId = message.localMessageId,
            clientMsgId = message.clientMsgId,
            sentAt = message.sentAt,
            receivedAt = message.receivedAt,
            readAt = message.readAt,
            revokedAt = message.revokedAt,
            createdAt = message.createdAt,
            updatedAt = message.updatedAt,
            mediaAttachments = canViewContent ? CloneMediaAttachments(message.mediaAttachments, canViewRaw) : new List<MessageMediaAttachmentDto>(),
            messageExtensions = canViewRaw ? CloneMessageExtensions(message.messageExtensions, profile) : new List<MessageExtensionViewDto>(),
            voiceTransText = canViewContent ? CloneVoiceTransText(message.voiceTransText, profile, canViewRaw) : null
        };
    }

    /// <summary>
    /// 脱敏会话。
    /// </summary>
    public static Conversation MaskConversation(Conversation conversation, SensitiveAccessProfile profile)
    {
        return new Conversation
        {
            id = conversation.id,
            wechatAccountId = conversation.wechatAccountId,
            conversationWxid = conversation.conversationWxid,
            conversationType = conversation.conversationType,
            displayName = conversation.displayName,
            displayAvatar = conversation.displayAvatar,
            unreadCount = conversation.unreadCount,
            messageCount = conversation.messageCount,
            isPinned = conversation.isPinned,
            isMuted = conversation.isMuted,
            lastMessageContent = profile.CanViewMessageContent
                ? MaskFreeText(conversation.lastMessageContent, profile)
                : HiddenMessageContent,
            lastMessageTime = conversation.lastMessageTime,
            createdAt = conversation.createdAt,
            updatedAt = conversation.updatedAt,
            isDeleted = conversation.isDeleted
        };
    }

    /// <summary>
    /// 脱敏短信记录。
    /// </summary>
    public static SmsRecordDto MaskSmsRecord(SmsRecordDto record, SensitiveAccessProfile profile)
    {
        return new SmsRecordDto
        {
            id = record.id,
            ownerWxid = record.ownerWxid,
            imei = record.imei,
            smsId = record.smsId,
            threadId = record.threadId,
            number = profile.CanViewPhoneNumber ? record.number : MaskPhone(record.number),
            type = record.type,
            rawDate = record.rawDate,
            smsTime = record.smsTime,
            content = profile.CanViewSmsContent ? MaskFreeText(record.content, profile) ?? string.Empty : HiddenSmsContent,
            isRead = record.isRead,
            simId = record.simId,
            blockType = record.blockType,
            sentNoticeType = record.sentNoticeType,
            isSentNoticeReceived = record.isSentNoticeReceived,
            readAt = record.readAt,
            sentNoticeAt = record.sentNoticeAt,
            source = record.source,
            createdAt = record.createdAt,
            updatedAt = record.updatedAt
        };
    }

    /// <summary>
    /// 脱敏通话记录。
    /// </summary>
    public static CallLogRecordDto MaskCallLogRecord(CallLogRecordDto record, SensitiveAccessProfile profile)
    {
        return new CallLogRecordDto
        {
            id = record.id,
            ownerWxid = record.ownerWxid,
            imei = record.imei,
            callLogId = record.callLogId,
            number = profile.CanViewPhoneNumber ? record.number : MaskPhone(record.number),
            type = record.type,
            rawDate = record.rawDate,
            callTime = record.callTime,
            durationSeconds = record.durationSeconds,
            hasRecording = profile.CanViewCallRecordUrl && (record.hasRecording || !string.IsNullOrWhiteSpace(record.recordUrl)),
            // 录音播放统一走 CreateCallRecordingAccessTokenAsync，不再把永久 recordUrl 随列表下发给浏览器。
            recordUrl = string.Empty,
            simId = record.simId,
            blockType = record.blockType,
            source = record.source,
            createdAt = record.createdAt,
            updatedAt = record.updatedAt
        };
    }

    /// <summary>
    /// 脱敏群成员。
    /// </summary>
    public static GroupMemberDto MaskGroupMember(GroupMemberDto member, SensitiveAccessProfile profile)
    {
        return new GroupMemberDto
        {
            // 保留操作键，后续 DTO 化时再拆 displayWxid / operationWxid。
            memberWxid = member.memberWxid,
            memberNickname = MaskFreeText(member.memberNickname, profile) ?? string.Empty,
            memberAvatar = member.memberAvatar,
            alias = profile.CanViewCustomerSensitive ? MaskFreeText(member.alias, profile) ?? string.Empty : string.Empty,
            memberRemarks = profile.CanViewCustomerSensitive ? MaskFreeText(member.memberRemarks, profile) ?? string.Empty : string.Empty,
            memberRole = member.memberRole
        };
    }

    /// <summary>
    /// 脱敏好友请求。
    /// </summary>
    public static FriendRequestDto MaskFriendRequest(FriendRequestDto request, SensitiveAccessProfile profile)
    {
        return new FriendRequestDto
        {
            id = request.id,
            ownerWxid = request.ownerWxid,
            wechatAccountId = request.wechatAccountId,
            // 保留操作键，页面通过/拒绝请求仍需要原始 requestWxid。
            requestWxid = request.requestWxid,
            nickname = MaskFreeText(request.nickname, profile) ?? string.Empty,
            avatar = request.avatar,
            gender = request.gender,
            region = profile.CanViewCustomerSensitive ? MaskFreeText(request.region, profile) ?? string.Empty : string.Empty,
            source = profile.CanViewCustomerSensitive ? MaskFreeText(request.source, profile) ?? string.Empty : HiddenIfNotEmpty(request.source),
            requestMessage = profile.CanViewMessageContent ? MaskFreeText(request.requestMessage, profile) ?? string.Empty : HiddenIfNotEmpty(request.requestMessage),
            status = request.status,
            requestTime = request.requestTime,
            responseTime = request.responseTime,
            responseMessage = profile.CanViewMessageContent ? MaskFreeText(request.responseMessage, profile) ?? string.Empty : HiddenIfNotEmpty(request.responseMessage),
            createdAt = request.createdAt,
            updatedAt = request.updatedAt
        };
    }

    /// <summary>
    /// 脱敏群邀请。
    /// </summary>
    public static GroupInvitationDto MaskGroupInvitation(GroupInvitationDto invitation, SensitiveAccessProfile profile)
    {
        return new GroupInvitationDto
        {
            id = invitation.id,
            weChatId = invitation.weChatId,
            chatRoomId = invitation.chatRoomId,
            inviter = profile.CanViewWechatId ? invitation.inviter : MaskWechatId(invitation.inviter),
            inviteName = MaskFreeText(invitation.inviteName, profile) ?? string.Empty,
            reason = profile.CanViewMessageContent ? MaskFreeText(invitation.reason, profile) ?? string.Empty : HiddenIfNotEmpty(invitation.reason),
            msgId = invitation.msgId,
            msgSvrId = invitation.msgSvrId,
            updateTime = invitation.updateTime,
            taskId = invitation.taskId,
            status = invitation.status,
            source = profile.CanViewCustomerSensitive ? MaskFreeText(invitation.source, profile) ?? string.Empty : HiddenIfNotEmpty(invitation.source),
            invitationTime = invitation.invitationTime,
            updatedAt = invitation.updatedAt,
            invited = invitation.invited.Select(member => new GroupInvitationMemberDto
            {
                userName = profile.CanViewWechatId ? member.userName : MaskWechatId(member.userName),
                nickName = MaskFreeText(member.nickName, profile) ?? string.Empty,
                avatar = member.avatar
            }).ToList()
        };
    }

    /// <summary>
    /// 脱敏群发助手历史。
    /// </summary>
    public static MassSendHistoryDto MaskMassSendHistory(MassSendHistoryDto history, SensitiveAccessProfile profile)
    {
        return new MassSendHistoryDto
        {
            id = history.id,
            ownerWxid = history.ownerWxid,
            wechatAccountId = history.wechatAccountId,
            messageTitle = profile.CanViewMessageContent ? MaskFreeText(history.messageTitle, profile) ?? string.Empty : HiddenIfNotEmpty(history.messageTitle),
            messageContent = profile.CanViewMessageContent ? MaskFreeText(history.messageContent, profile) ?? string.Empty : HiddenMessageContent,
            messageType = history.messageType,
            targetType = history.targetType,
            totalRecipients = history.totalRecipients,
            successSentCount = history.successSentCount,
            failedSentCount = history.failedSentCount,
            sendStatus = history.sendStatus,
            scheduledTime = history.scheduledTime,
            sentTime = history.sentTime,
            createdAt = history.createdAt,
            updatedAt = history.updatedAt,
            details = history.details.Select(detail => new MassSendHistoryDetailDto
            {
                id = detail.id,
                recipientWxid = profile.CanViewWechatId ? detail.recipientWxid : MaskWechatId(detail.recipientWxid),
                recipientDisplayName = MaskFreeText(detail.recipientDisplayName, profile) ?? string.Empty,
                sendStatus = detail.sendStatus,
                errorMessage = profile.CanViewCustomerSensitive ? MaskFreeText(detail.errorMessage, profile) ?? string.Empty : HiddenIfNotEmpty(detail.errorMessage),
                retryCount = detail.retryCount,
                sentTime = detail.sentTime
            }).ToList()
        };
    }

    /// <summary>
    /// 脱敏朋友圈时间轴。
    /// </summary>
    public static MomentsTimeline MaskMoment(MomentsTimeline moment, SensitiveAccessProfile profile)
    {
        return new MomentsTimeline
        {
            id = moment.id,
            wechatAccountId = moment.wechatAccountId,
            snsId = moment.snsId,
            userName = profile.CanViewWechatId ? moment.userName : MaskWechatId(moment.userName),
            nickName = MaskFreeText(moment.nickName, profile) ?? string.Empty,
            content = profile.CanViewMessageContent ? MaskFreeText(moment.content, profile) ?? string.Empty : HiddenMessageContent,
            createTime = moment.createTime,
            imagesJson = profile.CanViewMessageContent ? moment.imagesJson : string.Empty,
            commentsJson = profile.CanViewMessageContent ? MaskFreeText(moment.commentsJson, profile) ?? string.Empty : string.Empty,
            likesJson = MaskFreeText(moment.likesJson, profile) ?? string.Empty,
            receivedAt = moment.receivedAt,
            videoUrl = profile.CanViewMessageContent ? moment.videoUrl : string.Empty,
            linkInfoJson = profile.CanViewMessageContent ? MaskFreeText(moment.linkInfoJson, profile) : string.Empty,
            xmlContent = profile.CanViewMessageRaw ? MaskFreeText(moment.xmlContent, profile) : string.Empty,
            ownerWxid = moment.ownerWxid
        };
    }

    /// <summary>
    /// 脱敏视频号简表对象。
    /// </summary>
    private static FinderBriefDto MaskFinderBrief(FinderBriefDto value, FinderAccessProfile profile)
    {
        value ??= new FinderBriefDto();
        return new FinderBriefDto
        {
            feedId = value.feedId,
            userName = profile.Base.CanViewWechatId ? value.userName : MaskWechatId(value.userName),
            desc = profile.Base.CanViewMessageContent ? MaskFreeText(value.desc, profile.Base) ?? string.Empty : HiddenMessageContent,
            thumb = profile.CanViewFinderMedia ? value.thumb : string.Empty,
            nonceId = profile.CanViewFinderRaw ? value.nonceId : string.Empty,
            type = value.type
        };
    }

    /// <summary>
    /// 脱敏视频号提及条目。
    /// </summary>
    private static FinderMentionItemDto MaskFinderMentionItem(FinderMentionItemDto value, FinderAccessProfile profile)
    {
        value ??= new FinderMentionItemDto();
        return new FinderMentionItemDto
        {
            sphItem = MaskFinderBrief(value.sphItem, profile),
            id = value.id,
            type = value.type,
            mentionId = value.mentionId,
            commentId = value.commentId,
            userName = profile.Base.CanViewWechatId ? value.userName : MaskWechatId(value.userName),
            nickName = MaskFreeText(value.nickName, profile.Base) ?? string.Empty,
            avatar = profile.CanViewFinderMedia ? value.avatar : string.Empty,
            content = profile.Base.CanViewMessageContent ? MaskFreeText(value.content, profile.Base) ?? string.Empty : HiddenMessageContent,
            contentType = value.contentType,
            createTime = value.createTime,
            replayUsername = profile.Base.CanViewWechatId ? value.replayUsername : MaskWechatId(value.replayUsername),
            replayNickname = MaskFreeText(value.replayNickname, profile.Base) ?? string.Empty,
            rootCommentId = value.rootCommentId,
            refContent = profile.Base.CanViewMessageContent ? MaskFreeText(value.refContent, profile.Base) ?? string.Empty : HiddenMessageContent,
            fansId = value.fansId,
            followId = value.followId,
            relationType = value.relationType
        };
    }

    /// <summary>
    /// 脱敏视频号提及结果。
    /// </summary>
    private static FinderMentionNoticeDto MaskFinderMention(FinderMentionNoticeDto value, FinderAccessProfile profile)
    {
        value ??= new FinderMentionNoticeDto();
        return new FinderMentionNoticeDto
        {
            deviceUuid = value.deviceUuid,
            weChatId = profile.Base.CanViewWechatId ? value.weChatId : MaskWechatId(value.weChatId),
            sphUserName = profile.Base.CanViewWechatId ? value.sphUserName : MaskWechatId(value.sphUserName),
            success = value.success,
            errMsg = MaskFreeText(value.errMsg, profile.Base) ?? string.Empty,
            taskId = value.taskId,
            receivedAt = value.receivedAt,
            likeList = (value.likeList ?? new List<FinderMentionItemDto>()).Select(item => MaskFinderMentionItem(item, profile)).ToList(),
            commentList = (value.commentList ?? new List<FinderMentionItemDto>()).Select(item => MaskFinderMentionItem(item, profile)).ToList(),
            followList = (value.followList ?? new List<FinderMentionItemDto>()).Select(item => MaskFinderMentionItem(item, profile)).ToList()
        };
    }

    /// <summary>
    /// 脱敏视频号用户页作品条目。
    /// </summary>
    private static FinderUserPageItemDto MaskFinderUserPageItem(FinderUserPageItemDto value, FinderAccessProfile profile)
    {
        value ??= new FinderUserPageItemDto();
        return new FinderUserPageItemDto
        {
            feedId = value.feedId,
            userName = profile.Base.CanViewWechatId ? value.userName : MaskWechatId(value.userName),
            desc = profile.Base.CanViewMessageContent ? MaskFreeText(value.desc, profile.Base) ?? string.Empty : HiddenMessageContent,
            thumb = profile.CanViewFinderMedia ? value.thumb : string.Empty,
            nonceId = profile.CanViewFinderRaw ? value.nonceId : string.Empty,
            type = value.type,
            timestamp = value.timestamp,
            readCnt = profile.CanViewFinderMetrics ? value.readCnt : 0,
            likeCnt = profile.CanViewFinderMetrics ? value.likeCnt : 0,
            commentCnt = profile.CanViewFinderMetrics ? value.commentCnt : 0,
            favCnt = profile.CanViewFinderMetrics ? value.favCnt : 0,
            forwardCnt = profile.CanViewFinderMetrics ? value.forwardCnt : 0
        };
    }

    /// <summary>
    /// 脱敏视频号用户页结果。
    /// </summary>
    private static FinderUserPageDto MaskFinderUserPage(FinderUserPageDto value, FinderAccessProfile profile)
    {
        value ??= new FinderUserPageDto();
        return new FinderUserPageDto
        {
            deviceUuid = value.deviceUuid,
            weChatId = profile.Base.CanViewWechatId ? value.weChatId : MaskWechatId(value.weChatId),
            userName = profile.Base.CanViewWechatId ? value.userName : MaskWechatId(value.userName),
            nickName = MaskFreeText(value.nickName, profile.Base) ?? string.Empty,
            avatar = profile.CanViewFinderMedia ? value.avatar : string.Empty,
            signature = profile.Base.CanViewCustomerSensitive ? MaskFreeText(value.signature, profile.Base) ?? string.Empty : HiddenIfNotEmpty(value.signature),
            gender = value.gender,
            province = profile.Base.CanViewCustomerSensitive ? MaskFreeText(value.province, profile.Base) ?? string.Empty : string.Empty,
            city = profile.Base.CanViewCustomerSensitive ? MaskFreeText(value.city, profile.Base) ?? string.Empty : string.Empty,
            success = value.success,
            errMsg = MaskFreeText(value.errMsg, profile.Base) ?? string.Empty,
            taskId = value.taskId,
            receivedAt = value.receivedAt,
            sphList = (value.sphList ?? new List<FinderUserPageItemDto>()).Select(item => MaskFinderUserPageItem(item, profile)).ToList()
        };
    }

    /// <summary>
    /// 脱敏视频号评论条目。
    /// </summary>
    private static FinderCommentItemDto MaskFinderCommentItem(FinderCommentItemDto value, FinderAccessProfile profile)
    {
        value ??= new FinderCommentItemDto();
        return new FinderCommentItemDto
        {
            commentId = value.commentId,
            type = value.type,
            feedId = value.feedId,
            userName = profile.Base.CanViewWechatId ? value.userName : MaskWechatId(value.userName),
            nickName = MaskFreeText(value.nickName, profile.Base) ?? string.Empty,
            avatar = profile.CanViewFinderMedia ? value.avatar : string.Empty,
            content = profile.Base.CanViewMessageContent ? MaskFreeText(value.content, profile.Base) ?? string.Empty : HiddenMessageContent,
            contentType = value.contentType,
            createTime = value.createTime,
            likeCount = profile.CanViewFinderMetrics ? value.likeCount : 0,
            replyId = value.replyId
        };
    }

    /// <summary>
    /// 脱敏视频号评论列表结果。
    /// </summary>
    private static FinderCommentListDto MaskFinderCommentList(FinderCommentListDto value, FinderAccessProfile profile)
    {
        value ??= new FinderCommentListDto();
        return new FinderCommentListDto
        {
            deviceUuid = value.deviceUuid,
            weChatId = profile.Base.CanViewWechatId ? value.weChatId : MaskWechatId(value.weChatId),
            sphUserName = profile.Base.CanViewWechatId ? value.sphUserName : MaskWechatId(value.sphUserName),
            success = value.success,
            errMsg = MaskFreeText(value.errMsg, profile.Base) ?? string.Empty,
            taskId = value.taskId,
            receivedAt = value.receivedAt,
            commentList = (value.commentList ?? new List<FinderCommentItemDto>()).Select(item => MaskFinderCommentItem(item, profile)).ToList()
        };
    }

    /// <summary>
    /// 手机号打码。
    /// </summary>
    public static string MaskPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim();
        var digits = new string(text.Where(char.IsDigit).ToArray());
        if (digits.Length >= 11)
        {
            return digits[..3] + "****" + digits[^4..];
        }

        if (digits.Length >= 7)
        {
            return digits[..2] + "***" + digits[^2..];
        }

        return new string('*', Math.Min(text.Length, 6));
    }

    /// <summary>
    /// 微信号或 wxid 打码。
    /// </summary>
    public static string MaskWechatId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim();
        if (text.Length <= 4)
        {
            return new string('*', text.Length);
        }

        if (text.StartsWith("wxid_", StringComparison.OrdinalIgnoreCase) && text.Length > 9)
        {
            return "wxid_****" + text[^4..];
        }

        var head = Math.Min(2, text.Length / 2);
        var tail = Math.Min(2, text.Length - head);
        return text[..head] + "****" + text[^tail..];
    }

    /// <summary>
    /// 邮箱打码。
    /// </summary>
    public static string MaskEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim();
        var at = text.IndexOf('@');
        if (at <= 0)
        {
            return HiddenText;
        }

        return text[..1] + "***" + text[at..];
    }

    /// <summary>
    /// 对文本内手机号、邮箱、wxid 做片段打码。
    /// </summary>
    public static string? MaskFreeText(string? value, SensitiveAccessProfile profile)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var text = value;
        if (!profile.CanViewPhone)
        {
            text = MainlandPhoneRegex.Replace(text, match => MaskPhone(match.Value));
            text = EmailRegex.Replace(text, match => MaskEmail(match.Value));
        }

        if (!profile.CanViewWechatId)
        {
            text = WxidRegex.Replace(text, match => MaskWechatId(match.Value));
        }

        return text;
    }

    /// <summary>
    /// 构建视频号专用脱敏权限画像。
    /// </summary>
    private async Task<FinderAccessProfile> BuildFinderProfileAsync(ClaimsPrincipal? user)
    {
        var baseProfile = await BuildProfileAsync(user);
        if (AccountAccessGuard.IsAdmin(user))
        {
            return new FinderAccessProfile(
                baseProfile,
                CanReadFinder: true,
                CanExportFinder: true,
                CanViewFinderRaw: true,
                CanViewFinderMedia: true,
                CanViewFinderMetrics: true);
        }

        var userId = AccountAccessGuard.GetUserIdCandidates(user).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new FinderAccessProfile(
                baseProfile,
                CanReadFinder: false,
                CanExportFinder: false,
                CanViewFinderRaw: false,
                CanViewFinderMedia: false,
                CanViewFinderMetrics: false);
        }

        var canReadFinder = await HasAnyPermissionAsync(userId, Permissions.FinderOperation.Read);
        var canExportFinder = await HasAnyPermissionAsync(userId, Permissions.FinderOperation.Export);
        var canViewFinderRaw = await HasAnyPermissionAsync(
            userId,
            Permissions.FinderOperation.ViewRaw,
            Permissions.Message.ViewRaw);
        var canViewFinderMedia = await HasAnyPermissionAsync(
            userId,
            Permissions.FinderOperation.ViewMedia,
            Permissions.Message.ViewRaw);
        var canViewFinderMetrics = await HasAnyPermissionAsync(
            userId,
            Permissions.FinderOperation.ViewMetrics);

        return new FinderAccessProfile(
            baseProfile,
            canReadFinder,
            canExportFinder,
            canViewFinderRaw,
            canViewFinderMedia,
            canViewFinderMetrics);
    }

    private async Task<bool> HasAnyPermissionAsync(string userId, params string[] permissionCodes)
    {
        foreach (var permissionCode in permissionCodes.Where(code => !string.IsNullOrWhiteSpace(code)))
        {
            if (await _authService.HasPermissionAsync(userId, permissionCode))
            {
                return true;
            }
        }

        return false;
    }

    private static string HiddenIfNotEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : HiddenText;
    }

    private static List<MessageMediaAttachmentDto> CloneMediaAttachments(IEnumerable<MessageMediaAttachmentDto>? mediaAttachments, bool includeRawUrl)
    {
        return mediaAttachments?.Select(media => new MessageMediaAttachmentDto
        {
            id = media.id,
            messageId = media.messageId,
            mediaType = media.mediaType,
            // 媒体 URL 和本地路径都可直接定位原始图片、视频、语音或文件；无原始查看权限时只保留类型、大小、扩展名等展示元信息。
            mediaUrl = includeRawUrl ? media.mediaUrl : string.Empty,
            localPath = includeRawUrl ? media.localPath : string.Empty,
            mediaHash = includeRawUrl ? media.mediaHash : string.Empty,
            fileSize = media.fileSize,
            fileExtension = media.fileExtension,
            uploadStatus = media.uploadStatus,
            createdAt = media.createdAt,
            updatedAt = media.updatedAt
        }).ToList() ?? new List<MessageMediaAttachmentDto>();
    }

    private static List<MessageExtensionViewDto> CloneMessageExtensions(IEnumerable<MessageExtensionViewDto>? extensions, SensitiveAccessProfile profile)
    {
        return extensions?.Select(extension => new MessageExtensionViewDto
        {
            id = extension.id,
            messageId = extension.messageId,
            extensionKey = extension.extensionKey,
            extensionValue = MaskFreeText(extension.extensionValue, profile) ?? string.Empty,
            createdAt = extension.createdAt,
            updatedAt = extension.updatedAt
        }).ToList() ?? new List<MessageExtensionViewDto>();
    }

    private static VoiceToTextLogViewDto? CloneVoiceTransText(VoiceToTextLogViewDto? value, SensitiveAccessProfile profile, bool includeRawUrl)
    {
        if (value == null)
        {
            return null;
        }

        return new VoiceToTextLogViewDto
        {
            id = value.id,
            messageId = value.messageId,
            voiceUrl = includeRawUrl ? value.voiceUrl : string.Empty,
            transcribedText = MaskFreeText(value.transcribedText, profile) ?? string.Empty,
            transcribeStatus = value.transcribeStatus,
            accuracy = value.accuracy,
            errorMessage = value.errorMessage,
            transcribeTime = value.transcribeTime,
            createdAt = value.createdAt,
            updatedAt = value.updatedAt
        };
    }
}

/// <summary>
/// 视频号字段级脱敏权限画像。
/// </summary>
public sealed record FinderAccessProfile(
    SensitiveAccessProfile Base,
    bool CanReadFinder,
    bool CanExportFinder,
    bool CanViewFinderRaw,
    bool CanViewFinderMedia,
    bool CanViewFinderMetrics);

/// <summary>
/// 当前用户的敏感字段访问权限画像。
/// </summary>
public sealed record SensitiveAccessProfile(
    bool CanViewPhone,
    bool CanViewWechatId,
    bool CanViewCustomerSensitive,
    bool CanViewMessageContent,
    bool CanViewMessageRaw,
    bool CanViewPhoneNumber,
    bool CanViewSmsContent,
    bool CanViewCallRecordUrl)
{
    public static SensitiveAccessProfile FullAccess { get; } = new(
        true,
        true,
        true,
        true,
        true,
        true,
        true,
        true);

    public static SensitiveAccessProfile NoAccess { get; } = new(
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false);
}
