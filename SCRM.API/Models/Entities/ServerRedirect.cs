namespace SCRM.API.Models.Entities;

/// <summary>
/// 鏈嶅姟鍣ㄩ噸瀹氬悜琛?
/// </summary>
public class ServerRedirect
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string SourceServer { get; set; }
    public string TargetServer { get; set; }
    public int RedirectStatus { get; set; }
    public string RedirectReason { get; set; }
    public DateTime RedirectTime { get; set; }
    public DateTime ExpireTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

