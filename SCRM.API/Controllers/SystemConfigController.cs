using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.Services;
using SCRM.Services.Data;
using Jubo.JuLiao.IM.Wx.Proto;

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
    }
}
