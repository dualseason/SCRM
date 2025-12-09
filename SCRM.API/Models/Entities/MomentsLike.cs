namespace SCRM.API.Models.Entities;

/// <summary>
/// 鏈嬪弸鍦堢偣璧炶〃
/// </summary>
public class MomentsLike
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public string LikerWxid { get; set; }
    public string LikerNickname { get; set; }
    public DateTime LikeTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

