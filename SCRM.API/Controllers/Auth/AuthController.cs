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

namespace SCRM.Controllers.Auth
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;
        
        public AuthController(
            ApplicationDbContext context,
            JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserName) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new { Message = "用户名和密码不能为空" });
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == request.UserName && u.IsActive);

                if (user == null)
                {
                    return Unauthorized(new { Message = "用户名或密码错误" });
                }

                // WeChat CRM系统暂时不使用密码验证
                // 实际项目中可以集成企业微信或其他认证方式
                // TODO: 根据实际需求实现合适的认证机制

                // 更新最后登录时间
                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var tokenResponse = await _jwtService.GenerateTokenResponseAsync(user);

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

                if (!long.TryParse(request.UserId, out var userId))
                {
                    return BadRequest(new { Message = "无效的用户ID" });
                }

                var isValidRefreshToken = _jwtService.ValidateRefreshTokenAsync(request.UserId, request.RefreshToken);
                if (!isValidRefreshToken)
                {
                    return Unauthorized(new { Message = "无效的刷新令牌" });
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user == null)
                {
                    return Unauthorized(new { Message = "用户不存在或已被禁用" });
                }

                var tokenResponse = await _jwtService.GenerateTokenResponseAsync(user);

                _logger.Information("Token refreshed for user {UserId}", userId);
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
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                {
                    _jwtService.RevokeRefreshTokenAsync(userId.ToString());
                    _logger.Information("User {UserId} logged out", userId);
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
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                {
                    return Unauthorized(new { Message = "无效的用户信息" });
                }

                var user = await _context.WechatAccounts
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

                if (user == null)
                {
                    return NotFound(new { Message = "用户不存在" });
                }

                return Ok(new { Success = true, Data = user });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting user profile");
                return StatusCode(500, new { Message = "获取用户信息过程中发生错误" });
            }
        }

        private bool VerifyPassword(string password, string passwordHash)
        {
            // 使用 BCrypt.Net 进行安全的密码验证
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
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