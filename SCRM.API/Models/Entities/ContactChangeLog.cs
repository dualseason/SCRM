using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 联系人变更日志实体
/// </summary>
public class ContactChangeLog
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 对应的联系人 ID
    /// </summary>
    [Column("ContactId")]
    public int contactId { get; set; }

    /// <summary>
    /// 变更类型 (如：Update, Create, Delete)
    /// </summary>
    [Column("ChangeType")]
    public string changeType { get; set; } = string.Empty;

    /// <summary>
    /// 变更前的值
    /// </summary>
    [Column("OldValue")]
    public string oldValue { get; set; } = string.Empty;

    /// <summary>
    /// 变更后的值
    /// </summary>
    [Column("NewValue")]
    public string newValue { get; set; } = string.Empty;

    /// <summary>
    /// 变更的字段名称
    /// </summary>
    [Column("ChangedField")]
    public string changedField { get; set; } = string.Empty;

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
