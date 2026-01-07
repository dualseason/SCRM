using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.Services.Data;
using System.Security.Claims;

namespace SCRM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ContactsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ContactsController> _logger;

        public ContactsController(ApplicationDbContext context, ILogger<ContactsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Contacts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Contact>>> GetContacts(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20, 
            [FromQuery] string? search = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

            IQueryable<Contact> query = _context.Contacts
                .Include(c => c.Account)
                .ThenInclude(a => a.Client)
                .Include(c => c.Account)
                .ThenInclude(a => a.owner)
                .AsNoTracking();

            // RBAC Filtering
            if (!isAdmin)
            {
                // Only show contacts from accounts owned by the user
                query = query.Where(c => c.Account != null && c.Account.ownerId == userId);
            }

            // Search
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => 
                    c.nickname.Contains(search) || 
                    c.remarks.Contains(search) || 
                    c.wxid.Contains(search) ||
                    (c.Account != null && c.Account.nickname.Contains(search)));
            }

            // Pagination
            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(c => c.lastInteractionTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Metadata in headers
            Response.Headers.Add("X-Total-Count", totalCount.ToString());
            Response.Headers.Add("X-Page-Count", ((int)Math.Ceiling(totalCount / (double)pageSize)).ToString());

            return Ok(items);
        }
    }
}
