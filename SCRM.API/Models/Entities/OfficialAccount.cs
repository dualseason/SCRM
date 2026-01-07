using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 公众号账号实体
/// </summary>
[Table("OfficialAccounts")]
public class OfficialAccount
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
    /// 公众号 WXID
    /// </summary>
    [Column("AccountWxid")]
    public string accountWxid { get; set; } = string.Empty;

    /// <summary>
    /// 公众号名称
    /// </summary>
    [Column("AccountName")]
    public string accountName { get; set; } = string.Empty;

    /// <summary>
    /// 公众号昵称
    /// </summary>
    [Column("AccountNickname")]
    public string accountNickname { get; set; } = string.Empty;

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
    /// 关注状态
    /// </summary>
    [Column("FollowStatus")]
    public int followStatus { get; set; }

    /// <summary>
    /// 消息总数
    /// </summary>
    [Column("MessageCount")]
    public int messageCount { get; set; }

    /// <summary>
    /// 通知计数
    /// </summary>
    [Column("NotificationCount")]
    public int notificationCount { get; set; }

    /// <summary>
    /// 关注时间
    /// </summary>
    [Column("FollowTime")]
    public DateTime followTime { get; set; }

    /// <summary>
    /// 最后一条消息时间
    /// </summary>
    [Column("LastMessageTime")]
    public DateTime lastMessageTime { get; set; }

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


