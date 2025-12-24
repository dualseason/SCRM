using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities
{
    [Table("MomentsTimeline")]
    public class MomentsTimeline
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public long WechatAccountId { get; set; } // Added for multi-tenancy
        public long SnsId { get; set; } // WeChat SnsId (unique)

        public string UserName { get; set; } // Author Wxid
        public string NickName { get; set; } // Author Nick

        public string Content { get; set; }

        public long CreateTime { get; set; } // Timestamp

        // Stored as JSON string
        public string ImagesJson { get; set; } 
        
        // Stored as JSON string 
        public string CommentsJson { get; set; }
        
        // Stored as JSON string
        public string LikesJson { get; set; }

        public long ReceivedAt { get; set; } = DateTime.UtcNow.Ticks;

        // Foreign Key to Account/Device? 
        // Ideally linked to the Account that "saw" this. 
        // But Timeline is usually shared.
        // For simplicity, we just store it flat mostly. 
        // But maybe link to the "Viewer" (MyAccount) if we want multi-tenancy isolation.
        public string OwnerWxid { get; set; } // The Wxid of the device that synced this
    }
}
