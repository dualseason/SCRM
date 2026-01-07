using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 红包领取记录实体
/// </summary>
public class RedPacketRecord
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 对应的红包 ID
    /// </summary>
    [Column("RedPacketId")]
    public int redPacketId { get; set; }

    /// <summary>
    /// 领取者 WXID
    /// </summary>
    [Column("ReceiverWxid")]
    public string receiverWxid { get; set; } = string.Empty;

    /// <summary>
    /// 领取金额
    /// </summary>
    [Column("ReceivedAmount")]
    public decimal receivedAmount { get; set; }

    /// <summary>
    /// 货币类型
    /// </summary>
    [Column("Currency")]
    public string currency { get; set; } = string.Empty;

    /// <summary>
    /// 领取状态
    /// </summary>
    [Column("ReceiveStatus")]
    public int receiveStatus { get; set; }

    /// <summary>
    /// 领取留言
    /// </summary>
    [Column("ReceiveMessage")]
    public string receiveMessage { get; set; } = string.Empty;

    /// <summary>
    /// 领取时间
    /// </summary>
    [Column("ReceiveTime")]
    public DateTime receiveTime { get; set; }

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
