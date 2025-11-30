namespace SCRM.API.Models.Entities;

/// <summary>
/// 绯荤粺閫氱煡琛?
/// </summary>
public class SystemNotification
{
    public int Id { get; set; }
    public string NotificationTitle { get; set; }
    public string NotificationContent { get; set; }
    public int NotificationType { get; set; }
    public int TargetType { get; set; }
    public string TargetIdentifier { get; set; }
    public int Priority { get; set; }
    public int IsRead { get; set; }
    public DateTime SendTime { get; set; }
    public DateTime ExpiryTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

