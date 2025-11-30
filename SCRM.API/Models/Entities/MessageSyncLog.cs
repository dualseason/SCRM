namespace SCRM.API.Models.Entities;

/// <summary>
/// 娑堟伅鍚屾鏃ュ織琛?
/// </summary>
public class MessageSyncLog
{
    public int Id { get; set; }
    public int WechatAccountId { get; set; }
    public int SyncType { get; set; }
    public int SyncStatus { get; set; }
    public int TotalMessages { get; set; }
    public int SyncedMessages { get; set; }
    public string ErrorMessage { get; set; }
    public DateTime SyncStartTime { get; set; }
    public DateTime SyncEndTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

