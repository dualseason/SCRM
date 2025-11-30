namespace SCRM.API.Models.Entities;

/// <summary>
/// 缇ゅ彂娑堟伅璇︽儏琛?
/// </summary>
public class MassMessageDetail
{
    public int Id { get; set; }
    public int MassMessageId { get; set; }
    public string RecipientWxid { get; set; }
    public int SendStatus { get; set; }
    public string ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime SentTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

