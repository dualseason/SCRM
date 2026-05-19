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
        private static readonly HashSet<string> LegacyBroadcastBoolKeys = new(StringComparer.Ordinal)
        {
            "fastSend",
            "silentFunc",
            "silentAccept",
            "autoPic",
            "autoLogin",
            "addInWw",
            "lightscn",
            "forceRun",
            "disturb",
            "moreLog",
            "wx_show_alias",
            "wx_can_delete",
            "wx_can_block",
            "wx_can_exitGroup",
            "wx_del_conv",
            "wx_can_logout",
            "wx_can_changeAcnt",
            "wx_can_acntInfo",
            "wx_show_toast",
            "wx_can_sendcard"
        };

        private static readonly HashSet<string> LegacyBroadcastIntKeys = new(StringComparer.Ordinal)
        {
            "keepWake"
        };

        private static readonly HashSet<string> LegacyBroadcastStrKeys = new(StringComparer.Ordinal);

        private static readonly HashSet<string> LegacyDangerousRuntimeKeys = new(StringComparer.Ordinal)
        {
            "host",
            "port",
            "portstr",
            "fileUpUrl"
        };

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
                var msg = new SetConfigTaskMessage();
                var count = AppendLegacyAllowedConfig(msg, key, value, "Updated via Web Console");

                // 只有当实际上有配置在消息中时才发送
                if (count > 0)
                {
                    _logger.LogInformation("[配置更新] 正在广播配置更新 {Key} 给所有客户端...", key);
                    await _nettyService.SendMessageToNettyAsync(msg, "SetConfigTask");
                    _logger.LogInformation("[配置更新] 配置广播成功 {Key}", key);
                }
                else
                {
                    LogLegacyConfigSkipped(key);
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
                    count += AppendLegacyAllowedConfig(msg, cfg.key, cfg.value, "Batch Update");
                }

                if (count > 0)
                {
                    _logger.LogInformation("[配置更新] 正在批量广播 {Count} 项配置...", count);
                    await _nettyService.SendMessageToNettyAsync(msg, "SetConfigTask");
                }
                else
                {
                    _logger.LogInformation("[配置更新] 本次全局配置保存未包含允许通过旧入口广播的 Android 配置键。");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[配置更新] 批量广播失败");
            }
        }

        /// <summary>
        /// 旧全局配置入口的 Android 广播白名单。
        /// <para>
        /// 正式设备配置入口是 /features/device-config。这里仅保留低风险、明确被 62203/SmRun 消费的布尔键和 keepWake。
        /// host/port/fileUpUrl 等会引发重连或上传地址切换的运行时键，必须通过设备配置页按选中设备下发，避免全设备断连。
        /// </para>
        /// </summary>
        private static int AppendLegacyAllowedConfig(SetConfigTaskMessage msg, string key, string value, string desc)
        {
            key = key?.Trim() ?? string.Empty;
            value ??= string.Empty;

            if (LegacyBroadcastBoolKeys.Contains(key))
            {
                if (!bool.TryParse(value, out var boolValue))
                {
                    return 0;
                }

                msg.BoolConfs.Add(new BoolConfigMessage
                {
                    Key = key,
                    Value = boolValue,
                    Name = key,
                    Desc = desc
                });
                return 1;
            }

            if (LegacyBroadcastIntKeys.Contains(key))
            {
                if (!int.TryParse(value, out var intValue))
                {
                    return 0;
                }

                msg.IntConfs.Add(new IntConfigMessage
                {
                    Key = key,
                    Value = intValue,
                    Name = key,
                    Desc = desc
                });
                return 1;
            }

            if (LegacyBroadcastStrKeys.Contains(key))
            {
                msg.StrConfs.Add(new StrConfigMessage
                {
                    Key = key,
                    Value = value,
                    Name = key,
                    Desc = desc
                });
                return 1;
            }

            return 0;
        }

        private void LogLegacyConfigSkipped(string key)
        {
            if (LegacyDangerousRuntimeKeys.Contains(key))
            {
                _logger.LogWarning(
                    "[配置更新] 已保存全局配置 {Key}，但不会通过旧入口广播到所有 Android 客户端。请使用“设备配置/违禁词”页面按设备下发该运行时配置。",
                    key);
                return;
            }

            _logger.LogInformation("[配置更新] 已保存全局配置 {Key}，该键不是 62203 SetConfigTask 旧入口广播白名单，跳过 Android 广播。", key);
        }
    }
}

