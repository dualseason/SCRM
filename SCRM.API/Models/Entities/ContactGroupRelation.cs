namespace SCRM.API.Models.Entities;

/// <summary>
/// 鑱旂郴浜轰笌鍒嗙粍鐨勫叧鑱旇〃
/// </summary>
public class ContactGroupRelation
{
    public int Id { get; set; }
    public int ContactId { get; set; }
    public int GroupId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

