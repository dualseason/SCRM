namespace SCRM.API.Models.Entities;

/// <summary>
/// 璁惧鐗堟湰鏃ュ織琛?
/// </summary>
public class DeviceVersionLog
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string OldVersion { get; set; }
    public string NewVersion { get; set; }
    public DateTime UpgradeTime { get; set; }
    public int UpgradeStatus { get; set; }
    public string ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

