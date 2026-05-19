using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Data;
using SCRM.SHARED.Models.Dtos;
using SCRM.Services;
using SCRM.Services.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.API.Services.Core
{

    /// <summary>
    /// 客户端任务服务
    /// <para>负责向客户端(手机端)发送指令并处理同步/异步响应。</para>
    /// <para>核心功能：</para>
    /// <list type="bullet">
    /// <item>封装 Protobuf 消息构造与发送逻辑</item>
    /// <item>提供 SendTaskAndWaitAsync 机制，支持等待客户端的 TaskResultNotice 响应</item>
    /// <item>管理并行任务状态 (PendingTasks)</item>
    /// </list>
    /// </summary>
    public class ClientTaskService
    {
        private readonly NettyMessageService _nettyMessageService;
        private readonly Microsoft.Extensions.Logging.ILogger<ClientTaskService> _logger;
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

        /// <summary>
        /// 无等待任务上下文。
        /// <para>用于“服务端先返回已下发，客户端稍后再异步回执”的场景，补足失败结果的可读性。</para>
        /// </summary>
        private sealed class FireAndForgetTaskContext
        {
            /// <summary>
            /// 所属批次任务ID。
            /// <para>用于把逐条异步回执重新归并到同一条页面群发记录。</para>
            /// </summary>
            public long BatchTaskId { get; init; }

            /// <summary>
            /// 任务场景标识，仅用于日志诊断。
            /// </summary>
            public string TaskScene { get; init; } = string.Empty;

            /// <summary>
            /// 目标标识，例如 chatRoomId。
            /// </summary>
            public string TargetId { get; init; } = string.Empty;

            /// <summary>
            /// 失败时的摘要前缀。
            /// </summary>
            public string FailureSummary { get; init; } = string.Empty;

            /// <summary>
            /// 成功时的摘要前缀。
            /// </summary>
            public string SuccessSummary { get; init; } = string.Empty;
        }
        
        /// <summary>
        /// 挂起的任务字典: TaskId -> TaskCompletionSource (用于等待从 Netty 返回的异步结果)
        /// key: TaskId (long)
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, TaskCompletionSource<SCRM.SHARED.Models.Dtos.TaskResult>> _pendingTasks = new();

        /// <summary>
        /// 无等待任务上下文字典：TaskId -> 上下文。
        /// <para>当前主要用于“群页签逐群文本直发”这类 fire-and-forget 场景。</para>
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, FireAndForgetTaskContext> _fireAndForgetTaskContexts = new();

        /// <summary>
        /// 聊天发送任务上下文字典：TaskId -> 上下文。
        /// <para>
        /// TalkToFriendTaskResultNotice(1028) 只回传 MsgSvrId/CreateTime 等身份信息，不带 Content/ContentType；
        /// 服务端需要保存下发时的正文、类型、目标会话，才能在实时 WeChatTalkToFriendNotice 延迟或丢失时安全回填/补插本地消息。
        /// </para>
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, TalkToFriendTaskContext> _talkToFriendTaskContexts = new();

        /// <summary>聊天发送上下文保留时间。</summary>
        private static readonly TimeSpan TalkToFriendTaskContextTtl = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 朋友圈互动任务上下文字典：TaskId -> 上下文。
        /// <para>
        /// 安卓端点赞通常只回通用 TaskResultNotice，评论回包也不带评论文本；
        /// 服务端需要保留下发时的朋友圈 ID、评论内容、目标 wxid 等信息，才能在成功回执到达时立即写入本地时间线。
        /// </para>
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, MomentInteractionTaskContext> _momentInteractionTaskContexts = new();

        /// <summary>
        /// 朋友圈互动上下文保留时间。
        /// <para>超时回执仍可能稍后到达，因此保留一小段时间；超过该窗口按过期清理，避免长时间堆积。</para>
        /// </summary>
        private static readonly TimeSpan MomentInteractionTaskContextTtl = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 朋友圈发布任务上下文字典：TaskId -> 上下文。
        /// <para>PostSNSNewsTaskResultNotice 只回传 TaskId/CircleId/WeChatId，服务端需要保存下发时的附件类型、ClientRequestId 和可见范围摘要，才能处理超时、迟到回包和 UI 对账。</para>
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, MomentPostTaskContext> _momentPostTaskContexts = new();

        /// <summary>
        /// 朋友圈发布上下文保留时间。
        /// <para>Android Tsk47 可能 50 秒超时后继续用本地 DB 轮询补偿成功，因此这里保留 30 分钟用于迟到回包对账。</para>
        /// </summary>
        private static readonly TimeSpan MomentPostTaskContextTtl = TimeSpan.FromMinutes(30);

        /// <summary>
        /// 语音转文字任务上下文字典：TaskId -> 上下文。
        /// <para>
        /// Android 62203 语音转文字成功/失败都复用通用 TaskResultNotice，
        /// 回包只带 TaskId/TaskType/ErrMsg，不带 FriendId/MsgSvrId。
        /// 服务端必须在下发时保存上下文，结果回来后才能准确关联原语音消息并写入 VoiceToTextLogs。
        /// </para>
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, VoiceTransTextTaskContext> _voiceTransTextTaskContexts = new();

        /// <summary>
        /// 语音转文字任务上下文保留时间。
        /// <para>客户端超时约 35 秒，服务端等待 45 秒；这里多保留一段时间，用于处理迟到回包。</para>
        /// </summary>
        private static readonly TimeSpan VoiceTransTextTaskContextTtl = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 表情信息拉取任务上下文字典：TaskId -> 上下文。
        /// <para>PullEmojiInfoTaskResultNotice(1273) 自身没有 MsgSvrId/FriendId；消息级补图必须在下发 1272 时保存上下文。</para>
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, PullEmojiInfoTaskContext> _pullEmojiInfoTaskContexts = new();

        /// <summary>表情信息拉取上下文保留时间。</summary>
        private static readonly TimeSpan PullEmojiInfoTaskContextTtl = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 消息详情补偿任务上下文字典。
        /// <para>
        /// RequestTalkDetailTaskResultNotice(1029) 不携带 MsgSvrId/TaskId，服务端需要下发时保存上下文，
        /// 结果回来后才能把详情内容回填到原消息，避免 fallback 消息丢失 MsgSvrId。
        /// </para>
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RequestTalkDetailTaskContext> _requestTalkDetailTaskContexts = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>消息详情补偿上下文保留时间。</summary>
        private static readonly TimeSpan RequestTalkDetailTaskContextTtl = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 消息撤回任务上下文字典：TaskId -> 上下文。
        /// <para>RevokeMessageTask 也走通用 TaskResultNotice，服务端需要保留下发时的消息定位信息。</para>
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, RevokeMessageTaskContext> _revokeMessageTaskContexts = new();

        /// <summary>消息撤回上下文保留时间。</summary>
        private static readonly TimeSpan RevokeMessageTaskContextTtl = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 消息转发任务上下文字典：TaskId -> 上下文。
        /// <para>
        /// 三类转发任务都走通用 TaskResultNotice，回包不带目标会话；
        /// 服务端保存下发时的目标列表，用于成功后触发会话刷新和后续审计扩展。
        /// </para>
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, ForwardTaskContext> _forwardTaskContexts = new();

        /// <summary>消息转发上下文保留时间。</summary>
        private static readonly TimeSpan ForwardTaskContextTtl = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 联系人设置标签任务上下文字典：TaskId -> 上下文。
        /// <para>ContactSetLabelTask 结果是通用 TaskResultNotice，回包不带好友 wxid 与标签集合，因此下发时要保留上下文。</para>
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, ContactSetLabelTaskContext> _contactSetLabelTaskContexts = new();

        /// <summary>
        /// 标签创建/重命名任务上下文字典：TaskId -> 上下文。
        /// <para>仅在 LabelId 已知时用于成功回包后的本地标签字典乐观更新。</para>
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, ContactLabelTaskContext> _contactLabelTaskContexts = new();

        /// <summary>
        /// 标签删除任务上下文字典：TaskId -> 上下文。
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, ContactLabelDeleteTaskContext> _contactLabelDeleteTaskContexts = new();

        /// <summary>联系人标签任务上下文保留时间。</summary>
        private static readonly TimeSpan ContactLabelTaskContextTtl = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 群邀请审批任务上下文字典：TaskId -> 上下文。
        /// <para>ChatRoomInviteApproveTask 成功回包只带 TaskId/TaskType，需要下发时保存 WeChatId 和 MsgId 才能回写本地审批状态。</para>
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, ChatRoomInviteApproveTaskContext> _chatRoomInviteApproveTaskContexts = new();

        /// <summary>群邀请审批任务上下文保留时间。</summary>
        private static readonly TimeSpan ChatRoomInviteApproveTaskContextTtl = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 联系人补同步调度版本：同一连接同一原因只保留最新一轮。
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> _contactRefreshScheduleVersions = new();

        /// <summary>
        /// 朋友圈互动任务类型。
        /// </summary>
        public enum MomentInteractionKind
        {
            /// <summary>点赞或取消点赞。</summary>
            Like,

            /// <summary>发表评论或回复评论。</summary>
            CommentReply,

            /// <summary>删除评论。</summary>
            CommentDelete
        }

        /// <summary>
        /// 聊天发送任务上下文。
        /// <para>
        /// 下发 TalkToFriendTask 时保存，1028 回包时取回。WeChatId 可能在下发时未知，
        /// 结果处理会优先使用 1028/连接态中的账号 wxid，再回退到该字段。
        /// </para>
        /// </summary>
        public sealed class TalkToFriendTaskContext
        {
            /// <summary>任务 ID，同时作为本地消息 ID 使用。</summary>
            public long TaskId { get; init; }

            /// <summary>Netty 连接 ID。</summary>
            public string ConnectionId { get; init; } = string.Empty;

            /// <summary>微信账号 wxid；下发入口拿不到时允许为空。</summary>
            public string WeChatId { get; init; } = string.Empty;

            /// <summary>好友或群聊 wxid。</summary>
            public string FriendId { get; init; } = string.Empty;

            /// <summary>下发正文或媒体/结构化内容。</summary>
            public string Content { get; init; } = string.Empty;

            /// <summary>下发内容类型。</summary>
            public EnumContentType ContentType { get; init; } = EnumContentType.Text;

            /// <summary>群聊 @ 成员列表，多个 wxid 用英文逗号分隔。</summary>
            public string AtIds { get; init; } = string.Empty;

            /// <summary>上下文创建时间。</summary>
            public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

            /// <summary>上下文过期时间。</summary>
            public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.Add(TalkToFriendTaskContextTtl);
        }

        /// <summary>
        /// 朋友圈互动任务上下文。
        /// </summary>
        public sealed class MomentInteractionTaskContext
        {
            /// <summary>任务 ID。</summary>
            public long TaskId { get; init; }

            /// <summary>互动类型。</summary>
            public MomentInteractionKind Kind { get; init; }

            /// <summary>所属微信账号 wxid。</summary>
            public string WeChatId { get; init; } = string.Empty;

            /// <summary>朋友圈 snsId。</summary>
            public long CircleId { get; init; }

            /// <summary>是否取消点赞。</summary>
            public bool IsCancel { get; init; }

            /// <summary>评论或回复目标 wxid。</summary>
            public string ToWeChatId { get; init; } = string.Empty;

            /// <summary>评论内容。</summary>
            public string Content { get; init; } = string.Empty;

            /// <summary>被回复的评论 ID。</summary>
            public long ReplyCommentId { get; init; }

            /// <summary>删除评论时的评论 ID。</summary>
            public long CommentId { get; init; }

            /// <summary>发布时间。</summary>
            public long PublishTime { get; init; }

            /// <summary>过期时间。</summary>
            public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.Add(MomentInteractionTaskContextTtl);
        }

        /// <summary>
        /// 朋友圈发布任务上下文。
        /// <para>只保存对账所需的安全摘要，不保存正文、附件 URL、好友 wxid、标签或 POI 原文。</para>
        /// </summary>
        public sealed class MomentPostTaskContext
        {
            /// <summary>任务 ID。</summary>
            public long TaskId { get; init; }

            /// <summary>前端请求级幂等标识。</summary>
            public string ClientRequestId { get; init; } = string.Empty;

            /// <summary>目标微信账号。</summary>
            public string WeChatId { get; init; } = string.Empty;

            /// <summary>附件类型摘要。</summary>
            public string AttachmentType { get; init; } = string.Empty;

            /// <summary>附件数量。</summary>
            public int AttachmentCount { get; init; }

            /// <summary>可见范围类型摘要。</summary>
            public string VisibleType { get; init; } = string.Empty;

            /// <summary>标签数量。</summary>
            public int LabelCount { get; init; }

            /// <summary>好友数量。</summary>
            public int FriendCount { get; init; }

            /// <summary>提醒人数量。</summary>
            public int NotiUserCount { get; init; }

            /// <summary>追加评论数量。</summary>
            public int ExtCommentCount { get; init; }

            /// <summary>是否有首条评论。</summary>
            public bool HasComment { get; init; }

            /// <summary>是否包含 POI。</summary>
            public bool HasPoi { get; init; }

            /// <summary>是否使用慢速发布实验字段。</summary>
            public bool SendSlow { get; init; }

            /// <summary>归一化载荷摘要，不包含原文。</summary>
            public string PayloadHash { get; init; } = string.Empty;

            /// <summary>正文、评论和 POI 外显文本摘要，不包含原文。</summary>
            public string ContentHash { get; init; } = string.Empty;

            /// <summary>可见范围目标摘要，不包含标签或 wxid 原文。</summary>
            public string VisibleTargetsHash { get; init; } = string.Empty;

            /// <summary>提醒谁看目标摘要，不包含 wxid 原文。</summary>
            public string NotiUsersHash { get; init; } = string.Empty;

            /// <summary>附件内容摘要，不包含 URL 或 JSON 原文。</summary>
            public string AttachmentHash { get; init; } = string.Empty;

            /// <summary>实际下发微信账号摘要，不包含 wxid 原文。</summary>
            public string EffectiveWeChatIdHash { get; init; } = string.Empty;

            /// <summary>任务上下文创建时间。</summary>
            public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

            /// <summary>任务上下文过期时间。</summary>
            public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.Add(MomentPostTaskContextTtl);
        }

        /// <summary>
        /// 语音转文字任务上下文。
        /// </summary>
        public sealed class VoiceTransTextTaskContext
        {
            /// <summary>任务 ID。</summary>
            public long TaskId { get; init; }

            /// <summary>所属微信账号 wxid。</summary>
            public string WeChatId { get; init; } = string.Empty;

            /// <summary>好友或群会话 wxid。</summary>
            public string FriendId { get; init; } = string.Empty;

            /// <summary>目标语音消息的服务器消息 ID。</summary>
            public long MsgSvrId { get; init; }

            /// <summary>过期时间。</summary>
            public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.Add(VoiceTransTextTaskContextTtl);
        }

        /// <summary>
        /// 表情信息拉取任务上下文。
        /// <para>用于把 PullEmojiInfoTaskResultNotice(1273) 与某条聊天表情消息绑定，随后自动衔接 CDNDownloadFileTask(1269)。</para>
        /// </summary>
        public sealed class PullEmojiInfoTaskContext
        {
            /// <summary>任务 ID。</summary>
            public long TaskId { get; init; }

            /// <summary>Netty 连接 ID。</summary>
            public string ConnectionId { get; init; } = string.Empty;

            /// <summary>所属微信账号 wxid。</summary>
            public string WeChatId { get; init; } = string.Empty;

            /// <summary>表情 MD5。</summary>
            public string Md5 { get; init; } = string.Empty;

            /// <summary>目标聊天消息服务器 ID。</summary>
            public long MsgSvrId { get; init; }

            /// <summary>好友或群会话 wxid，可为空，仅用于日志诊断。</summary>
            public string FriendId { get; init; } = string.Empty;

            /// <summary>过期时间。</summary>
            public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.Add(PullEmojiInfoTaskContextTtl);
        }

        /// <summary>
        /// 消息详情补偿任务上下文。
        /// </summary>
        public sealed class RequestTalkDetailTaskContext
        {
            /// <summary>任务 ID。</summary>
            public long TaskId { get; init; }

            /// <summary>所属微信账号 wxid。</summary>
            public string WeChatId { get; init; } = string.Empty;

            /// <summary>好友或群会话 wxid。</summary>
            public string FriendId { get; init; } = string.Empty;

            /// <summary>微信本地消息 ID。</summary>
            public long MsgId { get; init; }

            /// <summary>微信服务器消息 ID。</summary>
            public long MsgSvrId { get; init; }

            /// <summary>媒体 MD5，原图/文件详情补偿时可能使用。</summary>
            public string Md5 { get; init; } = string.Empty;

            /// <summary>是否请求原始资源。</summary>
            public bool GetOriginal { get; init; }

            /// <summary>过期时间。</summary>
            public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.Add(RequestTalkDetailTaskContextTtl);
        }

        /// <summary>
        /// 消息撤回任务上下文。
        /// </summary>
        public sealed class RevokeMessageTaskContext
        {
            /// <summary>任务 ID。</summary>
            public long TaskId { get; init; }

            /// <summary>所属微信账号 wxid。</summary>
            public string WeChatId { get; init; } = string.Empty;

            /// <summary>好友或群会话 wxid。</summary>
            public string FriendId { get; init; } = string.Empty;

            /// <summary>被撤回消息的服务器消息 ID。</summary>
            public long MsgSvrId { get; init; }

            /// <summary>过期时间。</summary>
            public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.Add(RevokeMessageTaskContextTtl);
        }

        /// <summary>
        /// 消息转发任务上下文。
        /// </summary>
        public sealed class ForwardTaskContext
        {
            /// <summary>任务 ID。</summary>
            public long TaskId { get; init; }

            /// <summary>所属微信账号 wxid。</summary>
            public string WeChatId { get; init; } = string.Empty;

            /// <summary>转发类型：single / multi / byContent。</summary>
            public string ForwardType { get; init; } = string.Empty;

            /// <summary>原始消息所在会话；按内容兜底转发可能为空。</summary>
            public string SourceTalker { get; init; } = string.Empty;

            /// <summary>原始消息 MsgSvrId 列表；纯内容兜底可能为空。</summary>
            public IReadOnlyList<long> SourceMsgSvrIds { get; init; } = Array.Empty<long>();

            /// <summary>目标好友或群聊 wxid 列表。</summary>
            public IReadOnlyList<string> TargetFriendIds { get; init; } = Array.Empty<string>();

            /// <summary>转发附加说明；single / multi / byContent 三类转发均会保留该上下文。</summary>
            public string ExtMsg { get; init; } = string.Empty;

            /// <summary>是否按聊天记录样式转发。</summary>
            public bool SendRecord { get; init; }

            /// <summary>过期时间。</summary>
            public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.Add(ForwardTaskContextTtl);
        }

        /// <summary>
        /// 设置联系人标签任务上下文。
        /// </summary>
        public sealed class ContactSetLabelTaskContext
        {
            /// <summary>任务 ID。</summary>
            public long TaskId { get; init; }

            /// <summary>所属微信账号 wxid。</summary>
            public string WeChatId { get; init; } = string.Empty;

            /// <summary>好友 wxid。</summary>
            public string FriendId { get; init; } = string.Empty;

            /// <summary>完整标签 ID 集合。</summary>
            public IReadOnlyList<int> LabelIds { get; init; } = Array.Empty<int>();

            /// <summary>过期时间。</summary>
            public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.Add(ContactLabelTaskContextTtl);
        }

        /// <summary>
        /// 创建或重命名标签任务上下文。
        /// </summary>
        public sealed class ContactLabelTaskContext
        {
            /// <summary>任务 ID。</summary>
            public long TaskId { get; init; }

            /// <summary>所属微信账号 wxid。</summary>
            public string WeChatId { get; init; } = string.Empty;

            /// <summary>微信端标签 ID；0 表示创建新标签，需等待 ContactLabelAddNotice 返回真实 ID。</summary>
            public int LabelId { get; init; }

            /// <summary>标签名。</summary>
            public string LabelName { get; init; } = string.Empty;

            /// <summary>过期时间。</summary>
            public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.Add(ContactLabelTaskContextTtl);
        }

        /// <summary>
        /// 删除标签任务上下文。
        /// </summary>
        public sealed class ContactLabelDeleteTaskContext
        {
            /// <summary>任务 ID。</summary>
            public long TaskId { get; init; }

            /// <summary>所属微信账号 wxid。</summary>
            public string WeChatId { get; init; } = string.Empty;

            /// <summary>微信端标签 ID。</summary>
            public int LabelId { get; init; }

            /// <summary>过期时间。</summary>
            public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.Add(ContactLabelTaskContextTtl);
        }

        /// <summary>
        /// 群邀请审批任务上下文。
        /// </summary>
        public sealed class ChatRoomInviteApproveTaskContext
        {
            /// <summary>任务 ID。</summary>
            public long TaskId { get; init; }

            /// <summary>所属微信账号 wxid。</summary>
            public string WeChatId { get; init; } = string.Empty;

            /// <summary>群聊 wxid。</summary>
            public string RoomId { get; init; } = string.Empty;

            /// <summary>62203 群邀请确认主链使用的消息 ID。</summary>
            public long MsgId { get; init; }

            /// <summary>过期时间。</summary>
            public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.Add(ChatRoomInviteApproveTaskContextTtl);
        }

        public ClientTaskService(
            NettyMessageService nettyMessageService,
            Microsoft.Extensions.Logging.ILogger<ClientTaskService> logger,
            IDbContextFactory<ApplicationDbContext> dbContextFactory)
        {
            _nettyMessageService = nettyMessageService;
            _logger = logger;
            _dbContextFactory = dbContextFactory;
        }

        /// <summary>
        /// 完成挂起的任务
        /// 当 MessageRouter 收到 TaskResultNotice 时调用
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="success">是否成功</param>
        /// <param name="message">错误信息或结果描述</param>
        public void CompleteTask(long taskId, bool success, string? message = null, object? data = null)
        {
            if (_pendingTasks.TryRemove(taskId, out var tcs))
            {
                var normalizedMessage = string.IsNullOrWhiteSpace(message)
                    ? (success ? string.Empty : "任务执行失败")
                    : message.Trim();
                tcs.TrySetResult(new SCRM.SHARED.Models.Dtos.TaskResult
                {
                    taskId = taskId,
                    success = success,
                    message = normalizedMessage,
                    data = data
                });
            }
        }

        /// <summary>
        /// 判断任务是否仍在同步等待窗口内。
        /// <para>用于识别 Android timeout 后通过本地 DB 轮询补偿回来的迟到发圈结果。</para>
        /// </summary>
        public bool IsTaskPending(long taskId)
        {
            return taskId > 0 && _pendingTasks.ContainsKey(taskId);
        }

        /// <summary>
        /// 取走无等待任务上下文。
        /// <para>收到客户端异步结果时调用，用来把“裸 TalkToFriendTaskResultNotice”还原成更可读的业务失败信息。</para>
        /// </summary>
        public bool TryTakeFireAndForgetTaskContext(
            long taskId,
            out long batchTaskId,
            out string taskScene,
            out string targetId,
            out string failureSummary,
            out string successSummary)
        {
            if (_fireAndForgetTaskContexts.TryRemove(taskId, out var context) && context != null)
            {
                batchTaskId = context.BatchTaskId;
                taskScene = context.TaskScene;
                targetId = context.TargetId;
                failureSummary = context.FailureSummary;
                successSummary = context.SuccessSummary;
                return true;
            }

            batchTaskId = 0L;
            taskScene = string.Empty;
            targetId = string.Empty;
            failureSummary = string.Empty;
            successSummary = string.Empty;
            return false;
        }

        /// <summary>
        /// 注册聊天发送上下文。
        /// <para>必须在真正下发到 Netty 前保存，避免客户端极快返回 1028 时服务端还没有上下文。</para>
        /// </summary>
        private void RegisterTalkToFriendTaskContext(TalkToFriendTaskContext context)
        {
            if (context == null || context.TaskId == 0 || string.IsNullOrWhiteSpace(context.FriendId))
            {
                return;
            }

            PruneExpiredTalkToFriendTaskContexts();
            _talkToFriendTaskContexts[context.TaskId] = context;
        }

        /// <summary>
        /// 取走聊天发送上下文。
        /// <para>1028 成功或失败都应取走，避免同一回执被重复处理。</para>
        /// </summary>
        public bool TryTakeTalkToFriendTaskContext(long taskId, out TalkToFriendTaskContext? context)
        {
            PruneExpiredTalkToFriendTaskContexts();
            if (_talkToFriendTaskContexts.TryRemove(taskId, out context)
                && context.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }

            context = null;
            return false;
        }

        /// <summary>
        /// 移除聊天发送上下文。
        /// <para>仅用于任务没有成功送达 Netty 的场景；等待超时仍保留到 TTL，用于消费迟到 1028。</para>
        /// </summary>
        private void RemoveTalkToFriendTaskContext(long taskId)
        {
            if (taskId == 0)
            {
                return;
            }

            _talkToFriendTaskContexts.TryRemove(taskId, out _);
        }

        /// <summary>
        /// 清理过期的聊天发送上下文。
        /// </summary>
        private void PruneExpiredTalkToFriendTaskContexts()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _talkToFriendTaskContexts)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _talkToFriendTaskContexts.TryRemove(pair.Key, out _);
                }
            }
        }

        /// <summary>
        /// 注册朋友圈互动任务上下文。
        /// <para>由发送任务时调用，等待 TaskMessageHandler 在成功回执到达后取回并完成本地时间线增量更新。</para>
        /// </summary>
        private void RegisterMomentInteractionTaskContext(MomentInteractionTaskContext context)
        {
            if (context == null || context.TaskId == 0)
            {
                return;
            }

            PruneExpiredMomentInteractionTaskContexts();
            _momentInteractionTaskContexts[context.TaskId] = context;
        }

        /// <summary>
        /// 查询朋友圈互动任务上下文。
        /// <para>成功回执、失败回执都可能需要读取上下文；失败时也会移除，避免重复处理。</para>
        /// </summary>
        public bool TryTakeMomentInteractionTaskContext(long taskId, out MomentInteractionTaskContext? context)
        {
            PruneExpiredMomentInteractionTaskContexts();
            if (_momentInteractionTaskContexts.TryRemove(taskId, out context)
                && context.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }

            context = null;
            return false;
        }

        /// <summary>
        /// 清理过期的朋友圈互动任务上下文。
        /// </summary>
        private void PruneExpiredMomentInteractionTaskContexts()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _momentInteractionTaskContexts)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _momentInteractionTaskContexts.TryRemove(pair.Key, out _);
                }
            }
        }

        /// <summary>
        /// 注册朋友圈发布任务上下文。
        /// </summary>
        private void RegisterMomentPostTaskContext(MomentPostTaskContext context)
        {
            if (context == null || context.TaskId == 0)
            {
                return;
            }

            PruneExpiredMomentPostTaskContexts();
            _momentPostTaskContexts[context.TaskId] = context;
        }

        /// <summary>
        /// 查询朋友圈发布任务上下文。
        /// <para>不立即移除，允许同步回包、迟到回包和 CircleNewPublishNotice 后续对账共用同一上下文。</para>
        /// </summary>
        public bool TryGetMomentPostTaskContext(long taskId, out MomentPostTaskContext? context)
        {
            PruneExpiredMomentPostTaskContexts();
            if (_momentPostTaskContexts.TryGetValue(taskId, out context)
                && context.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }

            context = null;
            return false;
        }

        /// <summary>
        /// 删除朋友圈发布任务上下文。
        /// </summary>
        private void RemoveMomentPostTaskContext(long taskId)
        {
            if (taskId > 0)
            {
                _momentPostTaskContexts.TryRemove(taskId, out _);
            }
        }

        /// <summary>
        /// 清理过期的朋友圈发布任务上下文。
        /// </summary>
        private void PruneExpiredMomentPostTaskContexts()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _momentPostTaskContexts)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _momentPostTaskContexts.TryRemove(pair.Key, out _);
                }
            }
        }

        /// <summary>
        /// 注册语音转文字任务上下文。
        /// </summary>
        private void RegisterVoiceTransTextTaskContext(VoiceTransTextTaskContext context)
        {
            if (context == null || context.TaskId == 0)
            {
                return;
            }

            PruneExpiredVoiceTransTextTaskContexts();
            _voiceTransTextTaskContexts[context.TaskId] = context;
        }

        /// <summary>
        /// 取走语音转文字任务上下文。
        /// <para>通用 TaskResultNotice 到达后调用，取走即认为本次结果已经消费。</para>
        /// </summary>
        public bool TryTakeVoiceTransTextTaskContext(long taskId, out VoiceTransTextTaskContext? context)
        {
            PruneExpiredVoiceTransTextTaskContexts();
            if (_voiceTransTextTaskContexts.TryRemove(taskId, out context)
                && context.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }

            context = null;
            return false;
        }

        /// <summary>
        /// 清理过期的语音转文字任务上下文。
        /// </summary>
        private void PruneExpiredVoiceTransTextTaskContexts()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _voiceTransTextTaskContexts)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _voiceTransTextTaskContexts.TryRemove(pair.Key, out _);
                }
            }
        }

        /// <summary>
        /// 注册消息级表情信息拉取上下文。
        /// <para>必须在下发 1272 前保存，避免 1273 极快返回时找不到 MsgSvrId。</para>
        /// </summary>
        private void RegisterPullEmojiInfoTaskContext(PullEmojiInfoTaskContext context)
        {
            if (context == null
                || context.TaskId == 0
                || string.IsNullOrWhiteSpace(context.ConnectionId)
                || string.IsNullOrWhiteSpace(context.WeChatId)
                || string.IsNullOrWhiteSpace(context.Md5)
                || context.MsgSvrId == 0)
            {
                return;
            }

            PruneExpiredPullEmojiInfoTaskContexts();
            _pullEmojiInfoTaskContexts[context.TaskId] = context;
        }

        /// <summary>
        /// 取走消息级表情信息拉取上下文。
        /// <para>1273 到达后取走并衔接 1269，避免同一结果重复触发 CDN 下载。</para>
        /// </summary>
        public bool TryTakePullEmojiInfoTaskContext(long taskId, out PullEmojiInfoTaskContext? context)
        {
            PruneExpiredPullEmojiInfoTaskContexts();
            if (_pullEmojiInfoTaskContexts.TryRemove(taskId, out context)
                && context.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }

            context = null;
            return false;
        }

        /// <summary>
        /// 移除表情信息拉取上下文。
        /// <para>仅用于任务没有成功送达 Netty；等待超时仍保留到 TTL，用于消费迟到 1273。</para>
        /// </summary>
        private void RemovePullEmojiInfoTaskContext(long taskId)
        {
            if (taskId == 0)
            {
                return;
            }

            _pullEmojiInfoTaskContexts.TryRemove(taskId, out _);
        }

        /// <summary>
        /// 清理过期的表情信息拉取上下文。
        /// </summary>
        private void PruneExpiredPullEmojiInfoTaskContexts()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _pullEmojiInfoTaskContexts)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _pullEmojiInfoTaskContexts.TryRemove(pair.Key, out _);
                }
            }
        }

        /// <summary>
        /// 注册消息详情补偿上下文。
        /// <para>同一上下文按 TaskId、MsgId、MsgSvrId、FriendId 多键保存，便于 1029 回包缺字段时兜底定位。</para>
        /// </summary>
        private void RegisterRequestTalkDetailTaskContext(RequestTalkDetailTaskContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.WeChatId) || context.MsgSvrId == 0)
            {
                return;
            }

            PruneExpiredRequestTalkDetailTaskContexts();
            foreach (var key in BuildRequestTalkDetailTaskContextKeys(context))
            {
                _requestTalkDetailTaskContexts[key] = context;
            }
        }

        /// <summary>
        /// 按 1029 回包字段取走消息详情补偿上下文。
        /// </summary>
        public bool TryTakeRequestTalkDetailTaskContext(
            string weChatId,
            long msgId,
            string? friendId,
            out RequestTalkDetailTaskContext? context)
        {
            PruneExpiredRequestTalkDetailTaskContexts();

            foreach (var key in BuildRequestTalkDetailLookupKeys(weChatId, msgId, friendId))
            {
                if (_requestTalkDetailTaskContexts.TryGetValue(key, out context)
                    && context.ExpiresAt > DateTimeOffset.UtcNow)
                {
                    RemoveRequestTalkDetailTaskContext(context);
                    return true;
                }
            }

            var normalizedWeChatId = weChatId?.Trim() ?? string.Empty;
            var normalizedFriendId = friendId?.Trim() ?? string.Empty;
            var candidates = _requestTalkDetailTaskContexts.Values
                .Where(item => item.ExpiresAt > DateTimeOffset.UtcNow
                    && string.Equals(item.WeChatId, normalizedWeChatId, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(normalizedFriendId)
                        || string.Equals(item.FriendId, normalizedFriendId, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(item => item.TaskId)
                .Select(group => group.First())
                .OrderByDescending(item => item.ExpiresAt)
                .ToList();

            context = candidates.FirstOrDefault();
            if (context != null)
            {
                RemoveRequestTalkDetailTaskContext(context);
                return true;
            }

            context = null;
            return false;
        }

        private void RemoveRequestTalkDetailTaskContext(RequestTalkDetailTaskContext context)
        {
            foreach (var key in BuildRequestTalkDetailTaskContextKeys(context))
            {
                if (_requestTalkDetailTaskContexts.TryGetValue(key, out var existing)
                    && existing.TaskId == context.TaskId)
                {
                    _requestTalkDetailTaskContexts.TryRemove(key, out _);
                }
            }
        }

        private void PruneExpiredRequestTalkDetailTaskContexts()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _requestTalkDetailTaskContexts)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _requestTalkDetailTaskContexts.TryRemove(pair.Key, out _);
                }
            }
        }

        private static IEnumerable<string> BuildRequestTalkDetailTaskContextKeys(RequestTalkDetailTaskContext context)
        {
            if (context.TaskId > 0)
            {
                yield return $"task:{context.TaskId}";
            }

            if (context.MsgId > 0)
            {
                yield return $"msg:{NormalizeTaskContextKeyPart(context.WeChatId)}:{context.MsgId}";
            }

            if (context.MsgSvrId > 0)
            {
                yield return $"svr:{NormalizeTaskContextKeyPart(context.WeChatId)}:{context.MsgSvrId}";
            }

            if (!string.IsNullOrWhiteSpace(context.FriendId))
            {
                yield return $"friend:{NormalizeTaskContextKeyPart(context.WeChatId)}:{NormalizeTaskContextKeyPart(context.FriendId)}";
            }
        }

        private static IEnumerable<string> BuildRequestTalkDetailLookupKeys(string? weChatId, long msgId, string? friendId)
        {
            var normalizedWeChatId = NormalizeTaskContextKeyPart(weChatId);
            if (msgId > 0)
            {
                yield return $"msg:{normalizedWeChatId}:{msgId}";
            }

            if (!string.IsNullOrWhiteSpace(friendId))
            {
                yield return $"friend:{normalizedWeChatId}:{NormalizeTaskContextKeyPart(friendId)}";
            }
        }

        private static string NormalizeTaskContextKeyPart(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();
        }

        /// <summary>
        /// 注册消息撤回任务上下文。
        /// </summary>
        private void RegisterRevokeMessageTaskContext(RevokeMessageTaskContext context)
        {
            if (context == null || context.TaskId == 0)
            {
                return;
            }

            PruneExpiredRevokeMessageTaskContexts();
            _revokeMessageTaskContexts[context.TaskId] = context;
        }

        /// <summary>
        /// 取走消息撤回任务上下文。
        /// </summary>
        public bool TryTakeRevokeMessageTaskContext(long taskId, out RevokeMessageTaskContext? context)
        {
            PruneExpiredRevokeMessageTaskContexts();
            if (_revokeMessageTaskContexts.TryRemove(taskId, out context)
                && context.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }

            context = null;
            return false;
        }

        /// <summary>
        /// 清理过期的消息撤回任务上下文。
        /// </summary>
        private void PruneExpiredRevokeMessageTaskContexts()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _revokeMessageTaskContexts)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _revokeMessageTaskContexts.TryRemove(pair.Key, out _);
                }
            }
        }

        /// <summary>
        /// 注册消息转发任务上下文。
        /// </summary>
        private void RegisterForwardTaskContext(ForwardTaskContext context)
        {
            if (context == null || context.TaskId == 0)
            {
                return;
            }

            PruneExpiredForwardTaskContexts();
            _forwardTaskContexts[context.TaskId] = context;
        }

        /// <summary>
        /// 取走消息转发任务上下文。
        /// </summary>
        public bool TryTakeForwardTaskContext(long taskId, out ForwardTaskContext? context)
        {
            PruneExpiredForwardTaskContexts();
            if (_forwardTaskContexts.TryRemove(taskId, out context)
                && context.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }

            context = null;
            return false;
        }

        /// <summary>
        /// 移除消息转发任务上下文。
        /// <para>仅用于任务未成功送达 Netty 的场景；超时上下文保留到 TTL，以便消费迟到成功回执。</para>
        /// </summary>
        private void RemoveForwardTaskContext(long taskId)
        {
            if (taskId == 0)
            {
                return;
            }

            _forwardTaskContexts.TryRemove(taskId, out _);
        }

        /// <summary>
        /// 清理过期的消息转发任务上下文。
        /// </summary>
        private void PruneExpiredForwardTaskContexts()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _forwardTaskContexts)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _forwardTaskContexts.TryRemove(pair.Key, out _);
                }
            }
        }

        /// <summary>
        /// 注册设置联系人标签任务上下文。
        /// </summary>
        private void RegisterContactSetLabelTaskContext(ContactSetLabelTaskContext context)
        {
            if (context == null || context.TaskId == 0)
            {
                return;
            }

            PruneExpiredContactLabelTaskContexts();
            _contactSetLabelTaskContexts[context.TaskId] = context;
        }

        /// <summary>
        /// 取走设置联系人标签任务上下文。
        /// </summary>
        public bool TryTakeContactSetLabelTaskContext(long taskId, out ContactSetLabelTaskContext? context)
        {
            PruneExpiredContactLabelTaskContexts();
            if (_contactSetLabelTaskContexts.TryRemove(taskId, out context)
                && context.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }

            context = null;
            return false;
        }

        /// <summary>
        /// 注册标签创建/重命名任务上下文。
        /// </summary>
        private void RegisterContactLabelTaskContext(ContactLabelTaskContext context)
        {
            if (context == null || context.TaskId == 0)
            {
                return;
            }

            PruneExpiredContactLabelTaskContexts();
            _contactLabelTaskContexts[context.TaskId] = context;
        }

        /// <summary>
        /// 取走标签创建/重命名任务上下文。
        /// </summary>
        public bool TryTakeContactLabelTaskContext(long taskId, out ContactLabelTaskContext? context)
        {
            PruneExpiredContactLabelTaskContexts();
            if (_contactLabelTaskContexts.TryRemove(taskId, out context)
                && context.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }

            context = null;
            return false;
        }

        /// <summary>
        /// 注册删除标签任务上下文。
        /// </summary>
        private void RegisterContactLabelDeleteTaskContext(ContactLabelDeleteTaskContext context)
        {
            if (context == null || context.TaskId == 0)
            {
                return;
            }

            PruneExpiredContactLabelTaskContexts();
            _contactLabelDeleteTaskContexts[context.TaskId] = context;
        }

        /// <summary>
        /// 取走删除标签任务上下文。
        /// </summary>
        public bool TryTakeContactLabelDeleteTaskContext(long taskId, out ContactLabelDeleteTaskContext? context)
        {
            PruneExpiredContactLabelTaskContexts();
            if (_contactLabelDeleteTaskContexts.TryRemove(taskId, out context)
                && context.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }

            context = null;
            return false;
        }

        /// <summary>
        /// 注册群邀请审批任务上下文。
        /// </summary>
        private void RegisterChatRoomInviteApproveTaskContext(ChatRoomInviteApproveTaskContext context)
        {
            if (context == null || context.TaskId == 0 || context.MsgId == 0 || string.IsNullOrWhiteSpace(context.WeChatId))
            {
                return;
            }

            PruneExpiredChatRoomInviteApproveTaskContexts();
            _chatRoomInviteApproveTaskContexts[context.TaskId] = context;
        }

        /// <summary>
        /// 取走群邀请审批任务上下文。
        /// </summary>
        public bool TryTakeChatRoomInviteApproveTaskContext(long taskId, out ChatRoomInviteApproveTaskContext? context)
        {
            PruneExpiredChatRoomInviteApproveTaskContexts();
            if (_chatRoomInviteApproveTaskContexts.TryRemove(taskId, out context)
                && context.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }

            context = null;
            return false;
        }

        /// <summary>
        /// 清理过期的群邀请审批任务上下文。
        /// </summary>
        private void PruneExpiredChatRoomInviteApproveTaskContexts()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _chatRoomInviteApproveTaskContexts)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _chatRoomInviteApproveTaskContexts.TryRemove(pair.Key, out _);
                }
            }
        }

        /// <summary>
        /// 清理过期的联系人标签任务上下文。
        /// </summary>
        private void PruneExpiredContactLabelTaskContexts()
        {
            var now = DateTimeOffset.UtcNow;

            foreach (var pair in _contactSetLabelTaskContexts)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _contactSetLabelTaskContexts.TryRemove(pair.Key, out _);
                }
            }

            foreach (var pair in _contactLabelTaskContexts)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _contactLabelTaskContexts.TryRemove(pair.Key, out _);
                }
            }

            foreach (var pair in _contactLabelDeleteTaskContexts)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _contactLabelDeleteTaskContexts.TryRemove(pair.Key, out _);
                }
            }
        }

        /// <summary>
        /// 发送心跳请求 (1001)
        /// </summary>
        public async Task<bool> SendHeartBeatAsync(string connectionId)
        {
            var task = new HeartBeatMessage();
            return await _nettyMessageService.SendMessageToNettyAsync(
                task, 
                EnumMsgType.HeartBeatReq.ToString(), 
                connectionId);
        }

        /// <summary>
        /// 发送给好友发消息任务 (1.1)
        /// 此任务需要等待客户端的 TaskResultNotice 返回结果
        /// </summary>
        /// <param name="connectionId">连接ID</param>
        /// <param name="friendWxId">好友微信号</param>
        /// <param name="content">消息内容</param>
        /// <param name="contentType">消息类型</param>
        /// <returns>任务执行结果</returns>
        /// <summary>
        /// 通用任务发送并等待结果帮助方法
        /// </summary>
        private async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendTaskAndWaitAsync(
            Google.Protobuf.IMessage taskMessage,
            string msgType,
            string connectionId,
            long taskId,
            int timeoutMs = 15000)
        {
            var tcs = new TaskCompletionSource<SCRM.SHARED.Models.Dtos.TaskResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingTasks.TryAdd(taskId, tcs);

            _logger.LogInformation("SendTask start: Type={MsgType}, ConnectionId={ConnectionId}, TaskId={TaskId}, TimeoutMs={TimeoutMs}",
                msgType, connectionId, taskId, timeoutMs);
            var sent = await _nettyMessageService.SendMessageToNettyAsync(taskMessage, msgType, connectionId, customMessageId: taskId);

            if (!sent)
            {
                _pendingTasks.TryRemove(taskId, out _);
                _logger.LogWarning("SendTask failed before wait: Type={MsgType}, ConnectionId={ConnectionId}, TaskId={TaskId}",
                    msgType, connectionId, taskId);
                return new SCRM.SHARED.Models.Dtos.TaskResult
                {
                    taskId = taskId,
                    success = false,
                    message = "Failed to send to Netty"
                };
            }

            var timeoutTask = Task.Delay(timeoutMs);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingTasks.TryRemove(taskId, out _);
                _logger.LogWarning("SendTask timeout: Type={MsgType}, ConnectionId={ConnectionId}, TaskId={TaskId}, TimeoutMs={TimeoutMs}",
                    msgType, connectionId, taskId, timeoutMs);
                return new SCRM.SHARED.Models.Dtos.TaskResult
                {
                    taskId = taskId,
                    success = false,
                    message = "Timeout waiting for client response"
                };
            }

            var result = await tcs.Task;
            if (string.IsNullOrWhiteSpace(result.message))
            {
                result.message = BuildDefaultTaskResultMessage(msgType, result.success);
            }
            _logger.LogInformation("SendTask completed: Type={MsgType}, ConnectionId={ConnectionId}, TaskId={TaskId}, Success={Success}, Message={Message}",
                msgType, connectionId, taskId, result.success, result.message);
            return result;
        }

        /// <summary>
        /// 生成默认任务回执文案。
        /// <para>安卓端不少成功回执 message 为空，前端无法判断要不要延迟刷新；这里统一补业务可读文案。</para>
        /// </summary>
        private static string BuildDefaultTaskResultMessage(string msgType, bool success)
        {
            if (string.Equals(msgType, EnumMsgType.TalkToFriendTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "聊天消息任务已完成" : "聊天消息任务执行失败";
            }

            if (string.Equals(msgType, EnumMsgType.SendMultiPictureTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "图片任务已完成" : "图片任务执行失败";
            }

            if (string.Equals(msgType, EnumMsgType.VoiceTransTextTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "语音转文字任务已完成" : "语音转文字任务执行失败";
            }

            if (string.Equals(msgType, EnumMsgType.RevokeMessageTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "消息撤回成功，正在刷新会话" : "消息撤回失败";
            }

            if (string.Equals(msgType, EnumMsgType.ForwardMessageTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "消息转发任务已完成，正在等待微信消息同步" : "消息转发失败";
            }

            if (string.Equals(msgType, EnumMsgType.ForwardMultiMessageTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "多条消息转发任务已完成，正在等待微信消息同步" : "多条消息转发失败";
            }

            if (string.Equals(msgType, EnumMsgType.ForwardMessageByContentTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "原始内容转发任务已完成，正在等待微信消息同步" : "原始内容转发失败";
            }

            if (string.Equals(msgType, EnumMsgType.ClearAllChatMsgTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "微信端聊天记录清空任务已完成" : "微信端聊天记录清空失败";
            }

            if (string.Equals(msgType, EnumMsgType.SendLuckyMoneyTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "发红包任务已完成" : "发红包任务执行失败";
            }

            if (string.Equals(msgType, EnumMsgType.RemittanceTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "转账任务已完成" : "转账任务执行失败";
            }

            if (string.Equals(msgType, EnumMsgType.TriggerLabelPushTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "标签列表同步指令已下发" : "标签列表同步指令下发失败";
            }

            if (string.Equals(msgType, EnumMsgType.ContactLabelTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "联系人标签任务已完成，等待标签列表校准" : "联系人标签任务执行失败";
            }

            if (string.Equals(msgType, EnumMsgType.ContactLabelDeleteTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "联系人标签删除成功，等待标签列表校准" : "联系人标签删除失败";
            }

            if (string.Equals(msgType, EnumMsgType.ContactSetLabelTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "联系人标签设置成功，正在刷新联系人" : "联系人标签设置失败";
            }

            if (string.Equals(msgType, EnumMsgType.SendSmsTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "短信发送任务已完成" : "短信发送失败";
            }

            if (string.Equals(msgType, EnumMsgType.PullSmsTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "短信历史拉取完成，正在刷新列表" : "短信历史拉取失败";
            }

            if (string.Equals(msgType, EnumMsgType.PullCallLogTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "通话记录拉取完成，正在刷新列表" : "通话记录拉取失败";
            }

            if (string.Equals(msgType, EnumMsgType.AddFriendsTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "手机号加好友任务已完成，正在刷新联系人" : "手机号加好友失败";
            }

            if (string.Equals(msgType, EnumMsgType.AddFriendFromPhonebookTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "通讯录加好友任务已完成，正在刷新联系人" : "通讯录加好友失败";
            }

            if (string.Equals(msgType, EnumMsgType.AddFriendNameCardTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "名片加好友任务已完成，正在刷新联系人" : "名片加好友失败";
            }

            if (string.Equals(msgType, EnumMsgType.SendFriendVerifyTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "好友验证已重新发送，正在等待联系人同步" : "好友验证发送失败";
            }

            if (string.Equals(msgType, EnumMsgType.AcceptFriendAddRequestTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "好友申请处理完成，正在刷新联系人" : "好友申请处理失败";
            }

            if (string.Equals(msgType, EnumMsgType.ModifyFriendMemoTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "好友备注修改完成，正在刷新联系人" : "好友备注修改失败";
            }

            if (string.Equals(msgType, EnumMsgType.SetFriendPermissionTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "好友权限设置完成，正在刷新联系人" : "好友权限设置失败";
            }

            if (string.Equals(msgType, EnumMsgType.GetA8KeyTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "A8Key 获取任务已完成" : "A8Key 获取失败";
            }

            if (string.Equals(msgType, EnumMsgType.WechatSettingTask.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return success ? "微信资料设置任务已完成" : "微信资料设置失败";
            }

            return success ? "任务已完成" : "任务执行失败";
        }

        /// <summary>
        /// 联系人变更后的全量联系人补同步。
        /// <para>
        /// 加好友/删好友成功后，安卓端通常会主动上报 FriendPushNotice 或 FriendDelNotice；
        /// 但实机日志显示加好友成功后可能只先返回 TaskResultNotice / FriendTalkNotice。
        /// 这里由服务端再补发几次 TriggerFriendPushTask，确保 Web 端联系人列表最终刷新。
        /// </para>
        /// </summary>
        public void ScheduleContactListRefreshAfterMutation(string connectionId, long relatedTaskId, string reason)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return;
            }

            var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason.Trim();
            var scheduleKey = $"{connectionId}:{normalizedReason}";
            var scheduleVersion = DateTime.UtcNow.Ticks;
            _contactRefreshScheduleVersions[scheduleKey] = scheduleVersion;

            _ = Task.Run(async () =>
            {
                /*
                 * 联系人补同步需要兜底，但不能无限叠加：
                 * - DeleteFriendTask：安卓端会主动发 FriendDelNotice/FriendPushNotice，保留一次短延迟即可；
                 * - AddFriendWithSceneTask：申请提交不代表已经成为好友，保留 5/30/90 秒三轮；
                 * - FriendTalkNoticeVerified：已经通过验证且会补拉单个联系人，保留 1.5/6 秒两轮；
                 * - 同一连接同一原因若再次调度，旧版本自动停止，避免 Attempt=6 这类同步风暴。
                 */
                var delays = GetContactRefreshDelays(normalizedReason);
                for (var index = 0; index < delays.Length; index++)
                {
                    try
                    {
                        await Task.Delay(delays[index]);
                        if (!_contactRefreshScheduleVersions.TryGetValue(scheduleKey, out var currentVersion)
                            || currentVersion != scheduleVersion)
                        {
                            _logger.LogDebug(
                                "联系人变更补同步旧调度已停止: Reason={Reason}, RelatedTaskId={RelatedTaskId}, Attempt={Attempt}",
                                normalizedReason,
                                relatedTaskId,
                                index + 1);
                            return;
                        }

                        var syncTaskId = DateTime.UtcNow.Ticks;
                        var sent = await SendSyncFriendListTaskAsync(connectionId, taskId: syncTaskId);
                        _logger.LogInformation(
                            "联系人变更后 3056 补同步已下发: Reason={Reason}, RelatedTaskId={RelatedTaskId}, SyncTaskId={SyncTaskId}, Attempt={Attempt}, Sent={Sent}",
                            normalizedReason,
                            relatedTaskId,
                            syncTaskId,
                            index + 1,
                            sent);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "联系人变更后补同步异常: Reason={Reason}, RelatedTaskId={RelatedTaskId}, Attempt={Attempt}",
                            normalizedReason,
                            relatedTaskId,
                            index + 1);
                    }
                }
            });
        }

        /// <summary>
        /// 群资料或群成员变更后的补同步。
        /// <para>
        /// 群操作成功后，安卓端有时只回 TaskResultNotice，不一定马上推 ChatRoomChangedNotice/ChatroomPushNotice。
        /// 因此服务端补发群资料与会话列表同步，保证 Web 群聊页最终能看到新增、删除和改名结果。
        /// </para>
        /// </summary>
        public void ScheduleChatRoomRefreshAfterMutation(string connectionId, long relatedTaskId, string reason)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return;
            }

            var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason.Trim();
            var scheduleKey = $"{connectionId}:ChatRoom:{normalizedReason}";
            var scheduleVersion = DateTime.UtcNow.Ticks;
            _contactRefreshScheduleVersions[scheduleKey] = scheduleVersion;

            _ = Task.Run(async () =>
            {
                foreach (var delay in GetChatRoomRefreshDelays(normalizedReason))
                {
                    try
                    {
                        await Task.Delay(delay).ConfigureAwait(false);
                        if (!_contactRefreshScheduleVersions.TryGetValue(scheduleKey, out var currentVersion)
                            || currentVersion != scheduleVersion)
                        {
                            _logger.LogDebug(
                                "群聊变更补同步旧调度已停止: Reason={Reason}, RelatedTaskId={RelatedTaskId}",
                                normalizedReason,
                                relatedTaskId);
                            return;
                        }

                        var roomTaskId = DateTime.UtcNow.Ticks;
                        var conversationTaskId = roomTaskId + 1;
                        var roomSent = await SendTriggerChatRoomPushTaskAsync(connectionId, roomTaskId).ConfigureAwait(false);
                        var conversationSent = await SendTriggerConversationPushTaskAsync(
                            connectionId,
                            withName: true,
                            limit: 100,
                            taskId: conversationTaskId).ConfigureAwait(false);

                        _logger.LogInformation(
                            "群聊变更后补同步已下发: Reason={Reason}, RelatedTaskId={RelatedTaskId}, RoomTaskId={RoomTaskId}, RoomSent={RoomSent}, ConversationTaskId={ConversationTaskId}, ConversationSent={ConversationSent}",
                            normalizedReason,
                            relatedTaskId,
                            roomTaskId,
                            roomSent,
                            conversationTaskId,
                            conversationSent);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "群聊变更后补同步异常: Reason={Reason}, RelatedTaskId={RelatedTaskId}",
                            normalizedReason,
                            relatedTaskId);
                    }
                }
            });
        }

        /// <summary>
        /// 获取联系人变更后的补同步延迟序列。
        /// </summary>
        private static int[] GetContactRefreshDelays(string reason)
        {
            if (reason.Contains("FriendTalkNoticeVerified", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("AcceptFriend", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { 1500, 6000 };
            }

            if (reason.Contains("AddFriend", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { 5000, 30000, 90000 };
            }

            if (reason.Contains("DeleteFriend", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { 1500 };
            }

            return new[] { 1500, 6000 };
        }

        /// <summary>
        /// 群资料补同步延迟序列。
        /// <para>群成员增删通常需要微信数据库先落地，保留短、中两轮；避免频繁点击群操作时形成同步风暴。</para>
        /// </summary>
        private static int[] GetChatRoomRefreshDelays(string reason)
        {
            if (reason.Contains("SyncChatRooms", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { 2000, 6000, 15000 };
            }

            return new[] { 1500, 6000 };
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendTalkToFriendTaskAsync(string connectionId, string friendWxId, string content, EnumContentType contentType = EnumContentType.Text, string atIds = "")
        {
            var taskId = DateTime.UtcNow.Ticks;
            return await SendTalkToFriendTaskAsync(connectionId, friendWxId, content, contentType, atIds, taskId);
        }

        /// <summary>
        /// 发送单条聊天消息并允许调用方指定任务号。
        /// <para>多图兼容入口需要逐张复用 TalkToFriendTask，同时保留每张图自己的 TaskId，便于日志和异步回执排查。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendTalkToFriendTaskAsync(
            string connectionId,
            string friendWxId,
            string content,
            EnumContentType contentType,
            string atIds,
            long taskId)
        {
            var normalizedConnectionId = connectionId ?? string.Empty;
            var normalizedFriendWxId = friendWxId ?? string.Empty;
            RegisterTalkToFriendTaskContext(new TalkToFriendTaskContext
            {
                TaskId = taskId,
                ConnectionId = normalizedConnectionId,
                FriendId = normalizedFriendWxId.Trim(),
                Content = content ?? string.Empty,
                ContentType = contentType,
                AtIds = atIds ?? string.Empty
            });

            var task = BuildTalkToFriendTask(normalizedFriendWxId, content, contentType, atIds, taskId);
            var timeoutMs = GetTalkToFriendTimeoutMs(contentType, content);
            var result = await SendTaskAndWaitAsync(task, EnumMsgType.TalkToFriendTask.ToString(), normalizedConnectionId, taskId, timeoutMs);
            if (!result.success && string.Equals(result.message, "Failed to send to Netty", StringComparison.OrdinalIgnoreCase))
            {
                RemoveTalkToFriendTaskContext(taskId);
            }

            return result;
        }

        /// <summary>
        /// 构建聊天消息任务。
        /// <para>集中封装 62203 字段：ContentType、MsgId、Immediate、AtIds，避免多入口字段不一致。</para>
        /// </summary>
        private static TalkToFriendTaskMessage BuildTalkToFriendTask(
            string friendWxId,
            string? content,
            EnumContentType contentType,
            string? atIds,
            long taskId)
        {
            return new TalkToFriendTaskMessage
            {
                FriendId = friendWxId ?? string.Empty,
                Content = ByteString.CopyFromUtf8(content ?? string.Empty),
                ContentType = contentType,
                MsgId = taskId,
                Immediate = true,
                // 62203 对位：群聊 @ 成员列表，多个 wxid 用英文逗号分隔。
                AtIds = atIds ?? string.Empty
            };
        }

        /// <summary>
        /// 计算聊天消息任务等待超时。
        /// <para>
        /// 文本/表情通常数秒内完成；图片、视频、文件会经历安卓端下载、微信侧转码和回调，
        /// 继续使用 15 秒会导致服务端先报失败，而客户端后续仍在执行。
        /// </para>
        /// </summary>
        private static int GetTalkToFriendTimeoutMs(EnumContentType contentType, string? content)
        {
            return contentType switch
            {
                EnumContentType.Picture => 45000,
                EnumContentType.Voice => 45000,
                EnumContentType.Video => 90000,
                EnumContentType.File => 90000,
                EnumContentType.Link => 30000,
                EnumContentType.WeApp => 45000,
                EnumContentType.ShiPinHao => 45000,
                EnumContentType.FinderLive => 45000,
                _ when !string.IsNullOrWhiteSpace(content)
                    && (content.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                        || content.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    => 45000,
                _ => 15000
            };
        }

        /// <summary>
        /// 无等待下发单条聊天消息。
        /// <para>用于“文本群发到多个群聊”这类需要快速返回的场景，真正发送结果由客户端异步结果链再回传。</para>
        /// </summary>
        private async Task<bool> SendTalkToFriendTaskNoWaitAsync(
            string connectionId,
            string friendWxId,
            string content,
            EnumContentType contentType,
            long taskId,
            long batchTaskId = 0L,
            string atIds = "",
            string taskScene = "",
            string failureSummary = "",
            string successSummary = "")
        {
            var normalizedConnectionId = connectionId ?? string.Empty;
            var normalizedFriendWxId = friendWxId ?? string.Empty;
            RegisterTalkToFriendTaskContext(new TalkToFriendTaskContext
            {
                ContentType = contentType,
                TaskId = taskId,
                ConnectionId = normalizedConnectionId,
                FriendId = normalizedFriendWxId.Trim(),
                Content = content ?? string.Empty,
                AtIds = atIds ?? string.Empty
            });

            var task = BuildTalkToFriendTask(normalizedFriendWxId, content, contentType, atIds, taskId);

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TalkToFriendTask.ToString(),
                normalizedConnectionId,
                customMessageId: taskId);

            if (!sent)
            {
                RemoveTalkToFriendTaskContext(taskId);
            }

            if (sent && (!string.IsNullOrWhiteSpace(taskScene) || !string.IsNullOrWhiteSpace(failureSummary)))
            {
                _fireAndForgetTaskContexts[taskId] = new FireAndForgetTaskContext
                {
                    BatchTaskId = batchTaskId,
                    TaskScene = taskScene ?? string.Empty,
                    TargetId = friendWxId ?? string.Empty,
                    FailureSummary = failureSummary ?? string.Empty,
                    SuccessSummary = successSummary ?? string.Empty
                };
            }

            return sent;
        }

        private static bool IsChatRoomTarget(string? targetId)
        {
            return !string.IsNullOrWhiteSpace(targetId)
                && targetId.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveMessagePeerWxid(string ownerWxid, Message message)
        {
            var normalizedOwnerWxid = ownerWxid.Trim();
            var sender = message.senderWxid?.Trim() ?? string.Empty;
            var receiver = message.receiverWxid?.Trim() ?? string.Empty;

            if (IsChatRoomTarget(sender))
            {
                return sender;
            }

            if (IsChatRoomTarget(receiver))
            {
                return receiver;
            }

            if (!string.IsNullOrWhiteSpace(sender)
                && !string.Equals(sender, normalizedOwnerWxid, StringComparison.OrdinalIgnoreCase))
            {
                return sender;
            }

            if (!string.IsNullOrWhiteSpace(receiver)
                && !string.Equals(receiver, normalizedOwnerWxid, StringComparison.OrdinalIgnoreCase))
            {
                return receiver;
            }

            return string.Empty;
        }

        private static bool TryParsePositiveInt64(string? value, out long result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return long.TryParse(
                    value.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out result)
                && result > 0;
        }

        public async Task<bool> SendSyncFriendListTaskAsync(string connectionId, string weChatId = "", long? taskId = null)
        {
            // 同步好友列表通常不会直接回 TaskResultNotice，而是由安卓端触发 FriendPushNotice 批量上报。
            // 这里仍然补齐 3056 的正式 payload，方便安卓端、日志和后续 3057 响应链按同一 TaskId 关联。
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new SyncFriendListAsyncReqMessage
            {
                WeChatId = weChatId ?? string.Empty,
                TaskId = finalTaskId
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.SyncFriendListAsyncReq.ToString(),
                connectionId,
                customMessageId: finalTaskId);
        }

        /// <summary>
        /// 请求客户端按 MsgSvrId 补偿单条聊天消息，承接协议 RequestTalkMsgTask(1252)。
        /// <para>结果由 RequestTalkMsgTaskResultNotice(1253) 异步回传并落库；这里不等待通用 TaskResultNotice。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendRequestTalkMsgTaskAsync(
            string connectionId,
            string weChatId,
            long msgSvrId,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "连接 ID 为空，无法下发聊天消息补偿任务");
            }

            if (string.IsNullOrWhiteSpace(weChatId) || msgSvrId == 0)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "微信账号或 MsgSvrId 为空，无法下发聊天消息补偿任务");
            }

            var task = new RequestTalkMsgTaskMessage
            {
                WeChatId = weChatId.Trim(),
                MsgSvrId = msgSvrId
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.RequestTalkMsgTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "单条聊天消息补偿指令已下发，等待客户端上报 RequestTalkMsgTaskResultNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "单条聊天消息补偿指令下发失败");
        }

        /// <summary>
        /// 请求客户端按 MsgSvrId 补偿原始聊天正文/XML，承接协议 RequestTalkContentTask(1218)。
        /// <para>结果由 RequestTalkContentTaskResultNotice(1219) 异步回传，只会更新已有消息。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendRequestTalkContentTaskAsync(
            string connectionId,
            string weChatId,
            long msgSvrId,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "连接 ID 为空，无法下发原始消息正文补偿任务");
            }

            if (string.IsNullOrWhiteSpace(weChatId) || msgSvrId == 0)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "微信账号或 MsgSvrId 为空，无法下发原始消息正文补偿任务");
            }

            var task = new RequestTalkContentTaskMessage
            {
                WeChatId = weChatId.Trim(),
                MsgSvrId = msgSvrId
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.RequestTalkContentTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "原始消息正文补偿指令已下发，等待客户端上报 RequestTalkContentTaskResultNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "原始消息正文补偿指令下发失败");
        }

        /// <summary>
        /// 请求客户端按 MsgSvrId 补偿消息详情，承接协议 RequestTalkDetailTask(1078)。
        /// <para>调用方只传本地 MsgId 时，会先尝试从本地消息表补齐 MsgSvrId。</para>
        /// <para>结果由 RequestTalkDetailTaskResultNotice(1029) 异步回传，服务端按已有消息优先更新。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendRequestTalkDetailTaskAsync(
            string connectionId,
            string weChatId,
            string friendId,
            long msgId,
            string msgSvrId = "",
            string md5 = "",
            bool getOriginal = false,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "连接 ID 为空，无法下发消息详情补偿任务");
            }

            if (string.IsNullOrWhiteSpace(weChatId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "微信账号为空，无法下发消息详情补偿任务");
            }

            var resolvedTarget = await ResolveRequestTalkDetailTargetAsync(weChatId, friendId, msgId, msgSvrId);
            if (resolvedTarget.MsgSvrIdValue == 0)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(
                    finalTaskId,
                    "MsgSvrId 为空或格式不正确，且无法通过本地 MsgId 补齐，无法下发消息详情补偿任务");
            }

            var task = new RequestTalkDetailTaskMessage
            {
                WeChatId = weChatId.Trim(),
                FriendId = resolvedTarget.FriendId,
                MsgId = resolvedTarget.MsgId,
                MsgSvrId = resolvedTarget.MsgSvrIdText,
                Md5 = md5 ?? string.Empty,
                GetOriginal = getOriginal
            };

            if (resolvedTarget.FilledFromLocalMessage)
            {
                _logger.LogInformation(
                    "RequestTalkDetailTask 已从本地消息补齐定位信息：WeChatId={WeChatId}, FriendId={FriendId}, MsgId={MsgId}, MsgSvrId={MsgSvrId}, TaskId={TaskId}",
                    task.WeChatId,
                    task.FriendId,
                    task.MsgId,
                    task.MsgSvrId,
                    finalTaskId);
            }

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.RequestTalkDetailTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            if (sent)
            {
                RegisterRequestTalkDetailTaskContext(new RequestTalkDetailTaskContext
                {
                    TaskId = finalTaskId,
                    WeChatId = task.WeChatId,
                    FriendId = task.FriendId,
                    MsgId = task.MsgId,
                    MsgSvrId = resolvedTarget.MsgSvrIdValue,
                    Md5 = task.Md5,
                    GetOriginal = task.GetOriginal
                });

                await using var db = await _dbContextFactory.CreateDbContextAsync();
                var contextSaved = await db.SaveRequestTalkDetailPendingContext(
                    task.WeChatId,
                    task.FriendId,
                    task.MsgId,
                    resolvedTarget.MsgSvrIdValue,
                    task.Md5,
                    task.GetOriginal,
                    finalTaskId);

                if (!contextSaved)
                {
                    _logger.LogInformation(
                        "RequestTalkDetailTask pending context not saved to MessageExtensions because message was not found. WeChatId={WeChatId}, FriendId={FriendId}, MsgId={MsgId}, MsgSvrId={MsgSvrId}, TaskId={TaskId}",
                        task.WeChatId,
                        task.FriendId,
                        task.MsgId,
                        resolvedTarget.MsgSvrIdValue,
                        finalTaskId);
                }
            }

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "消息详情补偿指令已下发，等待客户端上报 RequestTalkDetailTaskResultNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "消息详情补偿指令下发失败");
        }

        /// <summary>
        /// 解析消息详情补偿任务的最终定位参数。
        /// <para>
        /// RequestTalkDetailTask 的 proto 允许传 MsgId，但 Android/62203 实际执行前会把 MsgSvrId 字符串解析为 long，
        /// 解析失败或为 0 时立即回 “msgSvrId错误”。因此服务端在下发前尽量用本地 MsgId 补齐 MsgSvrId。
        /// </para>
        /// </summary>
        private async Task<RequestTalkDetailTarget> ResolveRequestTalkDetailTargetAsync(
            string weChatId,
            string? friendId,
            long msgId,
            string? msgSvrId)
        {
            var ownerWxid = weChatId.Trim();
            var target = new RequestTalkDetailTarget
            {
                FriendId = friendId?.Trim() ?? string.Empty,
                MsgId = msgId,
                MsgSvrIdText = msgSvrId?.Trim() ?? string.Empty
            };

            if (TryParsePositiveInt64(target.MsgSvrIdText, out var parsedMsgSvrId))
            {
                target.MsgSvrIdValue = parsedMsgSvrId;
            }

            if (target.MsgSvrIdValue > 0 && target.MsgId > 0 && !string.IsNullOrWhiteSpace(target.FriendId))
            {
                return target;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var localMessage = await db.FindMessageForRequestTalkDetailTask(
                ownerWxid,
                target.MsgId,
                target.MsgSvrIdText,
                target.FriendId);

            if (localMessage == null)
            {
                return target;
            }

            var filled = false;
            var localMsgSvrId = localMessage.msgSvrId.GetValueOrDefault();
            if (target.MsgSvrIdValue == 0 && localMsgSvrId > 0)
            {
                target.MsgSvrIdValue = localMsgSvrId;
                target.MsgSvrIdText = localMsgSvrId.ToString(CultureInfo.InvariantCulture);
                filled = true;
            }

            if (target.MsgId == 0 && TryParsePositiveInt64(localMessage.localMessageId, out var localMsgId))
            {
                target.MsgId = localMsgId;
                filled = true;
            }

            if (string.IsNullOrWhiteSpace(target.FriendId))
            {
                var resolvedFriendId = ResolveMessagePeerWxid(ownerWxid, localMessage);
                if (!string.IsNullOrWhiteSpace(resolvedFriendId))
                {
                    target.FriendId = resolvedFriendId;
                    filled = true;
                }
            }

            target.FilledFromLocalMessage = filled;
            return target;
        }

        private sealed class RequestTalkDetailTarget
        {
            /// <summary>好友或群会话 wxid。</summary>
            public string FriendId { get; set; } = string.Empty;

            /// <summary>微信本地消息 ID，同时作为 RequestTalkDetailTaskResultNotice 的 MsgId 回填键。</summary>
            public long MsgId { get; set; }

            /// <summary>Android 可解析的 MsgSvrId 字符串。</summary>
            public string MsgSvrIdText { get; set; } = string.Empty;

            /// <summary>已解析的 MsgSvrId 数值。</summary>
            public long MsgSvrIdValue { get; set; }

            /// <summary>是否从本地消息补齐过定位字段。</summary>
            public bool FilledFromLocalMessage { get; set; }
        }

        /// <summary>
        /// 请求客户端对指定语音消息执行语音转文字，承接协议 VoiceTransTextTask(1226)。
        /// <para>
        /// Android 62203 使用通用 TaskResultNotice 回传结果：成功时 ErrMsg 是识别文本，失败时 ErrMsg 是错误信息。
        /// 下发前保存 TaskId -> WeChatId/FriendId/MsgSvrId 上下文，便于回包后写入 VoiceToTextLogs。
        /// </para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendVoiceTransTextTaskAsync(
            string connectionId,
            string weChatId,
            string friendId,
            long msgSvrId,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "连接 ID 为空，无法下发语音转文字任务");
            }

            if (string.IsNullOrWhiteSpace(weChatId) || string.IsNullOrWhiteSpace(friendId) || msgSvrId == 0)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "微信账号、会话 ID 或 MsgSvrId 为空，无法下发语音转文字任务");
            }

            var ownerWxid = weChatId.Trim();
            var targetFriendId = friendId.Trim();
            var task = new VoiceTransTextTaskMessage
            {
                WeChatId = ownerWxid,
                FriendId = targetFriendId,
                MsgSvrId = msgSvrId,
                TaskId = finalTaskId
            };

            RegisterVoiceTransTextTaskContext(new VoiceTransTextTaskContext
            {
                TaskId = finalTaskId,
                WeChatId = ownerWxid,
                FriendId = targetFriendId,
                MsgSvrId = msgSvrId
            });

            var result = await SendTaskAndWaitAsync(
                task,
                EnumMsgType.VoiceTransTextTask.ToString(),
                connectionId,
                finalTaskId,
                timeoutMs: 45000);

            // 如果消息根本没有发到 Netty，不会有迟到回包，立即清理上下文；超时则继续保留一段时间等待迟到结果。
            if (!result.success
                && string.Equals(result.message, "Failed to send to Netty", StringComparison.OrdinalIgnoreCase))
            {
                _voiceTransTextTaskContexts.TryRemove(finalTaskId, out _);
            }

            return result;
        }

        /// <summary>
        /// 请求客户端按 CDN 参数下载聊天/笔记媒体文件，承接协议 CDNDownloadFileTask(1269)。
        /// <para>结果由 CDNDownloadResultNotice(1271) 异步回传，服务端按 MsgSvrId 回填媒体 URL。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendCDNDownloadFileTaskAsync(
            string connectionId,
            string weChatId,
            string cdnUrl,
            string cdnKey,
            CDNFileType fileType,
            string fileId = "",
            string fileFmt = "",
            int fileSize = 0,
            long msgSvrId = 0,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "连接 ID 为空，无法下发 CDN 文件下载任务");
            }

            if (string.IsNullOrWhiteSpace(weChatId) || string.IsNullOrWhiteSpace(cdnUrl) || msgSvrId == 0)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "微信账号、CDN URL 或 MsgSvrId 为空，无法下发 CDN 文件下载任务");
            }

            var task = new CDNDownloadFileTaskMessage
            {
                WeChatId = weChatId.Trim(),
                CdnUrl = cdnUrl.Trim(),
                CdnKey = cdnKey ?? string.Empty,
                FileType = fileType,
                FileId = fileId ?? string.Empty,
                FileFmt = fileFmt ?? string.Empty,
                FileSize = fileSize,
                MsgSvrId = msgSvrId
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.CdndownloadFileTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            if (sent)
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                var contextSaved = await db.SaveCdnDownloadPendingContextByMsgSvrId(
                    task.WeChatId,
                    task.MsgSvrId,
                    task.CdnUrl,
                    (int)task.FileType,
                    task.FileId,
                    task.FileFmt,
                    task.FileSize,
                    finalTaskId);

                if (!contextSaved)
                {
                    _logger.LogInformation(
                        "CDN download pending context not saved because message was not found. WeChatId={WeChatId}, MsgSvrId={MsgSvrId}, FileId={FileId}, TaskId={TaskId}",
                        task.WeChatId,
                        task.MsgSvrId,
                        task.FileId,
                        finalTaskId);
                }
            }

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "CDN 文件下载指令已下发，等待客户端上报 CDNDownloadResultNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "CDN 文件下载指令下发失败");
        }

        /// <summary>
        /// 发送好友申请列表拉取任务，承接协议 PullFriendAddReqListTask(1234)。
        /// <para>结果由 FriendAddReqListNotice(2036) 异步上报并复用 FriendRequests 表落库。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPullFriendAddReqListTaskAsync(
            string connectionId,
            long startTime = 0,
            bool onlyNew = true,
            bool getAll = false,
            string weChatId = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new PullFriendAddReqListTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                StartTime = startTime,
                OnlyNew = onlyNew,
                GetAll = getAll
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.PullFriendAddReqListTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "好友申请列表拉取指令已下发，等待客户端上报 FriendAddReqListNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "好友申请列表拉取指令下发失败");
        }

        /// <summary>
        /// 启动微信好友检测/清粉任务，承接协议 PostFriendDetectTask(1095)。
        /// <para>过程进度由 PostFriendDetectCountNotice(2028) 异步回传，最终明细可再拉取 FriendDetectResultNotice(1280)。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPostFriendDetectTaskAsync(
            string connectionId,
            string weChatId,
            string message,
            bool onlyCheck = true,
            int skipHour = 24,
            int mode = 0,
            int max = 0,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "连接 ID 为空，无法启动好友检测任务");
            }

            if (string.IsNullOrWhiteSpace(weChatId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "微信账号为空，无法启动好友检测任务");
            }

            var task = new PostFriendDetectTaskMessage
            {
                WeChatId = weChatId.Trim(),
                TaskId = finalTaskId,
                Message = message ?? string.Empty,
                OnlyCheck = onlyCheck,
                SkipHour = skipHour,
                Mode = mode,
                Max = max
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.PostFriendDetectTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "好友检测任务已启动，等待客户端上报 PostFriendDetectCountNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "好友检测任务下发失败");
        }

        /// <summary>
        /// 停止微信好友检测/清粉任务，承接协议 PostStopFriendDetectTask(1096)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPostStopFriendDetectTaskAsync(
            string connectionId,
            string weChatId,
            long taskId)
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "连接 ID 为空，无法停止好友检测任务");
            }

            if (string.IsNullOrWhiteSpace(weChatId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "微信账号为空，无法停止好友检测任务");
            }

            var task = new PostStopFriendDetectTaskMessage
            {
                WeChatId = weChatId.Trim(),
                TaskId = finalTaskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.PostStopFriendDetectTask.ToString(), connectionId, finalTaskId, 30000);
        }

        /// <summary>
        /// 拉取好友检测最终结果，承接协议 GetFriendDetectResult(1279)。
        /// <para>结果由 FriendDetectResultNotice(1280) 异步回传。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendGetFriendDetectResultTaskAsync(
            string connectionId,
            string weChatId,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "连接 ID 为空，无法拉取好友检测结果");
            }

            if (string.IsNullOrWhiteSpace(weChatId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "微信账号为空，无法拉取好友检测结果");
            }

            var task = new GetFriendDetectResultMessage
            {
                WeChatId = weChatId.Trim()
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.GetFriendDetectResult.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "好友检测结果拉取指令已下发，等待客户端上报 FriendDetectResultNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "好友检测结果拉取指令下发失败");
        }

        /// <summary>
        /// 查询客户端当前已知微信账号列表。
        /// <para>
        /// 62203 的 3050/3051 是轻量账号状态查询接口；当前不等待结果，
        /// 安卓端收到后会用 3051 上报当前内存态账号列表。
        /// </para>
        /// </summary>
        public async Task<bool> SendGetWeChatsReqAsync(
            string connectionId,
            long unionId = 0,
            EnumAccountType accountType = EnumAccountType.Main)
        {
            var task = new GetWeChatsReqMessage
            {
                UnionId = unionId,
                AccountType = accountType
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.GetWeChatsReq.ToString(),
                connectionId);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAddFriendTaskAsync(string connectionId, string friendWxId, string message, int scene = 3, string weChatId = "")
        {
            return await SendAddFriendWithSceneTaskAsync(connectionId, friendWxId, message, string.Empty, string.Empty, scene, 0, string.Empty, weChatId);
        }

        /// <summary>
        /// 发送按场景添加好友任务，支持备注、标签和权限位。
        /// </summary>

        /// <summary>
        /// 发送群内加好友任务，承接协议 AddFriendInChatRoomTask(1214)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAddFriendInChatRoomTaskAsync(
            string connectionId,
            string chatRoomId,
            string friendId,
            string message,
            string remark = "",
            int permission = 0,
            long? taskId = null,
            string weChatId = "")
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new AddFriendInChatRoomTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                ChatroomId = chatRoomId ?? string.Empty,
                FriendId = friendId ?? string.Empty,
                Message = message ?? string.Empty,
                Remark = remark ?? string.Empty,
                Permission = permission,
                TaskId = finalTaskId
            };

            var result = await SendTaskAndWaitAsync(task, EnumMsgType.AddFriendInChatRoomTask.ToString(), connectionId, finalTaskId, 30000);
            if (result.success)
            {
                ScheduleContactListRefreshAfterMutation(connectionId, finalTaskId, "AddFriendInChatRoomTask");
            }
            return result;
        }

        /// <summary>
        /// 发送二维码入群任务，承接协议 JoinGroupByQrTask(1267)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendJoinGroupByQrTaskAsync(
            string connectionId,
            string qrUrl,
            string qrContent,
            string weChatId = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new JoinGroupByQrTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                QrUrl = qrUrl ?? string.Empty,
                QrContent = qrContent ?? string.Empty,
                TaskId = finalTaskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.JoinGroupByQrTask.ToString(), connectionId, finalTaskId, 30000);
        }

        /// <summary>
        /// 发送群接龙任务，承接协议 SendJielongTask(1268)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendJielongTaskAsync(
            string connectionId,
            string chatRoomId,
            string content,
            string title = "",
            string sample = "",
            string memo = "",
            long msgSvrId = 0,
            string weChatId = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new SendJielongTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                Chatoom = chatRoomId ?? string.Empty,
                Content = content ?? string.Empty,
                Title = title ?? string.Empty,
                Sample = sample ?? string.Empty,
                Memo = memo ?? string.Empty,
                MsgSvrId = msgSvrId,
                TaskId = finalTaskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.SendJielongTask.ToString(), connectionId, finalTaskId, 30000);
        }

        /// <summary>
        /// 发送群二维码拉取任务，承接协议 PullChatRoomQrCodeTask(1090)。
        /// 成功时由 PullChatRoomQrCodeTaskResultNotice 返回二维码地址。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPullChatRoomQrCodeTaskAsync(
            string connectionId,
            string chatRoomId,
            string weChatId = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new PullChatRoomQrCodeTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                ChatRoomId = chatRoomId ?? string.Empty,
                TaskId = finalTaskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.PullChatRoomQrCodeTask.ToString(), connectionId, finalTaskId, 30000);
        }

        /// <summary>
        /// 发送个人微信二维码拉取任务，承接协议 PullWeChatQrCodeTask(1079)。
        /// <para>
        /// 该协议的请求和结果体均不包含 TaskId，服务端只能用 TransportMessage.Id 做发送侧追踪；
        /// 最终二维码地址会由 PullWeChatQrCodeTaskResultNotice 作为异步任务结果推送到前端。
        /// </para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPullWeChatQrCodeTaskAsync(
            string connectionId,
            string weChatId = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new PullWeChatQrCodeTaskMessage
            {
                WeChatId = weChatId ?? string.Empty
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.PullWeChatQrCodeTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "个人二维码拉取指令已下发，等待客户端异步回执")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "个人二维码拉取指令下发失败");
        }

        /// <summary>
        /// 发送 POI 列表拉取任务，承接协议 GetPoiListTask(1290)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendGetPoiListTaskAsync(
            string connectionId,
            double lat,
            double lng,
            string keyword = "",
            string weChatId = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new GetPoiListTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                Lat = lat,
                Lng = lng,
                Keyword = keyword ?? string.Empty,
                TaskId = finalTaskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.GetPoiListTask.ToString(), connectionId, finalTaskId, 30000);
        }

        /// <summary>
        /// 发送表情信息拉取任务，承接协议 PullEmojiInfoTask(1272)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPullEmojiInfoTaskAsync(
            string connectionId,
            string md5,
            string weChatId = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new PullEmojiInfoTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                Md5 = md5 ?? string.Empty,
                TaskId = finalTaskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.PullEmojiInfoTask.ToString(), connectionId, finalTaskId, 30000);
        }

        /// <summary>
        /// 发送消息级表情补图任务。
        /// <para>下发 1272 前保存 MsgSvrId 上下文；收到 1273 后由 TaskMessageHandler 自动衔接 CDNDownloadFileTask(1269)。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPullEmojiInfoForMessageTaskAsync(
            string connectionId,
            string weChatId,
            string md5,
            long msgSvrId,
            string friendId = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "连接 ID 为空，无法下发表情补图任务");
            }

            if (string.IsNullOrWhiteSpace(weChatId) || string.IsNullOrWhiteSpace(md5) || msgSvrId == 0)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "微信账号、表情 MD5 或 MsgSvrId 为空，无法下发表情补图任务");
            }

            var normalizedWeChatId = weChatId.Trim();
            var normalizedMd5 = md5.Trim();
            var task = new PullEmojiInfoTaskMessage
            {
                WeChatId = normalizedWeChatId,
                Md5 = normalizedMd5,
                TaskId = finalTaskId
            };

            RegisterPullEmojiInfoTaskContext(new PullEmojiInfoTaskContext
            {
                TaskId = finalTaskId,
                ConnectionId = connectionId.Trim(),
                WeChatId = normalizedWeChatId,
                Md5 = normalizedMd5,
                MsgSvrId = msgSvrId,
                FriendId = friendId?.Trim() ?? string.Empty
            });

            var result = await SendTaskAndWaitAsync(
                task,
                EnumMsgType.PullEmojiInfoTask.ToString(),
                connectionId,
                finalTaskId,
                timeoutMs: 30000);

            if (!result.success
                && string.Equals(result.message, "Failed to send to Netty", StringComparison.OrdinalIgnoreCase))
            {
                RemovePullEmojiInfoTaskContext(finalTaskId);
            }

            return result;
        }

        /// <summary>
        /// 发送搜索联系人任务，承接协议 FindContactTask(1227)。
        /// <para>结果 FindContactTaskResult 不含 TaskId，因此这里采用下发即返回、异步通知展示结果。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendFindContactTaskAsync(
            string connectionId,
            string content,
            string weChatId = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new FindContactTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                Content = content ?? string.Empty
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.FindContactTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "搜索联系人指令已下发，等待客户端异步回执")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "搜索联系人指令下发失败");
        }

        /// <summary>
        /// 发送微信定位查询任务，承接协议 WeChatLocationTask(1258)。
        /// <para>结果 WeChatLocationTaskResultNotice 不含 TaskId，因此这里采用下发即返回、异步通知展示结果。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendWeChatLocationTaskAsync(
            string connectionId,
            bool noCache = false,
            string weChatId = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new WeChatLocationTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                NoCache = noCache
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.WeChatLocationTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "微信定位查询指令已下发，等待客户端异步回执")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "微信定位查询指令下发失败");
        }

        /// <summary>
        /// 发送钱包余额查询任务，承接协议 WalletBalanceTask(1262)。
        /// <para>结果 WalletBalanceTaskResultNotice 不含 TaskId，因此这里采用下发即返回、异步通知展示结果。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendWalletBalanceTaskAsync(
            string connectionId,
            int flag = 0,
            string weChatId = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new WalletBalanceTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                Flag = flag
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.WalletBalanceTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "钱包余额查询指令已下发，等待客户端异步回执")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "钱包余额查询指令下发失败");
        }

        /// <summary>
        /// 发送手机状态查询任务，承接协议 PhoneStateTask(1256)。
        /// <para>结果 PhoneStateTaskResultNotice 不含 TaskId，因此这里采用下发即返回、异步通知展示结果。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPhoneStateTaskAsync(
            string connectionId,
            string imei = "",
            string weChatId = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new PhoneStateTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                Imei = imei ?? string.Empty
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.PhoneStateTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "手机状态查询指令已下发，等待客户端异步回执")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "手机状态查询指令下发失败");
        }

        /// <summary>
        /// 下发手机短信发送任务，承接协议 SendSmsTask(1289)。
        /// <para>Android 端会通过通用 TaskResultNotice 返回发送任务结果。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendSmsTaskAsync(
            string connectionId,
            string weChatId,
            string imei,
            string number,
            string content,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "连接 ID 为空，无法下发短信任务");
            }

            if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(content))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "号码和短信内容不能为空");
            }

            var task = new SendSmsTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                Imei = imei ?? string.Empty,
                Number = number.Trim(),
                Content = content,
                TaskId = finalTaskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.SendSmsTask.ToString(), connectionId, finalTaskId, 30000);
        }

        /// <summary>
        /// 下发短信历史拉取任务，承接协议 PullSmsTask(1304)。
        /// <para>结果由 PullSmsTaskResultNotice(1305) 返回并在 PhoneMessageHandler 中落库。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPullSmsTaskAsync(
            string connectionId,
            string weChatId,
            string imei,
            long startTime,
            long endTime,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "连接 ID 为空，无法下发短信历史拉取任务");
            }

            var task = new PullSmsTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                IMEI = imei ?? string.Empty,
                StartTime = startTime,
                EndTime = endTime,
                TaskId = finalTaskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.PullSmsTask.ToString(), connectionId, finalTaskId, 45000);
        }

        /// <summary>
        /// 下发通话记录拉取任务，承接协议 PullCallLogTask(1306)。
        /// <para>结果由 PullCallLogTaskResultNotice(1307) 返回并在 PhoneMessageHandler 中落库。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPullCallLogTaskAsync(
            string connectionId,
            string weChatId,
            string imei,
            long startTime,
            long endTime,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "连接 ID 为空，无法下发通话记录拉取任务");
            }

            var task = new PullCallLogTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                IMEI = imei ?? string.Empty,
                StartTime = startTime,
                EndTime = endTime,
                TaskId = finalTaskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.PullCallLogTask.ToString(), connectionId, finalTaskId, 45000);
        }

        /// <summary>
        /// 发送企微用户同步任务，承接协议 TriggerQwUserPush(1285)。
        /// <para>结果由 QwUserPUshNotice(1286) 异步推送并在联系人 handler 中按 DbHelper.SaveContacts 口径落库。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendTriggerQwUserPushTaskAsync(
            string connectionId,
            string weChatId = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new TriggerQwUserPushMessage
            {
                WeChatId = weChatId ?? string.Empty,
                TaskId = finalTaskId
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TriggerQwUserPush.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "企微用户同步指令已下发，等待客户端上报 QwUserPUshNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "企微用户同步指令下发失败");
        }

        /// <summary>
        /// 发送聊天消息 MsgSvrId 快照同步任务，承接协议 TriggerChatMsgIdsPushTask(1251)。
        /// <para>结果由 ChatMsgIdsPushNotice(1050) 异步上报；当前服务端记录并推送摘要，不自动对账软删除。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendTriggerChatMsgIdsPushTaskAsync(
            string connectionId,
            long startTime,
            long endTime,
            string weChatId = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new TriggerChatMsgIdsPushTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                StartTime = startTime,
                EndTime = endTime
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TriggerChatMsgIdsPushTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "聊天消息 ID 快照同步指令已下发，等待客户端上报 ChatMsgIdsPushNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "聊天消息 ID 快照同步指令下发失败");
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAddFriendWithSceneTaskAsync(
            string connectionId,
            string friendWxId,
            string message,
            string remark = "",
            string label = "",
            int scene = 3,
            int permission = 0,
            string verificationImagePath = "",
            string weChatId = "")
        {
            var taskId = DateTime.UtcNow.Ticks;
            var task = new AddFriendWithSceneTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                Friend = friendWxId,
                Message = message ?? string.Empty,
                Scene = scene,
                Remark = remark ?? string.Empty,
                Label = label ?? string.Empty,
                Permission = permission,
                VerificationImagePath = verificationImagePath ?? string.Empty,
                TaskId = taskId
            };

            /*
             * 62203 对照说明：
             * - 安卓端 AddFriendWithSceneTask / Tsk142 原始任务超时约 35 秒；
             * - 服务端如果仍按 30 秒等待，会在安卓端尚未返回 VerifyS/VerifyF 前先误判超时；
             * - 若网页传入验证图片，旧分支还会经历下载/上传/降级流程，等待窗口需要更长。
             */
            var waitTimeoutMs = string.IsNullOrWhiteSpace(verificationImagePath) ? 45000 : 90000;
            var result = await SendTaskAndWaitAsync(task, EnumMsgType.AddFriendWithSceneTask.ToString(), connectionId, taskId, waitTimeoutMs);
            if (result.success)
            {
                ScheduleContactListRefreshAfterMutation(connectionId, taskId, "AddFriendWithSceneTask");
            }
            return result;
        }


        /// <summary>
        /// 发送微信群发任务，承接协议 WeChatGroupSendTask(1076)。
        /// </summary>
        /// <summary>
        /// 将群发助手的内容类型映射到 TalkToFriendTask 内容类型。
        /// <para>群聊目标不走微信“群发助手”，而是逐群直发；这里允许文本/图片/视频/文件等常用媒体复用同一条发送链。</para>
        /// </summary>
        private static EnumContentType NormalizeGroupSendTalkContentType(
            WeChatGroupSendTaskMessage.Types.EnumGroupMsgContentType groupContentType,
            string? content)
        {
            var text = content?.Trim() ?? string.Empty;
            var lower = text.Split('?', '#')[0].ToLowerInvariant();

            if (groupContentType == WeChatGroupSendTaskMessage.Types.EnumGroupMsgContentType.Text)
            {
                if (lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".png")
                    || lower.EndsWith(".gif") || lower.EndsWith(".webp") || lower.EndsWith(".bmp"))
                {
                    return EnumContentType.Picture;
                }

                if (lower.EndsWith(".mp4") || lower.EndsWith(".mov") || lower.EndsWith(".m4v")
                    || lower.EndsWith(".3gp") || lower.EndsWith(".avi")
                    || lower.EndsWith(".mkv") || lower.EndsWith(".webm"))
                {
                    return EnumContentType.Video;
                }

                if (text.StartsWith("{", StringComparison.Ordinal) && text.Contains("\"url\"", StringComparison.OrdinalIgnoreCase))
                {
                    return EnumContentType.File;
                }

                return EnumContentType.Text;
            }

            return groupContentType switch
            {
                WeChatGroupSendTaskMessage.Types.EnumGroupMsgContentType.Picture => EnumContentType.Picture,
                WeChatGroupSendTaskMessage.Types.EnumGroupMsgContentType.Voice => EnumContentType.Voice,
                WeChatGroupSendTaskMessage.Types.EnumGroupMsgContentType.Video => EnumContentType.Video,
                _ => EnumContentType.UnknownContent
            };
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendWeChatGroupSendTaskAsync(
            string connectionId,
            List<string> friendIds,
            string content,
            int contentType = 0,
            int duration = 0,
            bool original = false,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                _logger.LogWarning("SendWeChatGroupSendTask 参数错误：ConnectionId 为空，TaskId={TaskId}", finalTaskId);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("ConnectionId 为空，设备未在线");
            }
            if (friendIds == null || friendIds.Count == 0)
            {
                _logger.LogWarning("SendWeChatGroupSendTask 参数错误：目标列表为空，ConnectionId={ConnectionId}, TaskId={TaskId}",
                    connectionId, finalTaskId);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("群发目标为空");
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("SendWeChatGroupSendTask 参数错误：内容为空，ConnectionId={ConnectionId}, TaskId={TaskId}",
                    connectionId, finalTaskId);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("群发内容为空");
            }

            var normalizedTargets = friendIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedTargets.Count == 0)
            {
                _logger.LogWarning("SendWeChatGroupSendTask 参数错误：清洗后目标列表为空，ConnectionId={ConnectionId}, TaskId={TaskId}",
                    connectionId, finalTaskId);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("群发目标为空");
            }

            var groupContentType = System.Enum.IsDefined(typeof(WeChatGroupSendTaskMessage.Types.EnumGroupMsgContentType), contentType)
                ? (WeChatGroupSendTaskMessage.Types.EnumGroupMsgContentType)contentType
                : WeChatGroupSendTaskMessage.Types.EnumGroupMsgContentType.Text;

            var chatRoomCount = normalizedTargets.Count(IsChatRoomTarget);
            var hasChatRoomTargets = chatRoomCount > 0;
            var hasFriendTargets = chatRoomCount < normalizedTargets.Count;

            if (hasChatRoomTargets && hasFriendTargets)
            {
                _logger.LogWarning("SendWeChatGroupSendTask 参数错误：好友与群聊目标混用，ConnectionId={ConnectionId}, TaskId={TaskId}, TargetCount={TargetCount}",
                    connectionId, finalTaskId, normalizedTargets.Count);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("好友与群聊请分开下发");
            }

            /*
             * 2026-05-12 静态修复：
             * - 好友文本群发继续沿用 WeChatGroupSendTask / 群发助手链路；
             * - 群页签输入的是 chatRoomId，这类目标不适合走群发助手，
             *   这里改为逐群直发 TalkToFriendTask（仅文本），服务端立即返回“已下发”，
             *   后续真实结果继续走客户端异步回执。
             */
            if (hasChatRoomTargets)
            {
                var talkContentType = NormalizeGroupSendTalkContentType(groupContentType, content);
                if (talkContentType == EnumContentType.UnknownContent)
                {
                    _logger.LogWarning("SendWeChatGroupSendTask 暂不支持群聊群发内容类型，ConnectionId={ConnectionId}, TaskId={TaskId}, ContentType={ContentType}",
                        connectionId, finalTaskId, groupContentType);
                    return SCRM.SHARED.Models.Dtos.TaskResult.Fail($"群发群暂不支持内容类型：{groupContentType}");
                }

                var sentCount = 0;
                var failedCount = 0;
                string firstFailedTarget = string.Empty;

                foreach (var chatRoomId in normalizedTargets)
                {
                    var perTaskId = DateTime.UtcNow.Ticks + sentCount + failedCount;
                    var sent = await SendTalkToFriendTaskNoWaitAsync(
                        connectionId,
                        chatRoomId,
                        content,
                        talkContentType,
                        perTaskId,
                        batchTaskId: finalTaskId,
                        taskScene: $"MassSendChatRoom{talkContentType}",
                        failureSummary: "群发群消息发送失败",
                        successSummary: "群发群消息发送成功");

                    if (sent)
                    {
                        sentCount++;
                    }
                    else
                    {
                        failedCount++;
                        if (string.IsNullOrEmpty(firstFailedTarget))
                        {
                            firstFailedTarget = chatRoomId;
                        }
                    }
                }

                _logger.LogInformation(
                    "SendWeChatGroupSendTask 已改走逐群文本直发：ConnectionId={ConnectionId}, RequestTaskId={TaskId}, TargetCount={TargetCount}, SentCount={SentCount}, FailedCount={FailedCount}",
                    connectionId,
                    finalTaskId,
                    normalizedTargets.Count,
                    sentCount,
                    failedCount);

                if (sentCount == 0)
                {
                    return SCRM.SHARED.Models.Dtos.TaskResult.Fail("群发群指令下发失败");
                }

                var chatRoomDispatchMessage = failedCount == 0
                    ? $"已向 {sentCount} 个群下发文本消息，等待客户端异步结果"
                    : $"已向 {sentCount} 个群下发文本消息，{failedCount} 个群下发失败（首个失败目标：{firstFailedTarget}）";

                return new SCRM.SHARED.Models.Dtos.TaskResult
                {
                    taskId = finalTaskId,
                    success = failedCount == 0,
                    message = chatRoomDispatchMessage
                };
            }

            var task = new WeChatGroupSendTaskMessage
            {
                TaskId = finalTaskId,
                Content = content ?? string.Empty,
                ContentType = groupContentType,
                Duration = duration,
                Original = original
            };

            if (normalizedTargets is not null)
            {
                foreach (var friendId in normalizedTargets)
                {
                    task.FriendIds.Add(friendId);
                }
            }

            _logger.LogInformation("SendWeChatGroupSendTask: ConnectionId={ConnectionId}, TaskId={TaskId}, FriendCount={FriendCount}, ContentType={ContentType}, Duration={Duration}, Original={Original}, ContentEmpty={ContentEmpty}",
                connectionId, finalTaskId, task.FriendIds.Count, groupContentType, duration, original, string.IsNullOrWhiteSpace(content));

            var queued = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.WeChatGroupSendTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            if (!queued)
            {
                _logger.LogWarning("SendWeChatGroupSendTask 发送到 Netty 失败：ConnectionId={ConnectionId}, TaskId={TaskId}",
                    connectionId, finalTaskId);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("群发消息指令发送失败");
            }

            return new SCRM.SHARED.Models.Dtos.TaskResult
            {
                taskId = finalTaskId,
                success = true,
                message = $"已向 {task.FriendIds.Count} 个好友下发群发消息，等待客户端异步结果"
            };
        }
        public async Task<bool> SendGetGroupSendHistoryTaskAsync(
            string connectionId,
            long? taskId = null,
            long endTime = 0,
            string weChatId = "")
        {
             // 62203 会读取 EndTime 并透传到 Android 侧 tK128.prM40。
             // EndTime=0 保持旧行为，由客户端按当前默认策略拉取。
             var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
             var task = new GetGroupSendHistoryTaskMessage
             {
                 WeChatId = weChatId ?? string.Empty,
                 EndTime = endTime,
                 TaskId = finalTaskId
             };
             return await _nettyMessageService.SendMessageToNettyAsync(
                 task,
                 EnumMsgType.GetGroupSendHistoryTask.ToString(),
                 connectionId,
                 customMessageId: finalTaskId);
        }
        
        public Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPostSNSNewsTaskAsync(string connectionId, string content, List<string> attachments, long taskId)
        {
            return SendPostSNSNewsTaskAsync(connectionId, MomentPostRequestDto.FromLegacy(content, attachments), taskId);
        }

        /// <summary>
        /// 下发 62203 高级朋友圈发布任务。
        /// <para>支持附件类型、可见范围、POI、提醒谁看、首评/追加评论和慢发实验开关。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPostSNSNewsTaskAsync(string connectionId, MomentPostRequestDto? request, long taskId)
        {
            var normalized = NormalizeMomentPostRequest(request);
            var validation = MomentPostRequestValidator.Validate(normalized);
            if (!validation.IsValid)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, validation.ErrorMessage);
            }

            RegisterMomentPostTaskContext(BuildMomentPostTaskContext(taskId, normalized));
            var task = new PostSNSNewsTaskMessage
            {
                WeChatId = normalized.weChatId,
                Content = normalized.content,
                Comment = normalized.comment,
                SendSlow = normalized.sendSlow,
                TaskId = taskId
            };

            if (normalized.attachment.content.Count > 0)
            {
                task.Attachment = new PostSNSNewsTaskMessage.Types.AttachmentMessage
                {
                    Type = MapMomentAttachmentType(normalized.attachment.type)
                };
                foreach (var attachment in normalized.attachment.content)
                {
                    task.Attachment.Content.Add(attachment);
                }
            }

            if (ShouldSendVisible(normalized.visible))
            {
                task.Visible = new PostSNSNewsTaskMessage.Types.VisibleMessage
                {
                    Type = MapMomentVisibleType(normalized.visible.type),
                    Labels = string.Join(",", normalized.visible.labels),
                    Friends = string.Join(",", normalized.visible.friends)
                };
            }

            if (ShouldSendPoi(normalized.poi))
            {
                task.Poi = new PostSNSNewsTaskMessage.Types.PoiMessage
                {
                    City = normalized.poi.city,
                    Name = normalized.poi.name,
                    Address = normalized.poi.address,
                    Lat = normalized.poi.lat,
                    Lng = normalized.poi.lng,
                    PoiId = normalized.poi.poiId
                };
            }

            task.ExtComment.AddRange(normalized.extComment);
            task.NotiUsers.AddRange(normalized.notiUsers);

            var result = await SendTaskAndWaitAsync(
                task,
                EnumMsgType.PostSnsnewsTask.ToString(),
                connectionId,
                taskId,
                ResolveMomentPostTimeoutMs(normalized));

            if (!result.success
                && string.Equals(result.message, "Failed to send to Netty", StringComparison.OrdinalIgnoreCase))
            {
                RemoveMomentPostTaskContext(taskId);
            }

            return result;
        }

        private static MomentPostRequestDto NormalizeMomentPostRequest(MomentPostRequestDto? request)
        {
            return MomentPostRequestValidator.Normalize(request);
        }

        private static MomentPostTaskContext BuildMomentPostTaskContext(long taskId, MomentPostRequestDto request)
        {
            var auditMetadata = MomentPostRequestValidator.BuildSafeAuditMetadata(request);
            return new MomentPostTaskContext
            {
                TaskId = taskId,
                ClientRequestId = request.clientRequestId,
                WeChatId = request.weChatId,
                AttachmentType = request.attachment.type.ToString(),
                AttachmentCount = request.attachment.content.Count,
                VisibleType = request.visible.type.ToString(),
                LabelCount = request.visible.labels.Count,
                FriendCount = request.visible.friends.Count,
                NotiUserCount = request.notiUsers.Count,
                ExtCommentCount = request.extComment.Count,
                HasComment = !string.IsNullOrWhiteSpace(request.comment),
                HasPoi = ShouldSendPoi(request.poi),
                SendSlow = request.sendSlow,
                PayloadHash = auditMetadata["payloadHash"]?.ToString() ?? string.Empty,
                ContentHash = auditMetadata["contentHash"]?.ToString() ?? string.Empty,
                VisibleTargetsHash = auditMetadata["visibleTargetsHash"]?.ToString() ?? string.Empty,
                NotiUsersHash = auditMetadata["notiUsersHash"]?.ToString() ?? string.Empty,
                AttachmentHash = auditMetadata["attachmentHash"]?.ToString() ?? string.Empty,
                EffectiveWeChatIdHash = auditMetadata["effectiveWeChatIdHash"]?.ToString() ?? string.Empty,
                ExpiresAt = DateTimeOffset.UtcNow.Add(MomentPostTaskContextTtl)
            };
        }

        private static int ResolveMomentPostTimeoutMs(MomentPostRequestDto request)
        {
            var needsLongerWait = request.attachment.type == MomentPostAttachmentType.ShortVideo
                || request.attachment.type == MomentPostAttachmentType.LongVideo
                || request.attachment.type == MomentPostAttachmentType.ShiPinHao
                || request.attachment.type == MomentPostAttachmentType.FinderLive
                || !string.IsNullOrWhiteSpace(request.comment)
                || request.extComment.Count > 0
                || request.sendSlow;

            return needsLongerWait ? 90000 : 60000;
        }

        private static List<string> NormalizeStringList(IEnumerable<string>? values)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (values == null)
            {
                return result;
            }

            foreach (var value in values)
            {
                var normalized = value?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalized) || !seen.Add(normalized))
                {
                    continue;
                }

                result.Add(normalized);
            }

            return result;
        }

        private static bool ShouldSendVisible(MomentPostVisibleDto visible)
        {
            return visible.type != MomentPostVisibleType.Public
                || visible.labels.Count > 0
                || visible.friends.Count > 0;
        }

        private static bool ShouldSendPoi(MomentPostPoiDto poi)
        {
            return !string.IsNullOrWhiteSpace(poi.city)
                || !string.IsNullOrWhiteSpace(poi.name)
                || !string.IsNullOrWhiteSpace(poi.address)
                || !string.IsNullOrWhiteSpace(poi.poiId)
                || Math.Abs(poi.lat) > 0.000001f
                || Math.Abs(poi.lng) > 0.000001f;
        }

        private static PostSNSNewsTaskMessage.Types.AttachmentMessage.Types.EnumAttachType MapMomentAttachmentType(MomentPostAttachmentType type)
        {
            return type switch
            {
                MomentPostAttachmentType.Link => PostSNSNewsTaskMessage.Types.AttachmentMessage.Types.EnumAttachType.Link,
                MomentPostAttachmentType.Picture => PostSNSNewsTaskMessage.Types.AttachmentMessage.Types.EnumAttachType.Picture,
                MomentPostAttachmentType.ShortVideo => PostSNSNewsTaskMessage.Types.AttachmentMessage.Types.EnumAttachType.ShortVideo,
                MomentPostAttachmentType.LongVideo => PostSNSNewsTaskMessage.Types.AttachmentMessage.Types.EnumAttachType.LongVideo,
                MomentPostAttachmentType.ShiPinHao => PostSNSNewsTaskMessage.Types.AttachmentMessage.Types.EnumAttachType.ShiPinHao,
                MomentPostAttachmentType.ExtLink => PostSNSNewsTaskMessage.Types.AttachmentMessage.Types.EnumAttachType.ExtLink,
                MomentPostAttachmentType.FinderLive => PostSNSNewsTaskMessage.Types.AttachmentMessage.Types.EnumAttachType.FinderLive,
                _ => PostSNSNewsTaskMessage.Types.AttachmentMessage.Types.EnumAttachType.Picture
            };
        }

        private static PostSNSNewsTaskMessage.Types.VisibleMessage.Types.EnumVisibleType MapMomentVisibleType(MomentPostVisibleType type)
        {
            return type switch
            {
                MomentPostVisibleType.Public => PostSNSNewsTaskMessage.Types.VisibleMessage.Types.EnumVisibleType.Public,
                MomentPostVisibleType.Private => PostSNSNewsTaskMessage.Types.VisibleMessage.Types.EnumVisibleType.Private,
                MomentPostVisibleType.WhoVisible => PostSNSNewsTaskMessage.Types.VisibleMessage.Types.EnumVisibleType.WhoVisible,
                MomentPostVisibleType.WhoInvisible => PostSNSNewsTaskMessage.Types.VisibleMessage.Types.EnumVisibleType.WhoInvisible,
                _ => PostSNSNewsTaskMessage.Types.VisibleMessage.Types.EnumVisibleType.Public
            };
        }

        public async Task<bool> SendTriggerFriendPushTaskAsync(string connectionId, long taskId)
        {
            // Init task, fire-and-forget likely preferred unless we want to wait for "Start Push" Ack
            var task = new TriggerFriendPushTaskMessage { TaskId = taskId };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.TriggerFriendPushTask.ToString(), connectionId);
        }

        /// <summary>
        /// 请求客户端回传单个联系人资料。
        /// <para>用于好友验证通过后补昵称/头像；结果由 ContactInfoNotice 处理器落库。</para>
        /// </summary>
        public async Task<bool> SendGetContactInfoTaskAsync(
            string connectionId,
            string contact,
            string chatroom = "",
            string ticket = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new GetContactInfoTaskMessage
            {
                Contact = contact ?? string.Empty,
                Chatroom = chatroom ?? string.Empty,
                Ticket = ticket ?? string.Empty,
                TaskId = finalTaskId
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.GetContactInfoTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);
        }

        public async Task<bool> SendTriggerChatRoomPushTaskAsync(
            string connectionId,
            long taskId,
            int flag = 0,
            string weChatId = "")
        {
            var task = new TriggerChatRoomPushTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                Flag = flag,
                TaskId = taskId
            };
            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TriggerChatroomPushTask.ToString(),
                connectionId,
                customMessageId: taskId);
        }

        /// <summary>
        /// 请求客户端回传会话列表。
        /// <para>
        /// 62203 协议为 TriggerConversationPushTask(1232)，安卓端 tK89 会将结果以
        /// ConversationPushNotice(2035) 上报。群聊页同步时需要同时拉群资料和会话快照，
        /// 否则只有 Groups 表而没有 Conversations 表时，Web 端仍可能看不到群聊入口。
        /// </para>
        /// </summary>
        public async Task<bool> SendTriggerConversationPushTaskAsync(
            string connectionId,
            long startTime = 0,
            long endTime = 0,
            bool withName = true,
            int limit = 100,
            int offset = 0,
            long taskId = 0,
            string weChatId = "")
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            var task = new TriggerConversationPushTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                StartTime = startTime,
                EndTime = endTime,
                WithName = withName,
                TaskId = finalTaskId,
                Limit = limit,
                Offset = offset
            };

            _logger.LogInformation(
                "Send TriggerConversationPushTask: ConnectionId={ConnectionId}, TaskId={TaskId}, StartTime={StartTime}, EndTime={EndTime}, WithName={WithName}, Limit={Limit}, Offset={Offset}",
                connectionId,
                finalTaskId,
                startTime,
                endTime,
                withName,
                limit,
                offset);

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TriggerConversationPushTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);
        }

        /// <summary>
        /// 请求客户端回传历史聊天消息，承接协议 TriggerHistoryMsgPushTask(1089)。
        /// <para>结果由 HistoryMsgPushNotice(2033) 异步回传并按消息口径落库。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendTriggerHistoryMsgPushTaskAsync(
            string connectionId,
            string weChatId,
            string friendId = "",
            long startTime = 0,
            long endTime = 0,
            int flag = 0,
            int count = 50,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new TriggerHistoryMsgPushTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                FriendId = friendId ?? string.Empty,
                StartTime = startTime,
                EndTime = endTime,
                Flag = flag,
                Count = count,
                TaskId = finalTaskId
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TriggerHistoryMsgPushTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "历史消息同步指令已下发，等待客户端上报 HistoryMsgPushNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "历史消息同步指令下发失败");
        }

        /// <summary>
        /// 请求客户端上报指定会话已读状态，承接协议 TriggerMessageReadTask(1086)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendTriggerMessageReadTaskAsync(
            string connectionId,
            string weChatId,
            string friendId,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new TriggerMessageReadTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                FriendId = friendId ?? string.Empty
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TriggerMessageReadTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "会话已读同步指令已下发，等待客户端上报 PostMessageReadNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "会话已读同步指令下发失败");
        }

        /// <summary>
        /// 请求客户端回传未读会话列表，承接协议 TriggerUnreadPushTask(1238)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendTriggerUnreadPushTaskAsync(
            string connectionId,
            string weChatId,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new TriggerUnreadPushTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                TaskId = finalTaskId
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TriggerUnreadPushTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "未读会话列表同步指令已下发，等待客户端上报 UnreadListPushNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "未读会话列表同步指令下发失败");
        }

        /// <summary>
        /// 请求客户端把指定会话标记为未读，承接协议 TriggerUnReadTask(1281)。
        /// <para>
        /// Android 62203 该任务走 tK118/Tsk154，结果是通用 TaskResultNotice(TaskType=TriggerUnReadTask)，
        /// 不是 UnreadListPushNotice。成功后再异步触发一次 TriggerUnreadPushTask，用手机真实未读数校准会话列表。
        /// </para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendTriggerUnReadTaskAsync(
            string connectionId,
            string weChatId,
            string friendId,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new TriggerUnReadTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                FriendId = friendId ?? string.Empty,
                TaskId = finalTaskId
            };

            var result = await SendTaskAndWaitAsync(
                task,
                EnumMsgType.TriggerUnReadTask.ToString(),
                connectionId,
                finalTaskId,
                timeoutMs: 15000);

            if (result.success)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1.5));
                        await SendTriggerUnreadPushTaskAsync(connectionId, weChatId, DateTime.UtcNow.Ticks);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "标记未读成功后触发未读列表校准失败: ConnectionId={ConnectionId}, FriendId={FriendId}, TaskId={TaskId}",
                            connectionId,
                            friendId,
                            finalTaskId);
                    }
                });
            }

            return result;
        }

        /// <summary>
        /// 请求客户端回传业务联系人列表，承接协议 TriggerBizContactPushTask(1235)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendTriggerBizContactPushTaskAsync(
            string connectionId,
            string weChatId,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new TriggerBizContactPushTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                TaskId = finalTaskId
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TriggerBizContactPushTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "业务联系人同步指令已下发，等待客户端上报 BizContactPushNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "业务联系人同步指令下发失败");
        }

        /// <summary>
        /// 请求客户端回传企微会话列表，承接协议 TriggerQwConvPushTask(1240)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendTriggerQwConvPushTaskAsync(
            string connectionId,
            string weChatId,
            long startTime = 0,
            long endTime = 0,
            int limit = 100,
            int offset = 0,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new TriggerQwConvPushTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                StartTime = startTime,
                EndTime = endTime,
                TaskId = finalTaskId,
                Limit = limit,
                Offset = offset
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TriggerQwConvPushTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "企微会话同步指令已下发，等待客户端上报 QwConversPushNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "企微会话同步指令下发失败");
        }

        /// <summary>
        /// 请求客户端回传微信联系人标签列表，承接协议 TriggerLabelPushTask(1239)。
        /// <para>结果由 ContactLabelInfoNotice(2032) 异步上报，服务端按标签字典口径落库。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendTriggerLabelPushTaskAsync(
            string connectionId,
            string weChatId,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new TriggerLabelPushTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                TaskId = finalTaskId
            };

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TriggerLabelPushTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(finalTaskId, "联系人标签列表同步指令已下发，等待客户端上报 ContactLabelInfoNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(finalTaskId, "联系人标签列表同步指令下发失败");
        }

        /// <summary>
        /// 创建/重命名联系人标签，或调整标签成员，承接协议 ContactLabelTask(1224)。
        /// <para>AddList/DelList 是逗号分隔 wxid；LabelId=0 表示创建新标签，真实标签 ID 以后续 ContactLabelAddNotice 为准。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendContactLabelTaskAsync(
            string connectionId,
            string weChatId,
            string labelName,
            int labelId = 0,
            string addList = "",
            string delList = "",
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new ContactLabelTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                LabelName = labelName ?? string.Empty,
                LabelId = Math.Max(0, labelId),
                AddList = addList ?? string.Empty,
                DelList = delList ?? string.Empty,
                TaskId = finalTaskId
            };

            RegisterContactLabelTaskContext(new ContactLabelTaskContext
            {
                TaskId = finalTaskId,
                WeChatId = task.WeChatId,
                LabelId = task.LabelId,
                LabelName = task.LabelName
            });

            var result = await SendTaskAndWaitAsync(
                task,
                EnumMsgType.ContactLabelTask.ToString(),
                connectionId,
                finalTaskId,
                timeoutMs: 30000);

            if (result.success)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1.5));
                        await SendTriggerLabelPushTaskAsync(connectionId, task.WeChatId, DateTime.UtcNow.Ticks);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "联系人标签任务成功后触发标签列表校准失败: ConnectionId={ConnectionId}, LabelId={LabelId}, TaskId={TaskId}",
                            connectionId,
                            labelId,
                            finalTaskId);
                    }
                });
            }

            return result;
        }

        /// <summary>
        /// 删除联系人标签，承接协议 ContactLabelDeleteTask(1225)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendContactLabelDeleteTaskAsync(
            string connectionId,
            string weChatId,
            int labelId,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var task = new ContactLabelDeleteTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                LabelId = labelId,
                TaskId = finalTaskId
            };

            RegisterContactLabelDeleteTaskContext(new ContactLabelDeleteTaskContext
            {
                TaskId = finalTaskId,
                WeChatId = task.WeChatId,
                LabelId = task.LabelId
            });

            var result = await SendTaskAndWaitAsync(
                task,
                EnumMsgType.ContactLabelDeleteTask.ToString(),
                connectionId,
                finalTaskId,
                timeoutMs: 30000);

            if (result.success)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1.5));
                        await SendTriggerLabelPushTaskAsync(connectionId, task.WeChatId, DateTime.UtcNow.Ticks);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "联系人标签删除成功后触发标签列表校准失败: ConnectionId={ConnectionId}, LabelId={LabelId}, TaskId={TaskId}",
                            connectionId,
                            labelId,
                            finalTaskId);
                    }
                });
            }

            return result;
        }

        /// <summary>
        /// 设置单个好友的完整标签 ID 集合，承接协议 ContactSetLabelTask(1270)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendContactSetLabelTaskAsync(
            string connectionId,
            string weChatId,
            string friendId,
            IEnumerable<int>? labelIds,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var normalizedLabelIds = (labelIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();

            var task = new ContactSetLabelTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                FriendId = friendId ?? string.Empty,
                TaskId = finalTaskId
            };
            task.LabelIds.AddRange(normalizedLabelIds);

            RegisterContactSetLabelTaskContext(new ContactSetLabelTaskContext
            {
                TaskId = finalTaskId,
                WeChatId = task.WeChatId,
                FriendId = task.FriendId,
                LabelIds = normalizedLabelIds
            });

            return await SendTaskAndWaitAsync(
                task,
                EnumMsgType.ContactSetLabelTask.ToString(),
                connectionId,
                finalTaskId,
                timeoutMs: 30000);
        }


        /// <summary>
        /// 发送群邀请列表拉取任务，承接协议 GetChatRoomInviteListTask(1292)。
        /// </summary>
        public async Task<bool> SendGetChatRoomInviteListTaskAsync(string connectionId, string weChatId = "", long taskId = 0)
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            var task = new GetChatRoomInviteListTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                TaskId = finalTaskId
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.GetChatRoomInviteListTask.ToString(),
                connectionId,
                customMessageId: finalTaskId);
        }


        /// <summary>
        /// 视频号提及列表拉取任务，承接协议 SphGetMentionTask(2200)。
        /// </summary>
        public async Task<bool> SendSphGetMentionTaskAsync(string connectionId, long lastLikeId = 0, long lastCommentId = 0, long lastFollowId = 0, long taskId = 0)
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            var task = new SphGetMentionTaskMessage
            {
                LastLikeId = lastLikeId,
                LastCommentId = lastCommentId,
                LastFollowId = lastFollowId,
                TaskId = finalTaskId
            };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.SphGetMentionTask.ToString(), connectionId, customMessageId: finalTaskId);
        }

        /// <summary>
        /// 视频号评论列表拉取任务，承接本地增强协议 SphGetCommentTask(2204)。
        /// </summary>
        public async Task<bool> SendSphGetCommentTaskAsync(string connectionId, long feedId, string nonceId, string feedAuth, long refCommentId = 0, long replyCommentId = 0, int sortType = 0, long taskId = 0)
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            var task = new SphGetCommentTaskMessage
            {
                FeedId = feedId,
                NonceId = nonceId ?? string.Empty,
                FeedAuth = feedAuth ?? string.Empty,
                RefCommentId = refCommentId,
                ReplyCommentId = replyCommentId,
                SortType = sortType,
                TaskId = finalTaskId
            };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.SphGetCommentTask.ToString(), connectionId, customMessageId: finalTaskId);
        }

        /// <summary>
        /// 视频号用户页拉取任务，承接协议 SphUserPageTask(2202)。
        /// </summary>
        public async Task<bool> SendSphUserPageTaskAsync(string connectionId, string sphUserName, long taskId = 0)
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            var task = new SphUserPageTaskMessage
            {
                SphUserName = sphUserName ?? string.Empty,
                TaskId = finalTaskId
            };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.SphUserPageTask.ToString(), connectionId, customMessageId: finalTaskId);
        }

        /// <summary>
        /// 视频号发布任务，承接协议 SphPostTask(2210)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendSphPostTaskAsync(string connectionId, string content, List<string> medias, int mediaType = 0, string cover = "", long taskId = 0)
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            var task = new SphPostTaskMessage
            {
                Content = content ?? string.Empty,
                MediaType = mediaType,
                Cover = cover ?? string.Empty,
                TaskId = finalTaskId
            };
            if (medias != null)
            {
                foreach (var media in medias)
                {
                    if (!string.IsNullOrWhiteSpace(media))
                    {
                        task.Medias.Add(media);
                    }
                }
            }
            return await SendTaskAndWaitAsync(task, EnumMsgType.SphPostTask.ToString(), connectionId, finalTaskId, 60000);
        }

        /// <summary>
        /// 视频号评论任务，承接协议 SphCommentTask(2213)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendSphCommentTaskAsync(string connectionId, long feedId, string nonceId, string feedAuth, int type, string content, string media = "", long replyCommentId = 0, string replyUsername = "", long taskId = 0)
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            var task = new SphCommentTaskMessage
            {
                FeedId = feedId,
                NonceId = nonceId ?? string.Empty,
                FeedAuth = feedAuth ?? string.Empty,
                Type = type,
                Content = content ?? string.Empty,
                Media = media ?? string.Empty,
                ReplyCommentId = replyCommentId,
                ReplyUsername = replyUsername ?? string.Empty,
                TaskId = finalTaskId
            };
            return await SendTaskAndWaitAsync(task, EnumMsgType.SphCommentTask.ToString(), connectionId, finalTaskId, 30000);
        }

        /// <summary>
        /// 视频号点赞任务，承接协议 SphLikeTask(2212)。SmRun 当前会显式返回未实现结果。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendSphLikeTaskAsync(string connectionId, long feedId, int type = 1, bool isCancel = false, long taskId = 0)
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            var task = new SphLikeTaskMessage
            {
                FeedId = feedId,
                Type = type,
                IsCancel = isCancel,
                TaskId = finalTaskId
            };
            return await SendTaskAndWaitAsync(task, EnumMsgType.SphLikeTask.ToString(), connectionId, finalTaskId, 30000);
        }

        /// <summary>
        /// 视频号删评任务，承接协议 SphDelCommentTask(2214)。SmRun 当前会显式返回未实现结果。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendSphDelCommentTaskAsync(string connectionId, long feedId, long commentId, long taskId = 0)
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            var task = new SphDelCommentTaskMessage
            {
                FeedId = feedId,
                CommentId = commentId,
                TaskId = finalTaskId
            };
            return await SendTaskAndWaitAsync(task, EnumMsgType.SphDelCommentTask.ToString(), connectionId, finalTaskId, 30000);
        }

        public async Task<bool> SendTriggerCirclePushTaskAsync(
            string connectionId,
            long taskId,
            string weChatId = "",
            long startTime = 0,
            IEnumerable<long>? circleIds = null)
        {
             var task = new TriggerCirclePushTaskMessage
             {
                 WeChatId = weChatId ?? string.Empty,
                 StartTime = startTime,
                 TaskId = taskId
             };

             foreach (var circleId in circleIds ?? Enumerable.Empty<long>())
             {
                 // 微信 snsId 可能为负数，0 才表示无效。
                 if (circleId != 0)
                 {
                     task.CircleIds.Add(circleId);
                 }
             }

             return await _nettyMessageService.SendMessageToNettyAsync(
                 task,
                 EnumMsgType.TriggerCirclePushTask.ToString(),
                 connectionId,
                 customMessageId: taskId);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendOneKeyLikeTaskAsync(
            string connectionId,
            long taskId,
            string weChatId = "",
            int rate = 100,
            int num = 0,
            int endTime = 0,
            int timeOut = 0)
        {
            var task = new OneKeyLikeTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                TaskId = taskId,
                Rate = rate,
                Num = num,
                EndTime = endTime,
                TimeOut = timeOut
            };
            return await SendTaskAndWaitAsync(task, EnumMsgType.OneKeyLikeTask.ToString(), connectionId, taskId);
        }

        /// <summary>
        /// 撤回指定聊天消息，承接协议 RevokeMessageTask(1087)。
        /// <para>Android 通过通用 TaskResultNotice 回包；成功后服务端会按上下文标记本地消息为已撤回。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendRevokeMessageTaskAsync(
            string connectionId,
            string weChatId,
            string friendId,
            long msgSvrId,
            long taskId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "连接 ID 为空，无法下发消息撤回任务");
            }

            if (string.IsNullOrWhiteSpace(weChatId) || string.IsNullOrWhiteSpace(friendId) || msgSvrId == 0)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "微信账号、会话 ID 或 MsgSvrId 为空，无法撤回消息");
            }

            var ownerWxid = weChatId.Trim();
            var targetFriendId = friendId.Trim();
            var task = new RevokeMessageTaskMessage
            {
                WeChatId = ownerWxid,
                FriendId = targetFriendId,
                MsgId = msgSvrId,
                TaskId = taskId
            };

            RegisterRevokeMessageTaskContext(new RevokeMessageTaskContext
            {
                TaskId = taskId,
                WeChatId = ownerWxid,
                FriendId = targetFriendId,
                MsgSvrId = msgSvrId
            });

            return await SendTaskAndWaitAsync(task, EnumMsgType.RevokeMessageTask.ToString(), connectionId, taskId);
        }

        /// <summary>
        /// 转发一条已有聊天消息，承接协议 ForwardMessageTask(1088)。
        /// <para>
        /// Android 62203 使用 Talker + MsgSrvId 定位原消息，FriendIds 为目标接收人列表（通常逗号分隔）。
        /// 成功只表示微信侧转发动作已执行，不代表新消息已立即落入 SCRM；后续仍依赖 FriendTalkNotice/消息补偿同步。
        /// </para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendForwardMessageTaskAsync(
            string connectionId,
            string weChatId,
            string talker,
            long msgSvrId,
            string friendIds,
            string extMsg,
            long taskId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "连接 ID 为空，无法下发消息转发任务");
            }

            var normalizedFriendIds = NormalizeForwardTargetIds(friendIds);
            if (string.IsNullOrWhiteSpace(weChatId)
                || string.IsNullOrWhiteSpace(talker)
                || msgSvrId == 0
                || string.IsNullOrWhiteSpace(normalizedFriendIds))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "微信账号、原会话、MsgSvrId 或目标接收人为空，无法转发消息");
            }

            var task = new ForwardMessageTaskMessage
            {
                WeChatId = weChatId.Trim(),
                Talker = talker.Trim(),
                MsgSrvId = msgSvrId,
                FriendIds = normalizedFriendIds,
                ExtMsg = extMsg?.Trim() ?? string.Empty,
                TaskId = taskId
            };

            RegisterForwardTaskContext(new ForwardTaskContext
            {
                TaskId = taskId,
                WeChatId = task.WeChatId,
                ForwardType = "single",
                SourceTalker = task.Talker,
                SourceMsgSvrIds = new[] { msgSvrId },
                TargetFriendIds = SplitForwardTargetIds(normalizedFriendIds),
                ExtMsg = task.ExtMsg,
                SendRecord = false
            });

            var result = await SendTaskAndWaitAsync(task, EnumMsgType.ForwardMessageTask.ToString(), connectionId, taskId, 20000);
            if (!result.success
                && string.Equals(result.message, "Failed to send to Netty", StringComparison.OrdinalIgnoreCase))
            {
                RemoveForwardTaskContext(taskId);
            }

            return result;
        }

        /// <summary>
        /// 转发多条已有聊天消息，承接协议 ForwardMultiMessageTask(1092)。
        /// <para>
        /// Android 62203 使用 Talker + MsgIds 定位一组原消息，FriendIds 为目标接收人列表。
        /// SendRecord 表示是否附带转发记录，具体展示由微信客户端决定。
        /// </para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendForwardMultiMessageTaskAsync(
            string connectionId,
            string weChatId,
            string talker,
            IEnumerable<long>? msgIds,
            string friendIds,
            string extMsg,
            bool sendRecord,
            long taskId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "连接 ID 为空，无法下发多条消息转发任务");
            }

            var normalizedMsgIds = (msgIds ?? Enumerable.Empty<long>())
                .Where(id => id > 0)
                .Distinct()
                .ToArray();
            var normalizedFriendIds = NormalizeForwardTargetIds(friendIds);

            if (string.IsNullOrWhiteSpace(weChatId)
                || string.IsNullOrWhiteSpace(talker)
                || normalizedMsgIds.Length == 0
                || string.IsNullOrWhiteSpace(normalizedFriendIds))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "微信账号、原会话、消息 ID 列表或目标接收人为空，无法转发多条消息");
            }

            var task = new ForwardMultiMessageTaskMessage
            {
                WeChatId = weChatId.Trim(),
                Talker = talker.Trim(),
                FriendIds = normalizedFriendIds,
                ExtMsg = extMsg?.Trim() ?? string.Empty,
                SendRecord = sendRecord,
                TaskId = taskId
            };
            task.MsgIds.AddRange(normalizedMsgIds);

            RegisterForwardTaskContext(new ForwardTaskContext
            {
                TaskId = taskId,
                WeChatId = task.WeChatId,
                ForwardType = "multi",
                SourceTalker = task.Talker,
                SourceMsgSvrIds = normalizedMsgIds,
                TargetFriendIds = SplitForwardTargetIds(normalizedFriendIds),
                ExtMsg = task.ExtMsg,
                SendRecord = sendRecord
            });

            var result = await SendTaskAndWaitAsync(task, EnumMsgType.ForwardMultiMessageTask.ToString(), connectionId, taskId, 30000);
            if (!result.success
                && string.Equals(result.message, "Failed to send to Netty", StringComparison.OrdinalIgnoreCase))
            {
                RemoveForwardTaskContext(taskId);
            }

            return result;
        }

        /// <summary>
        /// 按原始内容转发消息，承接协议 ForwardMessageByContentTask(1220)。
        /// <para>
        /// 该任务用于没有完整本地消息对象时，按 MsgSvrId、MsgType、Content、Thumb 让 Android 侧构造转发请求。
        /// </para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendForwardMessageByContentTaskAsync(
            string connectionId,
            string weChatId,
            string friendIds,
            long msgSvrId,
            int msgType,
            string content,
            string thumb,
            string extMsg,
            long taskId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "连接 ID 为空，无法下发原始内容转发任务");
            }

            var normalizedFriendIds = NormalizeForwardTargetIds(friendIds);
            if (string.IsNullOrWhiteSpace(weChatId)
                || string.IsNullOrWhiteSpace(normalizedFriendIds)
                || msgType <= 0
                || (msgSvrId == 0 && string.IsNullOrWhiteSpace(content)))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "微信账号、目标接收人、消息类型为空，或缺少 MsgSvrId 且内容为空，无法按内容转发消息");
            }

            if (CountForwardTargetIds(normalizedFriendIds) != 1)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "按内容转发当前只支持单个目标，请选择一位好友或群聊");
            }

            var task = new ForwardMessageByContentTaskMessage
            {
                WeChatId = weChatId.Trim(),
                FriendIds = normalizedFriendIds,
                MsgSvrId = msgSvrId,
                MsgType = msgType,
                Content = content ?? string.Empty,
                Thumb = thumb ?? string.Empty,
                ExtMsg = extMsg?.Trim() ?? string.Empty,
                TaskId = taskId
            };

            RegisterForwardTaskContext(new ForwardTaskContext
            {
                TaskId = taskId,
                WeChatId = task.WeChatId,
                ForwardType = "byContent",
                SourceTalker = string.Empty,
                SourceMsgSvrIds = msgSvrId > 0 ? new[] { msgSvrId } : Array.Empty<long>(),
                TargetFriendIds = SplitForwardTargetIds(normalizedFriendIds),
                ExtMsg = task.ExtMsg,
                SendRecord = false
            });

            var result = await SendTaskAndWaitAsync(task, EnumMsgType.ForwardMessageByContentTask.ToString(), connectionId, taskId, 30000);
            if (!result.success
                && string.Equals(result.message, "Failed to send to Netty", StringComparison.OrdinalIgnoreCase))
            {
                RemoveForwardTaskContext(taskId);
            }

            return result;
        }

        /// <summary>
        /// 清空微信客户端聊天记录，承接协议 ClearAllChatMsgTask(1230)。
        /// <para>该任务只操作微信端本地聊天记录，不删除 SCRM 服务端已落库消息。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendClearAllChatMsgTaskAsync(
            string connectionId,
            string weChatId,
            int flag,
            long taskId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "连接 ID 为空，无法下发清空聊天记录任务");
            }

            if (string.IsNullOrWhiteSpace(weChatId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "微信账号为空，无法清空微信端聊天记录");
            }

            var task = new ClearAllChatMsgTaskMessage
            {
                WeChatId = weChatId.Trim(),
                Flag = flag,
                TaskId = taskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.ClearAllChatMsgTask.ToString(), connectionId, taskId, 45000);
        }

        /// <summary>
        /// 规整转发目标列表，保持 Android 62203 预期的逗号分隔格式。
        /// </summary>
        private static string NormalizeForwardTargetIds(string friendIds)
        {
            return string.Join(
                ',',
                SplitForwardTargetIds(friendIds));
        }

        /// <summary>
        /// 统计规整后的转发目标数，用于按内容转发的单目标保护。
        /// </summary>
        private static int CountForwardTargetIds(string friendIds)
        {
            return SplitForwardTargetIds(friendIds).Length;
        }

        private static string[] SplitForwardTargetIds(string friendIds)
        {
            return (friendIds ?? string.Empty)
                .Split(new[] { ',', '，', ';', '；', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendChatRoomActionTaskAsync(string connectionId, string chatRoomId, EnumChatRoomAction action, string content, int intValue, long taskId)
        {
            var task = new ChatRoomActionTaskMessage
            {
                ChatRoomId = chatRoomId,
                Action = action,
                Content = content,
                IntValue = intValue,
                TaskId = taskId
            };

            var result = await SendTaskAndWaitAsync(task, EnumMsgType.ChatRoomActionTask.ToString(), connectionId, taskId);
            if (result.success)
            {
                ScheduleChatRoomRefreshAfterMutation(connectionId, taskId, $"ChatRoomActionTask:{action}");
            }
            return result;
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAgreeJoinChatRoomTaskAsync(string connectionId, string talker, long msgSvrId, string msgContent, long taskId)
        {
            var task = new AgreeJoinChatRoomTaskMessage
            {
                Talker = talker,
                MsgSvrId = msgSvrId,
                MsgContent = msgContent,
                TaskId = taskId
            };
            return await SendTaskAndWaitAsync(task, EnumMsgType.AgreeJoinChatRoomTask.ToString(), connectionId, taskId, 30000); // 30s timeout for join
        }

        /// <summary>
        /// 发送群邀请确认任务，承接协议 ChatRoomInviteApproveTask(1221)。
        /// 62203 主链优先使用 MsgId；MsgSvrId 保留给旧安卓端兼容。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendChatRoomInviteApproveTaskAsync(
            string connectionId,
            long msgSvrId,
            string roomId = "",
            string msgContent = "",
            string weChatId = "",
            long msgId = 0,
            long? taskId = null)
        {
            var finalTaskId = taskId ?? DateTime.UtcNow.Ticks;
            var finalMsgId = msgId != 0 ? msgId : msgSvrId;
            var task = new ChatRoomInviteApproveTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                RoomId = roomId ?? string.Empty,
                MsgSvrId = msgSvrId,
                MsgId = finalMsgId,
                MsgContent = msgContent ?? string.Empty,
                TaskId = finalTaskId
            };
            RegisterChatRoomInviteApproveTaskContext(new ChatRoomInviteApproveTaskContext
            {
                TaskId = finalTaskId,
                WeChatId = task.WeChatId,
                RoomId = task.RoomId,
                MsgId = finalMsgId
            });
            return await SendTaskAndWaitAsync(task, EnumMsgType.ChatRoomInviteApproveTask.ToString(), connectionId, finalTaskId, 30000);
        }
        
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendDeleteFriendTaskAsync(string connectionId, string friendId, long taskId, string weChatId = "")
        {
            var task = new DeleteFriendTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                FriendId = friendId,
                TaskId = taskId
            };
            var result = await SendTaskAndWaitAsync(task, EnumMsgType.DeleteFriendTask.ToString(), connectionId, taskId);
            if (result.success)
            {
                ScheduleContactListRefreshAfterMutation(connectionId, taskId, "DeleteFriendTask");
            }
            return result;
        }

        /// <summary>
        /// 发送修改好友备注任务。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendModifyFriendMemoTaskAsync(
            string connectionId,
            string friendId,
            string memo,
            string desc,
            string phone,
            int delFlag,
            long taskId)
        {
            var task = new ModifyFriendMemoTaskMessage
            {
                FriendId = friendId,
                Memo = memo ?? string.Empty,
                Desc = desc ?? string.Empty,
                Phone = phone ?? string.Empty,
                DelFlag = delFlag,
                TaskId = taskId
            };
            var result = await SendTaskAndWaitAsync(task, EnumMsgType.ModifyFriendMemoTask.ToString(), connectionId, taskId, 30000);
            if (result.success)
            {
                ScheduleContactListRefreshAfterMutation(connectionId, taskId, "ModifyFriendMemoTask");
            }
            return result;
        }

        /// <summary>
        /// 发送好友权限设置任务。
        /// </summary>
        /// <param name="connectionId">设备连接ID。</param>
        /// <param name="friendId">目标好友 wxid。</param>
        /// <param name="permissionMask">权限位掩码：8=仅聊天，2=不让他看我朋友圈，1=不看他朋友圈。</param>
        /// <param name="taskId">任务 ID。</param>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendSetFriendPermissionTaskAsync(
            string connectionId,
            string friendId,
            int permissionMask,
            long taskId,
            string weChatId = "")
        {
            var task = new SetFriendPermissionTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                FriendId = friendId ?? string.Empty,
                Permission = permissionMask,
                TaskId = taskId
            };
            var result = await SendTaskAndWaitAsync(task, EnumMsgType.SetFriendPermissionTask.ToString(), connectionId, taskId, 30000);
            if (result.success)
            {
                ScheduleContactListRefreshAfterMutation(connectionId, taskId, "SetFriendPermissionTask");
            }
            return result;
        }

        /// <summary>
        /// 发送手机号加好友任务，对齐 62203 AddFriendsTask(1072)。
        /// <para>当前 Android 执行层只读取 Phones[0]；因此这里保留 repeated 字段，但上层批量应逐个手机号下发。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAddFriendsTaskAsync(
            string connectionId,
            IEnumerable<string>? phones,
            string message,
            string remark,
            string label,
            int permission,
            long taskId,
            string weChatId = "")
        {
            var normalizedPhones = (phones ?? Enumerable.Empty<string>())
                .Where(phone => !string.IsNullOrWhiteSpace(phone))
                .Select(phone => phone.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "设备未在线或连接不存在");
            }

            if (normalizedPhones.Count == 0)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "手机号为空");
            }

            var task = new AddFriendsTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                Message = message ?? string.Empty,
                Remark = remark ?? string.Empty,
                Label = label ?? string.Empty,
                Permission = permission,
                TaskId = taskId
            };
            task.Phones.AddRange(normalizedPhones);

            var result = await SendTaskAndWaitAsync(task, EnumMsgType.AddFriendsTask.ToString(), connectionId, taskId, 45000);
            if (result.success)
            {
                ScheduleContactListRefreshAfterMutation(connectionId, taskId, "AddFriendsTask");
            }
            return result;
        }

        /// <summary>
        /// 发送通讯录加好友任务，对齐 62203 AddFriendFromPhonebookTask(1215)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAddFriendFromPhonebookTaskAsync(
            string connectionId,
            string message,
            int count,
            int index,
            long taskId,
            bool reset = false,
            string weChatId = "")
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "设备未在线或连接不存在");
            }

            var task = new AddFriendFromPhonebookTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                Message = message ?? string.Empty,
                Count = Math.Max(0, count),
                Index = Math.Max(0, index),
                Reset = reset,
                TaskId = taskId
            };

            var result = await SendTaskAndWaitAsync(task, EnumMsgType.AddFriendFromPhonebookTask.ToString(), connectionId, taskId, 45000);
            if (result.success)
            {
                ScheduleContactListRefreshAfterMutation(connectionId, taskId, "AddFriendFromPhonebookTask");
            }
            return result;
        }

        /// <summary>
        /// 发送名片加好友任务，对齐 62203 AddFriendNameCardTask(1236)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAddFriendNameCardTaskAsync(
            string connectionId,
            long msgSvrId,
            string message,
            string remark,
            long taskId,
            string weChatId = "")
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "设备未在线或连接不存在");
            }

            if (msgSvrId == 0)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "名片消息 MsgSvrId 为空");
            }

            var task = new AddFriendNameCardTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                MsgSvrId = msgSvrId,
                Message = message ?? string.Empty,
                Remark = remark ?? string.Empty,
                TaskId = taskId
            };

            var result = await SendTaskAndWaitAsync(task, EnumMsgType.AddFriendNameCardTask.ToString(), connectionId, taskId, 45000);
            if (result.success)
            {
                ScheduleContactListRefreshAfterMutation(connectionId, taskId, "AddFriendNameCardTask");
            }
            return result;
        }

        /// <summary>
        /// 重新发送好友验证任务，对齐 62203 SendFriendVerifyTask(1231)。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendFriendVerifyTaskAsync(
            string connectionId,
            string friendId,
            string message,
            long taskId,
            string weChatId = "")
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "设备未在线或连接不存在");
            }

            if (string.IsNullOrWhiteSpace(friendId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "好友 wxid 为空");
            }

            var task = new SendFriendVerifyTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                FriendId = friendId.Trim(),
                Message = message ?? string.Empty,
                TaskId = taskId
            };

            var result = await SendTaskAndWaitAsync(task, EnumMsgType.SendFriendVerifyTask.ToString(), connectionId, taskId, 45000);
            if (result.success)
            {
                ScheduleContactListRefreshAfterMutation(connectionId, taskId, "SendFriendVerifyTask");
            }
            return result;
        }

        /// <summary>
        /// 发送多图消息任务。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendMultiPictureTaskAsync(
            string connectionId,
            string friendWxId,
            List<string> imageUrls,
            long taskId)
        {
            var normalizedUrls = imageUrls?
                .Where(imageUrl => !string.IsNullOrWhiteSpace(imageUrl))
                .Select(imageUrl => imageUrl.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "设备未在线或连接不存在");
            }

            if (string.IsNullOrWhiteSpace(friendWxId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "图片发送目标为空");
            }

            if (normalizedUrls.Count == 0)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "图片地址为空");
            }

            var successCount = 0;
            var errors = new List<string>();
            for (var index = 0; index < normalizedUrls.Count; index++)
            {
                var imageTaskId = taskId + index;
                var result = await SendTalkToFriendTaskAsync(
                    connectionId,
                    friendWxId,
                    normalizedUrls[index],
                    EnumContentType.Picture,
                    string.Empty,
                    imageTaskId);

                if (result.success)
                {
                    successCount++;
                    await Task.Delay(350);
                    continue;
                }

                errors.Add($"第{index + 1}张失败: {result.message}");
            }

            _logger.LogInformation(
                "SendMultiPictureTaskAsync fallback via TalkToFriendTask: ConnectionId={ConnectionId}, Friend={FriendWxId}, Success={SuccessCount}/{Total}",
                connectionId,
                friendWxId,
                successCount,
                normalizedUrls.Count);

            // 后续每张图片的真实结果会通过 TalkToFriendTaskResultNotice 异步返回。
            // 这里的返回值只表示任务已经全部下发，避免第一张图尚在微信进程下载时网页误报“失败”。
            if (errors.Count > 0)
            {
                _logger.LogWarning(
                    "SendMultiPictureTaskAsync dispatch partial warnings: ConnectionId={ConnectionId}, Friend={FriendWxId}, Errors={Errors}",
                    connectionId,
                    friendWxId,
                    string.Join("；", errors));
            }

            return SCRM.SHARED.Models.Dtos.TaskResult.Ok(taskId, $"图片已逐张下发 {successCount}/{normalizedUrls.Count}，等待客户端回执");
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAcceptFriendAddRequestTaskAsync(
            string connectionId,
            string friendId,
            string friendNick,
            long taskId,
            AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation operation = AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation.Accept,
            string remark = "",
            string replyMsg = "",
            bool addWithWW = false,
            bool onlyWW = false,
            int permission = 0,
            string weChatId = "")
        {
            var task = new AcceptFriendAddRequestTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                FriendId = friendId ?? string.Empty,
                FriendNick = friendNick ?? string.Empty,
                Operation = operation,
                Remark = remark ?? string.Empty,
                ReplyMsg = replyMsg ?? string.Empty,
                AddWithWW = addWithWW,
                OnlyWW = onlyWW,
                Permission = permission,
                TaskId = taskId
            };
            var result = await SendTaskAndWaitAsync(task, EnumMsgType.AcceptFriendAddRequestTask.ToString(), connectionId, taskId, 30000);
            if (result.success && operation == AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation.Accept)
            {
                ScheduleContactListRefreshAfterMutation(connectionId, taskId, "AcceptFriendAddRequestTask");
            }
            return result;
        }
        
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendScreenShotTaskAsync(string connectionId, long taskId)
        {
            var task = new ScreenShotTaskMessage
            {
                Type = 0, 
                Param = "", 
                TaskId = taskId
            };
            // 截图可能先尝试原生 screencap，再回落到微信 UI 截图与文件上传，Android 15 上 20s 容易误超时。
            return await SendTaskAndWaitAsync(task, EnumMsgType.ScreenShotTask.ToString(), connectionId, taskId, 45000);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPhoneActionTaskAsync(
            string connectionId,
            EnumPhoneAction action,
            long taskId,
            string strParam = "",
            int intParam = 0,
            string weChatId = "",
            string imei = "")
        {
            var task = new PhoneActionTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                Imei = imei ?? string.Empty,
                Action = action,
                StrParam = strParam ?? string.Empty,
                IntParam = intParam,
                TaskId = taskId
            };

            // 62203 中 PhoneCall 成功拉起系统拨号/通话后通常不再回成功 TaskResultNotice；
            // Reboot 成功时客户端会停止心跳并关闭连接，也不会再回成功 TaskResultNotice。
            // 对这两类动作只确认“已下发”，避免真实已执行却被等待超时标成失败；
            // 如果客户端能立即判定失败（如无权限/无 root），后续 TaskResultNotice 仍会通过异步事件提示前端。
            if (action == EnumPhoneAction.PhoneCall || action == EnumPhoneAction.Reboot)
            {
                var queued = await _nettyMessageService.SendMessageToNettyAsync(
                    task,
                    EnumMsgType.PhoneActionTask.ToString(),
                    connectionId,
                    customMessageId: taskId);
                var actionName = action == EnumPhoneAction.Reboot ? "设备重启" : "手机通话";
                return queued
                    ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(taskId, $"{actionName}动作已下发")
                    : SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, $"{actionName}动作下发失败");
            }

            if (action == EnumPhoneAction.UploadFile)
            {
                return await SendTaskAndWaitAsync(task, EnumMsgType.PhoneActionTask.ToString(), connectionId, taskId, 60000);
            }

            return await SendTaskAndWaitAsync(task, EnumMsgType.PhoneActionTask.ToString(), connectionId, taskId);
        }

        /// <summary>
        /// 下发 GetA8KeyTask(1284)。
        /// <para>Android 成功时通过通用 TaskResultNotice.ErrMsg 返回 A8Key 后的 URL。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendGetA8KeyTaskAsync(
            string connectionId,
            string weChatId,
            int type,
            string url,
            string userName,
            string msgSvrId,
            int reason,
            long taskId)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "Url 不能为空");
            }

            var task = new GetA8KeyTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                Type = type,
                Url = url.Trim(),
                UserName = userName ?? string.Empty,
                MsgSvrId = msgSvrId ?? string.Empty,
                Reason = reason,
                TaskId = taskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.GetA8KeyTask.ToString(), connectionId, taskId, 45000);
        }

        /// <summary>
        /// 下发 WechatSettingTask(1233)，用于修改昵称、头像、隐私、性别、地区、签名。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendWechatSettingTaskAsync(
            string connectionId,
            string weChatId,
            EnumSettings action,
            string content,
            int intParam,
            long taskId)
        {
            var task = new WechatSettingTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                Action = action,
                Content = content ?? string.Empty,
                IntParam = intParam,
                TaskId = taskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.WechatSettingTask.ToString(), connectionId, taskId, 45000);
        }

        public async Task<bool> SendTriggerConfigPushTaskAsync(string connectionId, long taskId)
        {
            var task = new TriggerConfigPushMessage { };
            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TriggerConfigPush.ToString(),
                connectionId,
                customMessageId: taskId == 0 ? DateTime.UtcNow.Ticks : taskId);
        }

        public async Task<bool> SendSetConfigTaskAsync(string connectionId, Dictionary<string, bool> boolConfs, Dictionary<string, int> intConfs, Dictionary<string, string> strConfs)
        {
            var task = new SetConfigTaskMessage();
            if (boolConfs != null)
                foreach (var kvp in boolConfs) task.BoolConfs.Add(new BoolConfigMessage { Key = kvp.Key, Value = kvp.Value });
            if (intConfs != null)
                foreach (var kvp in intConfs) task.IntConfs.Add(new IntConfigMessage { Key = kvp.Key, Value = kvp.Value });
            if (strConfs != null)
                foreach (var kvp in strConfs) task.StrConfs.Add(new StrConfigMessage { Key = kvp.Key, Value = kvp.Value });

            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.SetConfigTask.ToString(), connectionId);
        }

        /// <summary>
        /// 下发微信违禁词列表，承接协议 SetForbiddenWord(1383)。
        /// <para>Android 当前只读取 KeyWord，KeyWord2/KeyWord3 暂不依赖；该任务无 TaskId/回执。</para>
        /// </summary>
        public async Task<bool> SendSetForbiddenWordAsync(string connectionId, string weChatId, IEnumerable<string>? words)
        {
            var task = new SetForbiddenWordMessage
            {
                WeChatId = weChatId ?? string.Empty
            };

            var normalizedWords = (words ?? Enumerable.Empty<string>())
                .Select(word => word?.Trim() ?? string.Empty)
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            task.KeyWord.AddRange(normalizedWords);
            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.SetForbiddenWord.ToString(),
                connectionId);
        }

        /// <summary>
        /// 下发删除设备前置通知，承接协议 PostDeleteDeviceNotice(1097)。
        /// <para>
        /// 该 Notice 没有 TaskId 和标准回包；返回值只表示消息是否成功写入当前在线 Netty 通道。
        /// Android 收到后会主动关闭连接并停止定位/保活相关逻辑。
        /// </para>
        /// </summary>
        public async Task<bool> SendPostDeleteDeviceNoticeAsync(string connectionId, string imei)
        {
            var notice = new PostDeleteDeviceNoticeMessage
            {
                IMEI = imei ?? string.Empty
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                notice,
                EnumMsgType.PostDeleteDeviceNotice.ToString(),
                connectionId);
        }

        /// <summary>
        /// 下发设备 App 升级通知，承接协议 UpgradeDeviceAppNotice(1094)。
        /// <para>
        /// 该 Notice 没有 TaskId 和标准回包；返回值只表示消息是否成功写入当前在线 Netty 通道。
        /// Android 端收到后会启动 AppUpdateDownloadThread 下载/安装，安装结果当前没有标准回传。
        /// </para>
        /// </summary>
        public async Task<bool> SendUpgradeDeviceAppNoticeAsync(
            string connectionId,
            string weChatId,
            string imei,
            string packageName,
            string version,
            int versionCode,
            string packageUrl)
        {
            var notice = new UpgradeDeviceAppNoticeMessage
            {
                WeChatId = weChatId ?? string.Empty,
                IMEI = imei ?? string.Empty
            };

            notice.AppInfos.Add(new DeviceAppUpgradeMessage
            {
                PackageName = packageName ?? string.Empty,
                Version = version ?? string.Empty,
                VerNumber = versionCode,
                PackageUrl = packageUrl ?? string.Empty
            });

            return await _nettyMessageService.SendMessageToNettyAsync(
                notice,
                EnumMsgType.UpgradeDeviceAppNotice.ToString(),
                connectionId);
        }
        
        public async Task<bool> SendTakeLuckyMoneyTaskAsync(string connectionId, string weChatId, string friendId, long msgSvrId, string key)
        {
            var task = new TakeLuckyMoneyTaskMessage
            {
                WeChatId = weChatId,
                FriendId = friendId,
                MsgSvrId = msgSvrId,
                MsgKey = key, 
                TaskId = DateTime.UtcNow.Ticks
            };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.TakeLuckyMoneyTask.ToString(), connectionId);
        }

        /// <summary>
        /// 下发发红包任务，承接协议 SendLuckyMoneyTask(1217)。
        /// <para>支付密码只随本次 Protobuf 任务下发，不写入日志或数据库。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendLuckyMoneyTaskAsync(
            string connectionId,
            string weChatId,
            string friendId,
            int money,
            int number,
            string passwd,
            string wish,
            long taskId)
        {
            var task = new SendLuckyMoneyTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                FriendId = friendId ?? string.Empty,
                Money = money,
                Number = number,
                Passwd = passwd ?? string.Empty,
                Wish = wish ?? string.Empty,
                TaskId = taskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.SendLuckyMoneyTask.ToString(), connectionId, taskId, 60000);
        }

        /// <summary>
        /// 下发转账任务，承接协议 RemittanceTask(1260)。
        /// <para>支付密码只随本次 Protobuf 任务下发，不写入日志或数据库。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendRemittanceTaskAsync(
            string connectionId,
            string weChatId,
            string friendId,
            int money,
            string passwd,
            string memo,
            long taskId,
            string roomId = "")
        {
            var task = new RemittanceTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                FriendId = friendId ?? string.Empty,
                Money = money,
                Passwd = passwd ?? string.Empty,
                Memo = memo ?? string.Empty,
                TaskId = taskId,
                RoomId = roomId ?? string.Empty
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.RemittanceTask.ToString(), connectionId, taskId, 60000);
        }

        public async Task<bool> SendQueryHbDetailTaskAsync(string connectionId, string weChatId, string nativeUrl)
        {
            var task = new QueryHbDetailTaskMessage
            {
                WeChatId = weChatId,
                HbUrl = nativeUrl 
            };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.QueryHbDetailTask.ToString(), connectionId);
        }

        /// <summary>
        /// 查询红包状态，承接协议 QueryHbStatusTask(1287)。
        /// <para>该协议没有 TaskId，真实结果通过 QueryHbStatusTaskResultNotice(1288) 异步推送。</para>
        /// </summary>
        public async Task<bool> SendQueryHbStatusTaskAsync(string connectionId, string weChatId, string nativeUrl)
        {
            var task = new QueryHbStatusTaskMessage
            {
                WeChatId = weChatId ?? string.Empty,
                HbUrl = nativeUrl ?? string.Empty
            };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.QueryHbStatusTask.ToString(), connectionId);
        }

        /// <summary>
        /// 下发微信登出任务，承接协议 WechatLogoutTask(1222)。
        /// <para>客户端完成后应继续等待 AccountLogoutNotice/离线通知校准页面状态。</para>
        /// </summary>
        public async Task<bool> SendWechatLogoutTaskAsync(string connectionId, string weChatId)
        {
            var task = new WechatLogoutTaskMessage
            {
                WeChatId = weChatId ?? string.Empty
            };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.WechatLogoutTask.ToString(), connectionId);
        }
        
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendCircleLikeTaskAsync(string connectionId, string weChatId, long circleId, bool isCancel, long taskId)
        {
            var task = new CircleLikeTaskMessage
            {
                WeChatId = weChatId,
                CircleId = circleId,
                IsCancel = isCancel,
                TaskId = taskId
            };
            RegisterMomentInteractionTaskContext(new MomentInteractionTaskContext
            {
                TaskId = taskId,
                Kind = MomentInteractionKind.Like,
                WeChatId = weChatId ?? string.Empty,
                CircleId = circleId,
                IsCancel = isCancel
            });
            return await SendTaskAndWaitAsync(task, EnumMsgType.CircleLikeTask.ToString(), connectionId, taskId);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendDeleteSNSNewsTaskAsync(string connectionId, string weChatId, long circleId, long taskId = 0)
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            var task = new DeleteSNSNewsTaskMessage { WeChatId = weChatId ?? string.Empty, CircleId = circleId, TaskId = finalTaskId };
            return await SendTaskAndWaitAsync(task, EnumMsgType.DeleteSnsnewsTask.ToString(), connectionId, finalTaskId, 30000);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendCircleCommentDeleteTaskAsync(string connectionId, string weChatId, long circleId, long commentId, long publishTime, long taskId = 0)
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            var task = new CircleCommentDeleteTaskMessage { WeChatId = weChatId ?? string.Empty, CircleId = circleId, CommentId = commentId, PublishTime = publishTime, TaskId = finalTaskId };
            RegisterMomentInteractionTaskContext(new MomentInteractionTaskContext
            {
                TaskId = finalTaskId,
                Kind = MomentInteractionKind.CommentDelete,
                WeChatId = weChatId ?? string.Empty,
                CircleId = circleId,
                CommentId = commentId,
                PublishTime = publishTime
            });
            return await SendTaskAndWaitAsync(task, EnumMsgType.CircleCommentDeleteTask.ToString(), connectionId, finalTaskId, 30000);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendCircleCommentReplyTaskAsync(string connectionId, string weChatId, long circleId, string toWeChatId, string content, long replyCommentId, bool isResend = false, long taskId = 0)
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            var task = new CircleCommentReplyTaskMessage { WeChatId = weChatId ?? string.Empty, CircleId = circleId, ToWeChatId = toWeChatId ?? string.Empty, Content = content ?? string.Empty, ReplyCommentId = replyCommentId, IsResend = isResend, TaskId = finalTaskId };
            RegisterMomentInteractionTaskContext(new MomentInteractionTaskContext
            {
                TaskId = finalTaskId,
                Kind = MomentInteractionKind.CommentReply,
                WeChatId = weChatId ?? string.Empty,
                CircleId = circleId,
                ToWeChatId = toWeChatId ?? string.Empty,
                Content = content ?? string.Empty,
                ReplyCommentId = replyCommentId
            });
            var result = await SendTaskAndWaitAsync(task, EnumMsgType.CircleCommentReplyTask.ToString(), connectionId, finalTaskId, 30000);
            if (!result.success && IsCircleCommentClientCallbackTimeout(result.message))
            {
                // 62203 上评论可能已经写入微信，但安卓端 Tsk50 完成回调未捕获；
                // 这里与 TaskMessageHandler 保持同一口径，避免同步等待链先返回失败。
                result.success = true;
                result.message = $"朋友圈评论已提交但客户端未捕获完成回调：CircleId={circleId}; {result.message}";
            }
            return result;
        }

        private static bool IsCircleCommentClientCallbackTimeout(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.Contains("TimeOut WAIT_TO_FINISHED", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Timeout waiting for client response", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> SendPullFriendCircleTaskAsync(string connectionId, string weChatId, string friendId, long refSnsId = 0, int count = 20, long startTime = 0, long refTime = 0, long taskId = 0)
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            var task = new PullFriendCircleTaskMessage { WeChatId = weChatId ?? string.Empty, FriendId = friendId ?? string.Empty, StartTime = startTime, Count = count, RefTime = refTime, RefSnsId = refSnsId, TaskId = finalTaskId };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.PullFriendCircleTask.ToString(), connectionId, customMessageId: finalTaskId);
        }

        public async Task<bool> SendPullCircleDetailTaskAsync(string connectionId, string weChatId, long circleId, bool getBigMap = false)
        {
            var task = new PullCircleDetailTaskMessage { WeChatId = weChatId ?? string.Empty, CircleId = circleId, GetBigMap = getBigMap };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.PullCircleDetailTask.ToString(), connectionId);
        }

        public async Task<bool> SendTriggerCircleMsgPushTaskAsync(string connectionId, string weChatId, bool onlyComment = false, bool getAll = true, long taskId = 0)
        {
            var finalTaskId = taskId == 0 ? DateTime.UtcNow.Ticks : taskId;
            var task = new TriggerCircleMsgPushTaskMessage { WeChatId = weChatId ?? string.Empty, OnlyComment = onlyComment, GetAll = getAll, TaskId = finalTaskId };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.TriggerCircleMsgPushTask.ToString(), connectionId, customMessageId: finalTaskId);
        }

        public async Task<bool> SendCircleMsgReadTaskAsync(string connectionId, string weChatId, long circleId, int commentId = 0)
        {
            var task = new CircleMsgReadTaskMessage { WeChatId = weChatId ?? string.Empty, CircleId = circleId, CommentId = commentId };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.CircleMsgReadTask.ToString(), connectionId);
        }

        public async Task<bool> SendCircleMsgClearTaskAsync(string connectionId, string weChatId, long circleId, int commentId = 0, bool isRead = true)
        {
            var task = new CircleMsgClearTaskMessage { WeChatId = weChatId ?? string.Empty, CircleId = circleId, CommentId = commentId, IsRead = isRead };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.CircleMsgClearTask.ToString(), connectionId);
        }
    }
}

