using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 权限实体类
/// </summary>
[Table("permissions")]
public class Permission
{
    #region 核心属性

    /// <summary>
    /// 权限ID
    /// </summary>
    [Key]
    public long permissionId { get; set; }

    /// <summary>
    /// 权限名称
    /// </summary>
    public string permissionName { get; set; } = string.Empty;

    /// <summary>
    /// 权限编码
    /// </summary>
    public string permissionCode { get; set; } = string.Empty;

    /// <summary>
    /// 权限类型：1-功能权限 2-数据权限 3-敏感权限
    /// </summary>
    public short permissionType { get; set; }

    /// <summary>
    /// 权限描述
    /// </summary>
    public string? description { get; set; }

    /// <summary>
    /// 是否为敏感权限
    /// </summary>
    public bool isSensitive { get; set; }

    /// <summary>
    /// 是否为系统内置权限
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

    /// <summary>
    /// 权限编码别名 (与 permissionCode 相同，用于兼容性)
    /// </summary>
    [NotMapped]
    public string code
    {
        get { return permissionCode; }
        set { permissionCode = value; }
    }

    #endregion

    #region 元数据

    /// <summary>
    /// 权限所属模块名称
    /// </summary>
    [NotMapped]
    public string module { get; set; } = string.Empty;

    /// <summary>
    /// 显示排序权重
    /// </summary>
    [NotMapped]
    public int sortOrder { get; set; } = 0;

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
    /// 关联的角色权限关系列表
    /// </summary>
    public virtual ICollection<RolePermission> rolePermissions { get; set; } = new List<RolePermission>();

    #endregion
}

