using System;
using System.Collections.Generic;

namespace SCRM.SHARED.Models.Dtos
{
    /// <summary>
    /// 群发助手历史记录。
    /// <para>用于把 Android 上报的 GroupSendHistoryPushNotice 落库结果展示到 Web 群发页面。</para>
    /// </summary>
    public class MassSendHistoryDto
    {
        /// <summary>
        /// 群发历史自增 ID。
        /// </summary>
        public int id { get; set; }

        /// <summary>
        /// 所属微信账号 wxid。
        /// </summary>
        public string ownerWxid { get; set; } = string.Empty;

        /// <summary>
        /// 所属微信账号数值键，保持与 MassMessages 表一致。
        /// </summary>
        public long wechatAccountId { get; set; }

        /// <summary>
        /// 消息标题；群发助手历史中通常保存目标列表摘要。
        /// </summary>
        public string messageTitle { get; set; } = string.Empty;

        /// <summary>
        /// 消息内容。
        /// </summary>
        public string messageContent { get; set; } = string.Empty;

        /// <summary>
        /// 消息类型。
        /// </summary>
        public int messageType { get; set; }

        /// <summary>
        /// 目标类型。
        /// </summary>
        public int targetType { get; set; }

        /// <summary>
        /// 总目标数。
        /// </summary>
        public int totalRecipients { get; set; }

        /// <summary>
        /// 成功数。
        /// </summary>
        public int successSentCount { get; set; }

        /// <summary>
        /// 失败数。
        /// </summary>
        public int failedSentCount { get; set; }

        /// <summary>
        /// 发送状态。
        /// </summary>
        public int sendStatus { get; set; }

        /// <summary>
        /// 计划发送时间。
        /// </summary>
        public DateTime scheduledTime { get; set; }

        /// <summary>
        /// 实际发送时间。
        /// </summary>
        public DateTime sentTime { get; set; }

        /// <summary>
        /// 创建时间。
        /// </summary>
        public DateTime createdAt { get; set; }

        /// <summary>
        /// 更新时间。
        /// </summary>
        public DateTime updatedAt { get; set; }

        /// <summary>
        /// 接收人明细。
        /// </summary>
        public List<MassSendHistoryDetailDto> details { get; set; } = new();
    }

    /// <summary>
    /// 群发助手历史接收人明细。
    /// </summary>
    public class MassSendHistoryDetailDto
    {
        /// <summary>
        /// 明细自增 ID。
        /// </summary>
        public int id { get; set; }

        /// <summary>
        /// 接收人 wxid。
        /// </summary>
        public string recipientWxid { get; set; } = string.Empty;

        /// <summary>
        /// 接收人展示名，优先联系人备注，其次昵称，最后 wxid。
        /// </summary>
        public string recipientDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 发送状态。
        /// </summary>
        public int sendStatus { get; set; }

        /// <summary>
        /// 错误信息。
        /// </summary>
        public string errorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 重试次数。
        /// </summary>
        public int retryCount { get; set; }

        /// <summary>
        /// 发送时间。
        /// </summary>
        public DateTime sentTime { get; set; }
    }
}
