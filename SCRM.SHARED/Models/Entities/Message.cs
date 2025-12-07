namespace SCRM.API.Models.Entities;

/// <summary>
/// 聊天消息表
/// </summary>
public class Message
{
    /// <summary>
    /// 消息ID
    /// </summary>
    public long MessageId { get; set; }

    /// <summary>
    /// 所属微信账号ID
    /// </summary>
    public long AccountId { get; set; }

    /// <summary>
    /// 服务器消息ID
    /// </summary>
    public long? MsgSvrId { get; set; }

    /// <summary>
    /// 会话ID
    /// </summary>
    public long? ConversationId { get; set; }

    /// <summary>
    /// 发送者账号ID
    /// </summary>
    public long? SenderId { get; set; }

    /// <summary>
    /// 发送者微信WXID
    /// </summary>
    public string? SenderWxid { get; set; }

    /// <summary>
    /// 接收者账号ID
    /// </summary>
    public long? ReceiverId { get; set; }

    /// <summary>
    /// 接收者微信WXID
    /// </summary>
    public string? ReceiverWxid { get; set; }

    /// <summary>
    /// 聊天类型：1-单聊 2-群聊
    /// </summary>
    public short ChatType { get; set; }

    /// <summary>
    /// 消息类型
    /// </summary>
    public short MessageType { get; set; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// XML格式的消息内容
    /// </summary>
    public string? ContentXml { get; set; }

    /// <summary>
    /// 消息方向：1-发送 2-接收
    /// </summary>
    public short Direction { get; set; }

    /// <summary>
    /// 发送状态：1-待发送 2-已发送 3-已送达 4-已读 5-失败
    /// </summary>
    public short? SendStatus { get; set; }

    /// <summary>
    /// 读取状态：0-未读 1-已读 2-已删除
    /// </summary>
    public short? ReadStatus { get; set; }

    /// <summary>
    /// 是否已撤回
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// 是否已删除
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 本地消息ID
    /// </summary>
    public string? LocalMessageId { get; set; }

    /// <summary>
    /// 客户端消息ID
    /// </summary>
    public string? ClientMsgId { get; set; }

    /// <summary>
    /// 发送时间
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// 接收时间
    /// </summary>
    public DateTime? ReceivedAt { get; set; }

    /// <summary>
    /// 读取时间
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// 撤回时间
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 导航属性：账号
    /// </summary>
    public virtual WechatAccount? Account { get; set; }

    /// <summary>
    /// 导航属性：发送者
    /// </summary>
    public virtual WechatAccount? Sender { get; set; }

    /// <summary>
    /// 导航属性：接收者
    /// </summary>
    public virtual WechatAccount? Receiver { get; set; }
}
