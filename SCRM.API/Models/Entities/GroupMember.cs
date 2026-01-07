using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 群成员实体
/// </summary>
[Table("GroupMembers")]
public class GroupMember
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属群聊 ID
    /// </summary>
    [Column("GroupId")]
    public int groupId { get; set; }

    /// <summary>
    /// 成员 WXID
    /// </summary>
    [Column("MemberWxid")]
    public string memberWxid { get; set; } = string.Empty;

    /// <summary>
    /// 成员昵称
    /// </summary>
    [Column("MemberNickname")]
    public string memberNickname { get; set; } = string.Empty;

    /// <summary>
    /// 成员头像 URL
    /// </summary>
    [Column("MemberAvatar")]
    public string? memberAvatar { get; set; }

    /// <summary>
    /// 群内昵称 (名片)
    /// </summary>
    [Column("Alias")]
    public string? alias { get; set; }

    /// <summary>
    /// 成员性别 (0-未知, 1-男, 2-女)
    /// </summary>
    [Column("MemberGender")]
    public int? memberGender { get; set; }

    /// <summary>
    /// 成员地区
    /// </summary>
    [Column("Region")]
    public string? region { get; set; }

    /// <summary>
    /// 成员角色 (如：群主、管理员、普通成员)
    /// </summary>
    [Column("MemberRole")]
    public int memberRole { get; set; }

    /// <summary>
    /// 入群来源
    /// </summary>
    [Column("JoinSource")]
    public int joinSource { get; set; }

    /// <summary>
    /// 邀请者 WXID
    /// </summary>
    [Column("InviterWxid")]
    public string inviterWxid { get; set; } = string.Empty;

    /// <summary>
    /// 入群时间
    /// </summary>
    [Column("JoinTime")]
    public DateTime joinTime { get; set; }

    /// <summary>
    /// 是否被禁言 (0-否, 1-是)
    /// </summary>
    [Column("IsMuted")]
    public int isMuted { get; set; }

    /// <summary>
    /// 成员备注
    /// </summary>
    [Column("MemberRemarks")]
    public string memberRemarks { get; set; } = string.Empty;

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


