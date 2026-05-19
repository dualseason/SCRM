using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.Services.Data;
using SCRM.UI.Services;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace SCRM.API.Services
{
    public class ServerSystemConfigService : ISystemConfigService
    {
        /// <summary>
        /// 系统配置模型读取别名。
        /// 保存模型时会同步写入这些旧键；读取模型时也必须按同样顺序回退，
        /// 避免旧库只有 tcpServerHost/fileUploadUrl 等键时，Settings 页面显示模型默认值并在保存时覆盖真实配置。
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string[]> ModelReadAliases = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["host"] = new[] { "tcpServerHost" },
            ["port"] = new[] { "server_port", "tcpServerPort" },
            ["httpApiBaseUrl"] = new[] { "apiBaseUrl" },
            ["fileUpUrl"] = new[] { "fileUploadUrl" }
        };

        private readonly ApplicationDbContext _db;

        public ServerSystemConfigService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<SystemConfig>> GetConfigsAsync()
        {
            return await _db.SystemConfigs.AsNoTracking().ToListAsync();
        }

        public async Task<SystemConfig?> GetConfigByKeyAsync(string key)
        {
            return await _db.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.key == key);
        }

        public async Task<SystemConfig?> UpdateConfigAsync(SystemConfig config)
        {
            var existing = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.key == config.key);
            
            if (existing != null)
            {
                existing.value = config.value;
                existing.description = config.description;
                existing.updatedAt = DateTime.UtcNow;
                _db.SystemConfigs.Update(existing);
            }
            else
            {
                // config.createdAt = DateTime.UtcNow; // Entity might not have createdAt, checking again...
                config.updatedAt = DateTime.UtcNow;
                await _db.SystemConfigs.AddAsync(config);
                existing = config;
            }

            await _db.SaveChangesAsync();
            return existing;
        }
        public async Task<SCRM.SHARED.Models.SystemConfigModel> GetConfigModelAsync()
        {
            var rawConfigs = (await GetConfigsAsync()).ToList();
            var model = new SCRM.SHARED.Models.SystemConfigModel();
            
            // 属性名仍是主键；读取时额外支持历史别名回退，保持与 UpdateModelAsync 的别名同步机制对称。
            foreach (var prop in typeof(SCRM.SHARED.Models.SystemConfigModel).GetProperties())
            {
                var config = FindModelConfig(rawConfigs, prop);
                if (config != null)
                {
                    if (prop.PropertyType == typeof(bool))
                        prop.SetValue(model, bool.TryParse(config.value, out var b) ? b : false);
                    else if (prop.PropertyType == typeof(int))
                        prop.SetValue(model, int.TryParse(config.value, out var i) ? i : 0);
                    else
                        prop.SetValue(model, config.value);
                }
            }
            return model;
        }

        /// <summary>
        /// 按“主键 -> 历史别名”的顺序查找模型配置。
        /// <para>
        /// 若主键存在但值为空或格式不可用，会继续尝试别名；全部不可用时才返回第一个命中的配置，
        /// 以保持旧逻辑对空值/非法值的兼容表现。
        /// </para>
        /// </summary>
        private static SystemConfig? FindModelConfig(IReadOnlyCollection<SystemConfig> rawConfigs, PropertyInfo prop)
        {
            var candidateKeys = new List<string> { prop.Name };
            if (ModelReadAliases.TryGetValue(prop.Name, out var aliases))
            {
                candidateKeys.AddRange(aliases);
            }

            SystemConfig? firstMatchedConfig = null;
            foreach (var key in candidateKeys)
            {
                var config = rawConfigs.FirstOrDefault(c => c.key == key);
                if (config == null)
                {
                    continue;
                }

                firstMatchedConfig ??= config;
                if (IsUsableModelConfigValue(prop, config.value))
                {
                    return config;
                }
            }

            return firstMatchedConfig;
        }

        /// <summary>
        /// 判断配置值是否足以用于填充 Settings 模型。
        /// </summary>
        private static bool IsUsableModelConfigValue(PropertyInfo prop, string? value)
        {
            if (prop.PropertyType == typeof(bool))
            {
                return bool.TryParse(value, out _);
            }

            if (prop.PropertyType == typeof(int))
            {
                if (!int.TryParse(value, out var intValue))
                {
                    return false;
                }

                // TCP 端口不能使用 0 或负数；其它 int 配置保持原有宽松口径。
                return prop.Name != "port" || intValue > 0;
            }

            return !string.IsNullOrWhiteSpace(value);
        }

        public async Task UpdateModelAsync(SCRM.SHARED.Models.SystemConfigModel model)
        {
            foreach (var prop in typeof(SCRM.SHARED.Models.SystemConfigModel).GetProperties())
            {
                var value = prop.GetValue(model)?.ToString() ?? "";
                if (prop.PropertyType == typeof(bool)) value = value.ToLower();

                var config = new SystemConfig
                {
                    key = prop.Name, // Strictly use Property Name as DB Key
                    value = value,
                    description = "Updated via Web Settings",
                    updatedAt = DateTime.UtcNow
                };
                
                await UpdateConfigAsync(config);
                
                // 【新增：系统配置别名同步机制】
                // 为防止安卓端旧版本在本地缓存或映射表里读到残留的旧值，我们在后台保存时同步覆盖这些已被弃用但可能仍在发挥作用的同义映射
                var aliases = new List<string>();
                if (prop.Name == "host") aliases.Add("tcpServerHost");
                if (prop.Name == "port") { aliases.Add("server_port"); aliases.Add("tcpServerPort"); }
                if (prop.Name == "httpApiBaseUrl") aliases.Add("apiBaseUrl");
                if (prop.Name == "fileUpUrl") aliases.Add("fileUploadUrl");

                foreach (var alias in aliases)
                {
                    await UpdateConfigAsync(new SystemConfig
                    {
                        key = alias,
                        value = value,
                        description = $"Auto-synced from primary key {prop.Name}",
                        updatedAt = DateTime.UtcNow
                    });
                }
            }
        }
    }
}
