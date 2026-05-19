using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.Services.Data;
using SCRM.Services;
using SCRM.API.Services.Core;
using SCRM.API.Services.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.Controllers.Auth
{
    /// <summary>
    /// 设备管理控制器
    /// </summary>
    [ApiController]
    [Route("api/device")]
    public class DeviceController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly ApplicationDbContext _context;
        private readonly SensitiveMaskingService _sensitiveMaskingService;
        private readonly Microsoft.Extensions.Logging.ILogger<DeviceController> _logger;

        public DeviceController(
            AuthService authService,
            ApplicationDbContext context,
            SensitiveMaskingService sensitiveMaskingService,
            Microsoft.Extensions.Logging.ILogger<DeviceController> logger)
        {
            _authService = authService;
            _context = context;
            _sensitiveMaskingService = sensitiveMaskingService;
            _logger = logger;
        }

        /// <summary>
        /// 生成 VIP 激活码 (仅管理员)
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns>生成的激活码</returns>
        [HttpPost("generate_vip")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> GenerateVipKey([FromBody] GenerateVipKeyRequest request)
        {
            try
            {
                var vipKey = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
                
                var newKey = new VipKey
                {
                    uuid = vipKey,
                    type = request.type,
                    durationDays = request.days > 0 ? request.days : 30,
                    status = 0, // 未使用
                    createdAt = DateTime.UtcNow
                };

                _context.VipKeys.Add(newKey);
                await _context.SaveChangesAsync();

                _logger.LogInformation("已生成 VIP 激活码: {VipKey}, 类型: {Type}, 天数: {Days}", vipKey, request.type, newKey.durationDays);
                return Ok(new { success = true, vipKey = vipKey });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成 VIP 激活码时出错");
                return StatusCode(500, new { message = "生成 VIP 激活码失败" });
            }
        }

        /// <summary>
        /// 获取当前用户的所有设备
        /// </summary>
        /// <returns>设备列表</returns>
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<SrClient>>> GetDevices()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var devices = await _authService.GetDevicesForUserAsync(userId, isAdmin);
            return await _sensitiveMaskingService.MaskDevicesAsync(User, devices);
        }

        /// <summary>
        /// 获取特定设备详情
        /// </summary>
        /// <param name="id">设备 UUID</param>
        /// <returns>设备详情</returns>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<SrClient>> GetDevice(string id)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var client = await _authService.GetDeviceAsync(id, userId, isAdmin);

            if (client == null)
            {
                return NotFound();
            }

            var masked = await _sensitiveMaskingService.MaskDevicesAsync(User, new[] { client });
            return masked.First();
        }
    }

    /// <summary>
    /// VIP 激活码生成请求
    /// </summary>
    public class GenerateVipKeyRequest
    {
        /// <summary>
        /// 有效天数
        /// </summary>
        public int days { get; set; } = 30;

        /// <summary>
        /// 激活码类型：0-月卡, 1-季卡, 2-年卡, 3-永久, 4-天卡, 5-周卡
        /// </summary>
        public int type { get; set; } = 0;
    }
}
