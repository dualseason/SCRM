namespace SCRM.API.Models.Entities;

/// <summary>
/// 娑堟伅杞彂琛?
/// </summary>
public class MessageForward
{
    public int Id { get; set; }
    public int OriginalMessageId { get; set; }
    public string FromWxid { get; set; }
    public int ForwardCount { get; set; }
    public int IsLimitedViews { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

