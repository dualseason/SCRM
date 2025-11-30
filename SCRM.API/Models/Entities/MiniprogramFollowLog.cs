namespace SCRM.API.Models.Entities;

/// <summary>
/// 灏忕▼搴忓叧娉ㄤ簨浠舵棩蹇楄〃
/// </summary>
public class MiniprogramFollowLog
{
    public int Id { get; set; }
    public int MiniprogramAccountId { get; set; }
    public string FollowerWxid { get; set; }
    public int EventType { get; set; }
    public string EventReason { get; set; }
    public DateTime EventTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

