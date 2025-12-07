using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities
{
    [Table("vip_keys")]
    public class VipKey
    {
        [Key]
        [Column("uuid")]
        public string Uuid { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 0为月卡，1为季卡，2为年卡，3为永久，4天卡，5周卡
        /// </summary>
        [Column("type")]
        public int Type { get; set; } = 0;

        /// <summary>
        /// 0为正常(未使用)，1为已使用，-1为无效
        /// </summary>
        [Column("status")]
        public int Status { get; set; } = 0;

        /// <summary>
        /// 默认能用30天 (Duration in days, or specific logic in controller)
        /// </summary>
        [Column("duration_days")]
        public int DurationDays { get; set; } = 30;

        /// <summary>
        /// 使用（激活）的时间
        /// </summary>
        [Column("use_time")]
        public DateTime? UseTime { get; set; }

        /// <summary>
        /// 关联的设备/账户ID (WechatAccount.AccountId)
        /// </summary>
        [Column("account_id")]
        public long? AccountId { get; set; }

        /// <summary>
        /// 关联的设备IMEI (WechatAccount.WechatNumber)
        /// </summary>
        [Column("device_imei")]
        public string? DeviceImei { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
