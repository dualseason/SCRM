using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.Models.Entities
{
    /// <summary>
    /// 微信账号信息表
    /// </summary>
    [Table("wechat_accounts")]
    public class WechatAccount
    {
        /// <summary>账号ID</summary>
        [Key]
        [Column("account_id")]
        public long AccountId { get; set; }

        /// <summary>微信WXID</summary>
        [Required]
        [Column("wxid")]
        [StringLength(100)]
        public string Wxid { get; set; } = string.Empty;

        /// <summary>微信号</summary>
        [Column("wechat_number")]
        [StringLength(50)]
        public string? WechatNumber { get; set; }

        /// <summary>微信昵称</summary>
        [Column("nickname")]
        [StringLength(100)]
        public string? Nickname { get; set; }

        /// <summary>手机号</summary>
        [Column("mobile_phone")]
        [StringLength(20)]
        public string? MobilePhone { get; set; }

        /// <summary>性别：0-未知 1-男 2-女</summary>
        [Column("gender")]
        public short? Gender { get; set; }

        /// <summary>头像URL</summary>
        [Column("avatar_url")]
        [StringLength(500)]
        public string? AvatarUrl { get; set; }

        /// <summary>个性签名</summary>
        [Column("signature")]
        [StringLength(500)]
        public string? Signature { get; set; }

        /// <summary>二维码URL</summary>
        [Column("qr_code_url")]
        [StringLength(500)]
        public string? QrCodeUrl { get; set; }

        /// <summary>地区</summary>
        [Column("region")]
        [StringLength(100)]
        public string? Region { get; set; }

        /// <summary>账号状态：1-正常在线 2-离线 3-冻结 4-注销 5-异常</summary>
        [Column("account_status")]
        public short? AccountStatus { get; set; }

        /// <summary>最后在线时间</summary>
        [Column("last_online_at")]
        public DateTime? LastOnlineAt { get; set; }

        /// <summary>是否删除</summary>
        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        /// <summary>创建时间</summary>
        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        /// <summary>更新时间</summary>
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>删除时间</summary>
        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }
    }
}
