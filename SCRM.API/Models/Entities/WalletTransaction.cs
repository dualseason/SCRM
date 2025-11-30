namespace SCRM.API.Models.Entities;

/// <summary>
/// 閽卞寘浜ゆ槗璁板綍琛?
/// </summary>
public class WalletTransaction
{
    public int Id { get; set; }
    public int WechatAccountId { get; set; }
    public string TransactionId { get; set; }
    public int TransactionType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string SourceWxid { get; set; }
    public string TargetWxid { get; set; }
    public string Description { get; set; }
    public int TransactionStatus { get; set; }
    public string FailureReason { get; set; }
    public DateTime TransactionTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

