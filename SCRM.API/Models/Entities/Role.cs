using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 角色实体类
/// </summary>
[Table("roles")]
public class Role
{
    #region 核心属性

    /// <summary>
    /// 角色ID
    /// </summary>
    [Key]
    public long roleId { get; set; }

    /// <summary>
    /// 角色名称
    /// </summary>
    public string roleName { get; set; } = string.Empty;

    /// <summary>
    /// 角色名称别名 (与 roleName 相同，用于兼容性)
    /// </summary>
    [NotMapped]
    public string name
    {
        get { return roleName; }
        set { roleName = value; }
    }

    /// <summary>
    /// 角色等级：1-平台级 2-BOSS级 3-组长级 4-单账户级 5-临时金主级 6-临时认证级
    /// </summary>
    public short roleLevel { get; set; }

    /// <summary>
    /// 角色描述
    /// </summary>
    public string? description { get; set; }

    /// <summary>
    /// 是否系统内置角色
    /// </summary>
    public bool isSystem { get; set; }

    /// <summary>
    /// 是否已逻辑删除
    /// </summary>
    public bool isDeleted { get; set; }

    /// <summary>
    /// 是否有效 (isActive 属性，其值为 !isDeleted)
    /// </summary>
    [NotMapped]
    public bool isActive
    {
        get { return !isDeleted; }
        set { isDeleted = !value; }
    }

    #endregion

    #region 时间戳

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime createdAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime updatedAt { get; set; }

    #endregion

    #region 导航属性

    /// <summary>
    /// 角色关联的权限列表
    /// </summary>
    public virtual ICollection<RolePermission> rolePermissions { get; set; } = new List<RolePermission>();

    /// <summary>
    /// 拥有该角色的用户列表
    /// </summary>
    public virtual ICollection<UserRole> userRoles { get; set; } = new List<UserRole>();

    #endregion
}

