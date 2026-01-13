using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SCRM.API.Models.DTOs;
using SCRM.SHARED.Models;

namespace SCRM.API.Models.Entities;

/// <summary>
/// 手机客户端设备实体
/// </summary>
[Table("sr_clients")]
public class SrClient : ICacheable<SrClient>
{
    /// <summary>
    /// 设备的唯一标识 (UUID)
    /// </summary>
    [Key]
    [Column("uuid")]
    public string uuid { get; set; } = string.Empty;

    /// <summary>
    /// TCP 服务主机地址
    /// </summary>
    [Column("tcp_host")]
    public string tcpHost { get; set; } = string.Empty;

    /// <summary>
    /// TCP 服务端口
    /// </summary>
    [Column("tcp_port")]
    public int tcpPort { get; set; }

    /// <summary>
    /// 设备详细信息提示消息 (Protobuf 类型)
    /// </summary>
    [Column("device", TypeName = "jsonb")]
    public Jubo.JuLiao.IM.Wx.Proto.PostDeviceInfoNoticeMessage device { get; set; } = new();

    /// <summary>
    /// 设备的 IP 地址
    /// </summary>
    [Column("ip")]
    public string? ip { get; set; }

    /// <summary>
    /// 最后登录时间
    /// </summary>
    [Column("last_login_at")]
    public DateTime? lastLoginAt { get; set; }

    /// <summary>
    /// 是否在线
    /// </summary>
    [Column("is_online")]
    public bool isOnline { get; set; }

    /// <summary>
    /// 设备专属配置 (JSON格式)
    /// 用于覆盖全局默认配置
    /// </summary>
    [Column("custom_configs", TypeName = "jsonb")]
    public string? customConfigs { get; set; }

    /// <summary>
    /// 设备状态
    /// </summary>
    [Column("status")]
    public int status { get; set; }
    
    /// <summary>
    /// 归属用户 ID (RBAC)
    /// </summary>
    [Column("owner_id")]
    public string? ownerId { get; set; }

    /// <summary>
    /// 归属用户导航属性
    /// </summary>
    [ForeignKey("ownerId")]
    public virtual ApplicationUser? owner { get; set; }

    /// <summary>
    /// SignalR 连接 ID (用于精准推送)
    /// </summary>
    [Column("connection_id")]
    public string? connectionId { get; set; }

    /// <summary>
    /// 绑定的微信 ID (非持久化)
    /// </summary>
    [NotMapped]
    public string? weChatId { get; set; }

    /// <summary>
    /// 绑定的微信昵称 (非持久化)
    /// </summary>
    [NotMapped]
    public string? weChatNick { get; set; }

    /// <summary>
    /// 绑定的微信头像 (非持久化)
    /// </summary>
    [NotMapped]
    public string? weChatAvatar { get; set; }

    /// <summary>
    /// 微信号 (非持久化)
    /// </summary>
    [NotMapped]
    public string? wechatNumber { get; set; }

    /// <summary>
    /// 关联的微信账号 ID (非持久化)
    /// </summary>
    [NotMapped]
    public long? wechatAccountId { get; set; }

    /// <summary>
    /// 关联的微信账号列表
    /// </summary>
    public virtual ICollection<WechatAccount> accounts { get; set; } = new List<WechatAccount>();

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
    /// 鉴权 Token (非持久化)
    /// </summary>
    [NotMapped]
    public string? token { get; set; }



    public string GetId() => uuid;

    public SrClient CopyFrom(SrClient other)
    {
        this.tcpHost = other.tcpHost;
        this.tcpPort = other.tcpPort;
        this.device = other.device;
        this.ip = other.ip;
        this.lastLoginAt = other.lastLoginAt;
        this.isOnline = other.isOnline;
        this.status = other.status;
        this.ownerId = other.ownerId;
        this.connectionId = other.connectionId;
        this.updatedAt = DateTime.UtcNow;
        
        return this;
    }
}

