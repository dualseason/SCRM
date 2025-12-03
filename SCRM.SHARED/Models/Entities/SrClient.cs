using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities
{
    /// <summary>
    /// 客户端设备信息表
    /// </summary>
    [Table("sr_clients")]
    public class SrClient
    {
        /// <summary>主键ID</summary>
        [Key]
        [Column("id")]
        public long Id { get; set; }

        /// <summary>机器码/注册码 (唯一标识)</summary>
        [Required]
        [Column("machine_id")]
        [StringLength(100)]
        public string MachineId { get; set; } = string.Empty;

        /// <summary>客户端名称</summary>
        [Column("client_name")]
        [StringLength(100)]
        public string? ClientName { get; set; }

        /// <summary>IP地址</summary>
        [Column("ip_address")]
        [StringLength(50)]
        public string? IpAddress { get; set; }

        /// <summary>状态: 0-离线, 1-在线</summary>
        [Column("status")]
        public int Status { get; set; }

        /// <summary>最后心跳时间</summary>
        [Column("last_heartbeat")]
        public DateTime? LastHeartbeat { get; set; }

        /// <summary>客户端版本</summary>
        [Column("version")]
        [StringLength(50)]
        public string? Version { get; set; }

        /// <summary>当前登录的微信账号ID</summary>
        [Column("current_wechat_account_id")]
        public long? CurrentWechatAccountId { get; set; }

        /// <summary>备注</summary>
        [Column("remark")]
        [StringLength(500)]
        public string? Remark { get; set; }

        /// <summary>创建时间</summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>更新时间</summary>
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
