using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SCRM.API.Models.DTOs;
using SCRM.SHARED.Models;

namespace SCRM.API.Models.Entities
{
    [Table("sr_clients")]
    public class SrClient : ICacheable<SrClient>
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
        /// SignalR Connection ID (Persisted for targeting)
        /// </summary>
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

        public string GetId() => uuid;

        public SrClient CopyFrom(SrClient other)
        {
            this.tcpHost = other.tcpHost;
            this.tcpPort = other.tcpPort;
            this.device = other.device;
            this.ip = other.ip;
            this.lastLoginAt = other.lastLoginAt;
            this.isOnline = other.isOnline;
            this.status = other.status;
            this.OwnerId = other.OwnerId;
            this.ConnectionId = other.ConnectionId;
            this.updatedAt = DateTime.UtcNow;
            
            // Should we copy NotMapped fields? Not strictly necessary for DB sync, but maybe for Cache?
            // Usually not mapped are transient.
            // Accounts collection? No, Reference only.
            
            return this;
        }
    }
}
