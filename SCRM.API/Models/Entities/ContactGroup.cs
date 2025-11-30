namespace SCRM.API.Models.Entities;

/// <summary>
/// 鑱旂郴浜哄垎缁勮〃
/// </summary>
public class ContactGroup
{
    public int Id { get; set; }
    public int WechatAccountId { get; set; }
    public string GroupName { get; set; }
    public int GroupOrder { get; set; }
    public string GroupDescription { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

