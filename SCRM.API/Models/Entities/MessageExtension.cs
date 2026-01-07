using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 消息扩展信息实体
/// </summary>
[Table("MessageExtensions")]
public class MessageExtension
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属消息 ID
    /// </summary>
    [Column("MessageId")]
    public int messageId { get; set; }

    /// <summary>
    /// 扩展键
    /// </summary>
    [Column("ExtensionKey")]
    public string extensionKey { get; set; } = string.Empty;

    /// <summary>
    /// 扩展值
    /// </summary>
    [Column("ExtensionValue")]
    public string extensionValue { get; set; } = string.Empty;

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


