using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 群公告实体
/// </summary>
[Table("GroupAnnouncements")]
public class GroupAnnouncement
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
    /// 公告内容
    /// </summary>
    [Column("AnnouncementContent")]
    public string announcementContent { get; set; } = string.Empty;

    /// <summary>
    /// 发布者 WXID
    /// </summary>
    [Column("PublisherWxid")]
    public string publisherWxid { get; set; } = string.Empty;

    /// <summary>
    /// 公告类型
    /// </summary>
    [Column("AnnouncementType")]
    public int announcementType { get; set; }

    /// <summary>
    /// 是否位置置顶
    /// </summary>
    [Column("IsTopLevel")]
    public int isTopLevel { get; set; }

    /// <summary>
    /// 发布时间
    /// </summary>
    [Column("PublishTime")]
    public DateTime publishTime { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    [Column("ExpiryTime")]
    public DateTime expiryTime { get; set; }

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


