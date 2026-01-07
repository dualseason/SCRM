using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 应用版本实体
/// </summary>
[Table("app_versions")]
public class AppVersion
{
    /// <summary>
    /// 版本 ID
    /// </summary>
    [Key]
    [Column("VersionId")]
    public long versionId { get; set; }

    /// <summary>
    /// 版本号（如 1.0.0）
    /// </summary>
    [Column("VersionNumber")]
    public string versionNumber { get; set; } = string.Empty;

    /// <summary>
    /// 版本名称
    /// </summary>
    [Column("VersionName")]
    public string? versionName { get; set; }

    /// <summary>
    /// 版本类型：1-稳定版, 2-测试版, 3-Beta 版, 4-灰度版
    /// </summary>
    [Column("VersionType")]
    public short? versionType { get; set; }

    /// <summary>
    /// 平台：Android/iOS/Windows/MacOS/Web
    /// </summary>
    [Column("Platform")]
    public string platform { get; set; } = string.Empty;

    /// <summary>
    /// 下载地址
    /// </summary>
    [Column("DownloadUrl")]
    public string downloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// 更新说明
    /// </summary>
    [Column("ReleaseNotes")]
    public string? releaseNotes { get; set; }

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    [Column("FileSize")]
    public long? fileSize { get; set; }

    /// <summary>
    /// 文件哈希值
    /// </summary>
    [Column("FileHash")]
    public string? fileHash { get; set; }

    /// <summary>
    /// 最低 SDK 版本
    /// </summary>
    [Column("MinSdkVersion")]
    public string? minSdkVersion { get; set; }

    /// <summary>
    /// 最低操作系统版本
    /// </summary>
    [Column("MinOsVersion")]
    public string? minOsVersion { get; set; }

    /// <summary>
    /// 是否强制更新
    /// </summary>
    [Column("ForcedUpdate")]
    public bool forcedUpdate { get; set; }

    /// <summary>
    /// 是否已发布
    /// </summary>
    [Column("IsReleased")]
    public bool isReleased { get; set; }

    /// <summary>
    /// 是否已废弃
    /// </summary>
    [Column("IsDeprecated")]
    public bool isDeprecated { get; set; }

    /// <summary>
    /// 下载次数
    /// </summary>
    [Column("DownloadCount")]
    public long? downloadCount { get; set; }

    /// <summary>
    /// 发布时间
    /// </summary>
    [Column("ReleasedAt")]
    public DateTime? releasedAt { get; set; }

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
