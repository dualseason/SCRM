using System;
using System.Collections.Generic;

namespace SCRM.SHARED.Models.Dtos
{
    /// <summary>
    /// 视频号历史导出结果。
    /// <para>由服务端完成权限校验、字段脱敏和导出审计后返回给 UI；不包含数据库 raw payload。</para>
    /// </summary>
    public sealed class FinderHistoryExportDto
    {
        /// <summary>
        /// 是否允许本次导出。
        /// </summary>
        public bool success { get; set; }

        /// <summary>
        /// 导出结果提示。
        /// </summary>
        public string message { get; set; } = string.Empty;

        /// <summary>
        /// 设备 UUID。
        /// </summary>
        public string deviceUuid { get; set; } = string.Empty;

        /// <summary>
        /// 每类历史请求条数。
        /// </summary>
        public int countPerType { get; set; }

        /// <summary>
        /// 服务端导出时间。
        /// </summary>
        public DateTimeOffset exportedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// 导出所需权限。
        /// </summary>
        public string exportPermission { get; set; } = string.Empty;

        /// <summary>
        /// 导出字段策略版本。
        /// </summary>
        public string fieldPolicyVersion { get; set; } = "finder-export-v1";

        /// <summary>
        /// 是否已经按用户权限脱敏。
        /// </summary>
        public bool masked { get; set; } = true;

        /// <summary>
        /// 是否包含数据库 raw payloadJson。
        /// </summary>
        public bool rawPayloadIncluded { get; set; }

        /// <summary>
        /// 成功状态筛选条件。
        /// </summary>
        public bool? successFilter { get; set; }

        /// <summary>
        /// 任务 ID 筛选条件。
        /// </summary>
        public long? taskIdFilter { get; set; }

        /// <summary>
        /// 接收时间起点筛选条件。
        /// </summary>
        public DateTimeOffset? receivedFrom { get; set; }

        /// <summary>
        /// 接收时间终点筛选条件。
        /// </summary>
        public DateTimeOffset? receivedTo { get; set; }

        /// <summary>
        /// 提及历史。
        /// </summary>
        public List<FinderMentionNoticeDto> mentionHistory { get; set; } = new();

        /// <summary>
        /// 用户页历史。
        /// </summary>
        public List<FinderUserPageDto> userPageHistory { get; set; } = new();

        /// <summary>
        /// 评论历史。
        /// </summary>
        public List<FinderCommentListDto> commentHistory { get; set; } = new();
    }
}
