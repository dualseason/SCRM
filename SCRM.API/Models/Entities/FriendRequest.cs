using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 好友请求实体
/// </summary>
public class FriendRequest
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    [Column("Id")]
    public int id { get; set; }

    /// <summary>
    /// 所属微信账号 ID
    /// </summary>
    [Column("WechatAccountId")]
    public long wechatAccountId { get; set; }

    /// <summary>
    /// 请求者 WXID
    /// </summary>
    [Column("RequestWxid")]
    public string requestWxid { get; set; } = string.Empty;

    /// <summary>
    /// 昵称
    /// </summary>
    [Column("Nickname")]
    public string? nickname { get; set; }

    /// <summary>
    /// 头像 URL
    /// </summary>
    [Column("Avatar")]
    public string? avatar { get; set; }

    /// <summary>
    /// 性别 (0-未知, 1-男, 2-女)
    /// </summary>
    [Column("Gender")]
    public int? gender { get; set; }

    /// <summary>
    /// 地区
    /// </summary>
    [Column("Region")]
    public string? region { get; set; }

    /// <summary>
    /// 来源 (如：搜索, 扫码)
    /// </summary>
    [Column("Source")]
    public string? source { get; set; }

    /// <summary>
    /// 请求留言内容
    /// </summary>
    [Column("RequestMessage")]
    public string requestMessage { get; set; } = string.Empty;

    /// <summary>
    /// 状态 (0-待处理, 1-已通过, 2-已拒绝, 3-已忽略)
    /// </summary>
    [Column("Status")]
    public int status { get; set; }

    /// <summary>
    /// 请求发起时间
    /// </summary>
    [Column("RequestTime")]
    public DateTime requestTime { get; set; }

    /// <summary>
    /// 响应处理时间
    /// </summary>
    [Column("ResponseTime")]
    public DateTime responseTime { get; set; }

    /// <summary>
    /// 响应结果留言
    /// </summary>
    [Column("ResponseMessage")]
    public string responseMessage { get; set; } = string.Empty;

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
