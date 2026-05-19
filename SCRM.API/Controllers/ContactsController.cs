using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Data;
using SCRM.API.Services.Security;
using SCRM.SHARED.Models.Dtos;
using SCRM.Services.Data;

namespace SCRM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ContactsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ContactsController> _logger;
        private readonly AccountAccessGuard _accountAccessGuard;
        private readonly SensitiveMaskingService _sensitiveMaskingService;

        public ContactsController(
            ApplicationDbContext context,
            ILogger<ContactsController> logger,
            AccountAccessGuard accountAccessGuard,
            SensitiveMaskingService sensitiveMaskingService)
        {
            _context = context;
            _logger = logger;
            _accountAccessGuard = accountAccessGuard;
            _sensitiveMaskingService = sensitiveMaskingService;
        }

        // GET: api/Contacts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Contact>>> GetContacts(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20, 
            [FromQuery] string? search = null)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var accessibleAccountIds = await _accountAccessGuard.GetAccessibleAccountIdsAsync(User);
            if (accessibleAccountIds.Count == 0)
            {
                Response.Headers["X-Total-Count"] = "0";
                Response.Headers["X-Page-Count"] = "0";
                return Ok(Array.Empty<Contact>());
            }

            IQueryable<Contact> query = _context.Contacts
                .Include(c => c.Account)
                .ThenInclude(a => a!.Client)
                .Include(c => c.Account)
                .ThenInclude(a => a!.owner)
                .AsNoTracking()
                .Where(c => !c.isDeleted && accessibleAccountIds.Contains(c.ownerWxid));

            // 搜索仍在服务端明文字段上执行，返回前统一按权限脱敏，避免破坏现有联系人页体验。
            var normalizedSearch = search?.Trim();
            if (!string.IsNullOrEmpty(normalizedSearch))
            {
                query = query.Where(c => 
                    (c.nickname != null && c.nickname.Contains(normalizedSearch)) ||
                    (c.remarks != null && c.remarks.Contains(normalizedSearch)) ||
                    (c.wxid != null && c.wxid.Contains(normalizedSearch)) ||
                    (c.Account != null && c.Account.nickname != null && c.Account.nickname.Contains(normalizedSearch)));
            }

            // Pagination
            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(c => c.lastInteractionTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var maskedItems = await _sensitiveMaskingService.MaskContactsAsync(User, items);

            // Metadata in headers
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Count"] = ((int)Math.Ceiling(totalCount / (double)pageSize)).ToString();

            return Ok(maskedItems);
        }

        /// <summary>
        /// 读取指定微信账号已落库的联系人标签字典。
        /// <para>该接口只查询 ContactTags；如需让手机端重新同步，请走联系人标签同步任务。</para>
        /// </summary>
        [HttpGet("{accountId}/labels")]
        public async Task<ActionResult<IEnumerable<ContactLabelDto>>> GetContactLabels(
            string accountId,
            [FromQuery] bool includeDeleted = false)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return BadRequest("accountId 不能为空");
            }

            var accountIdTrimmed = accountId.Trim();
            if (!await _accountAccessGuard.CanAccessAccountAsync(User, accountIdTrimmed))
            {
                _logger.LogWarning("联系人标签访问拒绝：当前用户无权访问账号。AccountId={AccountId}", accountIdTrimmed);
                return Ok(Array.Empty<ContactLabelDto>());
            }

            var labels = await _context.GetContactLabels(accountIdTrimmed, includeDeleted);
            return Ok(labels);
        }
    }
}
