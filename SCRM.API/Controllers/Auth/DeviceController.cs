using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.Services.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.Controllers.Auth
{
    [ApiController]
    [Route("api/device")]
    public class DeviceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

        public DeviceController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("generate_vip")]
        [Authorize(Roles = "Admin,SuperAdmin")] // Only admins can generate keys
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

    public class GenerateVipKeyRequest
    {
        public int Days { get; set; } = 30;
        public int Type { get; set; } = 0; // 0=Month, 1=Season, 2=Year, 3=Forever, 4=Day, 5=Week
    }
}
