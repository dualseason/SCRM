using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SCRM.API.Models.DTOs;
using SCRM.SHARED.Models;

namespace SCRM.API.Models.Entities
{
    [Table("sr_clients")]
    public class SrClient
    {
        [Key]
        public string uuid { get; set; }

        public string tcpHost { get; set; }
        public int tcpPort { get; set; }

        [Column(TypeName = "jsonb")]
        public SCRM.API.Models.DTOs.Device device { get; set; }

        public string? ip { get; set; }
        public DateTime? lastLoginAt { get; set; }
        public bool isOnline { get; set; }
        public int status { get; set; }
        
        /// <summary>
        /// Owner User ID (RBAC)
        /// </summary>
        [Column("owner_id")]
        public string? OwnerId { get; set; }

        [ForeignKey("OwnerId")]
        public virtual ApplicationUser? Owner { get; set; }

        /// <summary>
        /// SignalR Connection ID (Runtime only)
        /// </summary>
        [NotMapped]
        public string? ConnectionId { get; set; }

        [NotMapped]
        public string? WeChatId { get; set; }

        [NotMapped]
        public string? WeChatNick { get; set; }

        [NotMapped]
        public long? WechatAccountId { get; set; }

        public virtual ICollection<WechatAccount> Accounts { get; set; } = new List<WechatAccount>();

        public DateTime createdAt { get; set; } = DateTime.UtcNow;
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;
    }
}
