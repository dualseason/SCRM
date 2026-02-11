using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 会话实体
/// </summary>
public class Conversation
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    [Column("id")]
    public int id { get; set; }

    /// <summary>
    /// 所属微信账号 ID
    /// </summary>
    [Column("wechat_account_id")]
    public string wechatAccountId { get; set; }

    /// <summary>
    /// 会话对方的 WXID (好友 ID 或群聊 ID)
    /// </summary>
    [Column("conversation_wxid")]
    public string conversationWxid { get; set; } = string.Empty;

    /// <summary>
    /// 会话类型 (1-单聊, 2-群聊)
    /// </summary>
    [Column("conversation_type")]
    public int conversationType { get; set; }

    /// <summary>
    /// 会话显示名称
    /// </summary>
    [Column("display_name")]
    public string displayName { get; set; } = string.Empty;

    /// <summary>
    /// 会话头像 URL
    /// </summary>
    [Column("display_avatar")]
    public string displayAvatar { get; set; } = string.Empty;

    /// <summary>
    /// 未读消息计数
    /// </summary>
    [Column("unread_count")]
    public int unreadCount { get; set; }

    /// <summary>
    /// 总消息计数
    /// </summary>
    [Column("message_count")]
    public int messageCount { get; set; }

    /// <summary>
    /// 是否置顶 (0-否, 1-是)
    /// </summary>
    [Column("is_pinned")]
    public int isPinned { get; set; }

    /// <summary>
    /// 是否免打扰 (0-否, 1-是)
    /// </summary>
    [Column("is_muted")]
    public int isMuted { get; set; }

    /// <summary>
    /// 最后一条消息内容预览
    /// </summary>
    [Column("last_message_content")]
    public string? lastMessageContent { get; set; }

    /// <summary>
    /// 最后一条消息时间
    /// </summary>
    [Column("last_message_time")]
    public DateTime lastMessageTime { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("created_at")]
    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    [Column("updated_at")]
    public DateTime updatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 是否已删除
    /// </summary>
    [Column("is_deleted")]
    public bool isDeleted { get; set; }
}
