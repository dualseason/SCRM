namespace SCRM.API.Models.Entities;

/// <summary>
/// 娑堟伅鎵╁睍淇℃伅琛?
/// </summary>
public class MessageExtension
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public string ExtensionKey { get; set; }
    public string ExtensionValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

