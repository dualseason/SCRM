using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 小程序消息记录实体
/// </summary>
[Table("MiniprogramMessages")]
public class MiniprogramMessage
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属小程序 ID
    /// </summary>
    [Column("MiniprogramAccountId")]
    public int miniprogramAccountId { get; set; }

    /// <summary>
    /// 微信消息 ID
    /// </summary>
    [Column("MessageId")]
    public string messageId { get; set; } = string.Empty;

    /// <summary>
    /// 消息类型
    /// </summary>
    [Column("MessageType")]
    public int messageType { get; set; }

    /// <summary>
    /// 消息内容
    /// </summary>
    [Column("MessageContent")]
    public string messageContent { get; set; } = string.Empty;

    /// <summary>
    /// 发送者 WXID
    /// </summary>
    [Column("SenderWxid")]
    public string senderWxid { get; set; } = string.Empty;

    /// <summary>
    /// 是否已读 (0-否, 1-是)
    /// </summary>
    [Column("IsRead")]
    public int isRead { get; set; }

    /// <summary>
    /// 消息时间
    /// </summary>
    [Column("MessageTime")]
    public DateTime messageTime { get; set; }

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


