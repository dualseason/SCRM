using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 钱包交易记录实体
/// </summary>
[Table("WalletTransactions")]
public class WalletTransaction
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属微信账号 ID
    /// </summary>
    [Column("WechatAccountId")]
    public long wechatAccountId { get; set; }

    /// <summary>
    /// 交易 ID (微信生成的 ID)
    /// </summary>
    [Column("TransactionId")]
    public string transactionId { get; set; } = string.Empty;

    /// <summary>
    /// 交易类型 (如：转账、红包、支付等)
    /// </summary>
    [Column("TransactionType")]
    public int transactionType { get; set; }

    /// <summary>
    /// 交易金额
    /// </summary>
    [Column("Amount")]
    public decimal amount { get; set; }

    /// <summary>
    /// 币种 (如：CNY)
    /// </summary>
    [Column("Currency")]
    public string currency { get; set; } = string.Empty;

    /// <summary>
    /// 来源 WXID
    /// </summary>
    [Column("SourceWxid")]
    public string sourceWxid { get; set; } = string.Empty;

    /// <summary>
    /// 目标 WXID
    /// </summary>
    [Column("TargetWxid")]
    public string targetWxid { get; set; } = string.Empty;

    /// <summary>
    /// 交易说明
    /// </summary>
    [Column("Description")]
    public string description { get; set; } = string.Empty;

    /// <summary>
    /// 交易状态
    /// </summary>
    [Column("TransactionStatus")]
    public int transactionStatus { get; set; }

    /// <summary>
    /// 失败原因
    /// </summary>
    [Column("FailureReason")]
    public string failureReason { get; set; } = string.Empty;

    /// <summary>
    /// 交易发生时间
    /// </summary>
    [Column("TransactionTime")]
    public DateTime transactionTime { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("CreatedAt")]
    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    [Column("UpdatedAt")]
    public DateTime updatedAt { get; set; } = DateTime.UtcNow;
}


