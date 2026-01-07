using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 角色与权限的关联实体类
/// </summary>
[Table("role_permissions")]
public class RolePermission
{
    #region 核心属性

    /// <summary>
    /// 角色权限关联 ID
    /// </summary>
    [Key]
    [Column("RolePermId")]
    public long rolePermId { get; set; }

    /// <summary>
    /// 角色 ID
    /// </summary>
    [Column("RoleId")]
    public long roleId { get; set; }

    /// <summary>
    /// 权限 ID
    /// </summary>
    [Column("PermissionId")]
    public long permissionId { get; set; }

    /// <summary>
    /// 是否已授予该权限
    /// </summary>
    [Column("IsGranted")]
    public bool isGranted { get; set; }

    /// <summary>
    /// 权限授予时间
    /// </summary>
    [Column("GrantedAt")]
    public DateTime grantedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("CreatedAt")]
    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    #endregion

    #region 导航属性

    /// <summary>
    /// 关联的角色实体
    /// </summary>
    public virtual Role? role { get; set; }

    /// <summary>
    /// 关联的权限实体
    /// </summary>
    public virtual Permission? permission { get; set; }

    #endregion
}


