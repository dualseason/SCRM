namespace SCRM.API.Models.Entities;

/// <summary>
/// 娑堟伅濯掍綋琛?
/// </summary>
public class MessageMedia
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public int MediaType { get; set; }
    public string MediaUrl { get; set; }
    public string LocalPath { get; set; }
    public string MediaHash { get; set; }
    public long FileSize { get; set; }
    public string FileExtension { get; set; }
    public int UploadStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

