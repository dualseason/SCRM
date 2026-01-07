using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 群发消息明细实体
/// </summary>
[Table("MassMessageDetails")]
public class MassMessageDetail
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属群发消息 ID
    /// </summary>
    [Column("MassMessageId")]
    public int massMessageId { get; set; }

    /// <summary>
    /// 接收者 WXID
    /// </summary>
    [Column("RecipientWxid")]
    public string recipientWxid { get; set; } = string.Empty;

    /// <summary>
    /// 发送状态
    /// </summary>
    [Column("SendStatus")]
    public int sendStatus { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    [Column("ErrorMessage")]
    public string errorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 重试次数
    /// </summary>
    [Column("RetryCount")]
    public int retryCount { get; set; }

    /// <summary>
    /// 发送时间
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


