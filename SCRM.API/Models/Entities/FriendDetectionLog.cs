namespace SCRM.API.Models.Entities;

/// <summary>
/// 濂藉弸妫€娴嬫棩蹇楄〃
/// </summary>
public class FriendDetectionLog
{
    public int Id { get; set; }
    public int ContactId { get; set; }
    public int DetectionType { get; set; }
    public int DetectionResult { get; set; }
    public string DetailInfo { get; set; }
    public DateTime DetectionTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

