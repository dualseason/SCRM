namespace SCRM.API.Models.Entities;

/// <summary>
/// 璁惧瀹氫綅琛?
/// </summary>
public class DeviceLocation
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Address { get; set; }
    public double Accuracy { get; set; }
    public DateTime LocationTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

