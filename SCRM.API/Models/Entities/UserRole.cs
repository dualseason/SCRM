using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 用户角色关联实体类
/// </summary>
[Table("user_roles")]
public class UserRole
{
    #region 核心属性

    /// <summary>
    /// 用户角色关联 ID
    /// </summary>
    [Key]
    [Column("UserRoleId")]
    public long userRoleId { get; set; }

    /// <summary>
    /// 用户账号 ID
    /// </summary>
    [Column("AccountId")]
    public long accountId { get; set; }

    /// <summary>
    /// 用户 ID 别名 (与 accountId 相同，用于兼容性)
    /// </summary>
    [NotMapped]
    public long userId
    {
        get { return accountId; }
        set { accountId = value; }
    }

    /// <summary>
    /// 角色 ID
    /// </summary>
    [Column("RoleId")]
    public long roleId { get; set; }

    /// <summary>
    /// 分配该角色的管理者账号 ID
    /// </summary>
    [Column("AssignedBy")]
    public long? assignedBy { get; set; }

    /// <summary>
    /// 分配时间
    /// </summary>
    [Column("AssignedAt")]
    public DateTime assignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 角色权限过期时间
    /// </summary>
    [Column("ExpiresAt")]
    public DateTime? expiresAt { get; set; }

    /// <summary>
    /// 此关联是否有效
    /// </summary>
    [Column("IsActive")]
    public bool isActive { get; set; } = true;

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

    #endregion

    #region 导航属性

    /// <summary>
    /// 关联的用户微信账号
    /// </summary>
    public virtual WechatAccount? account { get; set; }

    /// <summary>
    /// 用户账号别名 (与 account 相同，用于兼容性)
    /// </summary>
    [NotMapped]
    public virtual WechatAccount? user
    {
        get { return account; }
        set { account = value; }
    }

    /// <summary>
    /// 关联的角色实体
    /// </summary>
    public virtual Role? role { get; set; }

    /// <summary>
    /// 分配者所属的微信账号实体
    /// </summary>
    public virtual WechatAccount? assignedByAccount { get; set; }

    #endregion
}


