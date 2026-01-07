using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 语音转文字日志实体
/// </summary>
[Table("VoiceToTextLogs")]
public class VoiceToTextLog
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属消息 ID
    /// </summary>
    [Column("MessageId")]
    public int messageId { get; set; }

    /// <summary>
    /// 语音文件 URL
    /// </summary>
    [Column("VoiceUrl")]
    public string voiceUrl { get; set; } = string.Empty;

    /// <summary>
    /// 转录后的文字内容
    /// </summary>
    [Column("TranscribedText")]
    public string transcribedText { get; set; } = string.Empty;

    /// <summary>
    /// 转录状态
    /// </summary>
    [Column("TranscribeStatus")]
    public int transcribeStatus { get; set; }

    /// <summary>
    /// 准确率
    /// </summary>
    [Column("Accuracy")]
    public double accuracy { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    [Column("ErrorMessage")]
    public string errorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 转录时间
    /// </summary>
    [Column("TranscribeTime")]
    public DateTime transcribeTime { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("CreatedAt")]
    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    [Column("UpdatedAt")]
    public DateTime updatedAt { get; set; } = DateTime.UtcNow;
}


