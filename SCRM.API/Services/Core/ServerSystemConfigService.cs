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
            var rawConfigs = await GetConfigsAsync();
            var model = new SCRM.SHARED.Models.SystemConfigModel();
            
            // "Zero-Overhead" Implementation: Use Property Name directly
            // This is standard C# Reflection (Cached by Runtime), extremely fast compared to Attributes
            foreach (var prop in typeof(SCRM.SHARED.Models.SystemConfigModel).GetProperties())
            {
                var config = rawConfigs.FirstOrDefault(c => c.key == prop.Name);
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
