using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 群二维码实体
/// </summary>
[Table("GroupQrcodes")]
public class GroupQrcode
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
    /// 二维码 URL
    /// </summary>
    [Column("QrcodeUrl")]
    public string qrcodeUrl { get; set; } = string.Empty;

    /// <summary>
    /// 二维码原始数据
    /// </summary>
    [Column("QrcodeData")]
    public string qrcodeData { get; set; } = string.Empty;

    /// <summary>
    /// 是否已过期 (0-否, 1-是)
    /// </summary>
    [Column("IsExpired")]
    public int isExpired { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    [Column("ExpiryTime")]
    public DateTime expiryTime { get; set; }

    /// <summary>
    /// 扫描次数
    /// </summary>
    [Column("ScanCount")]
    public int scanCount { get; set; }

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


