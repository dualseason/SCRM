namespace SCRM.API.Models.Entities;

/// <summary>
/// 鑱旂郴浜轰笌鏍囩鐨勫叧鑱旇〃
/// </summary>
public class ContactTagRelation
{
    public int Id { get; set; }
    public int ContactId { get; set; }
    public int TagId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

