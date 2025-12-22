using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;

namespace SCRM.API.Models
{
    public class GlobalCache
    {
        // 应用程序用户缓存
        public static ConcurrentDictionary<string, ApplicationUser> applicationUsers { get; set; } = new();

        // 客户端设备缓存 (SrClient)
        public static ConcurrentDictionary<string, SrClient> srClients { get; set; } = new();

        // 微信账号缓存 (WechatAccount) - Key: AccountId
        public static ConcurrentDictionary<long, WechatAccount> wechatAccounts { get; set; } = new();

        /// <summary>
        /// 全局统一的 JSON 序列化选项 (CamelCase + CaseInsensitive)
        /// 用于手动序列化/反序列化场景，保持与 Web API Controller 一致的行为
        /// </summary>
        public static readonly JsonSerializerOptions GlobalJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
    }
}
