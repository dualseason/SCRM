namespace SCRM.API.Models.Entities;

/// <summary>
/// 鏈嬪弸鍦堣瘎璁鸿〃
/// </summary>
public class MomentsComment
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public string CommenterWxid { get; set; }
    public string CommentContent { get; set; }
    public int ReplyTo { get; set; }
    public string ReplyToWxid { get; set; }
    public int LikeCount { get; set; }
    public DateTime CommentTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

