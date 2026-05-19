using System;

using System.Collections.Generic;

using System.Threading.Tasks;

using SCRM.API.Models.Entities; // Assuming Entities are here or in SHARED. If SHARED, adjust namespace.

using SCRM.SHARED.Models.Dtos;

using SCRM.SHARED.Models;



namespace SCRM.Shared.Interfaces

{

    /// <summary>

    /// CRM 核心业务服务接口 (操作契约)

    /// </summary>

    public interface ICrmService

    {

        // --- 试点模块: 设备管理 (Phase 2) ---

        Task<List<SrClient>> GetDevicesAsync();

        Task<SrClient?> GetDeviceAsync(string uuid);

        /// <summary>
        /// 删除设备。
        /// <para>在线设备会先下发 PostDeleteDeviceNotice(1097)，再删除服务端本地记录。</para>
        /// </summary>
        Task<bool> DeleteDeviceAsync(string uuid);

        /// <summary>
        /// 下发设备 App 升级通知。
        /// <para>UpgradeDeviceAppNotice(1094) 无结果回包，返回成功只表示通知已写入在线 TCP 通道。</para>
        /// </summary>
        Task<TaskResult> UpgradeDeviceAppAsync(string deviceUuid, string packageName, string version, int versionCode, string packageUrl, string weChatId = "");



        // --- 铺开模块: 聊天/联系人 (Phase 3) ---

        Task<List<Contact>> GetContactsAsync(string? accountId = null);

        Task<List<Conversation>> GetConversationsAsync(string? accountId = null);

        

        // Chat

        Task<List<Message>> GetMessagesAsync(string accountId, string conversationId, int count = 50);

        /// <summary>
        /// 获取指定群聊的成员列表。
        /// </summary>
        Task<List<GroupMemberDto>> GetGroupMembersAsync(string accountId, string chatRoomId);

        /// <summary>

        /// 发送消息

        /// </summary>

        /// <param name="deviceUuid">设备UUID (标识哪个微信)</param>

        /// <param name="conversationId">对方WXID</param>

        /// <param name="content">内容</param>

        /// <param name="type">类型</param>

        Task<TaskResult> SendMessageAsync(string deviceUuid, string conversationId, string content, int type = 1, string atIds = "");



        /// <summary>

        /// 请求设备截屏

        /// </summary>

        Task<TaskResult> RequestScreenShotAsync(string deviceUuid); 

        /// <summary>
        /// 为设备截图 URL 签发短期预览 token。
        /// <para>前端预览截图时只使用返回的短期 Url，不直接把上传返回的截图 URL 绑定到图片标签。</para>
        /// </summary>
        Task<MediaAccessTokenDto> CreateScreenshotAccessTokenAsync(string screenshotUrl, string deviceUuid = "", int expiresMinutes = 5);

        // ... existing methods ...

        Task<WechatAccountSettings> GetAccountSettingsAsync(string accountId);

        Task<bool> UpdateAccountSettingsAsync(string accountId, WechatAccountSettings settings);

        Task<TaskResult> SendGroupMessageAsync(string deviceUuid, List<string> friendIds, string content, int contentType = 0, int duration = 0, bool original = false);

        /// <summary>
        /// 请求设备同步微信“群发助手”历史。
        /// <para>结果由 Android 通过 GroupSendHistoryPushNotice 异步上报，服务端落库后页面再读取历史列表。</para>
        /// </summary>
        Task<TaskResult> SyncMassSendHistoryAsync(string deviceUuid, long endTime = 0, string weChatId = "");

        /// <summary>
        /// 读取指定微信账号的群发助手历史。
        /// </summary>
        Task<List<MassSendHistoryDto>> GetMassSendHistoryAsync(string accountId, int count = 50);

        Task<bool> ExecuteGroupActionAsync(string deviceUuid, string chatRoomId, int action, string content, int intValue);

        Task<bool> AgreeJoinGroupAsync(string deviceUuid, string talker, long msgSvrId, string content);

        Task<TaskResult> JoinGroupByQrAsync(string deviceUuid, string qrUrl = "", string qrContent = "");

        Task<TaskResult> PullChatRoomQrCodeAsync(string deviceUuid, string chatRoomId);

        /// <summary>
        /// 执行手机/微信进程侧设备操作。
        /// <para>action 使用 PhoneActionTask.EnumPhoneAction 数值；PhoneCall 只表示已下发，不承诺系统拨号一定成功。</para>
        /// </summary>
        Task<TaskResult> ExecutePhoneActionAsync(string deviceUuid, int action, string strParam = "", int intParam = 0, string weChatId = "", string imei = "");

        /// <summary>
        /// 拉取当前登录微信的个人二维码。
        /// </summary>
        Task<TaskResult> PullWeChatQrCodeAsync(string deviceUuid);

        /// <summary>
        /// 拉取当前位置附近 POI 列表。
        /// </summary>
        Task<TaskResult> GetPoiListAsync(string deviceUuid, double lat, double lng, string keyword = "");

        /// <summary>
        /// 拉取指定 MD5 的微信表情详情。
        /// </summary>
        Task<TaskResult> PullEmojiInfoAsync(string deviceUuid, string md5);

        /// <summary>
        /// 为指定聊天表情消息拉取表情详情并自动衔接 CDN 补图。
        /// </summary>
        Task<TaskResult> PullEmojiInfoForMessageAsync(string deviceUuid, string md5, long msgSvrId, string friendId = "");

        /// <summary>
        /// 搜索微信联系人。
        /// </summary>
        Task<TaskResult> FindContactAsync(string deviceUuid, string content);

        /// <summary>
        /// 查询微信侧定位信息。
        /// </summary>
        Task<TaskResult> GetWeChatLocationAsync(string deviceUuid, bool noCache = false);

        /// <summary>
        /// 查询微信零钱和银行卡摘要。
        /// </summary>
        Task<TaskResult> GetWalletBalanceAsync(string deviceUuid, int flag = 0);

        /// <summary>
        /// 查询手机电量、网络和存储状态。
        /// </summary>
        Task<TaskResult> GetPhoneStateAsync(string deviceUuid, string imei = "");

        /// <summary>
        /// 发送手机短信。
        /// </summary>
        Task<TaskResult> SendSmsAsync(string deviceUuid, string number, string content, string weChatId = "", string imei = "");

        /// <summary>
        /// 拉取手机短信历史。
        /// </summary>
        Task<TaskResult> PullSmsAsync(string deviceUuid, long startTime, long endTime, string weChatId = "", string imei = "");

        /// <summary>
        /// 拉取手机通话记录。
        /// </summary>
        Task<TaskResult> PullCallLogsAsync(string deviceUuid, long startTime, long endTime, string weChatId = "", string imei = "");

        /// <summary>
        /// 读取已落库短信记录。
        /// </summary>
        Task<List<SmsRecordDto>> GetSmsRecordsAsync(string accountId, string imei = "", int count = 200);

        /// <summary>
        /// 读取已落库通话记录。
        /// </summary>
        Task<List<CallLogRecordDto>> GetCallLogRecordsAsync(string accountId, string imei = "", int count = 200);

        /// <summary>
        /// 为通话录音签发短期播放 token。
        /// <para>前端播放录音时只使用返回的短期 Url，不直接打开 CallLogRecordDto.recordUrl。</para>
        /// </summary>
        Task<MediaAccessTokenDto> CreateCallRecordingAccessTokenAsync(int callLogId, int expiresMinutes = 5);

        /// <summary>
        /// 为聊天媒体 URL 签发短期访问 token。
        /// <para>用于图片、视频、语音、文件等原始媒体的查看或下载；调用前会重新校验 raw 权限和资源归属。</para>
        /// </summary>
        Task<MediaAccessTokenDto> CreateMediaAccessTokenAsync(string mediaUrl, string accountId = "", string deviceUuid = "", int expiresMinutes = 5);

        /// <summary>
        /// 查询敏感媒体访问审计。
        /// <para>用于管理端查看短期媒体 token 签发、拒绝、打开和打开失败记录。</para>
        /// </summary>
        Task<SensitiveMediaAccessAuditQueryResultDto> GetSensitiveMediaAccessAuditsAsync(SensitiveMediaAccessAuditQueryDto query);

        /// <summary>
        /// 请求客户端同步企微用户列表，结果由 QwUserPUshNotice 异步上报并落联系人。
        /// </summary>
        Task<TaskResult> SyncQwUsersAsync(string deviceUuid);

        /// <summary>
        /// 请求客户端回传指定时间段内的聊天消息 MsgSvrId 快照。
        /// </summary>
        Task<TaskResult> SyncChatMsgIdsAsync(string deviceUuid, long startTime, long endTime);

        /// <summary>
        /// 请求客户端回传历史聊天消息。
        /// </summary>
        Task<TaskResult> SyncHistoryMessagesAsync(string deviceUuid, string friendId = "", long startTime = 0, long endTime = 0, int flag = 0, int count = 50, string weChatId = "");

        /// <summary>
        /// 请求客户端同步指定会话已读状态。
        /// </summary>
        Task<TaskResult> SyncMessageReadAsync(string deviceUuid, string friendId, string weChatId = "");

        /// <summary>
        /// 请求客户端回传未读会话列表。
        /// </summary>
        Task<TaskResult> SyncUnreadListAsync(string deviceUuid, string weChatId = "");

        /// <summary>
        /// 请求客户端同步单个会话未读状态。
        /// </summary>
        Task<TaskResult> SyncConversationUnreadAsync(string deviceUuid, string friendId, string weChatId = "");

        /// <summary>
        /// 请求客户端回传业务联系人列表。
        /// </summary>
        Task<TaskResult> SyncBizContactsAsync(string deviceUuid, string weChatId = "");

        /// <summary>
        /// 请求客户端回传企微会话列表。
        /// </summary>
        Task<TaskResult> SyncQwConversationsAsync(string deviceUuid, long startTime = 0, long endTime = 0, int limit = 100, int offset = 0, string weChatId = "");

        /// <summary>
        /// 请求客户端异步同步当前微信好友列表。
        /// <para>下发 3056，真实联系人数据仍等待 FriendPushNotice 落库。</para>
        /// </summary>
        Task<TaskResult> SyncFriendListAsync(string deviceUuid, string weChatId = "");

        /// <summary>
        /// 请求客户端回传当前微信账号快照。
        /// </summary>
        Task<TaskResult> RefreshWeChatAccountsAsync(string deviceUuid);

        /// <summary>
        /// 请求客户端同步联系人标签列表。
        /// </summary>
        Task<TaskResult> SyncContactLabelsAsync(string deviceUuid, string weChatId = "");

        /// <summary>
        /// 读取已落库的联系人标签字典快照。
        /// <para>该方法只查询 ContactTags，不下发手机任务；同步动作请调用 SyncContactLabelsAsync。</para>
        /// </summary>
        Task<List<ContactLabelDto>> GetContactLabelsAsync(string accountId, bool includeDeleted = false);

        /// <summary>
        /// 创建或重命名联系人标签，也可通过 AddList/DelList 调整标签成员。
        /// </summary>
        Task<TaskResult> SaveContactLabelAsync(string deviceUuid, string labelName, int labelId = 0, string addList = "", string delList = "", string weChatId = "");

        /// <summary>
        /// 删除联系人标签。
        /// </summary>
        Task<TaskResult> DeleteContactLabelAsync(string deviceUuid, int labelId, string weChatId = "");

        /// <summary>
        /// 设置单个好友的完整标签 ID 集合。
        /// </summary>
        Task<TaskResult> SetContactLabelsAsync(string deviceUuid, string friendId, IEnumerable<int>? labelIds, string weChatId = "");

        /// <summary>
        /// 按 MsgSvrId 请求客户端补偿单条聊天消息。
        /// </summary>
        Task<TaskResult> RequestTalkMsgAsync(string deviceUuid, long msgSvrId, string weChatId = "");

        /// <summary>
        /// 按 MsgSvrId 请求客户端补偿原始聊天正文/XML。
        /// </summary>
        Task<TaskResult> RequestTalkContentAsync(string deviceUuid, long msgSvrId, string weChatId = "");

        /// <summary>
        /// 请求客户端补偿聊天消息详情。
        /// </summary>
        Task<TaskResult> RequestTalkDetailAsync(string deviceUuid, string friendId, long msgId, string msgSvrId = "", string md5 = "", bool getOriginal = false, string weChatId = "");

        /// <summary>
        /// 请求客户端对指定语音消息执行语音转文字。
        /// </summary>
        Task<TaskResult> VoiceTransTextAsync(string deviceUuid, string friendId, long msgSvrId, string weChatId = "");

        /// <summary>
        /// 请求客户端撤回指定聊天消息。
        /// </summary>
        Task<TaskResult> RevokeMessageAsync(string deviceUuid, string friendId, long msgSvrId, string weChatId = "");

        /// <summary>
        /// 请求客户端转发一条已有聊天消息。
        /// </summary>
        Task<TaskResult> ForwardMessageAsync(string deviceUuid, string talker, long msgSvrId, string friendIds, string extMsg = "", string weChatId = "");

        /// <summary>
        /// 请求客户端转发多条已有聊天消息。
        /// </summary>
        Task<TaskResult> ForwardMultiMessageAsync(string deviceUuid, string talker, IEnumerable<long>? msgIds, string friendIds, string extMsg = "", bool sendRecord = false, string weChatId = "");

        /// <summary>
        /// 请求客户端按原始内容转发消息。
        /// </summary>
        Task<TaskResult> ForwardMessageByContentAsync(string deviceUuid, string friendIds, long msgSvrId, int msgType, string content, string thumb = "", string extMsg = "", string weChatId = "");

        /// <summary>
        /// 请求客户端清空微信端聊天记录。
        /// </summary>
        Task<TaskResult> ClearAllChatMsgAsync(string deviceUuid, int flag = 0, string weChatId = "");

        /// <summary>
        /// 请求客户端查询红包详情。
        /// </summary>
        Task<TaskResult> QueryHbDetailAsync(string deviceUuid, string hbUrl, string weChatId = "");

        /// <summary>
        /// 请求客户端查询红包状态。
        /// </summary>
        Task<TaskResult> QueryHbStatusAsync(string deviceUuid, string hbUrl, string weChatId = "");

        /// <summary>
        /// 请求客户端发送微信红包，金额单位为分。
        /// </summary>
        Task<TaskResult> SendLuckyMoneyAsync(string deviceUuid, string friendId, int money, int number, string passwd, string wish = "", string weChatId = "");

        /// <summary>
        /// 请求客户端执行微信转账，金额单位为分。
        /// </summary>
        Task<TaskResult> RemittanceAsync(string deviceUuid, string friendId, int money, string passwd, string memo = "", string roomId = "", string weChatId = "");

        /// <summary>
        /// 请求客户端执行微信账号登出。
        /// </summary>
        Task<TaskResult> WechatLogoutAsync(string deviceUuid, string weChatId = "");

        /// <summary>
        /// 请求客户端下载微信 CDN 媒体文件。
        /// </summary>
        Task<TaskResult> DownloadCdnFileAsync(string deviceUuid, string cdnUrl, string cdnKey, int fileType, string fileId = "", string fileFmt = "", int fileSize = 0, long msgSvrId = 0, string weChatId = "");

        /// <summary>
        /// 启动好友检测/清粉任务。
        /// </summary>
        Task<TaskResult> StartFriendDetectAsync(string deviceUuid, string message, bool onlyCheck = true, int skipHour = 24, int mode = 0, int max = 0, string weChatId = "");

        /// <summary>
        /// 停止好友检测/清粉任务。
        /// </summary>
        Task<TaskResult> StopFriendDetectAsync(string deviceUuid, long taskId = 0, string weChatId = "");

        /// <summary>
        /// 拉取好友检测/清粉最终结果。
        /// </summary>
        Task<TaskResult> GetFriendDetectResultAsync(string deviceUuid, string weChatId = "");

        /// <summary>
        /// 请求客户端执行 GetA8Key，成功结果通常在 message 中返回 URL。
        /// </summary>
        Task<TaskResult> GetA8KeyAsync(string deviceUuid, int type, string url, string userName = "", string msgSvrId = "", int reason = 0, string weChatId = "");

        /// <summary>
        /// 修改微信资料或隐私设置。
        /// </summary>
        Task<TaskResult> UpdateWechatSettingAsync(string deviceUuid, int action, string content = "", int intParam = 0, string weChatId = "");

        /// <summary>
        /// 请求客户端回传当前配置快照。
        /// </summary>
        Task<TaskResult> TriggerConfigPushAsync(string deviceUuid);

        /// <summary>
        /// 下发 Android 设备级配置。
        /// <para>配置键直接对应 SmRun/62203 的 SetConfigTask(1382)，不要与账号自动化设置混用。</para>
        /// </summary>
        Task<TaskResult> SetDeviceConfigAsync(string deviceUuid, DeviceConfigDto config);

        /// <summary>
        /// 下发微信违禁词列表。
        /// </summary>
        Task<TaskResult> SetForbiddenWordAsync(string deviceUuid, IEnumerable<string>? words, string weChatId = "");

        Task<bool> ApproveChatRoomInviteAsync(string deviceUuid, long msgSvrId, string roomId = "", string msgContent = "", long msgId = 0);

        Task<TaskResult> SendJielongAsync(string deviceUuid, string chatRoomId, string content, string title = "", string sample = "", string memo = "", long msgSvrId = 0);

        /// <summary>
        /// 读取指定微信账号的好友请求列表。
        /// </summary>
        Task<List<FriendRequestDto>> GetFriendRequestsAsync(string accountId, int count = 50, bool pendingOnly = false);

        /// <summary>
        /// 请求客户端拉取好友申请历史/补偿列表。
        /// </summary>
        Task<TaskResult> PullFriendAddReqListAsync(string deviceUuid, long startTime = 0, bool onlyNew = true, bool getAll = false);

        Task<TaskResult> AcceptFriendRequestAsync(
            string deviceUuid,
            string friendId,
            string friendNick,
            string remark = "",
            string replyMsg = "",
            bool addWithWW = false,
            bool onlyWW = false,
            int permission = 0,
            int operation = 1);

        Task<TaskResult> AddFriendWithSceneAsync(string deviceUuid, string friendWxid, string message, string remark = "", string label = "", int scene = 3, int permission = 0, string verificationImagePath = "");

        Task<TaskResult> AddFriendInChatRoomAsync(string deviceUuid, string chatRoomId, string friendId, string message, string remark = "", int permission = 0);

        Task<TaskResult> AddFriendsByPhoneAsync(string deviceUuid, IEnumerable<string> phones, string message, string remark = "", string label = "", int permission = 0);

        Task<TaskResult> AddFriendFromPhonebookAsync(string deviceUuid, string message, int count = 1, int index = 0, bool reset = false);

        Task<TaskResult> AddFriendNameCardAsync(string deviceUuid, long msgSvrId, string message, string remark = "");

        Task<TaskResult> SendFriendVerifyAsync(string deviceUuid, string friendId, string message);

        Task<TaskResult> ModifyFriendMemoAsync(string deviceUuid, string friendId, string memo, string desc = "", string phone = "", int delFlag = 0);

        Task<TaskResult> SetFriendPermissionAsync(string deviceUuid, string friendId, int permissionMask);

        Task<TaskResult> DeleteFriendAsync(string deviceUuid, string friendId);

        Task<bool> SyncChatRoomsAsync(string deviceUuid, int flag = 0, string weChatId = "");

        Task<bool> GetChatRoomInviteListAsync(string deviceUuid);

        /// <summary>
        /// 读取指定微信账号的群邀请审批列表。
        /// </summary>
        Task<List<GroupInvitationDto>> GetGroupInvitationsAsync(string accountId, string chatRoomId = "", int count = 100, bool pendingOnly = false);

        Task<TaskResult> SyncMomentsAsync(string deviceUuid, long startTime = 0, IEnumerable<long>? circleIds = null, string weChatId = "");

        Task<TaskResult> OneKeyLikeMomentsAsync(string deviceUuid, int rate = 100, int num = 0, int endTime = 0, int timeOut = 0);

        Task<TaskResult> PostMomentAsync(string deviceUuid, string content, List<string> imageUrls);

        /// <summary>
        /// 发布朋友圈高级协议任务。
        /// <para>支持 62203/SmRun PostSNSNewsTask 的附件、可见范围、POI、提醒谁看、评论和慢发字段。</para>
        /// </summary>
        Task<TaskResult> PostMomentAdvancedAsync(string deviceUuid, MomentPostRequestDto request);

        Task<TaskResult> DeleteMomentAsync(string deviceUuid, long circleId);

        Task<TaskResult> LikeMomentAsync(string deviceUuid, long circleId, bool isCancel = false);

        Task<TaskResult> DeleteMomentCommentAsync(string deviceUuid, long circleId, long commentId, long publishTime);

        Task<TaskResult> ReplyMomentCommentAsync(string deviceUuid, long circleId, string toWeChatId, string content, long replyCommentId, bool isResend = false);

        Task<TaskResult> PullFriendMomentsAsync(string deviceUuid, string friendId, long refSnsId = 0, int count = 20, long startTime = 0, long refTime = 0);

        Task<TaskResult> PullMomentDetailAsync(string deviceUuid, long circleId, bool getBigMap = false);

        Task<TaskResult> SyncMomentMessagesAsync(string deviceUuid, bool onlyComment = false, bool getAll = true);

        Task<TaskResult> MarkMomentMessageReadAsync(string deviceUuid, long circleId, int commentId = 0);

        Task<TaskResult> ClearMomentMessageAsync(string deviceUuid, long circleId, int commentId = 0, bool isRead = true);

        Task<bool> SendMultiPictureAsync(string deviceUuid, string friendWxid, List<string> imageUrls);

        Task<TaskResult> SphGetMentionAsync(string deviceUuid, long lastLikeId = 0, long lastCommentId = 0, long lastFollowId = 0);

        Task<TaskResult> SphGetCommentAsync(string deviceUuid, long feedId, string nonceId, string feedAuth, long refCommentId = 0, long replyCommentId = 0, int sortType = 0);

        Task<TaskResult> SphUserPageAsync(string deviceUuid, string sphUserName);

        Task<TaskResult> SphPostAsync(string deviceUuid, string content, List<string> medias, int mediaType = 0, string cover = "");

        Task<TaskResult> SphCommentAsync(string deviceUuid, long feedId, string nonceId, string feedAuth, int type, string content, string media = "", long replyCommentId = 0, string replyUsername = "");

        Task<TaskResult> SphLikeAsync(string deviceUuid, long feedId, int type = 1, bool isCancel = false);

        Task<TaskResult> SphDelCommentAsync(string deviceUuid, long feedId, long commentId);

        Task<List<MomentsTimeline>> GetMomentsAsync(string deviceUuid, int count = 50, string weChatId = "");

        Task<List<FinderMentionNoticeDto>> GetFinderMentionHistoryAsync(string deviceUuid, int count = 10, bool? success = null, long? taskId = null, DateTimeOffset? receivedFrom = null, DateTimeOffset? receivedTo = null);

        Task<List<FinderUserPageDto>> GetFinderUserPageHistoryAsync(string deviceUuid, int count = 10, bool? success = null, long? taskId = null, DateTimeOffset? receivedFrom = null, DateTimeOffset? receivedTo = null);

        Task<List<FinderCommentListDto>> GetFinderCommentHistoryAsync(string deviceUuid, int count = 10, bool? success = null, long? taskId = null, DateTimeOffset? receivedFrom = null, DateTimeOffset? receivedTo = null);

        Task<FinderHistoryExportDto> ExportFinderHistoryAsync(string deviceUuid, int count = 10, bool? success = null, long? taskId = null, DateTimeOffset? receivedFrom = null, DateTimeOffset? receivedTo = null);

    }

}
