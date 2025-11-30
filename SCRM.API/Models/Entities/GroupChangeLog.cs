namespace SCRM.API.Models.Entities;

/// <summary>
/// 缇ゅ彉鏇存棩蹇楄〃
/// </summary>
public class GroupChangeLog
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string ChangeType { get; set; }
    public string ChangedBy { get; set; }
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public string ChangedField { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

