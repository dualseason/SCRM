namespace SCRM.API.Models.Entities;

/// <summary>
/// 娑堟伅鎾ゅ洖琛?
/// </summary>
public class MessageRevocation
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public string RevokerWxid { get; set; }
    public string RevocationReason { get; set; }
    public DateTime RevocationTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

