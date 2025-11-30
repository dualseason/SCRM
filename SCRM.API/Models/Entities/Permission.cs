using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 权限表
/// </summary>
[Table("permissions")]
public class Permission
{
    /// <summary>
    /// 权限ID
    /// </summary>
    [Key]
    public long PermissionId { get; set; }

    /// <summary>
    /// 权限名称
    /// </summary>
    public string PermissionName { get; set; } = string.Empty;

    /// <summary>
    /// 权限编码
    /// </summary>
    public string PermissionCode { get; set; } = string.Empty;

    /// <summary>
    /// 权限类型：1-功能权限 2-数据权限 3-敏感权限
    /// </summary>
    public short PermissionType { get; set; }

    /// <summary>
    /// 权限描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否敏感权限
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// 是否系统权限
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
    /// 权限编码 别名 (与 PermissionCode 相同，用于兼容性)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Code
    {
        get { return PermissionCode; }
        set { PermissionCode = value; }
    }

    /// <summary>
    /// 权限所属模块
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// 排序顺序
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int SortOrder { get; set; } = 0;

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
}
