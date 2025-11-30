namespace SCRM.API.Models.Entities;

/// <summary>
/// 缇ゆ垚鍛樿〃
/// </summary>
public class GroupMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string MemberWxid { get; set; }
    public string MemberNickname { get; set; }
    public int MemberRole { get; set; }
    public int JoinSource { get; set; }
    public string InviterWxid { get; set; }
    public DateTime JoinTime { get; set; }
    public int IsMuted { get; set; }
    public string MemberRemarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

