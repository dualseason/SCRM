using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 朋友圈点赞实体
/// </summary>
public class MomentsLike
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 朋友圈文章 ID
    /// </summary>
    [Column("PostId")]
    public int postId { get; set; }

    /// <summary>
    /// 点赞者 WXID
    /// </summary>
    [Column("LikerWxid")]
    public string likerWxid { get; set; } = string.Empty;

    /// <summary>
    /// 点赞者昵称
    /// </summary>
    [Column("LikerNickname")]
    public string likerNickname { get; set; } = string.Empty;

    /// <summary>
    /// 点赞时间
    /// </summary>
    [Column("LikeTime")]
    public DateTime likeTime { get; set; }

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
