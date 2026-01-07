using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 小程序账号实体
/// </summary>
[Table("MiniprogramAccounts")]
public class MiniprogramAccount
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
    /// 小程序 AppId
    /// </summary>
    [Column("AppId")]
    public string appId { get; set; } = string.Empty;

    /// <summary>
    /// 小程序名称
    /// </summary>
    [Column("AppName")]
    public string appName { get; set; } = string.Empty;

    /// <summary>
    /// 头像 URL
    /// </summary>
    [Column("Avatar")]
    public string avatar { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    [Column("Description")]
    public string description { get; set; } = string.Empty;

    /// <summary>
    /// 账号类型
    /// </summary>
    [Column("AccountType")]
    public int accountType { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    [Column("Status")]
    public int status { get; set; }

    /// <summary>
    /// 访问次数
    /// </summary>
    [Column("AccessCount")]
    public int accessCount { get; set; }

    /// <summary>
    /// 最后访问时间
    /// </summary>
    [Column("LastAccessTime")]
    public DateTime lastAccessTime { get; set; }

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


