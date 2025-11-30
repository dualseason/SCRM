namespace SCRM.API.Models.Entities;

/// <summary>
/// 鍏紬鍙锋秷鎭褰曡〃
/// </summary>
public class OfficialAccountMessage
{
    public int Id { get; set; }
    public int OfficialAccountId { get; set; }
    public string MessageId { get; set; }
    public int MessageType { get; set; }
    public string MessageContent { get; set; }
    public string SenderWxid { get; set; }
    public int IsRead { get; set; }
    public DateTime MessageTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

