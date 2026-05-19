using System;

namespace SCRM.SHARED.Models.Dtos
{
    /// <summary>
    /// 朋友圈发布任务结构化结果。
    /// <para>由 PostSNSNewsTaskResultNotice 与服务端下发前保存的任务上下文合成，供 UI 展示发布中、超时等待、迟到成功和校准状态。</para>
    /// </summary>
    public sealed class MomentPostResultDto
    {
        /// <summary>
        /// 服务端下发任务 ID。
        /// </summary>
        public long taskId { get; set; }

        /// <summary>
        /// 前端请求级幂等标识。
        /// </summary>
        public string clientRequestId { get; set; } = string.Empty;

        /// <summary>
        /// Android 回传的微信账号。
        /// </summary>
        public string weChatId { get; set; } = string.Empty;

        /// <summary>
        /// 是否发布成功。
        /// </summary>
        public bool success { get; set; }

        /// <summary>
        /// 微信朋友圈 snsId；只有 0 表示无效，负数也可能是合法 snsId。
        /// </summary>
        public long circleId { get; set; }

        /// <summary>
        /// Android/微信端错误码。
        /// </summary>
        public string code { get; set; } = string.Empty;

        /// <summary>
        /// 安全截断后的错误摘要。
        /// </summary>
        public string errorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 当前结果状态，例如 Published、PublishedNeedSync、Failed、LatePublished。
        /// </summary>
        public string status { get; set; } = string.Empty;

        /// <summary>
        /// 安全状态，例如 Ok、AccountMismatch。
        /// </summary>
        public string securityStatus { get; set; } = "Ok";

        /// <summary>
        /// 回包微信号是否与下发上下文中的微信号一致。
        /// </summary>
        public bool accountMatched { get; set; } = true;

        /// <summary>
        /// 该结果是否在服务端等待窗口超时后才到达。
        /// </summary>
        public bool isLate { get; set; }

        /// <summary>
        /// 成功后是否需要触发朋友圈列表/详情校准。
        /// </summary>
        public bool needSync { get; set; }

        /// <summary>
        /// 附件类型摘要，不包含附件 URL 原文。
        /// </summary>
        public string attachmentType { get; set; } = string.Empty;

        /// <summary>
        /// 附件数量。
        /// </summary>
        public int attachmentCount { get; set; }

        /// <summary>
        /// 可见范围类型摘要。
        /// </summary>
        public string visibleType { get; set; } = string.Empty;

        /// <summary>
        /// 标签数量，不包含标签原文。
        /// </summary>
        public int labelCount { get; set; }

        /// <summary>
        /// 好友数量，不包含 wxid 原文。
        /// </summary>
        public int friendCount { get; set; }

        /// <summary>
        /// 提醒人数量，不包含 wxid 原文。
        /// </summary>
        public int notiUserCount { get; set; }

        /// <summary>
        /// 追加评论数量。
        /// </summary>
        public int extCommentCount { get; set; }

        /// <summary>
        /// 是否包含首条自动评论。
        /// </summary>
        public bool hasComment { get; set; }

        /// <summary>
        /// 是否包含 POI。
        /// </summary>
        public bool hasPoi { get; set; }

        /// <summary>
        /// 是否使用慢速发布实验字段。
        /// </summary>
        public bool sendSlow { get; set; }

        /// <summary>
        /// 服务端收到结果的时间。
        /// </summary>
        public DateTimeOffset receivedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
