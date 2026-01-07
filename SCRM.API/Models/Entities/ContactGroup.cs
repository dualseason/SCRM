using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 联系人分组实体
/// </summary>
public class ContactGroup
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
    /// 分组名称
    /// </summary>
    [Column("GroupName")]
    public string groupName { get; set; } = string.Empty;

    /// <summary>
    /// 排序权重
    /// </summary>
    [Column("GroupOrder")]
    public int groupOrder { get; set; }

    /// <summary>
    /// 分组描述
    /// </summary>
    [Column("GroupDescription")]
    public string groupDescription { get; set; } = string.Empty;

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

    /// <summary>
    /// 是否已删除
    /// </summary>
    [Column("IsDeleted")]
    public bool isDeleted { get; set; }
}
