using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 消息转发实体
/// </summary>
[Table("MessageForwards")]
public class MessageForward
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 原始消息 ID
    /// </summary>
    [Column("OriginalMessageId")]
    public int originalMessageId { get; set; }

    /// <summary>
    /// 转发来源 WXID
    /// </summary>
    [Column("FromWxid")]
    public string fromWxid { get; set; } = string.Empty;

    /// <summary>
    /// 转发次数
    /// </summary>
    [Column("ForwardCount")]
    public int forwardCount { get; set; }

    /// <summary>
    /// 是否限制查看 (0-否, 1-是)
    /// </summary>
    [Column("IsLimitedViews")]
    public int isLimitedViews { get; set; }

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


