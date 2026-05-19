namespace SCRM.SHARED.Models.Dtos;

/// <summary>
/// 手机短信记录 DTO。
/// </summary>
public class SmsRecordDto
{
    public int id { get; set; }
    public string ownerWxid { get; set; } = string.Empty;
    public string imei { get; set; } = string.Empty;
    public int smsId { get; set; }
    public int threadId { get; set; }
    public string number { get; set; } = string.Empty;
    public int type { get; set; }
    public long rawDate { get; set; }
    public DateTime smsTime { get; set; }
    public string content { get; set; } = string.Empty;
    public bool isRead { get; set; }
    public int simId { get; set; }
    public int blockType { get; set; }
    public int sentNoticeType { get; set; }
    public bool isSentNoticeReceived { get; set; }
    public DateTime? readAt { get; set; }
    public DateTime? sentNoticeAt { get; set; }
    public string source { get; set; } = string.Empty;
    public DateTime createdAt { get; set; }
    public DateTime updatedAt { get; set; }
}

/// <summary>
/// 手机通话记录 DTO。
/// </summary>
public class CallLogRecordDto
{
    public int id { get; set; }
    public string ownerWxid { get; set; } = string.Empty;
    public string imei { get; set; } = string.Empty;
    public int callLogId { get; set; }
    public string number { get; set; } = string.Empty;
    public int type { get; set; }
    public long rawDate { get; set; }
    public DateTime callTime { get; set; }
    public int durationSeconds { get; set; }
    /// <summary>
    /// 当前用户是否可申请录音播放 token。
    /// <para>前端只用该字段决定是否展示播放按钮；真实 recordUrl 不应随列表下发。</para>
    /// </summary>
    public bool hasRecording { get; set; }
    public string recordUrl { get; set; } = string.Empty;
    public int simId { get; set; }
    public int blockType { get; set; }
    public string source { get; set; } = string.Empty;
    public DateTime createdAt { get; set; }
    public DateTime updatedAt { get; set; }
}
