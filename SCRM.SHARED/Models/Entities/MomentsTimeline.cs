using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities
{
    /// <summary>
    /// 朋友圈时间轴实体
    /// </summary>
    [Table("MomentsTimeline")]
    public class MomentsTimeline
    {
        /// <summary>
        /// 自增 ID
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long id { get; set; }

        /// <summary>
        /// 所属微信账号 ID
        /// </summary>
        public long wechatAccountId { get; set; }

        /// <summary>
        /// 微信 SnsId (唯一标识一条朋友圈)
        /// </summary>
        public long snsId { get; set; }

        /// <summary>
        /// 作者 WXID
        /// </summary>
        public string userName { get; set; } = string.Empty;

        /// <summary>
        /// 作者昵称
        /// </summary>
        public string nickName { get; set; } = string.Empty;

        /// <summary>
        /// 朋友圈文本内容
        /// </summary>
        public string content { get; set; } = string.Empty;

        /// <summary>
        /// 发布时间戳
        /// </summary>
        public long createTime { get; set; }

        /// <summary>
        /// 图片列表 (JSON 字符串)
        /// </summary>
        public string imagesJson { get; set; } = string.Empty;
        
        /// <summary>
        /// 评论列表 (JSON 字符串)
        /// </summary>
        public string commentsJson { get; set; } = string.Empty;
        
        /// <summary>
        /// 点赞列表 (JSON 字符串)
        /// </summary>
        public string likesJson { get; set; } = string.Empty;

        /// <summary>
        /// 系统接收时间
        /// </summary>
        public long receivedAt { get; set; } = DateTime.UtcNow.Ticks;

        /// <summary>
        /// 视频 URL
        /// </summary>
        public string? videoUrl { get; set; }

        /// <summary>
        /// 链接内容 (JSON 字符串)
        /// </summary>
        public string? linkInfoJson { get; set; }

        /// <summary>
        /// 原始 XML 内容
        /// </summary>
        public string? xmlContent { get; set; }

        /// <summary>
        /// 同步该数据的设备 WXID
        /// </summary>
        public string ownerWxid { get; set; } = string.Empty;
    }
}
