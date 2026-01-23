using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 联系人信息实体
/// </summary>
[Index(nameof(wechatAccountId), nameof(wxid), IsUnique = true)]
public class Contact
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("id")]
    public int id { get; set; }

    /// <summary>
    /// 所属微信账号 ID
    /// </summary>
    [Column("wechat_account_id")]
    public long wechatAccountId { get; set; }

    [ForeignKey("wechatAccountId")]
    public virtual WechatAccount? Account { get; set; }

    /// <summary>
    /// 微信 WXID
    /// </summary>
    [Column("wxid")]
    public string wxid { get; set; } = string.Empty;

    /// <summary>
    /// 所属微信账号 WXID (冗余字段，方便查询)
    /// </summary>
    [Column("owner_wxid")]
    public string ownerWxid { get; set; } = string.Empty;

    /// <summary>
    /// 好友微信号 (Alias/FriendNo)
    /// </summary>
    [Column("friend_no")]
    public string friendNo { get; set; } = string.Empty;

    /// <summary>
    /// 来源扩展信息
    /// </summary>
    [Column("source_ext")]
    public string sourceExt { get; set; } = string.Empty;

    /// <summary>
    /// 微信昵称
    /// </summary>
    [Column("nickname")]
    public string nickname { get; set; } = string.Empty;

    /// <summary>
    /// 备注名
    /// </summary>
    [Column("remarks")]
    public string remarks { get; set; } = string.Empty;

    /// <summary>
    /// 头像 URL
    /// </summary>
    [Column("avatar")]
    public string avatar { get; set; } = string.Empty;

    /// <summary>
    /// 性别 (0-未知, 1-男, 2-女)
    /// </summary>
    [Column("gender")]
    public int gender { get; set; }

    /// <summary>
    /// 个性签名
    /// </summary>
    [Column("signature")]
    public string signature { get; set; } = string.Empty;

    /// <summary>
    /// 手机号
    /// </summary>
    [Column("phone")]
    public string phone { get; set; } = string.Empty;

    /// <summary>
    /// 电子邮箱
    /// </summary>
    [Column("email")]
    public string email { get; set; } = string.Empty;

    /// <summary>
    /// 国家
    /// </summary>
    [Column("country")]
    public string country { get; set; } = string.Empty;

    /// <summary>
    /// 省份
    /// </summary>
    [Column("province")]
    public string province { get; set; } = string.Empty;

    /// <summary>
    /// 城市
    /// </summary>
    [Column("city")]
    public string city { get; set; } = string.Empty;

    /// <summary>
    /// 描述信息
    /// </summary>
    [Column("description")]
    public string description { get; set; } = string.Empty;

    /// <summary>
    /// 来源
    /// </summary>
    [Column("source")]
    public string source { get; set; } = string.Empty;

    /// <summary>
    /// 标签 ID 列表 (逗号分隔)
    /// </summary>
    [Column("label_ids")]
    public string labelIds { get; set; } = string.Empty;

    /// <summary>
    /// 联系人类型
    /// </summary>
    [Column("contact_type")]
    public int contactType { get; set; }

    /// <summary>
    /// 是否为好友
    /// </summary>
    [Column("is_friend")]
    public int isFriend { get; set; }

    /// <summary>
    /// 是否被拉黑
    /// </summary>
    [Column("is_blocked")]
    public int isBlocked { get; set; }

    /// <summary>
    /// 是否星标
    /// </summary>
    [Column("is_starred")]
    public int isStarred { get; set; }

    /// <summary>
    /// 最后互动时间
    /// </summary>
    [Column("last_interaction_time")]
    public DateTime lastInteractionTime { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("created_at")]
    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    [Column("updated_at")]
    public DateTime updatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 是否已删除
    /// </summary>
    [Column("is_deleted")]
    public bool isDeleted { get; set; }
}
