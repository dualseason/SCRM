namespace SCRM.API.Models.Entities;

/// <summary>
/// 璁惧鎺堟潈琛?
/// </summary>
public class DeviceAuthorization
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string AuthToken { get; set; }
    public DateTime AuthTime { get; set; }
    public DateTime ExpiryTime { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

