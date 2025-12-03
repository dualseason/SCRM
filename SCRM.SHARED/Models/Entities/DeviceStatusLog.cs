namespace SCRM.API.Models.Entities;

/// <summary>
/// 璁惧鐘舵€佹棩蹇楄〃
/// </summary>
public class DeviceStatusLog
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string OldStatus { get; set; }
    public string NewStatus { get; set; }
    public string ChangeReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

