using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.Services.Data;
using SCRM.API.Models.Entities;
using SCRM.Services;
using SCRM.API.Services.Core;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SCRM.SHARED.Models;
using Microsoft.AspNetCore.Identity;
using SCRM.Models.Configurations;

namespace SCRM.Controllers.Auth
{
    /// <summary>
    /// 身份验证控制器
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly Microsoft.Extensions.Logging.ILogger<AuthController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly AuthService _authService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NettySettings _nettySettings;
        
        public AuthController(
            ApplicationDbContext context,
            AuthService authService,
            UserManager<ApplicationUser> userManager,
            Microsoft.Extensions.Options.IOptions<NettySettings> nettySettings,
            Microsoft.Extensions.Logging.ILogger<AuthController> logger)
        {
            _context = context;
            _authService = authService;
            _userManager = userManager;
            _nettySettings = nettySettings.Value;
            _logger = logger;
        }

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="request">登录请求</param>
        /// <returns>身份令牌响应</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.userName) || string.IsNullOrEmpty(request.password))
                {
                    return BadRequest(new { message = "用户名和密码不能为空" });
                }

                var user = await _userManager.FindByNameAsync(request.userName);
                if (user == null || !await _userManager.CheckPasswordAsync(user, request.password))
                {
                    return Unauthorized(new { message = "用户名或密码错误" });
                }

                var tokenResponse = await _authService.GenerateTokenResponseAsync(user);
                
                // 填充 TCP 配置
                tokenResponse.tcpHost = _nettySettings.Host;
                tokenResponse.tcpPort = _nettySettings.Port;

                _logger.LogInformation("用户 {UserName} 登录成功", request.userName);
                return Ok(new { success = true, data = tokenResponse });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "用户 {UserName} 登录过程中发生错误", request.userName);
                return StatusCode(500, new { message = "登录过程中发生错误" });
            }
        }

        /// <summary>
        /// 刷新身份令牌
        /// </summary>
        /// <param name="request">刷新令牌请求</param>
        /// <returns>新的身份令牌响应</returns>
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.userId) || string.IsNullOrEmpty(request.refreshToken))
                {
                    return BadRequest(new { message = "用户ID和刷新令牌不能为空" });
                }

                var isValidRefreshToken = _authService.ValidateRefreshTokenAsync(request.userId, request.refreshToken);
                if (!isValidRefreshToken)
                {
                    return Unauthorized(new { message = "无效的刷新令牌" });
                }

                var user = await _userManager.FindByIdAsync(request.userId);
                if (user == null)
                {
                    // 尝试旧版用户
                    /*
                    if (long.TryParse(request.userId, out var legacyUserId))
                    {
                        // Legacy logic disabled due to schema change: Id is now string
                        // var legacyUser = await _context.LegacyWechatUsers.FirstOrDefaultAsync(u => u.Id == legacyUserId && u.IsActive);
                        // if (legacyUser != null)
                        // {
                        //     var legacyTokenResponse = await _authService.GenerateTokenResponseAsync(legacyUser);
                        //     legacyTokenResponse.tcpHost = _nettySettings.Host;
                        //     legacyTokenResponse.tcpPort = _nettySettings.Port;
                        //     return Ok(new { success = true, data = legacyTokenResponse });
                        // }
                    }
                    */
                    return Unauthorized(new { message = "用户不存在或已被禁用" });
                }

                var tokenResponse = await _authService.GenerateTokenResponseAsync(user);
                
                tokenResponse.tcpHost = _nettySettings.Host;
                tokenResponse.tcpPort = _nettySettings.Port;

                _logger.LogInformation("用户 {UserId} 的令牌已刷新", request.userId);
                return Ok(new { success = true, data = tokenResponse });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "用户 {UserId} 刷新令牌过程中发生错误", request.userId);
                return StatusCode(500, new { message = "刷新令牌过程中发生错误" });
            }
        }

        /// <summary>
        /// 退出登录
        /// </summary>
        /// <returns>退出结果</returns>
        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null)
                {
                    _authService.RevokeRefreshTokenAsync(userIdClaim.Value);
                    _logger.LogInformation("用户 {UserId} 已退出登录", userIdClaim.Value);
                }

                return Ok(new { success = true, message = "退出登录成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "退出登录时发生错误");
                return StatusCode(500, new { message = "退出登录过程中发生错误" });
            }
        }

        /// <summary>
        /// 获取当前用户信息
        /// </summary>
        /// <returns>用户信息</returns>
        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return Unauthorized(new { message = "无效的用户信息" });
                }

                var user = await _userManager.FindByIdAsync(userIdClaim.Value);
                if (user != null)
                {
                    return Ok(new { success = true, data = new {
                        id = user.Id,
                        userName = user.UserName,
                        email = user.Email,
                    }});
                }

                // 旧版兼容逻辑
                /*
                if (long.TryParse(userIdClaim.Value, out var userId))
                {
                    var legacyUser = await _context.WechatAccounts
                        .Where(u => u.wxid == userIdClaim.Value) // Potential Fix: query by wxid directly if possible, else disable
                        .Select(u => new
                        {
                            id = u.wxid,
                            userName = u.wxid,
                            email = (string?)null,
                            firstName = u.nickname,
                            lastName = (string?)null,
                            phoneNumber = u.mobilePhone,
                            isActive = u.isActive,
                            lastLoginAt = u.lastOnlineAt,
                            u.createdAt
                        })
                        .FirstOrDefaultAsync();

                    if (legacyUser != null)
                    {
                        return Ok(new { success = true, data = legacyUser });
                    }
                }
                */

                return NotFound(new { message = "用户不存在" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取用户信息时出错");
                return StatusCode(500, new { message = "获取用户信息过程中发生错误" });
            }
        }
    }

    /// <summary>
    /// 登录请求对象
    /// </summary>
    public class LoginRequest
    {
        public string userName { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }

    /// <summary>
    /// 刷新令牌请求对象
    /// </summary>
    public class RefreshTokenRequest
    {
        public string userId { get; set; } = string.Empty;
        public string refreshToken { get; set; } = string.Empty;
    }
}