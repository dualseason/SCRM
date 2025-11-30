namespace SCRM.API.Models.Entities;

/// <summary>
/// 鍏紬鍙疯处鍙疯〃
/// </summary>
public class OfficialAccount
{
    public int Id { get; set; }
    public int WechatAccountId { get; set; }
    public string AccountWxid { get; set; }
    public string AccountName { get; set; }
    public string AccountNickname { get; set; }
    public string Avatar { get; set; }
    public string Description { get; set; }
    public int AccountType { get; set; }
    public int FollowStatus { get; set; }
    public int MessageCount { get; set; }
    public int NotificationCount { get; set; }
    public DateTime FollowTime { get; set; }
    public DateTime LastMessageTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

