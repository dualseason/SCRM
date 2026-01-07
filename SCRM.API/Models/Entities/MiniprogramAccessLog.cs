using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 小程序访问记录实体
/// </summary>
[Table("MiniprogramAccessLogs")]
public class MiniprogramAccessLog
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属小程序 ID
    /// </summary>
    [Column("MiniprogramAccountId")]
    public int miniprogramAccountId { get; set; }

    /// <summary>
    /// 访问者 WXID
    /// </summary>
    [Column("AccessorWxid")]
    public string accessorWxid { get; set; } = string.Empty;

    /// <summary>
    /// 页面 ID
    /// </summary>
    [Column("PageId")]
    public int pageId { get; set; }

    /// <summary>
    /// 页面路径
    /// </summary>
    [Column("PagePath")]
    public string pagePath { get; set; } = string.Empty;

    /// <summary>
    /// 停留时长 (秒)
    /// </summary>
    [Column("StayDuration")]
    public int stayDuration { get; set; }

    /// <summary>
    /// 访问来源
    /// </summary>
    [Column("AccessSource")]
    public string accessSource { get; set; } = string.Empty;

    /// <summary>
    /// 访问时间
    /// </summary>
    [Column("AccessTime")]
    public DateTime accessTime { get; set; }

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


