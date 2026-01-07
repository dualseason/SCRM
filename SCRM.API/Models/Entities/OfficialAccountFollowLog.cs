using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 公众号关注事件日志实体
/// </summary>
[Table("OfficialAccountFollowLogs")]
public class OfficialAccountFollowLog
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属公众号 ID
    /// </summary>
    [Column("OfficialAccountId")]
    public int officialAccountId { get; set; }

    /// <summary>
    /// 关注者 WXID
    /// </summary>
    [Column("FollowerWxid")]
    public string followerWxid { get; set; } = string.Empty;

    /// <summary>
    /// 事件类型 (如：关注、取消关注)
    /// </summary>
    [Column("EventType")]
    public int eventType { get; set; }

    /// <summary>
    /// 事件原因
    /// </summary>
    [Column("EventReason")]
    public string eventReason { get; set; } = string.Empty;

    /// <summary>
    /// 事件发生时间
    /// </summary>
    [Column("EventTime")]
    public DateTime eventTime { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("CreatedAt")]
    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    [Column("UpdatedAt")]
    public DateTime updatedAt { get; set; } = DateTime.UtcNow;
}


