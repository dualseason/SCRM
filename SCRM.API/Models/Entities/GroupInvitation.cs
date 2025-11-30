namespace SCRM.API.Models.Entities;

/// <summary>
/// 缇ら個璇疯〃
/// </summary>
public class GroupInvitation
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string InviterWxid { get; set; }
    public string InviteeWxid { get; set; }
    public int InvitationStatus { get; set; }
    public string InvitationMessage { get; set; }
    public DateTime InvitationTime { get; set; }
    public DateTime ResponseTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

