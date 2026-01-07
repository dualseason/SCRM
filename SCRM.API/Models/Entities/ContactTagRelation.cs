using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 联系人与标签的关联实体
/// </summary>
public class ContactTagRelation
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 联系人 ID
    /// </summary>
    [Column("ContactId")]
    public int contactId { get; set; }

    /// <summary>
    /// 标签 ID
    /// </summary>
    [Column("TagId")]
    public int tagId { get; set; }

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
