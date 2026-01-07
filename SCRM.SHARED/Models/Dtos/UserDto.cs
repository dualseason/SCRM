using System.Collections.Generic;

namespace SCRM.Models.Dtos
{
    /// <summary>
    /// 用户基本信息传输对象
    /// </summary>
    public class UserDto
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public string id { get; set; } = string.Empty;

        /// <summary>
        /// 用户名
        /// </summary>
        public string userName { get; set; } = string.Empty;

        /// <summary>
        /// 电子邮箱
        /// </summary>
        public string email { get; set; } = string.Empty;

        /// <summary>
        /// 名字
        /// </summary>
        public string firstName { get; set; } = string.Empty;

        /// <summary>
        /// 姓氏
        /// </summary>
        public string lastName { get; set; } = string.Empty;

        /// <summary>
        /// 角色列表
        /// </summary>
        public List<string> roles { get; set; } = new();

        /// <summary>
        /// 权限列表
        /// </summary>
        public List<string> permissions { get; set; } = new();
    }

    /// <summary>
    /// 登录/令牌响应对象
    /// </summary>
    public class TokenResponse
    {
        /// <summary>
        /// 会话令牌
        /// </summary>
        public string token { get; set; } = string.Empty;

        /// <summary>
        /// 刷新令牌
        /// </summary>
        public string refreshToken { get; set; } = string.Empty;

        /// <summary>
        /// 过期时间戳
        /// </summary>
        public long expiresAt { get; set; }

        /// <summary>
        /// 用户信息
        /// </summary>
        public UserDto user { get; set; } = new();
        
        /// <summary>
        /// TCP服务主机地址
        /// </summary>
        public string tcpHost { get; set; } = string.Empty;

        /// <summary>
        /// TCP服务端口
        /// </summary>
        public int tcpPort { get; set; }
    }

    /// <summary>
    /// 用户权限详情对象
    /// </summary>
    public class UserPermissionInfo
    {
        /// <summary>
        /// 用户信息
        /// </summary>
        public UserDto user { get; set; } = new();

        /// <summary>
        /// 角色列表
        /// </summary>
        public List<string> roles { get; set; } = new();

        /// <summary>
        /// 权限列表
        /// </summary>
        public List<string> permissions { get; set; } = new();
    }
}
