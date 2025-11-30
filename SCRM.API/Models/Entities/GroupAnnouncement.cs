namespace SCRM.API.Models.Entities;

/// <summary>
/// 缇ゅ叕鍛婅〃
/// </summary>
public class GroupAnnouncement
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string AnnouncementContent { get; set; }
    public string PublisherWxid { get; set; }
    public int AnnouncementType { get; set; }
    public int IsTopLevel { get; set; }
    public DateTime PublishTime { get; set; }
    public DateTime ExpiryTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

