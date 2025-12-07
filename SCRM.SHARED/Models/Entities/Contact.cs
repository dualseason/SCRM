namespace SCRM.API.Models.Entities;

/// <summary>
/// 联系人表
/// </summary>
public class Contact
{
    public int Id { get; set; }
    public int WechatAccountId { get; set; }
    public string Wxid { get; set; }
    public string Nickname { get; set; }
    public string Remarks { get; set; }
    public string Avatar { get; set; }
    public int Gender { get; set; }
    public string Signature { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Country { get; set; }
    public string Province { get; set; }
    public string City { get; set; }
    public int ContactType { get; set; }
    public int IsFriend { get; set; }
    public int IsBlocked { get; set; }
    public int IsStarred { get; set; }
    public DateTime LastInteractionTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
