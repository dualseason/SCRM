namespace SCRM.API.Models.DTOs
{
    /// <summary>
    /// 手机注册请求数据传输对象
    /// </summary>
    public class PhoneRegDto
    {
        /// <summary>
        /// 设备的 IMEI 码
        /// </summary>
        public string imei { get; set; } = string.Empty;

        /// <summary>
        /// 注册码 (可能包含 Email 或旧版注册码)
        /// </summary>
        public string regCode { get; set; } = string.Empty;

        /// <summary>
        /// 设备的 MAC 地址
        /// </summary>
        public string mac { get; set; } = string.Empty;

        /// <summary>
        /// 回传 Key (用于标识特定请求)
        /// </summary>
        public string returnKey { get; set; } = string.Empty;

        /// <summary>
        /// 硬件厂商 (Manufacturer)
        /// </summary>
        public string hsman { get; set; } = string.Empty;

        /// <summary>
        /// 硬件型号 (Model)
        /// </summary>
        public string hstype { get; set; } = string.Empty;

        /// <summary>
        /// Android API 版本
        /// </summary>
        public string androidApi { get; set; } = string.Empty;

        /// <summary>
        /// Android ID
        /// </summary>
        public string androidId { get; set; } = string.Empty;

        /// <summary>
        /// 应用程序版本号
        /// </summary>
        public int versionCode { get; set; }

        /// <summary>
        /// 应用程序包名
        /// </summary>
        public string packageName { get; set; } = string.Empty;

        /// <summary>
        /// 用户电子邮箱
        /// </summary>
        public string userEmail { get; set; } = string.Empty;
    }
}
