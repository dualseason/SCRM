using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 系统审计日志实体
/// 记录关键业务操作和系统事件
/// </summary>
[Table("system_logs")]
public class SystemLog
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Key]
    [Column("id")]
    public long id { get; set; }

    /// <summary>
    /// 日志级别 (Info, Warning, Error)
    /// </summary>
    [Required]
    [Column("level")]
    [StringLength(20)]
    public string level { get; set; } = "Info";

    /// <summary>
    /// 所属模块 (Auth, Account, System, etc.)
    /// </summary>
    [Required]
    [Column("module")]
    [StringLength(50)]
    public string module { get; set; } = string.Empty;

    /// <summary>
    /// 操作动作 (Login, Takeover, Update, etc.)
    /// </summary>
    [Required]
    [Column("action")]
    [StringLength(50)]
    public string action { get; set; } = string.Empty;

    /// <summary>
    /// 详细消息内容
    /// </summary>
    [Column("message")]
    public string? message { get; set; }

    /// <summary>
    /// 操作人 ID (可能是 UserId 或空)
    /// </summary>
    [Column("operator_id")]
    [StringLength(450)]
    public string? operatorId { get; set; }

    /// <summary>
    /// 目标对象 ID (如 AccountId, Wxid)
    /// </summary>
    [Column("target_id")]
    [StringLength(100)]
    public string? targetId { get; set; }

    /// <summary>
    /// 客户端 IP 地址
    /// </summary>
    [Column("client_ip")]
    [StringLength(50)]
    public string? clientIp { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("created_at")]
    public DateTime createdAt { get; set; } = DateTime.UtcNow;
}

