using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 公众号订阅通知记录实体
/// </summary>
[Table("OfficialAccountSubscriptions")]
public class OfficialAccountSubscription
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属公众号 ID
    /// </summary>
    [Column("OfficialAccountId")]
    public int officialAccountId { get; set; }

    /// <summary>
    /// 订阅者 WXID
    /// </summary>
    [Column("SubscriberWxid")]
    public string subscriberWxid { get; set; } = string.Empty;

    /// <summary>
    /// 通知类型
    /// </summary>
    [Column("NotificationType")]
    public int notificationType { get; set; }

    /// <summary>
    /// 通知状态
    /// </summary>
    [Column("NotificationStatus")]
    public int notificationStatus { get; set; }

    /// <summary>
    /// 通知内容
    /// </summary>
    [Column("NotificationContent")]
    public string notificationContent { get; set; } = string.Empty;

    /// <summary>
    /// 订阅时间
    /// </summary>
    [Column("SubscribeTime")]
    public DateTime subscribeTime { get; set; }

    /// <summary>
    /// 最后一次通知时间
    /// </summary>
    [Column("LastNotifyTime")]
    public DateTime lastNotifyTime { get; set; }

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


