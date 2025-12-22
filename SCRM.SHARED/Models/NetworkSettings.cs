namespace SCRM.SHARED.Models
{
    /// <summary>
    /// 网络配置模型，支持从 appsettings.json 载入
    /// </summary>
    public class NetworkSettings
    {
        public const string SectionName = "NetworkSettings";

        /// <summary>
        /// API 基础 URL (如 http://localhost:42718)
        /// </summary>
        public string ApiBaseUrl { get; set; } = "http://localhost:42718";

        /// <summary>
        /// Android 模拟器专用的 API 基础 URL
        /// </summary>
        public string AndroidApiBaseUrl { get; set; } = "http://10.0.2.2:42718";

        /// <summary>
        /// 根据平台获取有效的 API 地址
        /// </summary>
        /// <param name="isAndroid">是否为 Android 平台</param>
        /// <returns>API 地址</returns>
        public string GetEffectiveApiUrl(bool isAndroid)
        {
            return isAndroid ? AndroidApiBaseUrl : ApiBaseUrl;
        }
    }
}
