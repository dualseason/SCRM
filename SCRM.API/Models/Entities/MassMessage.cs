using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 群发消息实体
/// </summary>
[Table("MassMessages")]
public class MassMessage
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属微信账号 ID
    /// </summary>
    [Column("WechatAccountId")]
    public long wechatAccountId { get; set; }

    /// <summary>
    /// 消息标题
    /// </summary>
    [Column("MessageTitle")]
    public string messageTitle { get; set; } = string.Empty;

    /// <summary>
    /// 消息内容
    /// </summary>
    [Column("MessageContent")]
    public string messageContent { get; set; } = string.Empty;

    /// <summary>
    /// 消息类型
    /// </summary>
    [Column("MessageType")]
    public int messageType { get; set; }

    /// <summary>
    /// 目标类型
    /// </summary>
    [Column("TargetType")]
    public int targetType { get; set; }

    /// <summary>
    /// 总接收人数
    /// </summary>
    [Column("TotalRecipients")]
    public int totalRecipients { get; set; }

    /// <summary>
    /// 成功发送人数
    /// </summary>
    [Column("SuccessSentCount")]
    public int successSentCount { get; set; }

    /// <summary>
    /// 失败发送人数
    /// </summary>
    [Column("FailedSentCount")]
    public int failedSentCount { get; set; }

    /// <summary>
    /// 发送状态
    /// </summary>
    [Column("SendStatus")]
    public int sendStatus { get; set; }

    /// <summary>
    /// 计划发送时间
    /// </summary>
    [Column("ScheduledTime")]
    public DateTime scheduledTime { get; set; }

    /// <summary>
    /// 实际发送时间
    /// </summary>
    [Column("SentTime")]
    public DateTime sentTime { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("CreatedAt")]
    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    [Column("UpdatedAt")]
    public DateTime updatedAt { get; set; } = DateTime.UtcNow;
}


