namespace SCRM.API.Models.Entities;

/// <summary>
/// 璇煶杞枃瀛楁棩蹇楄〃
/// </summary>
public class VoiceToTextLog
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public string VoiceUrl { get; set; }
    public string TranscribedText { get; set; }
    public int TranscribeStatus { get; set; }
    public double Accuracy { get; set; }
    public string ErrorMessage { get; set; }
    public DateTime TranscribeTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

