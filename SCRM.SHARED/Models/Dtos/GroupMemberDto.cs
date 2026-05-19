namespace SCRM.SHARED.Models.Dtos;

/// <summary>
/// 群成员展示数据。
/// <para>用于 Web 端展示群聊消息发送者昵称，避免直接依赖 API 层实体。</para>
/// </summary>
public class GroupMemberDto
{
    /// <summary>
    /// 成员 wxid。
    /// </summary>
    public string memberWxid { get; set; } = string.Empty;

    /// <summary>
    /// 成员昵称。
    /// </summary>
    public string memberNickname { get; set; } = string.Empty;

    /// <summary>
    /// 成员头像。
    /// </summary>
    public string memberAvatar { get; set; } = string.Empty;

    /// <summary>
    /// 群内昵称 / 名片。
    /// </summary>
    public string alias { get; set; } = string.Empty;

    /// <summary>
    /// 成员备注。
    /// </summary>
    public string memberRemarks { get; set; } = string.Empty;

    /// <summary>
    /// 成员角色。
    /// </summary>
    public int memberRole { get; set; }
}
