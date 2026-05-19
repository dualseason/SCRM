using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Core;
using SCRM.API.Services.Data;
using SCRM.API.Services.Security;
using SCRM.Models.Constants;
using SCRM.Services.Data;
using SCRM.SHARED.Models;

namespace SCRM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ServerDeviceCommandService _deviceCommandService;
        private readonly SensitiveMaskingService _sensitiveMaskingService;
        private readonly DeviceOperationGuard _deviceOperationGuard;

        public DevicesController(
            ApplicationDbContext context,
            ServerDeviceCommandService deviceCommandService,
            SensitiveMaskingService sensitiveMaskingService,
            DeviceOperationGuard deviceOperationGuard)
        {
            _context = context;
            _deviceCommandService = deviceCommandService;
            _sensitiveMaskingService = sensitiveMaskingService;
            _deviceOperationGuard = deviceOperationGuard;
        }

        // GET: api/Devices
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SrClient>>> GetDevices()
        {
            IQueryable<SrClient> query = _context.SrClients
                .AsNoTracking()
                .Include(c => c.owner);

            if (!AccountAccessGuard.IsAdmin(User))
            {
                var userIds = AccountAccessGuard.GetUserIdCandidates(User).ToList();
                if (userIds.Count == 0)
                {
                    return Ok(Array.Empty<SrClient>());
                }

                // 兼容早期未写 owner 的历史设备；显式归属其他用户的设备不返回。
                query = query.Where(c => c.ownerId == null
                    || c.ownerId == string.Empty
                    || (c.ownerId != null && userIds.Contains(c.ownerId)));
            }

            var devices = await query.OrderByDescending(c => c.lastLoginAt).ToListAsync();
            var maskedDevices = await _sensitiveMaskingService.MaskDevicesAsync(User, devices);
            return Ok(maskedDevices);
        }

        // DELETE: api/Devices/d8bbc1ff...
        [HttpDelete("{uuid}")]
        public async Task<IActionResult> DeleteDevice(string uuid)
        {
            var device = await _context.SrClients.FindAsync(uuid);
            if (device == null)
            {
                return NotFound();
            }

            var guard = await _deviceOperationGuard.CheckAsync(
                User,
                uuid,
                nameof(DeleteDevice),
                Permissions.DeviceTask.DeleteDevice,
                $"DevicesController.{nameof(DeleteDevice)}",
                targetId: uuid,
                destructive: true,
                httpContext: HttpContext);
            if (!guard.Allowed)
            {
                return Forbid();
            }

            // PostDeleteDeviceNotice(1097) 无任务回执；通知失败不阻止后台删除离线设备。
            try
            {
                await _deviceCommandService.NotifyDeviceDeleteAsync(device.uuid);
            }
            catch
            {
                // 保持删除接口幂等可用；通知失败只影响客户端主动断开，不影响本地删除。
            }

            await _context.DeleteSrClient(device);
            await _deviceOperationGuard.RecordDeviceDeletedAsync(
                User,
                device.uuid,
                $"DevicesController.{nameof(DeleteDevice)}",
                httpContext: HttpContext);

            return NoContent();
        }
    }
}
