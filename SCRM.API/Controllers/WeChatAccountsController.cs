using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Security;
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
        private readonly AccountAccessGuard _accountAccessGuard;
        private readonly SensitiveMaskingService _sensitiveMaskingService;

        public WeChatAccountsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            AccountAccessGuard accountAccessGuard,
            SensitiveMaskingService sensitiveMaskingService)
        {
            _context = context;
            _userManager = userManager;
            _accountAccessGuard = accountAccessGuard;
            _sensitiveMaskingService = sensitiveMaskingService;
        }

        // GET: api/WeChatAccounts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WechatAccount>>> GetWeChatAccounts()
        {
            var accessibleAccountIds = await _accountAccessGuard.GetAccessibleAccountIdsAsync(User);
            if (accessibleAccountIds.Count == 0)
            {
                return Ok(Array.Empty<WechatAccount>());
            }

            var accounts = await _context.WechatAccounts
                .AsNoTracking()
                .Include(c => c.owner)
                .Include(c => c.Client)
                .Where(c => accessibleAccountIds.Contains(c.wxid))
                .OrderByDescending(c => c.lastOnlineAt)
                .ToListAsync();

            var maskedAccounts = await _sensitiveMaskingService.MaskWechatAccountsAsync(User, accounts);
            return Ok(maskedAccounts);
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
