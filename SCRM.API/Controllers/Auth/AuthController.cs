using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.Services.Data;
using SCRM.API.Models.Entities;
using SCRM.Services;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SCRM.SHARED.Models;
using Microsoft.AspNetCore.Identity;
using SCRM.Models.Configurations;

namespace SCRM.Controllers.Auth
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

        private readonly ApplicationDbContext _context;
        private readonly AuthService _authService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NettySettings _nettySettings;
        
        public AuthController(
            ApplicationDbContext context,
            AuthService authService,
            UserManager<ApplicationUser> userManager,
            Microsoft.Extensions.Options.IOptions<NettySettings> nettySettings)
        {
            _context = context;
            _authService = authService;
            _userManager = userManager;
            _nettySettings = nettySettings.Value;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserName) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new { Message = "用户名和密码不能为空" });
                }

                var user = await _userManager.FindByNameAsync(request.UserName);
                if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
                {
                    return Unauthorized(new { Message = "用户名或密码错误" });
                }

                var tokenResponse = await _authService.GenerateTokenResponseAsync(user);
                
                // Populate TCP Configuration
                tokenResponse.TcpHost = _nettySettings.Host;
                tokenResponse.TcpPort = _nettySettings.Port;

                _logger.Information("User {UserName} logged in successfully", request.UserName);
                return Ok(new { Success = true, Data = tokenResponse });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during login for user {UserName}", request.UserName);
                return StatusCode(500, new { Message = "登录过程中发生错误" });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.RefreshToken))
                {
                    return BadRequest(new { Message = "用户ID和刷新令牌不能为空" });
                }

                var isValidRefreshToken = _authService.ValidateRefreshTokenAsync(request.UserId, request.RefreshToken);
                if (!isValidRefreshToken)
                {
                    return Unauthorized(new { Message = "无效的刷新令牌" });
                }

                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                {
                    // Try legacy user
                    if (long.TryParse(request.UserId, out var legacyUserId))
                    {
                        var legacyUser = await _context.LegacyWechatUsers.FirstOrDefaultAsync(u => u.Id == legacyUserId && u.IsActive);
                        if (legacyUser != null)
                        {
                             var legacyTokenResponse = await _authService.GenerateTokenResponseAsync(legacyUser);
                             // Populate TCP Configuration
                             legacyTokenResponse.TcpHost = _nettySettings.Host;
                             legacyTokenResponse.TcpPort = _nettySettings.Port;
                             return Ok(new { Success = true, Data = legacyTokenResponse });
                        }
                    }
                    return Unauthorized(new { Message = "用户不存在或已被禁用" });
                }

                var tokenResponse = await _authService.GenerateTokenResponseAsync(user);
                
                // Populate TCP Configuration
                tokenResponse.TcpHost = _nettySettings.Host;
                tokenResponse.TcpPort = _nettySettings.Port;

                _logger.Information("Token refreshed for user {UserId}", request.UserId);
                return Ok(new { Success = true, Data = tokenResponse });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during token refresh for user {UserId}", request.UserId);
                return StatusCode(500, new { Message = "刷新令牌过程中发生错误" });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null)
                {
                    _authService.RevokeRefreshTokenAsync(userIdClaim.Value);
                    _logger.Information("User {UserId} logged out", userIdClaim.Value);
                }

                return Ok(new { Success = true, Message = "退出登录成功" });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during logout");
                return StatusCode(500, new { Message = "退出登录过程中发生错误" });
            }
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return Unauthorized(new { Message = "无效的用户信息" });
                }

                var user = await _userManager.FindByIdAsync(userIdClaim.Value);
                if (user != null)
                {
                     return Ok(new { Success = true, Data = new {
                        Id = user.Id,
                        UserName = user.UserName,
                        Email = user.Email,
                        // Add other properties as needed
                     }});
                }

                // Legacy fallback
                if (long.TryParse(userIdClaim.Value, out var userId))
                {
                    var legacyUser = await _context.WechatAccounts
                        .Where(u => u.AccountId == userId)
                        .Select(u => new
                        {
                            Id = u.AccountId,
                            UserName = u.Wxid,
                            Email = (string)null,
                            FirstName = u.Nickname,
                            LastName = (string)null,
                            PhoneNumber = u.MobilePhone,
                            IsActive = u.IsActive,
                            LastLoginAt = u.LastOnlineAt,
                            u.CreatedAt
                        })
                        .FirstOrDefaultAsync();

                    if (legacyUser != null)
                    {
                        return Ok(new { Success = true, Data = legacyUser });
                    }
                }

                return NotFound(new { Message = "用户不存在" });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting user profile");
                return StatusCode(500, new { Message = "获取用户信息过程中发生错误" });
            }
        }
    }

    public class LoginRequest
    {
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

        public string UserId { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}