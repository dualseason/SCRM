namespace SCRM.API.Models.Entities;

/// <summary>
/// 鍏紬鍙峰叧娉ㄤ簨浠舵棩蹇楄〃
/// </summary>
public class OfficialAccountFollowLog
{
    public int Id { get; set; }
    public int OfficialAccountId { get; set; }
    public string FollowerWxid { get; set; }
    public int EventType { get; set; }
    public string EventReason { get; set; }
    public DateTime EventTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

