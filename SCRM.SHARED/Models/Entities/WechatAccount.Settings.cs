using System.Text.Json;
using SCRM.SHARED.Models;

namespace SCRM.API.Models.Entities
{
    public partial class WechatAccount
    {
        /// <summary>
        /// 获取结构化的配置对象
        /// </summary>
        public WechatAccountSettings GetSettings()
        {
            if (string.IsNullOrEmpty(this.settings))
            {
                return new WechatAccountSettings();
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                return JsonSerializer.Deserialize<WechatAccountSettings>(this.settings, options) ?? new WechatAccountSettings();
            }
            catch
            {
                return new WechatAccountSettings();
            }
        }

        /// <summary>
        /// 更新配置并设置更新时间
        /// </summary>
        public void UpdateSettings(WechatAccountSettings newSettings)
        {
            if (newSettings == null) throw new ArgumentNullException(nameof(newSettings));

            var options = new JsonSerializerOptions
            {
                WriteIndented = false // Compact JSON for DB storage
            };
            this.settings = JsonSerializer.Serialize(newSettings, options);
            this.updatedAt = DateTime.UtcNow;
        }
    }
}
