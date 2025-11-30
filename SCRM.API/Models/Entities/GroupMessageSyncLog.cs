namespace SCRM.API.Models.Entities;

/// <summary>
/// 缇ゆ秷鎭悓姝ユ棩蹇楄〃
/// </summary>
public class GroupMessageSyncLog
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int SyncStatus { get; set; }
    public int TotalMessages { get; set; }
    public int SyncedMessages { get; set; }
    public string ErrorMessage { get; set; }
    public DateTime SyncStartTime { get; set; }
    public DateTime SyncEndTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

