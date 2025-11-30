namespace SCRM.API.Models.Entities;

/// <summary>
/// 鑱旂郴浜哄彉鏇存棩蹇楄〃
/// </summary>
public class ContactChangeLog
{
    public int Id { get; set; }
    public int ContactId { get; set; }
    public string ChangeType { get; set; }
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public string ChangedField { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

