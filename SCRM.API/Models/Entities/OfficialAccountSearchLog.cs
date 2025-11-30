namespace SCRM.API.Models.Entities;

/// <summary>
/// 鍏紬鍙锋悳绱㈣褰曡〃
/// </summary>
public class OfficialAccountSearchLog
{
    public int Id { get; set; }
    public int WechatAccountId { get; set; }
    public string SearchKeyword { get; set; }
    public int SearchResultCount { get; set; }
    public string SelectedWxid { get; set; }
    public int FollowAction { get; set; }
    public DateTime SearchTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

