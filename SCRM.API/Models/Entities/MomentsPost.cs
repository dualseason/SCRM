namespace SCRM.API.Models.Entities;

/// <summary>
/// 鏈嬪弸鍦堟枃绔犺〃
/// </summary>
public class MomentsPost
{
    public int Id { get; set; }
    public long WechatAccountId { get; set; }
    public string AuthorWxid { get; set; }
    public string PostContent { get; set; }
    public long SnsId { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ShareCount { get; set; }
    public int ViewCount { get; set; }
    public string PostCover { get; set; }
    public string? ImagesJson { get; set; }
    public string? VideoUrl { get; set; }
    public string? LinkInfoJson { get; set; }
    public int IsVisible { get; set; }
    public int CanComment { get; set; }
    public int CanLike { get; set; }
    public DateTime PublishTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

