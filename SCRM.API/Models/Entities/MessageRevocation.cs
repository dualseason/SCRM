using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 消息撤回实体
/// </summary>
[Table("MessageRevocations")]
public class MessageRevocation
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 被撤回的消息 ID
    /// </summary>
    [Column("MessageId")]
    public int messageId { get; set; }

    /// <summary>
    /// 撤回者 WXID
    /// </summary>
    [Column("RevokerWxid")]
    public string revokerWxid { get; set; } = string.Empty;

    /// <summary>
    /// 撤回原因
    /// </summary>
    [Column("RevocationReason")]
    public string revocationReason { get; set; } = string.Empty;

    /// <summary>
    /// 撤回时间
    /// </summary>
    [Column("RevocationTime")]
    public DateTime revocationTime { get; set; }

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


