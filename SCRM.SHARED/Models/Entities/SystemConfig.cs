using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 系统配置表
/// 存储全局键值对配置，如下发给客户端的 fileUpUrl
/// </summary>
[Table("system_configs")]
public class SystemConfig
{
    [Key]
    [Column("id")]
    public int id { get; set; }

    /// <summary>
    /// 配置键 (Unique)
    /// </summary>
    [Required]
    [MaxLength(100)]
    [Column("key")]
    public string key { get; set; } = string.Empty;

    /// <summary>
    /// 配置值
    /// </summary>
    [MaxLength(2000)]
    [Column("value")]
    public string value { get; set; } = string.Empty;

    /// <summary>
    /// 描述说明
    /// </summary>
    [MaxLength(500)]
    [Column("description")]
    public string? description { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    [Column("updated_at")]
    public DateTime updatedAt { get; set; } = DateTime.UtcNow;
}
