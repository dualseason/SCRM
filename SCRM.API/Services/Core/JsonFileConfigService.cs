using System.Text.Json;
using SCRM.API.Models.Entities;
using SCRM.UI.Services;

namespace SCRM.API.Services
{
    public class JsonFileConfigService : ISystemConfigService
    {
        private readonly string _configPath;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ILogger<JsonFileConfigService> _logger;

        public JsonFileConfigService(IWebHostEnvironment env, ILogger<JsonFileConfigService> logger)
        {
            _configPath = Path.Combine(env.ContentRootPath, "system_config.json");
            _logger = logger;
            InitializeFile();
        }

        private void InitializeFile()
        {
            try
            {
                var defaults = new List<SystemConfig>
                {
                    // 基础连接配置
                    new SystemConfig { key = "tcpServerHost", value = "127.0.0.1", description = "TCP 服务器主机" },
                    new SystemConfig { key = "tcpServerPort", value = "8647", description = "TCP 端口 [Netty]" },
                    new SystemConfig { key = "httpApiBaseUrl", value = "http://*:42718", description = "API 基础 URL [Host]" },
                    new SystemConfig { key = "keepWake", value = "5", description = "保持唤醒间隔(分钟)" },
                    
                    // 认证与安全
                    new SystemConfig { key = "tokenExpiryMinutes", value = "259200", description = "Token 默认有效期(分钟)" },
                    new SystemConfig { key = "jwtSecretKey", value = "ThisIsASecretKeyForJWTTokenGenerationAndValidationInSCRMSystem2024", description = "JWT 密钥" },
                    new SystemConfig { key = "jwtIssuer", value = "SCRM", description = "JWT Issuer" },
                    new SystemConfig { key = "jwtAudience", value = "SCRM.Clients", description = "JWT Audience" },

                    // 文件上传
                    new SystemConfig { key = "fileUploadStorePath", value = "wwwroot/uploads", description = "文件存储路径" },
                    new SystemConfig { key = "fileUploadUrlPrefix", value = "uploads", description = "文件请求前缀" },
                    new SystemConfig { key = "fileUploadUrl", value = "http://*:42718/uploads", description = "完整文件上传/访问 URL" },

                    // 客户端行为开关
                    new SystemConfig { key = "autoLogin", value = "True", description = "自动登录" },
                    new SystemConfig { key = "autoPic", value = "True", description = "自动下载图片" },
                    new SystemConfig { key = "forceRun", value = "True", description = "强制运行" },
                    new SystemConfig { key = "silentFunc", value = "False", description = "静默功能" },
                    new SystemConfig { key = "logLevel", value = "INFO", description = "日志级别" },
                    
                    // 其他
                    new SystemConfig { key = "autoUpdateUrl", value = "", description = "自动更新 URL" },
                    new SystemConfig { key = "clientConfigPath", value = "/sdcard/Android/media/.cache/sys_config.dat", description = "客户端配置路径" }
                };

                List<SystemConfig> currentConfigs = new();
                if (File.Exists(_configPath))
                {
                    try 
                    {
                        var content = File.ReadAllText(_configPath);
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            currentConfigs = JsonSerializer.Deserialize<List<SystemConfig>>(content) ?? new();
                        }
                    }
                    catch { /* ignore corrupted file, will rewrite */ }
                }

                bool changed = false;
                foreach (var def in defaults)
                {
                    if (!currentConfigs.Any(c => c.key == def.key))
                    {
                        def.updatedAt = DateTime.UtcNow;
                        currentConfigs.Add(def);
                        changed = true;
                    }
                }

                if (changed || !File.Exists(_configPath))
                {
                    File.WriteAllText(_configPath, JsonSerializer.Serialize(currentConfigs, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize config file at {Path}", _configPath);
            }
        }

        private async Task<List<SystemConfig>> ReadFileAsync()
        {
            if (!File.Exists(_configPath)) return new List<SystemConfig>();
            
            try 
            {
                var content = await File.ReadAllTextAsync(_configPath);
                if (string.IsNullOrWhiteSpace(content)) return new List<SystemConfig>();
                return JsonSerializer.Deserialize<List<SystemConfig>>(content) ?? new List<SystemConfig>();
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error reading config file");
                 return new List<SystemConfig>();
            }
        }

        private async Task WriteFileAsync(List<SystemConfig> configs)
        {
             await _lock.WaitAsync();
             try
             {
                 var json = JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true });
                 await File.WriteAllTextAsync(_configPath, json);
             }
             finally
             {
                 _lock.Release();
             }
        }

        public async Task<IEnumerable<SystemConfig>> GetConfigsAsync()
        {
            return await ReadFileAsync();
        }

        public async Task<SystemConfig?> GetConfigByKeyAsync(string key)
        {
            var configs = await ReadFileAsync();
            return configs.FirstOrDefault(c => c.key == key);
        }

        public async Task<SystemConfig?> UpdateConfigAsync(SystemConfig config)
        {
             await _lock.WaitAsync(); // Lock for read-modify-write
             try
             {
                 var configs = await ReadFileAsync(); // Read inside lock for safety
                 
                 // If ReadFileAsync is implemented using file stream it's okay, but it uses ReadAllText which is not atomic.
                 // Better to lock around the whole operation.
                 
                 // Actually ReadFileAsync helper does not lock? 
                 // I should move locking to public methods or make private helpers assume lock/no lock.
                 // For simplicity, lock in public methods.
                 
                 // Wait, ReadFileAsync is async. WriteFileAsync is async.
                 // Locking is needed.
             }
             finally
             {
                 _lock.Release();
             }
             
             // Redo Implementation with proper locking in Update
             
             return await UpdateConfigInternal(config);
        }
        
        // .... Implementation Details ...
        // Re-writing simpler version below in write_to_file tool.
        // It will implement ISystemConfigService.
        
        private async Task<SystemConfig?> UpdateConfigInternal(SystemConfig config)
        {
             await _lock.WaitAsync();
             try
             {
                 string content = "[]";
                 if (File.Exists(_configPath)) content = await File.ReadAllTextAsync(_configPath);
                 
                 var list = JsonSerializer.Deserialize<List<SystemConfig>>(content) ?? new List<SystemConfig>();
                 var existing = list.FirstOrDefault(c => c.key == config.key);
                 
                 if (existing != null)
                 {
                     existing.value = config.value;
                     existing.description = config.description ?? existing.description;
                     existing.updatedAt = DateTime.UtcNow;
                     config = existing; // Return updated object
                 }
                 else
                 {
                     config.updatedAt = DateTime.UtcNow;
                     // config.id = list.Count + 1; // ID logic might be weak but acceptable for JSON
                     list.Add(config);
                 }
                 
                 await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                 return config;
             }
             finally
             {
                 _lock.Release();
             }
        }
    }
}
