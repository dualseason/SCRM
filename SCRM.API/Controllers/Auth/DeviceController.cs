using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCRM.API.Models.Entities;
using SCRM.Services.Data;

namespace SCRM.Controllers.Auth
{
    /// <summary>
    /// 设备管理控制器 - 管理VIP密钥生成和设备信息
    /// </summary>
    [ApiController]
    [Route("api/device")]
    [Produces("application/json")]
    public class DeviceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

        /// <summary>
        /// 初始化设备管理控制器
        /// </summary>
        /// <param name="context">数据库上下文</param>
        public DeviceController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 生成VIP密钥
        /// </summary>
        /// <param name="request">VIP密钥生成请求</param>
        /// <returns>生成的VIP密钥信息</returns>
        /// <response code="200">成功生成VIP密钥</response>
        /// <response code="401">未授权访问</response>
        /// <response code="403">权限不足（仅管理员可生成）</response>
        /// <response code="500">服务器内部错误</response>
        [HttpPost("generate_vip")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> GenerateVipKey([FromBody] GenerateVipKeyRequest request)
        {
            try
            {
                var vipKey = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper(); // Simple 16-char key
                
                var newKey = new VipKey
                {
                    Uuid = vipKey,
                    Type = request.Type, // 0=Month, etc.
                    DurationDays = request.Days > 0 ? request.Days : 30,
                    Status = 0, // Unused
                    CreatedAt = DateTime.UtcNow
                };

                _context.VipKeys.Add(newKey);
                await _context.SaveChangesAsync();

                _logger.Information("Generated VIP Key: {VipKey}, Type: {Type}, Days: {Days}", vipKey, request.Type, newKey.DurationDays);
                return Ok(new { Success = true, VipKey = vipKey });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating VIP key");
                return StatusCode(500, new { Message = "Error generating VIP key" });
            }
        }

        /// <summary>
        /// 获取设备列表
        /// </summary>
        /// <returns>设备列表</returns>
        /// <response code="200">成功获取设备列表</response>
        /// <response code="401">未授权访问</response>
        /// <response code="500">服务器内部错误</response>
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<SrClient>>> GetDevices()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

            var allClients = await _context.GetAllSrClients();

            if (!isAdmin && !string.IsNullOrEmpty(userId))
            {
               return allClients.Where(c => c.OwnerId == userId || c.OwnerId == null).ToList();
            }

            return allClients;
        }

        /// <summary>
        /// 根据ID获取指定设备信息
        /// </summary>
        /// <param name="id">设备ID</param>
        /// <returns>设备详细信息</returns>
        /// <response code="200">成功获取设备信息</response>
        /// <response code="401">未授权访问</response>
        /// <response code="403">权限不足（无权访问该设备）</response>
        /// <response code="404">设备不存在</response>
        /// <response code="500">服务器内部错误</response>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<SrClient>> GetDevice(string id)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

            var client = await _context.GetSrClient(id);

            if (client == null)
            {
                return NotFound();
            }

            if (!isAdmin && client.OwnerId != null && client.OwnerId != userId)
            {
                return Forbid();
            }

            return client;
        }
    }

    /// <summary>
    /// VIP密钥生成请求模型
    /// </summary>
    public class GenerateVipKeyRequest
    {
        /// <summary>
        /// VIP有效天数（默认30天）
        /// </summary>
        /// <example>30</example>
        public int Days { get; set; } = 30;

        /// <summary>
        /// VIP类型（0=月卡，1=季卡，2=年卡，3=永久，4=日卡，5=周卡）
        /// </summary>
        /// <example>0</example>
        public int Type { get; set; } = 0;
    }
}
