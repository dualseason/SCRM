using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 群邀请实体
/// </summary>
[Table("GroupInvitations")]
public class GroupInvitation
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
    /// 邀请者 WXID
    /// </summary>
    [Column("InviterWxid")]
    public string inviterWxid { get; set; } = string.Empty;

    /// <summary>
    /// 被邀请者 WXID
    /// </summary>
    [Column("InviteeWxid")]
    public string inviteeWxid { get; set; } = string.Empty;

    /// <summary>
    /// 邀请状态 (如：待确认、已同意、已拒绝)
    /// </summary>
    [Column("InvitationStatus")]
    public int invitationStatus { get; set; }

    /// <summary>
    /// 邀请附言
    /// </summary>
    [Column("InvitationMessage")]
    public string invitationMessage { get; set; } = string.Empty;

    /// <summary>
    /// 邀请时间
    /// </summary>
    [Column("InvitationTime")]
    public DateTime invitationTime { get; set; }

    /// <summary>
    /// 响应时间 (同意/拒绝时间)
    /// </summary>
    [Column("ResponseTime")]
    public DateTime responseTime { get; set; }

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
}


