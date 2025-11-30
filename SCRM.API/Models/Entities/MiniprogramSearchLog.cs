namespace SCRM.API.Models.Entities;

/// <summary>
/// 灏忕▼搴忔悳绱㈣褰曡〃
/// </summary>
public class MiniprogramSearchLog
{
    public int Id { get; set; }
    public int WechatAccountId { get; set; }
    public string SearchKeyword { get; set; }
    public int SearchResultCount { get; set; }
    public string SelectedAppId { get; set; }
    public int AccessAction { get; set; }
    public DateTime SearchTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

