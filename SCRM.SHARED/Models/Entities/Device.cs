namespace SCRM.API.Models.Entities;

/// <summary>
/// 设备信息表
/// </summary>
public class Device
{
    /// <summary>
    /// 设备ID (主键)
    /// </summary>
    public long DeviceId { get; set; }

    /// <summary>
    /// 设备ID 别名 (与 DeviceId 相同，用于兼容性)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long Id
    {
        get { return DeviceId; }
        set { DeviceId = value; }
    }

    /// <summary>
    /// 设备唯一标识（UUID/IMEI等）
    /// </summary>
    public string DeviceUuid { get; set; } = string.Empty;

    /// <summary>
    /// 设备类型：1-Android手机 2-iOS手机 3-Windows客服端 4-MacOS客服端 5-Web客服端
    /// </summary>
    public short DeviceType { get; set; }

    /// <summary>
    /// 设备名称
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// 操作系统类型：Android/iOS/Windows/MacOS
    /// </summary>
    public string? OsType { get; set; }

    /// <summary>
    /// 操作系统版本
    /// </summary>
    public string? OsVersion { get; set; }

    /// <summary>
    /// 设备型号
    /// </summary>
    public string? DeviceModel { get; set; }

    /// <summary>
    /// 设备品牌
    /// </summary>
    public string? DeviceBrand { get; set; }

    /// <summary>
    /// 当前SDK版本
    /// </summary>
    public string? SdkVersion { get; set; }

    /// <summary>
    /// APP版本
    /// </summary>
    public string? AppVersion { get; set; }

    /// <summary>
    /// 是否激活
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 是否删除
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 删除时间
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// 导航属性：设备授权
    /// </summary>
    public virtual ICollection<DeviceAuthorization> DeviceAuthorizations { get; set; } = new List<DeviceAuthorization>();

    /// <summary>
    /// 导航属性：设备命令
    /// </summary>
    public virtual ICollection<DeviceCommand> DeviceCommands { get; set; } = new List<DeviceCommand>();

    /// <summary>
    /// 导航属性：设备心跳
    /// </summary>
    public virtual ICollection<DeviceHeartbeat> DeviceHeartbeats { get; set; } = new List<DeviceHeartbeat>();

    /// <summary>
    /// 导航属性：设备位置
    /// </summary>
    public virtual ICollection<DeviceLocation> DeviceLocations { get; set; } = new List<DeviceLocation>();

    /// <summary>
    /// 导航属性：设备状态日志
    /// </summary>
    public virtual ICollection<DeviceStatusLog> DeviceStatusLogs { get; set; } = new List<DeviceStatusLog>();

    /// <summary>
    /// 导航属性：设备版本日志
    /// </summary>
    public virtual ICollection<DeviceVersionLog> DeviceVersionLogs { get; set; } = new List<DeviceVersionLog>();

    /// <summary>
    /// 导航属性：服务器重定向
    /// </summary>
    public virtual ICollection<ServerRedirect> ServerRedirects { get; set; } = new List<ServerRedirect>();

    /// <summary>
    /// 导航属性：系统通知
    /// </summary>
    public virtual ICollection<SystemNotification> SystemNotifications { get; set; } = new List<SystemNotification>();

    /// <summary>
    /// 当前连接ID (非数据库字段)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? ConnectionId { get; set; }
}
