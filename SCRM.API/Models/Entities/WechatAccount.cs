using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities
{
    /// <summary>
    /// 寰俊璐﹀彿淇℃伅琛?
    /// </summary>
    [Table("wechat_accounts")]
    public class WechatAccount
    {
        /// <summary>璐﹀彿ID</summary>
        [Key]
        [Column("account_id")]
        public long AccountId { get; set; }

        /// <summary>寰俊WXID</summary>
        [Required]
        [Column("wxid")]
        [StringLength(100)]
        public string Wxid { get; set; } = string.Empty;

        /// <summary>寰俊鍙?/summary>
        [Column("wechat_number")]
        [StringLength(50)]
        public string? WechatNumber { get; set; }

        /// <summary>寰俊鏄电О</summary>
        [Column("nickname")]
        [StringLength(100)]
        public string? Nickname { get; set; }

        /// <summary>鎵嬫満鍙?/summary>
        [Column("mobile_phone")]
        [StringLength(20)]
        public string? MobilePhone { get; set; }

        /// <summary>鎬у埆锛?-鏈煡 1-鐢?2-濂?/summary>
        [Column("gender")]
        public short? Gender { get; set; }

        /// <summary>澶村儚URL</summary>
        [Column("avatar_url")]
        [StringLength(500)]
        public string? AvatarUrl { get; set; }

        /// <summary>涓€х鍚?/summary>
        [Column("signature")]
        [StringLength(500)]
        public string? Signature { get; set; }

        /// <summary>浜岀淮鐮乁RL</summary>
        [Column("qr_code_url")]
        [StringLength(500)]
        public string? QrCodeUrl { get; set; }

        /// <summary>鍦板尯</summary>
        [Column("region")]
        [StringLength(100)]
        public string? Region { get; set; }

        /// <summary>璐﹀彿鐘舵€侊細1-姝ｅ父鍦ㄧ嚎 2-绂荤嚎 3-鍐荤粨 4-娉ㄩ攢 5-寮傚父</summary>
        [Column("account_status")]
        public short? AccountStatus { get; set; }

        /// <summary>鏈€鍚庡湪绾挎椂闂?/summary>
        [Column("last_online_at")]
        public DateTime? LastOnlineAt { get; set; }

        /// <summary>鏄惁鍒犻櫎</summary>
        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        /// <summary>是否有效（IsActive 属性，值为 !IsDeleted）</summary>
        [NotMapped]
        public bool IsActive
        {
            get { return !IsDeleted; }
            set { IsDeleted = !value; }
        }

        /// <summary>鍒涘缓鏃堕棿</summary>
        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        /// <summary>鏇存柊鏃堕棿</summary>
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>鍒犻櫎鏃堕棿</summary>
        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }
    }
}

