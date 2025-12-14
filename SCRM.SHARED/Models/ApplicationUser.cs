using Microsoft.AspNetCore.Identity;
using SCRM.API.Models.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.SHARED.Models
{
    public class ApplicationUser : IdentityUser, ICacheable<ApplicationUser>
    {
        public string GetId() => Id;

        public ApplicationUser CopyFrom(ApplicationUser other)
        {
            this.UserName = other.UserName;
            this.NormalizedUserName = other.NormalizedUserName;
            this.Email = other.Email;
            this.NormalizedEmail = other.NormalizedEmail;
            this.EmailConfirmed = other.EmailConfirmed;
            this.PasswordHash = other.PasswordHash;
            this.SecurityStamp = other.SecurityStamp;
            this.ConcurrencyStamp = other.ConcurrencyStamp;
            this.PhoneNumber = other.PhoneNumber;
            this.PhoneNumberConfirmed = other.PhoneNumberConfirmed;
            this.TwoFactorEnabled = other.TwoFactorEnabled;
            this.LockoutEnd = other.LockoutEnd;
            this.LockoutEnabled = other.LockoutEnabled;
            this.AccessFailedCount = other.AccessFailedCount;
            // Roles is NotMapped, skip. Collections skip.
            return this;
        }
        /// <summary>
        /// 拥有的客户端设备
        /// </summary>
        public virtual ICollection<SrClient> Clients { get; set; } = new List<SrClient>();

        /// <summary>
        /// 拥有的微信账号
        /// </summary>
        public virtual ICollection<WechatAccount> WechatAccounts { get; set; } = new List<WechatAccount>();

        /// <summary>
        /// 拥有的激活码
        /// </summary>
        public virtual ICollection<VipKey> VipKeys { get; set; } = new List<VipKey>();

        /// <summary>
        /// 角色列表 (非映射属性，用于UI显示)
        /// </summary>
        [NotMapped]
        public List<string> Roles { get; set; } = new List<string>();
    }
}
