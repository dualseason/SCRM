namespace SCRM.API.Models.Entities;

/// <summary>
/// 鍏紬鍙疯闃呴€氱煡璁板綍琛?
/// </summary>
public class OfficialAccountSubscription
{
    public int Id { get; set; }
    public int OfficialAccountId { get; set; }
    public string SubscriberWxid { get; set; }
    public int NotificationType { get; set; }
    public int NotificationStatus { get; set; }
    public string NotificationContent { get; set; }
    public DateTime SubscribeTime { get; set; }
    public DateTime LastNotifyTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

