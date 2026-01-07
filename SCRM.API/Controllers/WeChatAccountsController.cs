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
    public class WeChatAccountsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WeChatAccountsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: api/WeChatAccounts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WechatAccount>>> GetWeChatAccounts()
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            IQueryable<WechatAccount> query = _context.WechatAccounts.Include(c => c.owner);

            if (!isAdmin)
            {
                query = query.Where(c => c.ownerId == userId);
            }

            return await query.OrderByDescending(c => c.lastOnlineAt).ToListAsync();
        }

        // DELETE: api/WeChatAccounts/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWeChatAccount(long id)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            var account = await _context.WechatAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            // RBAC Check
            if (!isAdmin && account.ownerId != userId)
            {
                return Forbid();
            }

            // Optional: Don't hard delete? For now, we follow standard delete.
            // Client might need to re-login.
            _context.WechatAccounts.Remove(account);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
