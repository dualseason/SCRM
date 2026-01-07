using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 群聊实体
/// </summary>
[Table("Groups")]
public class Group
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

    /// <summary>
    /// 群聊 WXID
    /// </summary>
    [Column("group_wxid")]
    public string groupWxid { get; set; } = string.Empty;

    /// <summary>
    /// 群聊名称
    /// </summary>
    [Column("group_name")]
    public string groupName { get; set; } = string.Empty;

    /// <summary>
    /// 群公告
    /// </summary>
    [Column("group_notice")]
    public string groupNotice { get; set; } = string.Empty;

    /// <summary>
    /// 群主 WXID
    /// </summary>
    [Column("owner_wxid")]
    public string ownerWxid { get; set; } = string.Empty;

    /// <summary>
    /// 成员数量
    /// </summary>
    [Column("member_count")]
    public int memberCount { get; set; }

    /// <summary>
    /// 群头像 URL
    /// </summary>
    [Column("group_avatar")]
    public string groupAvatar { get; set; } = string.Empty;

    /// <summary>
    /// 群描述
    /// </summary>
    [Column("group_description")]
    public string groupDescription { get; set; } = string.Empty;

    /// <summary>
    /// 是否开启免打扰 (0-否, 1-是)
    /// </summary>
    [Column("is_muted")]
    public int isMuted { get; set; }

    /// <summary>
    /// 是否置顶 (0-否, 1-是)
    /// </summary>
    [Column("is_pinned")]
    public int isPinned { get; set; }

    /// <summary>
    /// 群状态
    /// </summary>
    [Column("group_status")]
    public int groupStatus { get; set; }

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


