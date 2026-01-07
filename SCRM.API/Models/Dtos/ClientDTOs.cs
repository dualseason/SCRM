using System.Text.Json.Serialization;

namespace SCRM.API.Models.DTOs
{
    /// <summary>
    /// 通用 API 响应包装类
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public class ApiResponse<T>
    {
        /// <summary>
        /// 业务状态码 (0 表示成功)
        /// </summary>
        [JsonPropertyName("bizCode")]
        public int code { get; set; }

        /// <summary>
        /// 响应消息
        /// </summary>
        [JsonPropertyName("msg")]
        public string message { get; set; } = string.Empty;

        /// <summary>
        /// 响应数据
        /// </summary>
        [JsonPropertyName("data")]
        public T data { get; set; } = default!;

        public static ApiResponse<T> Success(T data, string message = "Success")
        {
            return new ApiResponse<T> { code = 0, message = message, data = data };
        }

        public static ApiResponse<T> Fail(int code, string message)
        {
            return new ApiResponse<T> { code = code, message = message, data = default! };
        }
    }

    /// <summary>
    /// 用户搜索/设置参数
    /// </summary>
    public class UserSearchParams
    {
        /// <summary>
        /// 服务器 URL
        /// </summary>
        public string serverUrl { get; set; } = string.Empty;

        /// <summary>
        /// 心跳数据
        /// </summary>
        public string heartbeatData { get; set; } = string.Empty;

        /// <summary>
        /// 心跳间隔 (秒)
        /// </summary>
        public int interval { get; set; }
    }

    /// <summary>
    /// 用户认证令牌详情
    /// </summary>
    public class UserAuthToken
    {
        /// <summary>
        /// 用户 ID
        /// </summary>
        public string userId { get; set; } = string.Empty;

        /// <summary>
        /// 会话令牌
        /// </summary>
        public string token { get; set; } = string.Empty;

        /// <summary>
        /// TCP 服务主机
        /// </summary>
        public string tcpHost { get; set; } = string.Empty;

        /// <summary>
        /// TCP 服务端口
        /// </summary>
        public int tcpPort { get; set; }
    }

    /// <summary>
    /// 用户认证信息
    /// </summary>
    public class UserAuthInfo
    {
        /// <summary>
        /// 用户标识 (如用户名或 Email)
        /// </summary>
        public string userIdentifier { get; set; } = string.Empty;
    }
}
