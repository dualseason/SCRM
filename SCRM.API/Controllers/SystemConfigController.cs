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
        private readonly ApplicationDbContext _context;
        private readonly INettyService _nettyService;
        private readonly ILogger<SystemConfigController> _logger;

        public SystemConfigController(ApplicationDbContext context, INettyService nettyService, ILogger<SystemConfigController> logger)
        {
            _context = context;
            _nettyService = nettyService;
            _logger = logger;
        }

        // GET: api/SystemConfig
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SystemConfig>>> GetSystemConfigs()
        {
            return await _context.SystemConfigs.OrderBy(c => c.key).ToListAsync();
        }

        // GET: api/SystemConfig/key/{key}
        [HttpGet("key/{key}")]
        public async Task<ActionResult<SystemConfig>> GetSystemConfigByKey(string key)
        {
            var systemConfig = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.key == key);

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

            var existingConfig = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.key == config.key);
            bool isUpdate = false;

            if (existingConfig != null)
            {
                existingConfig.value = config.value;
                existingConfig.description = config.description ?? existingConfig.description;
                existingConfig.updatedAt = DateTime.UtcNow;
                isUpdate = true;
            }
            else
            {
                config.updatedAt = DateTime.UtcNow;
                _context.SystemConfigs.Add(config);
            }

            await _context.SaveChangesAsync();
            
            // Side Effects: Push configuration to clients
            await PushConfigToClients(config.key, config.value);

            if (isUpdate)
            {
                return Ok(existingConfig);
            }

            return CreatedAtAction("GetSystemConfigByKey", new { key = config.key }, config);
        }

        private async Task PushConfigToClients(string key, string value)
        {
            try
            {
                var msg = new ConfigPushNoticeMessage();
                
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
                    _logger.LogInformation("Pushing config update for {Key} to all clients", key);
                    // 广播发送 "ConfigPushNotice"
                    await _nettyService.SendMessageToNettyAsync(msg, "ConfigPushNotice");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pushing config update for {Key}", key);
            }
        }
    }
}
