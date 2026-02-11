using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.Services;
using SCRM.Services.Data;
using Jubo.JuLiao.IM.Wx.Proto;
using System.Reflection;

namespace SCRM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // 只有登录用户可以管理配置
    public class SystemConfigController : ControllerBase
    {
        private readonly SCRM.UI.Services.ISystemConfigService _configService;
        private readonly INettyService _nettyService;
        private readonly ILogger<SystemConfigController> _logger;

        public SystemConfigController(SCRM.UI.Services.ISystemConfigService configService, INettyService nettyService, ILogger<SystemConfigController> logger)
        {
            _configService = configService;
            _nettyService = nettyService;
            _logger = logger;
        }

        // GET: api/SystemConfig
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SystemConfig>>> GetSystemConfigs()
        {
            return Ok(await _configService.GetConfigsAsync());
        }

        // GET: api/SystemConfig/key/{key}
        [HttpGet("key/{key}")]
        public async Task<ActionResult<SystemConfig>> GetSystemConfigByKey(string key)
        {
            var systemConfig = await _configService.GetConfigByKeyAsync(key);

            if (systemConfig == null)
            {
                return NotFound();
            }

            return systemConfig;
        }

        // POST: api/SystemConfig
        [HttpPost]
        public async Task<ActionResult<SystemConfig>> PostSystemConfig([FromBody] SystemConfig config)
        {
            if (string.IsNullOrEmpty(config.key))
            {
                return BadRequest("Key is required");
            }

            // Update via Service
            config.updatedAt = DateTime.UtcNow;
            await _configService.UpdateConfigAsync(config);
            
            // Side Effects: Push configuration to clients
            await PushConfigToClients(config.key, config.value);

            return Ok(config);
        }

        private async Task PushConfigToClients(string key, string value)
        {
            try
            {
                //var msg = new ConfigPushNoticeMessage();
                var msg= new SetConfigTaskMessage();
                // 根据 Key 类型构建不同的配置消息
                // 目前只处理 fileUpUrl 作为 String Config
                if (key == "fileUpUrl" || key == "host" || key == "portstr" || key == "apiBaseUrl" || key == "clientConfigPath" || key == "logLevel" || key == "autoUpdateUrl")
                {
                    msg.StrConfs.Add(new StrConfigMessage
                    {
                        Key = key,
                        Value = value,
                        Name = key,
                        Desc = "Updated via Web Console"
                    });
                }
                else if (key == "autoLogin" || key == "autoPic" || key == "fastSend" || key == "silentFunc" || key == "forceRun")
                {
                     if (bool.TryParse(value, out bool boolVal))
                     {
                         msg.BoolConfs.Add(new BoolConfigMessage
                         {
                             Key = key,
                             Value = boolVal,
                             Name = key,
                             Desc = "Updated via Web Console"
                         });
                     }
                }
                 else if (key == "keepWake" || key == "server_port")
                {
                     if (int.TryParse(value, out int intVal))
                     {
                         msg.IntConfs.Add(new IntConfigMessage
                         {
                             Key = key,
                             Value = intVal,
                             Name = key,
                             Desc = "Updated via Web Console"
                         });
                     }
                }

                // 只有当实际上有配置在消息中时才发送
                if (msg.StrConfs.Count > 0 || msg.BoolConfs.Count > 0 || msg.IntConfs.Count > 0)
                {
                    _logger.LogInformation("[配置更新] 正在广播配置更新 {Key} 给所有客户端...", key);
                    // 广播发送 "ConfigPushNotice"
                    await _nettyService.SendMessageToNettyAsync(msg, "SetConfigTask");
                    _logger.LogInformation("[配置更新] 配置广播成功 {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[配置更新] 广播失败 {Key}", key);
            }
        }
        // GET: api/SystemConfig/model
        // GET: api/SystemConfig/model
        [HttpGet("model")]
        public async Task<ActionResult<SCRM.SHARED.Models.SystemConfigModel>> GetSystemConfigModel()
        {
            return Ok(await _configService.GetConfigModelAsync());
        }

        // POST: api/SystemConfig/model
        [HttpPost("model")]
        public async Task<ActionResult> PostSystemConfigModel([FromBody] SCRM.SHARED.Models.SystemConfigModel model)
        {
            // 1. Save to Database (Delegated to Service)
            await _configService.UpdateModelAsync(model);

            // 2. Prepare Configs for Push (Iterate Model Properties directly)
            var updates = new List<SystemConfig>();
            foreach (var prop in typeof(SCRM.SHARED.Models.SystemConfigModel).GetProperties())
            {
                var value = prop.GetValue(model)?.ToString() ?? "";
                if (prop.PropertyType == typeof(bool)) value = value.ToLower();

                updates.Add(new SystemConfig 
                { 
                    key = prop.Name, 
                    value = value 
                });
            }

            // 3. Batch Push
            if (updates.Count > 0)
            {
                await PushBatchConfigToClients(updates);
            }

            return Ok();
        }

        private async Task PushBatchConfigToClients(List<SystemConfig> configs)
        {
            try
            {
                var msg = new SetConfigTaskMessage();
                int count = 0;

                foreach (var cfg in configs)
                {
                    // Filter Logic similar to individual push but cleaner
                    if (bool.TryParse(cfg.value, out bool boolVal))
                    {
                        // Check if key is meant to be boolean (Heuristic or Attribute based?)
                        // To be safe, let's trust the value parsing for now as per previous logic
                        // But wait, "8647" parses as int, "true" parses as bool.
                        // We must match the expected type by Client.
                        // Simple Heuristic:
                        if (cfg.key == "server_port" || cfg.key == "port" || cfg.key == "keepWake" || cfg.key == "server_port" || cfg.key == "tcpServerPort" || cfg.key == "tokenExpiryMinutes")
                        {
                             // Integer
                             if (int.TryParse(cfg.value, out int iVal))
                             {
                                 msg.IntConfs.Add(new IntConfigMessage { Key = cfg.key, Value = iVal, Name=cfg.key, Desc="Batch Update" });
                                 count++;
                             }
                        }
                        else if (cfg.value.ToLower() == "true" || cfg.value.ToLower() == "false")
                        {
                            // Boolean
                            msg.BoolConfs.Add(new BoolConfigMessage { Key = cfg.key, Value = bool.Parse(cfg.value), Name = cfg.key, Desc = "Batch Update" });
                            count++;
                        }
                        else
                        {
                            // String
                             msg.StrConfs.Add(new StrConfigMessage { Key = cfg.key, Value = cfg.value, Name = cfg.key, Desc = "Batch Update" });
                             count++;
                        }
                    }
                    else if (int.TryParse(cfg.value, out int intVal))
                    {
                         // Integer
                         // Identify if it SHOULD be integer. 
                         // To avoid identifying "192.168..." as int (fails) or phone numbers.
                         // For config, int usually means int.
                         msg.IntConfs.Add(new IntConfigMessage { Key = cfg.key, Value = intVal, Name = cfg.key, Desc = "Batch Update" });
                         count++;
                    }
                    else
                    {
                        // String
                        msg.StrConfs.Add(new StrConfigMessage { Key = cfg.key, Value = cfg.value, Name = cfg.key, Desc = "Batch Update" });
                        count++;
                    }
                }

                if (count > 0)
                {
                    _logger.LogInformation("[配置更新] 正在批量广播 {Count} 项配置...", count);
                    await _nettyService.SendMessageToNettyAsync(msg, "SetConfigTask");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[配置更新] 批量广播失败");
            }
        }
    }
}

