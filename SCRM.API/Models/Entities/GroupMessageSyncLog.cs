using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 群消息同步日志实体
/// </summary>
[Table("GroupMessageSyncLogs")]
public class GroupMessageSyncLog
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属群聊 ID
    /// </summary>
    [Column("GroupId")]
    public int groupId { get; set; }

    /// <summary>
    /// 同步状态
    /// </summary>
    [Column("SyncStatus")]
    public int syncStatus { get; set; }

    /// <summary>
    /// 消息总数
    /// </summary>
    [Column("TotalMessages")]
    public int totalMessages { get; set; }

    /// <summary>
    /// 已同步的消息数
    /// </summary>
    [Column("SyncedMessages")]
    public int syncedMessages { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    [Column("ErrorMessage")]
    public string errorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 同步开始时间
    /// </summary>
    [Column("SyncStartTime")]
    public DateTime syncStartTime { get; set; }

    /// <summary>
    /// 同步结束时间
    /// </summary>
    [Column("SyncEndTime")]
    public DateTime syncEndTime { get; set; }

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


