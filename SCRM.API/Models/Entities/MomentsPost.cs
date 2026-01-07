using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 朋友圈文章实体
/// </summary>
public class MomentsPost
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
    /// 作者 WXID
    /// </summary>
    [Column("AuthorWxid")]
    public string authorWxid { get; set; } = string.Empty;

    /// <summary>
    /// 发布内容
    /// </summary>
    [Column("PostContent")]
    public string postContent { get; set; } = string.Empty;

    /// <summary>
    /// 微信 SnsId
    /// </summary>
    [Column("SnsId")]
    public long snsId { get; set; }

    /// <summary>
    /// 点赞数
    /// </summary>
    [Column("LikeCount")]
    public int likeCount { get; set; }

    /// <summary>
    /// 评论数
    /// </summary>
    [Column("CommentCount")]
    public int commentCount { get; set; }

    /// <summary>
    /// 分享数
    /// </summary>
    [Column("ShareCount")]
    public int shareCount { get; set; }

    /// <summary>
    /// 查看数
    /// </summary>
    [Column("ViewCount")]
    public int viewCount { get; set; }

    /// <summary>
    /// 封面图 URL
    /// </summary>
    [Column("PostCover")]
    public string postCover { get; set; } = string.Empty;

    /// <summary>
    /// 图片列表 (JSON 字符串)
    /// </summary>
    [Column("ImagesJson")]
    public string? imagesJson { get; set; }

    /// <summary>
    /// 视频 URL
    /// </summary>
    [Column("VideoUrl")]
    public string? videoUrl { get; set; }

    /// <summary>
    /// 链接内容 (JSON 字符串)
    /// </summary>
    [Column("LinkInfoJson")]
    public string? linkInfoJson { get; set; }

    /// <summary>
    /// 是否可见 (0-否, 1-是)
    /// </summary>
    [Column("IsVisible")]
    public int isVisible { get; set; }

    /// <summary>
    /// 是否允许评论 (0-否, 1-是)
    /// </summary>
    [Column("CanComment")]
    public int canComment { get; set; }

    /// <summary>
    /// 是否允许点赞 (0-否, 1-是)
    /// </summary>
    [Column("CanLike")]
    public int canLike { get; set; }

    /// <summary>
    /// 发布时间
    /// </summary>
    [Column("PublishTime")]
    public DateTime publishTime { get; set; }

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

    /// <summary>
    /// 是否已删除
    /// </summary>
    [Column("IsDeleted")]
    public bool isDeleted { get; set; }
}
