using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 消息转发明细实体
/// </summary>
[Table("MessageForwardDetails")]
public class MessageForwardDetail
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属消息转发 ID
    /// </summary>
    [Column("MessageForwardId")]
    public int messageForwardId { get; set; }

    /// <summary>
    /// 转发目标 WXID
    /// </summary>
    [Column("ToWxid")]
    public string toWxid { get; set; } = string.Empty;

    /// <summary>
    /// 转发状态
    /// </summary>
    [Column("ForwardStatus")]
    public int forwardStatus { get; set; }

    /// <summary>
    /// 转发时间
    /// </summary>
    [Column("ForwardTime")]
    public DateTime forwardTime { get; set; }

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


