namespace SCRM.API.Models.Entities;

/// <summary>
/// 联系人标签实体
/// </summary>
public class ContactTag
{
    /// <summary>
    /// 自增 ID
    /// </summary>
    public int id { get; set; }

    /// <summary>
    /// 所属微信账号 ID
    /// </summary>
    public long wechatAccountId { get; set; }

    /// <summary>
    /// 标签名称
    /// </summary>
    public string tagName { get; set; } = string.Empty;

    /// <summary>
    /// 微信端的标签 ID
    /// </summary>
    public int labelId { get; set; }

    /// <summary>
    /// 标签颜色
    /// </summary>
    public string tagColor { get; set; } = string.Empty;

    /// <summary>
    /// 标签描述
    /// </summary>
    public string tagDescription { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime updatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 是否已删除
    /// </summary>
    public bool isDeleted { get; set; }
}
