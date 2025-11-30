namespace SCRM.API.Models.Entities;

/// <summary>
/// 缇よ亰琛?
/// </summary>
public class Group
{
    public int Id { get; set; }
    public int WechatAccountId { get; set; }
    public string GroupWxid { get; set; }
    public string GroupName { get; set; }
    public string GroupNotice { get; set; }
    public string OwnerWxid { get; set; }
    public int MemberCount { get; set; }
    public string GroupAvatar { get; set; }
    public string GroupDescription { get; set; }
    public int IsMuted { get; set; }
    public int IsPinned { get; set; }
    public int GroupStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

