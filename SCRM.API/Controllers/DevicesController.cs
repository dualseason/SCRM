using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
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
        private readonly UserManager<ApplicationUser> _userManager;

        public DevicesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: api/Devices
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SrClient>>> GetDevices()
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            IQueryable<SrClient> query = _context.SrClients.Include(c => c.owner);

            if (!isAdmin)
            {
                query = query.Where(c => c.ownerId == userId);
            }

            return await query.OrderByDescending(c => c.lastLoginAt).ToListAsync();
        }

        // DELETE: api/Devices/d8bbc1ff...
        [HttpDelete("{uuid}")]
        public async Task<IActionResult> DeleteDevice(string uuid)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            var device = await _context.SrClients.FindAsync(uuid);
            if (device == null)
            {
                return NotFound();
            }

            // RBAC Check
            if (!isAdmin && device.ownerId != userId)
            {
                return Forbid();
            }

            _context.SrClients.Remove(device);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
