using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 小程序搜索记录实体
/// </summary>
[Table("MiniprogramSearchLogs")]
public class MiniprogramSearchLog
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属微信账号 ID
    /// </summary>
    [Column("WechatAccountId")]
    public long wechatAccountId { get; set; }

    /// <summary>
    /// 搜索关键词
    /// </summary>
    [Column("SearchKeyword")]
    public string searchKeyword { get; set; } = string.Empty;

    /// <summary>
    /// 搜索结果数量
    /// </summary>
    [Column("SearchResultCount")]
    public int searchResultCount { get; set; }

    /// <summary>
    /// 选中的 AppId
    /// </summary>
    [Column("SelectedAppId")]
    public string selectedAppId { get; set; } = string.Empty;

    /// <summary>
    /// 访问动作
    /// </summary>
    [Column("AccessAction")]
    public int accessAction { get; set; }

    /// <summary>
    /// 搜索时间
    /// </summary>
    [Column("SearchTime")]
    public DateTime searchTime { get; set; }

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


