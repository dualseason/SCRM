namespace SCRM.API.Models.Entities;

/// <summary>
/// 灏忕▼搴忚闂褰曡〃
/// </summary>
public class MiniprogramAccessLog
{
    public int Id { get; set; }
    public int MiniprogramAccountId { get; set; }
    public string AccessorWxid { get; set; }
    public int PageId { get; set; }
    public string PagePath { get; set; }
    public int StayDuration { get; set; }
    public string AccessSource { get; set; }
    public DateTime AccessTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

