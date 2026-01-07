using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// VIP 激活码实体
/// </summary>
[Table("vip_keys")]
public class VipKey
{
    /// <summary>
    /// 激活码唯一标识 (UUID)
    /// </summary>
    [Key]
    [Column("uuid")]
    public string uuid { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 激活码类型：0-月卡, 1-季卡, 2-年卡, 3-永久, 4-天卡, 5-周卡
    /// </summary>
    [Column("type")]
    public int type { get; set; }

    /// <summary>
    /// 状态：0-未使用, 1-已使用, -1-无效
    /// </summary>
    [Column("status")]
    public int status { get; set; }

    /// <summary>
    /// 持续天数
    /// </summary>
    [Column("duration_days")]
    public int durationDays { get; set; } = 30;

    /// <summary>
    /// 使用 (激活) 时间
    /// </summary>
    [Column("use_time")]
    public DateTime? useTime { get; set; }

    /// <summary>
    /// 使用该激活码的微信账号 ID
    /// </summary>
    [Column("account_id")]
    public long? accountId { get; set; }

    /// <summary>
    /// 使用该激活码的设备 IMEI
    /// </summary>
    [Column("device_imei")]
    public string? deviceImei { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("created_at")]
    public DateTime createdAt { get; set; } = DateTime.UtcNow;
}

