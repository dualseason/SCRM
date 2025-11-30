namespace SCRM.API.Models.Entities;

/// <summary>
/// 缇や簩缁寸爜琛?
/// </summary>
public class GroupQrcode
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string QrcodeUrl { get; set; }
    public string QrcodeData { get; set; }
    public int IsExpired { get; set; }
    public DateTime ExpiryTime { get; set; }
    public int ScanCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

