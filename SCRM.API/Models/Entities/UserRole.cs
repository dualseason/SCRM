using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 用户角色表
/// </summary>
[Table("user_roles")]
public class UserRole
{
    /// <summary>
    /// 用户角色ID
    /// </summary>
    public long UserRoleId { get; set; }

    /// <summary>
    /// 用户账号ID
    /// </summary>
    public long AccountId { get; set; }

    /// <summary>
    /// 用户ID 别名 (与 AccountId 相同，用于兼容性)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long UserId
    {
        get { return AccountId; }
        set { AccountId = value; }
    }

    /// <summary>
    /// 角色ID
    /// </summary>
    public long RoleId { get; set; }

    /// <summary>
    /// 分配者账号ID
    /// </summary>
    public long? AssignedBy { get; set; }

    /// <summary>
    /// 分配时间
    /// </summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 导航属性：用户账号
    /// </summary>
    public virtual WechatAccount? Account { get; set; }

    /// <summary>
    /// 导航属性：用户 别名 (与 Account 相同，用于兼容性)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public virtual WechatAccount? User
    {
        get { return Account; }
        set { Account = value; }
    }

    /// <summary>
    /// 导航属性：角色
    /// </summary>
    public virtual Role? Role { get; set; }

    /// <summary>
    /// 导航属性：分配者账号
    /// </summary>
    public virtual WechatAccount? AssignedByAccount { get; set; }
}
