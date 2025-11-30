namespace SCRM.API.Models.Entities;

/// <summary>
/// 绾㈠寘棰嗗彇璁板綍琛?
/// </summary>
public class RedPacketRecord
{
    public int Id { get; set; }
    public int RedPacketId { get; set; }
    public string ReceiverWxid { get; set; }
    public decimal ReceivedAmount { get; set; }
    public string Currency { get; set; }
    public int ReceiveStatus { get; set; }
    public string ReceiveMessage { get; set; }
    public DateTime ReceiveTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

