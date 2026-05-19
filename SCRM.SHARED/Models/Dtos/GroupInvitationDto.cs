namespace SCRM.SHARED.Models.Dtos;

/// <summary>
/// 群邀请成员展示数据。
/// <para>对应 62203 协议中的 InvitedMemMessage。</para>
/// </summary>
public class GroupInvitationMemberDto
{
    /// <summary>成员 wxid。</summary>
    public string userName { get; set; } = string.Empty;

    /// <summary>成员昵称。</summary>
    public string nickName { get; set; } = string.Empty;

    /// <summary>成员头像。</summary>
    public string avatar { get; set; } = string.Empty;
}

/// <summary>
/// 群邀请展示数据。
/// <para>用于 Web 端展示 ChatRoomInvitePushNotice / ChatRoomInviteListNotice 的持久化结果。</para>
/// </summary>
public class GroupInvitationDto
{
    public int id { get; set; }
    public string weChatId { get; set; } = string.Empty;
    public string chatRoomId { get; set; } = string.Empty;
    public string inviter { get; set; } = string.Empty;
    public string inviteName { get; set; } = string.Empty;
    public string reason { get; set; } = string.Empty;
    public long msgId { get; set; }
    public long msgSvrId { get; set; }
    public long updateTime { get; set; }
    public long taskId { get; set; }
    public int status { get; set; }
    public string source { get; set; } = string.Empty;
    public DateTime invitationTime { get; set; }
    public DateTime updatedAt { get; set; }
    public List<GroupInvitationMemberDto> invited { get; set; } = new();
}
