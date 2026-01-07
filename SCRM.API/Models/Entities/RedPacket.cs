using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 红包发送记录实体
/// </summary>
public class RedPacket
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
    /// 发送者 WXID
    /// </summary>
    [Column("SenderWxid")]
    public string senderWxid { get; set; } = string.Empty;

    /// <summary>
    /// 红包总金额
    /// </summary>
    [Column("TotalAmount")]
    public decimal totalAmount { get; set; }

    /// <summary>
    /// 红包个数
    /// </summary>
    [Column("TotalCount")]
    public int totalCount { get; set; }

    /// <summary>
    /// 货币类型
    /// </summary>
    [Column("Currency")]
    public string currency { get; set; } = string.Empty;

    /// <summary>
    /// 红包留言
    /// </summary>
    [Column("RedPacketMessage")]
    public string redPacketMessage { get; set; } = string.Empty;

    /// <summary>
    /// 目标类型 (单聊/群聊)
    /// </summary>
    [Column("TargetType")]
    public string targetType { get; set; } = string.Empty;

    /// <summary>
    /// 目标 WXID
    /// </summary>
    [Column("TargetWxid")]
    public string targetWxid { get; set; } = string.Empty;

    /// <summary>
    /// 红包状态
    /// </summary>
    [Column("RedPacketStatus")]
    public int redPacketStatus { get; set; }

    /// <summary>
    /// 已领取个数
    /// </summary>
    [Column("ReceivedCount")]
    public int receivedCount { get; set; }

    /// <summary>
    /// 已领取总金额
    /// </summary>
    [Column("ReceivedAmount")]
    public decimal receivedAmount { get; set; }

    /// <summary>
    /// 发送时间
    /// </summary>
    [Column("SendTime")]
    public DateTime sendTime { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    [Column("ExpiryTime")]
    public DateTime expiryTime { get; set; }

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

    /// <summary>
    /// 是否已删除
    /// </summary>
    [Column("IsDeleted")]
    public bool isDeleted { get; set; }
}
