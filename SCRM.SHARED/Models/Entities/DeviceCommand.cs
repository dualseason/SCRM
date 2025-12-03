namespace SCRM.API.Models.Entities;

/// <summary>
/// 璁惧鍛戒护琛?
/// </summary>
public class DeviceCommand
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string CommandType { get; set; }
    public string CommandData { get; set; }
    public int ExecutionStatus { get; set; }
    public DateTime IssuedTime { get; set; }
    public DateTime ExecutedTime { get; set; }
    public string ExecutionResult { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

