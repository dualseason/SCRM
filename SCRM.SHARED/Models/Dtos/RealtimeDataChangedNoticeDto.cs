using System;

namespace SCRM.SHARED.Models.Dtos
{
    /// <summary>
    /// 实时数据变更通知。
    /// <para>该 DTO 只携带刷新所需的路由键和摘要，不携带朋友圈正文、视频号正文、头像、封面、NonceId 等原始敏感字段。</para>
    /// </summary>
    public sealed class RealtimeDataChangedNoticeDto
    {
        /// <summary>
        /// 数据域，例如 moments、finder。
        /// </summary>
        public string scope { get; set; } = string.Empty;

        /// <summary>
        /// 变更类型，例如 timeline、mention、userpage、comment。
        /// </summary>
        public string changeType { get; set; } = string.Empty;

        /// <summary>
        /// 设备 UUID。前端收到通知后优先用该字段决定是否重拉数据。
        /// </summary>
        public string deviceUuid { get; set; } = string.Empty;

        /// <summary>
        /// 微信号。实时广播场景默认留空，避免按设备组广播时泄露原始账号标识。
        /// </summary>
        public string weChatId { get; set; } = string.Empty;

        /// <summary>
        /// 关联任务 ID；非任务触发时为 0。
        /// </summary>
        public long taskId { get; set; }

        /// <summary>
        /// 本次变更是否成功。
        /// </summary>
        public bool success { get; set; } = true;

        /// <summary>
        /// 本次变更涉及的数据条数。
        /// </summary>
        public int itemCount { get; set; }

        /// <summary>
        /// 不含正文和原始身份字段的安全摘要。
        /// </summary>
        public string summary { get; set; } = string.Empty;

        /// <summary>
        /// 服务端收到或生成该变更通知的时间。
        /// </summary>
        public DateTimeOffset receivedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
