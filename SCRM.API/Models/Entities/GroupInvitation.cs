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
    /// 所属微信账号 wxid。
    /// <para>62203 群邀请协议直接携带 WeChatId，旧 GroupId 不能表达多微信账号归属。</para>
    /// </summary>
    [Column("WeChatId")]
    public string weChatId { get; set; } = string.Empty;

    /// <summary>
    /// 群聊 wxid。
    /// </summary>
    [Column("ChatRoomId")]
    public string chatRoomId { get; set; } = string.Empty;

    /// <summary>
    /// 邀请者 WXID
    /// </summary>
    [Column("InviterWxid")]
    public string inviterWxid { get; set; } = string.Empty;

    /// <summary>
    /// 邀请人展示名。
    /// </summary>
    [Column("InviteName")]
    public string inviteName { get; set; } = string.Empty;

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
    /// 邀请原因 / 验证文案。
    /// </summary>
    [Column("Reason")]
    public string reason { get; set; } = string.Empty;

    /// <summary>
    /// 62203 群邀请确认主链使用的消息 ID。
    /// </summary>
    [Column("MsgId")]
    public long msgId { get; set; }

    /// <summary>
    /// 旧链路兼容字段；多数 62203 Notice 不携带该值。
    /// </summary>
    [Column("MsgSvrId")]
    public long msgSvrId { get; set; }

    /// <summary>
    /// 微信侧更新时间戳，保留原始秒/毫秒值。
    /// </summary>
    [Column("UpdateTime")]
    public long updateTime { get; set; }

    /// <summary>
    /// 列表拉取任务 ID；实时推送时为 0。
    /// </summary>
    [Column("TaskId")]
    public long taskId { get; set; }

    /// <summary>
    /// 被邀请成员 JSON。
    /// </summary>
    [Column("InvitedJson", TypeName = "jsonb")]
    public string invitedJson { get; set; } = "[]";

    /// <summary>
    /// 数据来源：Push / List / CompatPush。
    /// </summary>
    [Column("Source")]
    public string source { get; set; } = string.Empty;

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


