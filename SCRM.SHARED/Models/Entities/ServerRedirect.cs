using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 服务器重定向实体
/// </summary>
[Table("ServerRedirects")]
public class ServerRedirect
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 设备 ID
    /// </summary>
    [Column("DeviceId")]
    public int deviceId { get; set; }

    /// <summary>
    /// 来源服务器地址
    /// </summary>
    [Column("SourceServer")]
    public string sourceServer { get; set; } = string.Empty;

    /// <summary>
    /// 目标服务器地址
    /// </summary>
    [Column("TargetServer")]
    public string targetServer { get; set; } = string.Empty;

    /// <summary>
    /// 重定向状态
    /// </summary>
    [Column("RedirectStatus")]
    public int redirectStatus { get; set; }

    /// <summary>
    /// 重定向原因
    /// </summary>
    [Column("RedirectReason")]
    public string redirectReason { get; set; } = string.Empty;

    /// <summary>
    /// 重定向时间
    /// </summary>
    [Column("RedirectTime")]
    public DateTime redirectTime { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    [Column("ExpireTime")]
    public DateTime expireTime { get; set; }

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


