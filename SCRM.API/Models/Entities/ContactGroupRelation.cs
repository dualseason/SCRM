using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 联系人与分组的关联实体
/// </summary>
public class ContactGroupRelation
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
    /// 分组 ID
    /// </summary>
    [Column("GroupId")]
    public int groupId { get; set; }

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
