namespace SCRM.API.Models.Entities;

/// <summary>
/// 娑堟伅杞彂璇︽儏琛?
/// </summary>
public class MessageForwardDetail
{
    public int Id { get; set; }
    public int MessageForwardId { get; set; }
    public string ToWxid { get; set; }
    public int ForwardStatus { get; set; }
    public DateTime ForwardTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

