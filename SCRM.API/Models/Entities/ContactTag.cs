namespace SCRM.API.Models.Entities;

/// <summary>
/// 鑱旂郴浜烘爣绛捐〃
/// </summary>
public class ContactTag
{
    public int Id { get; set; }
    public int WechatAccountId { get; set; }
    public string TagName { get; set; }
    public string TagColor { get; set; }
    public string TagDescription { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

