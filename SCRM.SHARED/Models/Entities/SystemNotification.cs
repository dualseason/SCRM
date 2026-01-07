using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 系统通知实体
/// </summary>
[Table("SystemNotifications")]
public class SystemNotification
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 通知标题
    /// </summary>
    [Column("NotificationTitle")]
    public string notificationTitle { get; set; } = string.Empty;

    /// <summary>
    /// 通知内容
    /// </summary>
    [Column("NotificationContent")]
    public string notificationContent { get; set; } = string.Empty;

    /// <summary>
    /// 通知类型
    /// </summary>
    [Column("NotificationType")]
    public int notificationType { get; set; }

    /// <summary>
    /// 目标类型 (如：全部用户、指定用户、指定设备)
    /// </summary>
    [Column("TargetType")]
    public int targetType { get; set; }

    /// <summary>
    /// 目标标识符
    /// </summary>
    [Column("TargetIdentifier")]
    public string targetIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// 优先级
    /// </summary>
    [Column("Priority")]
    public int priority { get; set; }

    /// <summary>
    /// 是否已读 (0-否, 1-是)
    /// </summary>
    [Column("IsRead")]
    public int isRead { get; set; }

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
}


