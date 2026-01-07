using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 好友检测日志实体
/// </summary>
public class FriendDetectionLog
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 联系人 ID
    /// </summary>
    [Column("ContactId")]
    public int contactId { get; set; }

    /// <summary>
    /// 检测类型 (如：1-单向好友, 2-双向好友)
    /// </summary>
    [Column("DetectionType")]
    public int detectionType { get; set; }

    /// <summary>
    /// 检测结果 (如：1-成功, 0-失败)
    /// </summary>
    [Column("DetectionResult")]
    public int detectionResult { get; set; }

    /// <summary>
    /// 详细信息 (如：解析出的错误原因)
    /// </summary>
    [Column("DetailInfo")]
    public string detailInfo { get; set; } = string.Empty;

    /// <summary>
    /// 检测执行时间
    /// </summary>
    [Column("DetectionTime")]
    public DateTime detectionTime { get; set; }

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
