using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 群变更日志实体
/// </summary>
[Table("GroupChangeLogs")]
public class GroupChangeLog
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
    /// 变更类型
    /// </summary>
    [Column("ChangeType")]
    public string changeType { get; set; } = string.Empty;

    /// <summary>
    /// 变更执行者 WXID
    /// </summary>
    [Column("ChangedBy")]
    public string changedBy { get; set; } = string.Empty;

    /// <summary>
    /// 变更前的值
    /// </summary>
    [Column("OldValue")]
    public string oldValue { get; set; } = string.Empty;

    /// <summary>
    /// 变更后的值
    /// </summary>
    [Column("NewValue")]
    public string newValue { get; set; } = string.Empty;

    /// <summary>
    /// 变更的字段名
    /// </summary>
    [Column("ChangedField")]
    public string changedField { get; set; } = string.Empty;

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


