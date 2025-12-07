using Microsoft.AspNetCore.Identity;
using SCRM.API.Models.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.SHARED.Models
{
    public class ApplicationUser : IdentityUser
    {
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
