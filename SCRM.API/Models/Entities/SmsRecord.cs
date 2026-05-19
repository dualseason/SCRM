using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 手机短信记录。
/// <para>数据来自 Android 端 SmsPushNotice、PullSmsTaskResultNotice、SmsReadNotice、SmsSentNotice。</para>
/// </summary>
[Table("SmsRecords")]
public class SmsRecord
{
    #region 基础字段

    /// <summary>
    /// 自增主键。
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属微信账号 wxid。
    /// </summary>
    [Column("OwnerWxid")]
    public string ownerWxid { get; set; } = string.Empty;

    /// <summary>
    /// 设备 IMEI。部分设备可能为空；查询端应传空字符串表示不过滤 IMEI，不要用设备 UUID 代替。
    /// </summary>
    [Column("Imei")]
    public string imei { get; set; } = string.Empty;

    /// <summary>
    /// Android 短信库原始 ID。
    /// </summary>
    [Column("SmsId")]
    public int smsId { get; set; }

    /// <summary>
    /// Android 短信会话线程 ID。
    /// </summary>
    [Column("ThreadId")]
    public int threadId { get; set; }

    #endregion

    #region 短信内容

    /// <summary>
    /// 对方号码。
    /// </summary>
    [Column("Number")]
    public string number { get; set; } = string.Empty;

    /// <summary>
    /// Android 短信类型，通常 1=收件箱，2=已发送。
    /// </summary>
    [Column("Type")]
    public int type { get; set; }

    /// <summary>
    /// Android 原始时间戳，保留秒/毫秒原值便于排查。
    /// </summary>
    [Column("RawDate")]
    public long rawDate { get; set; }

    /// <summary>
    /// 短信时间，统一转换为 UTC。
    /// </summary>
    [Column("SmsTime")]
    public DateTime smsTime { get; set; }

    /// <summary>
    /// 短信正文。
    /// </summary>
    [Column("Content")]
    public string content { get; set; } = string.Empty;

    /// <summary>
    /// 是否已读。
    /// </summary>
    [Column("IsRead")]
    public bool isRead { get; set; }

    /// <summary>
    /// SIM 卡 ID。
    /// </summary>
    [Column("SimId")]
    public int simId { get; set; }

    /// <summary>
    /// 拦截类型。
    /// </summary>
    [Column("BlockType")]
    public int blockType { get; set; }

    #endregion

    #region 状态补充

    /// <summary>
    /// SmsSentNotice 上报的类型/状态值。
    /// </summary>
    [Column("SentNoticeType")]
    public int sentNoticeType { get; set; }

    /// <summary>
    /// 是否收到发送状态通知。
    /// </summary>
    [Column("IsSentNoticeReceived")]
    public bool isSentNoticeReceived { get; set; }

    /// <summary>
    /// 已读通知时间。
    /// </summary>
    [Column("ReadAt")]
    public DateTime? readAt { get; set; }

    /// <summary>
    /// 发送状态通知时间。
    /// </summary>
    [Column("SentNoticeAt")]
    public DateTime? sentNoticeAt { get; set; }

    /// <summary>
    /// 数据来源：Push / Pull / ReadNotice / SentNotice。
    /// </summary>
    [Column("Source")]
    public string source { get; set; } = string.Empty;

    #endregion

    #region 时间字段

    /// <summary>
    /// 创建时间。
    /// </summary>
    [Column("CreatedAt")]
    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间。
    /// </summary>
    [Column("UpdatedAt")]
    public DateTime updatedAt { get; set; } = DateTime.UtcNow;

    #endregion
}
