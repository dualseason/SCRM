namespace SCRM.API.Models.Entities;

/// <summary>
/// 璁惧蹇冭烦琛?
/// </summary>
public class DeviceHeartbeat
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public DateTime HeartbeatTime { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double BatteryLevel { get; set; }
    public string NetworkStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

