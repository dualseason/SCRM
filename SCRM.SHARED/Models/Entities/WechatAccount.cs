using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SCRM.SHARED.Models;

namespace SCRM.API.Models.Entities
{
    /// <summary>
    /// 微信账号信息表
    /// </summary>
    [Table("wechat_accounts")]
    public partial class WechatAccount : ICacheable<WechatAccount>
    {
        /// <summary>账号ID</summary>
        [Key]
        [Column("account_id")]
        public long accountId { get; set; }

        /// <summary>
        /// 所属用户ID (ApplicationUser)
        /// </summary>
        [Column("owner_id")]
        public string? ownerId { get; set; }

        [ForeignKey("ownerId")]
        public virtual ApplicationUser? owner { get; set; }

        /// <summary>微信WXID</summary>
        [Required]
        [Column("wxid")]
        [StringLength(100)]
        public string wxid { get; set; } = string.Empty;

        /// <summary>微信号</summary>
        [Column("wechat_number")]
        [StringLength(50)]
        public string? wechatNumber { get; set; }

        /// <summary>
        /// 客户端生成的唯一标识符 (UUID)
        /// </summary>
        [Column("client_uuid")]
        [StringLength(64)]
        public string? clientUuid { get; set; }

        bool isOnline=> Client?.isOnline ?? false;

        [ForeignKey("clientUuid")]
        public virtual SrClient? Client { get; set; }

        /// <summary>微信昵称</summary>
        [Column("nickname")]
        [StringLength(100)]
        public string? nickname { get; set; }

        /// <summary>手机号</summary>
        [Column("mobile_phone")]
        [StringLength(20)]
        public string? mobilePhone { get; set; }

        /// <summary>性别：0-未知 1-男 2-女</summary>
        [Column("gender")]
        public short? gender { get; set; }

        /// <summary>头像URL</summary>
        [Column("avatar_url")]
        [StringLength(500)]
        public string? avatarUrl { get; set; }

        /// <summary>个性签名</summary>
        [Column("signature")]
        [StringLength(500)]
        public string? signature { get; set; }

        /// <summary>二维码URL</summary>
        [Column("qr_code_url")]
        [StringLength(500)]
        public string? qrCodeUrl { get; set; }

        /// <summary>地区</summary>
        [Column("region")]
        [StringLength(100)]
        public string? region { get; set; }

        /// <summary>账号状态：1-正常在线 2-离线 3-冻结 4-注销 5-异常</summary>
        [Column("account_status")]
        public short? accountStatus { get; set; }

        /// <summary>最后在线时间</summary>
        [Column("last_online_at")]
        public DateTime? lastOnlineAt { get; set; }

        /// <summary>是否删除</summary>
        [Column("is_deleted")]
        public bool isDeleted { get; set; }

        /// <summary>是否有效（IsActive 属性，值为 !IsDeleted）</summary>
        [NotMapped]
        public bool isActive
        {
            get { return !isDeleted; }
            set { isDeleted = !value; }
        }

        /// <summary>创建时间</summary>
        [Column("created_at")]
        public DateTime? createdAt { get; set; }

        /// <summary>更新时间</summary>
        [Column("updated_at")]
        public DateTime? updatedAt { get; set; }

        /// <summary>删除时间</summary>
        [Column("deleted_at")]
        public DateTime? deletedAt { get; set; }

        /// <summary>VIP过期时间</summary>
        [Column("vip_expiry_date")]
        public DateTime? vipExpiryDate { get; set; }

        /// <summary>是否是VIP</summary>
        [NotMapped]
        public bool isVip
        {
            get { return vipExpiryDate.HasValue && vipExpiryDate.Value > DateTime.UtcNow; }
        }

        public string GetId() => accountId.ToString();

        public WechatAccount CopyFrom(WechatAccount other)
        {
            this.ownerId = other.ownerId;
            this.wxid = other.wxid;
            this.wechatNumber = other.wechatNumber;
            this.clientUuid = other.clientUuid;
            this.nickname = other.nickname;
            this.mobilePhone = other.mobilePhone;
            this.gender = other.gender;
            this.avatarUrl = other.avatarUrl;
            this.signature = other.signature;
            this.qrCodeUrl = other.qrCodeUrl;
            this.region = other.region;
            this.accountStatus = other.accountStatus;
            this.lastOnlineAt = other.lastOnlineAt;
            this.isDeleted = other.isDeleted;
            this.updatedAt = DateTime.UtcNow;
            this.deletedAt = other.deletedAt;
            this.vipExpiryDate = other.vipExpiryDate;
            this.settings = other.settings;
            return this;
        }

        /// <summary>
        /// 扩展设置（JSON格式，存储AutoAcceptLuckyMoney等配置）
        /// </summary>
        [Column("settings")]
        public string? settings { get; set; }
    }
}
