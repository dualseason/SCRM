using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 角色表
/// </summary>
[Table("roles")]
public class Role
{
    /// <summary>
    /// 角色ID
    /// </summary>
    [Key]
    public long RoleId { get; set; }

    /// <summary>
    /// 角色名称
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// 角色名称 别名 (与 RoleName 相同，用于兼容性)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Name
    {
        get { return RoleName; }
        set { RoleName = value; }
    }

    /// <summary>
    /// 角色等级：1-平台级 2-BOSS级 3-组长级 4-单账户级 5-临时金主级 6-临时认证级
    /// </summary>
    public short RoleLevel { get; set; }

    /// <summary>
    /// 角色描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否系统角色
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// 是否删除
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 是否有效（IsActive 属性，值为 !IsDeleted）
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsActive
    {
        get { return !IsDeleted; }
        set { IsDeleted = !value; }
    }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 导航属性：角色权限关系
    /// </summary>
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    /// <summary>
    /// 导航属性：用户角色关系
    /// </summary>
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
