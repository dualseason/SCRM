using System;

namespace SCRM.SHARED.Models.Dtos;

/// <summary>
/// 微信联系人标签展示数据。
/// <para>
/// 标签字典由 Android 端 ContactLabelInfoNotice / ContactLabelAddNotice 回传后写入 ContactTags；
/// Web 页面只读取该快照，不直接修改数据库。
/// </para>
/// </summary>
public class ContactLabelDto
{
    /// <summary>数据库自增 ID。</summary>
    public int id { get; set; }

    /// <summary>所属微信账号 wxid。</summary>
    public string ownerWxid { get; set; } = string.Empty;

    /// <summary>所属微信账号数值键。</summary>
    public long wechatAccountId { get; set; }

    /// <summary>微信端标签 ID。</summary>
    public int labelId { get; set; }

    /// <summary>标签名称。</summary>
    public string tagName { get; set; } = string.Empty;

    /// <summary>标签颜色；当前 62203 主链一般为空，预留给后续 UI。</summary>
    public string tagColor { get; set; } = string.Empty;

    /// <summary>标签描述；当前 62203 主链一般为空，预留给后续 UI。</summary>
    public string tagDescription { get; set; } = string.Empty;

    /// <summary>创建时间。</summary>
    public DateTime createdAt { get; set; }

    /// <summary>更新时间。</summary>
    public DateTime updatedAt { get; set; }

    /// <summary>是否已软删除。</summary>
    public bool isDeleted { get; set; }
}
