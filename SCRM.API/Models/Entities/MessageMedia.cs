using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 消息媒体实体
/// </summary>
[Table("MessageMedias")]
public class MessageMedia
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属消息 ID
    /// </summary>
    [Column("MessageId")]
    public int messageId { get; set; }

    /// <summary>
    /// 媒体类型 (如：图片、视频、语音等)
    /// </summary>
    [Column("MediaType")]
    public int mediaType { get; set; }

    /// <summary>
    /// 媒体 URL
    /// </summary>
    [Column("MediaUrl")]
    public string mediaUrl { get; set; } = string.Empty;

    /// <summary>
    /// 本地存储路径
    /// </summary>
    [Column("LocalPath")]
    public string localPath { get; set; } = string.Empty;

    /// <summary>
    /// 媒体文件哈希值
    /// </summary>
    [Column("MediaHash")]
    public string mediaHash { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小 (字节)
    /// </summary>
    [Column("FileSize")]
    public long fileSize { get; set; }

    /// <summary>
    /// 文件扩展名
    /// </summary>
    [Column("FileExtension")]
    public string fileExtension { get; set; } = string.Empty;

    /// <summary>
    /// 上传状态
    /// </summary>
    [Column("UploadStatus")]
    public int uploadStatus { get; set; }

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


