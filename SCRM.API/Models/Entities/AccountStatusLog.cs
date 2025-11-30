namespace SCRM.API.Models.Entities;

/// <summary>
/// 璐﹀彿鐘舵€佹棩蹇楄〃
/// </summary>
public class AccountStatusLog
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public int WechatAccountId { get; set; }
    public string OldStatus { get; set; }
    public string NewStatus { get; set; }
    public string ChangeReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

