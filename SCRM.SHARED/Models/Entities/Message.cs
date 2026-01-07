using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 聊天消息实体
/// </summary>
public class Message
{
    #region 属性

    /// <summary>
    /// 消息 ID (自增)
    /// </summary>
    [Column("message_id")]
    public long messageId { get; set; }

    /// <summary>
    /// 所属微信账号 ID
    /// </summary>
    [Column("account_id")]
    public long accountId { get; set; }

    /// <summary>
    /// 服务器消息 ID (MsgSvrId)
    /// </summary>
    [Column("msg_svr_id")]
    public long? msgSvrId { get; set; }

    /// <summary>
    /// 会话 ID
    /// </summary>
    [Column("conversation_id")]
    public long? conversationId { get; set; }

    /// <summary>
    /// 发送者用户 ID (后端系统用户)
    /// </summary>
    [Column("sender_id")]
    public long? senderId { get; set; }

    /// <summary>
    /// 发送者微信 WXID
    /// </summary>
    [Column("sender_wxid")]
    public string? senderWxid { get; set; }

    /// <summary>
    /// 接收者用户 ID (后端系统用户)
    /// </summary>
    [Column("receiver_id")]
    public long? receiverId { get; set; }

    /// <summary>
    /// 接收者微信 WXID
    /// </summary>
    [Column("receiver_wxid")]
    public string? receiverWxid { get; set; }

    /// <summary>
    /// 聊天类型 (1-单聊, 2-群聊)
    /// </summary>
    [Column("chat_type")]
    public short chatType { get; set; }

    /// <summary>
    /// 消息类型 (文本、图片、链接等)
    /// </summary>
    [Column("message_type")]
    public short messageType { get; set; }

    /// <summary>
    /// 消息内容
    /// </summary>
    [Column("content")]
    public string? content { get; set; }

    /// <summary>
    /// XML 格式的消息原始内容
    /// </summary>
    [Column("content_xml")]
    public string? contentXml { get; set; }

    /// <summary>
    /// 消息方向 (1-发送, 2-接收)
    /// </summary>
    [Column("direction")]
    public short direction { get; set; }

    /// <summary>
    /// 发送状态 (1-待发送, 2-已发送, 3-已送达, 4-已读, 5-失败)
    /// </summary>
    [Column("send_status")]
    public short? sendStatus { get; set; }

    /// <summary>
    /// 读取状态 (0-未读, 1-已读, 2-已删除)
    /// </summary>
    [Column("read_status")]
    public short? readStatus { get; set; }

    /// <summary>
    /// 是否已撤回
    /// </summary>
    [Column("is_revoked")]
    public bool isRevoked { get; set; }

    /// <summary>
    /// 是否已删除
    /// </summary>
    [Column("is_deleted")]
    public bool isDeleted { get; set; }

    /// <summary>
    /// 本地消息 ID
    /// </summary>
    [Column("local_message_id")]
    public string? localMessageId { get; set; }

    /// <summary>
    /// 客户端消息 ID
    /// </summary>
    [Column("client_msg_id")]
    public string? clientMsgId { get; set; }

    /// <summary>
    /// 发送时间
    /// </summary>
    [Column("sent_at")]
    public DateTime? sentAt { get; set; }

    /// <summary>
    /// 接收时间
    /// </summary>
    [Column("received_at")]
    public DateTime? receivedAt { get; set; }

    /// <summary>
    /// 读取时间
    /// </summary>
    [Column("read_at")]
    public DateTime? readAt { get; set; }

    /// <summary>
    /// 撤回时间
    /// </summary>
    [Column("revoked_at")]
    public DateTime? revokedAt { get; set; }

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

    #endregion

    #region 导航属性

    /// <summary>
    /// 关联的微信账号
    /// </summary>
    public virtual WechatAccount? account { get; set; }

    /// <summary>
    /// 消息发送者
    /// </summary>
    public virtual WechatAccount? sender { get; set; }

    /// <summary>
    /// 消息接收者
    /// </summary>
    public virtual WechatAccount? receiver { get; set; }

    #endregion
}
