namespace SCRM.API.Models.Entities;

/// <summary>
/// 应用版本表
/// </summary>
public class AppVersion
{
    /// <summary>
    /// 版本ID
    /// </summary>
    public long VersionId { get; set; }

    /// <summary>
    /// 版本号（如1.0.0）
    /// </summary>
    public string VersionNumber { get; set; } = string.Empty;

    /// <summary>
    /// 版本名称
    /// </summary>
    public string? VersionName { get; set; }

    /// <summary>
    /// 版本类型：1-稳定版 2-测试版 3-beta版 4-灰度版
    /// </summary>
    public short? VersionType { get; set; }

    /// <summary>
    /// 平台：Android/iOS/Windows/MacOS/Web
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// 下载地址
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// 更新说明
    /// </summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// 文件哈希值
    /// </summary>
    public string? FileHash { get; set; }

    /// <summary>
    /// 最低SDK版本
    /// </summary>
    public string? MinSdkVersion { get; set; }

    /// <summary>
    /// 最低操作系统版本
    /// </summary>
    public string? MinOsVersion { get; set; }

    /// <summary>
    /// 是否强制更新
    /// </summary>
    public bool ForcedUpdate { get; set; }

    /// <summary>
    /// 是否已发布
    /// </summary>
    public bool IsReleased { get; set; }

    /// <summary>
    /// 是否已废弃
    /// </summary>
    public bool IsDeprecated { get; set; }

    /// <summary>
    /// 下载次数
    /// </summary>
    public long? DownloadCount { get; set; }

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime? ReleasedAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
