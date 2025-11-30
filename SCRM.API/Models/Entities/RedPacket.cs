namespace SCRM.API.Models.Entities;

/// <summary>
/// 绾㈠寘鍙戦€佽褰曡〃
/// </summary>
public class RedPacket
{
    public int Id { get; set; }
    public int WechatAccountId { get; set; }
    public string SenderWxid { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalCount { get; set; }
    public string Currency { get; set; }
    public string RedPacketMessage { get; set; }
    public string TargetType { get; set; }
    public string TargetWxid { get; set; }
    public int RedPacketStatus { get; set; }
    public int ReceivedCount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public DateTime SendTime { get; set; }
    public DateTime ExpiryTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

