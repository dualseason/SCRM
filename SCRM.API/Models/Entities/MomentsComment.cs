using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 朋友圈评论实体
/// </summary>
public class MomentsComment
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
    /// 评论者 WXID
    /// </summary>
    [Column("CommenterWxid")]
    public string commenterWxid { get; set; } = string.Empty;

    /// <summary>
    /// 微信端的评论 ID
    /// </summary>
    [Column("WeChatCommentId")]
    public long weChatCommentId { get; set; }

    /// <summary>
    /// 回复的评论 ID (如果有)
    /// </summary>
    [Column("ReplyCommentId")]
    public long? replyCommentId { get; set; }

    /// <summary>
    /// 评论内容
    /// </summary>
    [Column("CommentContent")]
    public string commentContent { get; set; } = string.Empty;

    /// <summary>
    /// 回复对象的 ID
    /// </summary>
    [Column("ReplyTo")]
    public int replyTo { get; set; }

    /// <summary>
    /// 回复对象的 WXID
    /// </summary>
    [Column("ReplyToWxid")]
    public string replyToWxid { get; set; } = string.Empty;

    /// <summary>
    /// 点赞数
    /// </summary>
    [Column("LikeCount")]
    public int likeCount { get; set; }

    /// <summary>
    /// 评论时间
    /// </summary>
    [Column("CommentTime")]
    public DateTime commentTime { get; set; }

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
