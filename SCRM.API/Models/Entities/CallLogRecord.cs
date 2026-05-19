using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 手机通话记录。
/// <para>数据来自 Android 端 CallLogPushNotice 与 PullCallLogTaskResultNotice。</para>
/// </summary>
[Table("CallLogRecords")]
public class CallLogRecord
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
    /// Android 通话记录原始 ID。
    /// </summary>
    [Column("CallLogId")]
    public int callLogId { get; set; }

    #endregion

    #region 通话内容

    /// <summary>
    /// 对方号码。
    /// </summary>
    [Column("Number")]
    public string number { get; set; } = string.Empty;

    /// <summary>
    /// Android 通话类型，通常 1=来电，2=去电，3=未接。
    /// </summary>
    [Column("Type")]
    public int type { get; set; }

    /// <summary>
    /// Android 原始时间戳，保留秒/毫秒原值便于排查。
    /// </summary>
    [Column("RawDate")]
    public long rawDate { get; set; }

    /// <summary>
    /// 通话时间，统一转换为 UTC。
    /// </summary>
    [Column("CallTime")]
    public DateTime callTime { get; set; }

    /// <summary>
    /// 通话时长，单位秒。
    /// </summary>
    [Column("DurationSeconds")]
    public int durationSeconds { get; set; }

    /// <summary>
    /// 通话录音 URL 或本地路径。Android 上传成功后通常写入 URL。
    /// </summary>
    [Column("RecordUrl")]
    public string recordUrl { get; set; } = string.Empty;

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

    /// <summary>
    /// 数据来源：Push / Pull。
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
