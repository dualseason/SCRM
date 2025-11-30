namespace SCRM.API.Models.Entities;

/// <summary>
/// 浼氳瘽琛?
/// </summary>
public class Conversation
{
    public int Id { get; set; }
    public int WechatAccountId { get; set; }
    public string ConversationWxid { get; set; }
    public int ConversationType { get; set; }
    public string DisplayName { get; set; }
    public string DisplayAvatar { get; set; }
    public int UnreadCount { get; set; }
    public int MessageCount { get; set; }
    public int IsPinned { get; set; }
    public int IsMuted { get; set; }
    public DateTime LastMessageTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

