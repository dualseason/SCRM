namespace SCRM.API.Models.Entities;

/// <summary>
/// 濂藉弸璇锋眰琛?
/// </summary>
public class FriendRequest
{
    public int Id { get; set; }
    public int WechatAccountId { get; set; }
    public string RequestWxid { get; set; }
    public string RequestMessage { get; set; }
    public int Status { get; set; }
    public DateTime RequestTime { get; set; }
    public DateTime ResponseTime { get; set; }
    public string ResponseMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

