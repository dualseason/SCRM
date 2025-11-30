namespace SCRM.API.Models.Entities;

/// <summary>
/// 角色权限关系表
/// </summary>
public class RolePermission
{
    /// <summary>
    /// 角色权限关系ID
    /// </summary>
    public long RolePermId { get; set; }

    /// <summary>
    /// 角色ID
    /// </summary>
    public long RoleId { get; set; }

    /// <summary>
    /// 权限ID
    /// </summary>
    public long PermissionId { get; set; }

    /// <summary>
    /// 是否授予
    /// </summary>
    public bool IsGranted { get; set; }

    /// <summary>
    /// 授予时间
    /// </summary>
    public DateTime GrantedAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 导航属性：角色
    /// </summary>
    public virtual Role? Role { get; set; }

    /// <summary>
    /// 导航属性：权限
    /// </summary>
    public virtual Permission? Permission { get; set; }
}
