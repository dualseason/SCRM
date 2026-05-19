using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities
{
    /// <summary>
    /// 视频号结果历史持久化实体。
    /// </summary>
    [Table("FinderResultHistory")]
    public class FinderResultHistory
    {
        /// <summary>
        /// 自增主键。
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long id { get; set; }

        /// <summary>
        /// 历史归属键。
        /// <para>优先使用 device:{uuid}，兜底使用 wx:{weChatId}。</para>
        /// </summary>
        public string ownerKey { get; set; } = string.Empty;

        /// <summary>
        /// 设备 UUID。
        /// </summary>
        public string deviceUuid { get; set; } = string.Empty;

        /// <summary>
        /// 微信号。
        /// </summary>
        public string weChatId { get; set; } = string.Empty;

        /// <summary>
        /// 结果类型。
        /// <para>mention / userpage / comment。</para>
        /// </summary>
        public string resultType { get; set; } = string.Empty;

        /// <summary>
        /// 任务 ID。
        /// </summary>
        public long taskId { get; set; }

        /// <summary>
        /// 是否成功。
        /// </summary>
        public bool success { get; set; }

        /// <summary>
        /// 摘要信息，便于列表快速展示。
        /// </summary>
        public string summary { get; set; } = string.Empty;

        /// <summary>
        /// 结构化结果 JSON。
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string payloadJson { get; set; } = "{}";

        /// <summary>
        /// 业务接收时间。
        /// </summary>
        public DateTime receivedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 入库创建时间。
        /// </summary>
        public DateTime createdAt { get; set; } = DateTime.UtcNow;
    }
}
