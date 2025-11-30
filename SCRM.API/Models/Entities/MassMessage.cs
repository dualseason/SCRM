namespace SCRM.API.Models.Entities;

/// <summary>
/// 缇ゅ彂娑堟伅琛?
/// </summary>
public class MassMessage
{
    public int Id { get; set; }
    public int WechatAccountId { get; set; }
    public string MessageTitle { get; set; }
    public string MessageContent { get; set; }
    public int MessageType { get; set; }
    public int TargetType { get; set; }
    public int TotalRecipients { get; set; }
    public int SuccessSentCount { get; set; }
    public int FailedSentCount { get; set; }
    public int SendStatus { get; set; }
    public DateTime ScheduledTime { get; set; }
    public DateTime SentTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

