namespace SCRM.API.Models.Entities;

/// <summary>
/// 灏忕▼搴忔秷鎭褰曡〃
/// </summary>
public class MiniprogramMessage
{
    public int Id { get; set; }
    public int MiniprogramAccountId { get; set; }
    public string MessageId { get; set; }
    public int MessageType { get; set; }
    public string MessageContent { get; set; }
    public string SenderWxid { get; set; }
    public int IsRead { get; set; }
    public DateTime MessageTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

