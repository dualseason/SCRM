using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.Models.Constants;
using SCRM.Services.Data;
using SCRM.API.Models.Entities;
using SCRM.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.Controllers.Permission
{
    /// <summary>
    /// 权限管理控制器 - 处理系统权限的查询、检查和缓存管理
    /// </summary>
    [ApiController]
    [Route("api/permissions")]
    [Authorize]
    [Produces("application/json")]
    public class PermissionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AuthService _authService;

        /// <summary>
        /// 初始化权限管理控制器
        /// </summary>
        /// <param name="context">数据库上下文</param>
        /// <param name="authService">认证服务</param>
        public PermissionController(ApplicationDbContext context, AuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        /// <summary>
        /// 获取所有权限列表
        /// </summary>
        /// <returns>所有权限的列表</returns>
        /// <response code="200">成功获取权限列表</response>
        /// <response code="401">未授权访问</response>
        /// <response code="403">权限不足（仅管理员可访问）</response>
        /// <response code="500">服务器内部错误</response>
        [HttpGet]
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> GetAllPermissions()
        {
            try
            {
                var permissions = await _authService.GetAllPermissionsAsync();
                return Ok(new { Success = true, Data = permissions });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Success = false, Message = "获取权限列表失败" });
            }
        }

        /// <summary>
        /// 获取按模块分组的权限列表
        /// </summary>
        /// <returns>按模块分组后的权限字典</returns>
        /// <response code="200">成功获取分组权限</response>
        /// <response code="401">未授权访问</response>
        /// <response code="403">权限不足（仅管理员可访问）</response>
        /// <response code="500">服务器内部错误</response>
        [HttpGet("grouped")]
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> GetGroupedPermissions()
        {
            try
            {
                var permissions = await _authService.GetAllPermissionsAsync();
                var grouped = permissions
                    .GroupBy(p => p.Module)
                    .ToDictionary(g => g.Key, g => g.OrderBy(p => p.SortOrder).ToList());

                return Ok(new { Success = true, Data = grouped });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Success = false, Message = "获取分组权限失败" });
            }
        }

        /// <summary>
        /// 获取指定用户的权限信息
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>用户权限信息</returns>
        /// <response code="200">成功获取用户权限</response>
        /// <response code="401">未授权访问</response>
        /// <response code="403">权限不足（仅管理员可访问）</response>
        /// <response code="500">服务器内部错误</response>
        [HttpGet("user/{userId}")]
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> GetUserPermissions(int userId)
        {
            try
            {
                var permissionInfo = await _authService.GetUserPermissionInfoAsync(userId);
                return Ok(new { Success = true, Data = permissionInfo });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Success = false, Message = "获取用户权限失败" });
            }
        }

        /// <summary>
        /// 获取当前登录用户的权限信息
        /// </summary>
        /// <returns>当前用户的权限信息</returns>
        /// <response code="200">成功获取当前用户权限</response>
        /// <response code="401">未授权访问</response>
        /// <response code="500">服务器内部错误</response>
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

                var permissionInfo = await _authService.GetUserPermissionInfoAsync(userId);
                return Ok(new { Success = true, Data = permissionInfo });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Success = false, Message = "获取当前用户权限失败" });
            }
        }

        /// <summary>
        /// 检查当前用户是否具有指定权限
        /// </summary>
        /// <param name="request">权限检查请求</param>
        /// <returns>权限检查结果</returns>
        /// <response code="200">成功检查权限</response>
        /// <response code="401">未授权访问</response>
        /// <response code="500">服务器内部错误</response>
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

                var hasPermission = await _authService.HasPermissionAsync(userId, request.Permission);
                return Ok(new { Success = true, Data = new { HasPermission = hasPermission } });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Success = false, Message = "权限检查失败" });
            }
        }

        /// <summary>
        /// 通过GET请求检查当前用户是否具有指定权限
        /// </summary>
        /// <param name="permission">要检查的权限名称</param>
        /// <returns>权限检查结果</returns>
        /// <response code="200">成功检查权限</response>
        /// <response code="401">未授权访问</response>
        /// <response code="500">服务器内部错误</response>
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

                var hasPermission = await _authService.HasPermissionAsync(userId, permission);
                return Ok(new { Success = true, Data = new { HasPermission = hasPermission, Permission = permission } });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Success = false, Message = "权限检查失败" });
            }
        }

        /// <summary>
        /// 批量检查当前用户的多个权限
        /// </summary>
        /// <param name="request">批量权限检查请求</param>
        /// <returns>批量权限检查结果（包含是否拥有全部权限和任意权限）</returns>
        /// <response code="200">成功检查权限</response>
        /// <response code="401">未授权访问</response>
        /// <response code="500">服务器内部错误</response>
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

                var hasAllPermissions = await _authService.HasAllPermissionsAsync(userId, request.Permissions);
                var hasAnyPermission = await _authService.HasAnyPermissionAsync(userId, request.Permissions);

                return Ok(new {
                    Success = true,
                    Data = new {
                        HasAllPermissions = hasAllPermissions,
                        HasAnyPermission = hasAnyPermission
                    }
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Success = false, Message = "批量权限检查失败" });
            }
        }

        /// <summary>
        /// 清空权限缓存（仅管理员可用）
        /// </summary>
        /// <returns>清空缓存的结果</returns>
        /// <response code="200">成功清空缓存</response>
        /// <response code="401">未授权访问</response>
        /// <response code="403">权限不足（仅管理员可访问）</response>
        /// <response code="500">服务器内部错误</response>
        [HttpGet("cache/clear")]
        [Authorize(Policy = "RequireAdminRole")]
        public IActionResult ClearCache()
        {
            try
            {
                if (_authService is AuthService service)
                {
                    service.ClearAllCache();
                }

                return Ok(new { Success = true, Message = "缓存已清空" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Success = false, Message = "清空缓存失败" });
            }
        }
    }

    /// <summary>
    /// 单个权限检查请求模型
    /// </summary>
    public class CheckPermissionRequest
    {
        /// <summary>
        /// 要检查的权限名称
        /// </summary>
        /// <example>user.view</example>
        public string Permission { get; set; } = string.Empty;
    }

    /// <summary>
    /// 批量权限检查请求��型
    /// </summary>
    public class CheckPermissionsRequest
    {
        /// <summary>
        /// 要检查的权限名称数组
        /// </summary>
        /// <example>["user.view", "user.edit", "user.delete"]</example>
        public string[] Permissions { get; set; } = new string[0];
    }
}