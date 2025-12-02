using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 用户实体 - WechatAccount的别名，用于兼容性
/// </summary>
[System.ComponentModel.DataAnnotations.Schema.NotMapped]
public class User
{
    /// <summary>
    /// 用户ID (映射到AccountId)
    /// </summary>
    public long Id
    {
        get { return WechatAccount?.AccountId ?? 0; }
        set
        {
            if (WechatAccount != null)
                WechatAccount.AccountId = value;
        }
    }

    /// <summary>
    /// 用户ID别名 (与Id相同，用于兼容性)
    /// </summary>
    public long UserId
    {
        get { return Id; }
        set { Id = value; }
    }

    /// <summary>
    /// 用户名 (映射到Nickname)
    /// </summary>
    public string UserName
    {
        get { return WechatAccount?.Nickname ?? string.Empty; }
        set
        {
            if (WechatAccount != null)
                WechatAccount.Nickname = value;
        }
    }

    /// <summary>
    /// 邮箱 (映射到MobilePhone)
    /// </summary>
    public string? Email
    {
        get { return WechatAccount?.MobilePhone; }
        set
        {
            if (WechatAccount != null)
                WechatAccount.MobilePhone = value;
        }
    }

    /// <summary>
    /// 名字 (为空，微信账号没有此概念)
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// 姓氏 (为空，微信账号没有此概念)
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// 是否有效 (映射到WechatAccount的IsActive)
    /// </summary>
    public bool IsActive
    {
        get { return WechatAccount?.IsActive ?? false; }
        set
        {
            if (WechatAccount != null)
                WechatAccount.IsActive = value;
        }
    }

    /// <summary>
    /// 创建时间 (映射到CreatedAt)
    /// </summary>
    public DateTime? CreatedAt
    {
        get { return WechatAccount?.CreatedAt; }
        set
        {
            if (WechatAccount != null)
                WechatAccount.CreatedAt = value;
        }
    }

    /// <summary>
    /// 更新时间 (映射到UpdatedAt)
    /// </summary>
    public DateTime? UpdatedAt
    {
        get { return WechatAccount?.UpdatedAt; }
        set
        {
            if (WechatAccount != null)
                WechatAccount.UpdatedAt = value;
        }
    }

    /// <summary>
    /// 最后登录时间 (映射到LastOnlineAt)
    /// </summary>
    public DateTime? LastLoginAt
    {
        get { return WechatAccount?.LastOnlineAt; }
        set
        {
            if (WechatAccount != null)
                WechatAccount.LastOnlineAt = value;
        }
    }

    /// <summary>
    /// 微信账号实体
    /// </summary>
    public virtual WechatAccount? WechatAccount { get; set; }

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public User() { }

    /// <summary>
    /// 从微信账号构造用户
    /// </summary>
    /// <param name="wechatAccount">微信账号</param>
    public User(WechatAccount wechatAccount)
    {
        WechatAccount = wechatAccount;
    }

    /// <summary>
    /// 从用户ID构造用户
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="userName">用户名</param>
    /// <param name="email">邮箱</param>
    public User(long userId, string userName, string? email = null)
    {
        WechatAccount = new WechatAccount
        {
            AccountId = userId,
            Nickname = userName,
            MobilePhone = email
        };
    }

    /// <summary>
    /// 隐式转换操作符：从WechatAccount转换为User
    /// </summary>
    /// <param name="wechatAccount">微信账号</param>
    public static implicit operator User(WechatAccount? wechatAccount)
    {
        return new User(wechatAccount ?? new WechatAccount());
    }

    /// <summary>
    /// 隐式转换操作符：从User转换为WechatAccount
    /// </summary>
    /// <param name="user">用户</param>
    public static implicit operator WechatAccount?(User user)
    {
        return user?.WechatAccount;
    }
}