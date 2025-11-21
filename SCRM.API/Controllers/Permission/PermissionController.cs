using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.Attributes;
using SCRM.Constants;
using SCRM.Data;
using SCRM.Models.Identity;
using SCRM.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.Controllers.Permission
{
    [ApiController]
    [Route("api/permissions")]
    [Authorize]
    public class PermissionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionService _permissionService;

        public PermissionController(ApplicationDbContext context, IPermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }

        [HttpGet]
        [RequirePermission(Permissions.Permission.View)]
        public async Task<IActionResult> GetAllPermissions()
        {
            try
            {
                var permissions = await _permissionService.GetAllPermissionsAsync();
                return Ok(new { Success = true, Data = permissions });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "获取权限列表失败" });
            }
        }

        [HttpGet("grouped")]
        [RequirePermission(Permissions.Permission.View)]
        public async Task<IActionResult> GetGroupedPermissions()
        {
            try
            {
                var permissions = await _permissionService.GetAllPermissionsAsync();
                var grouped = permissions
                    .GroupBy(p => p.Module)
                    .ToDictionary(g => g.Key, g => g.OrderBy(p => p.SortOrder).ToList());

                return Ok(new { Success = true, Data = grouped });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "获取分组权限失败" });
            }
        }

        [HttpGet("user/{userId}")]
        [RequirePermission(Permissions.User.View)]
        public async Task<IActionResult> GetUserPermissions(int userId)
        {
            try
            {
                var permissionInfo = await _permissionService.GetUserPermissionInfoAsync(userId);
                return Ok(new { Success = true, Data = permissionInfo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "获取用户权限失败" });
            }
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentUserPermissions()
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                {
                    return Unauthorized(new { Success = false, Message = "无效的用户信息" });
                }

                var permissionInfo = await _permissionService.GetUserPermissionInfoAsync(userId);
                return Ok(new { Success = true, Data = permissionInfo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "获取当前用户权限失败" });
            }
        }

        [HttpPost("check")]
        public async Task<IActionResult> CheckPermission([FromBody] CheckPermissionRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                {
                    return Unauthorized(new { Success = false, Message = "无效的用户信息" });
                }

                var hasPermission = await _permissionService.HasPermissionAsync(userId, request.Permission);
                return Ok(new { Success = true, Data = new { HasPermission = hasPermission } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "权限检查失败" });
            }
        }

        [HttpGet("check/{permission}")]
        public async Task<IActionResult> CheckSinglePermission(string permission)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                {
                    return Unauthorized(new { Success = false, Message = "无效的用户信息" });
                }

                var hasPermission = await _permissionService.HasPermissionAsync(userId, permission);
                return Ok(new { Success = true, Data = new { HasPermission = hasPermission, Permission = permission } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "权限检查失败" });
            }
        }

        [HttpPost("check-permissions")]
        public async Task<IActionResult> CheckPermissions([FromBody] CheckPermissionsRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                {
                    return Unauthorized(new { Success = false, Message = "无效的用户信息" });
                }

                var hasAllPermissions = await _permissionService.HasAllPermissionsAsync(userId, request.Permissions);
                var hasAnyPermission = await _permissionService.HasAnyPermissionAsync(userId, request.Permissions);

                return Ok(new {
                    Success = true,
                    Data = new {
                        HasAllPermissions = hasAllPermissions,
                        HasAnyPermission = hasAnyPermission
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "批量权限检查失败" });
            }
        }

        [HttpGet("cache/clear")]
        [RequirePermission(Permissions.System.Maintenance)]
        public IActionResult ClearCache()
        {
            try
            {
                if (_permissionService is PermissionService service)
                {
                    service.ClearAllCache();
                }

                return Ok(new { Success = true, Message = "缓存已清空" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "清空缓存失败" });
            }
        }
    }

    public class CheckPermissionRequest
    {
        public string Permission { get; set; } = string.Empty;
    }

    public class CheckPermissionsRequest
    {
        public string[] Permissions { get; set; } = new string[0];
    }
}