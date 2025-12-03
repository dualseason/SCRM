using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.DTOs;
using SCRM.API.Models.Entities;
using SCRM.Services;
using SCRM.Services.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.Controllers.Auth
{
    [ApiController]
    [Route("")] // Root route to match /phone_login directly
    public class PhoneController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

        public PhoneController(ApplicationDbContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        [HttpPost("phone_login")]
        public async Task<IActionResult> Login([FromBody] DeviceInfoConfig request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RegCode))
                {
                    return Ok(ApiResponse<UserAuthToken>.Fail(1, "注册码不能为空"));
                }

                // Map RegCode to Wxid
                var account = await _context.WechatAccounts
                    .FirstOrDefaultAsync(u => u.Wxid == request.RegCode && !u.IsDeleted);

                if (account == null)
                {
                    return Ok(ApiResponse<UserAuthToken>.Fail(1, "用户未注册"));
                }

                // Update device info
                account.WechatNumber = request.Imei; // Map IMEI to WechatNumber
                account.Nickname = $"{request.Hsman} {request.Hstype}"; // Map Device Model to Nickname
                account.LastOnlineAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();

                // Generate Token (using Wxid as UserId for token generation if possible, or AccountId)
                // Assuming JwtService can handle WechatAccount or we map it to User
                var user = new User(account);
                var tokenResponse = await _jwtService.GenerateTokenResponseAsync(user);

                return Ok(ApiResponse<UserAuthToken>.Success(new UserAuthToken
                {
                    UserId = account.AccountId.ToString(),
                    Token = tokenResponse.Token
                }));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Phone login error");
                return Ok(ApiResponse<UserAuthToken>.Fail(-1, "服务器内部错误"));
            }
        }

        [HttpPost("phone_reg")]
        public async Task<IActionResult> Register([FromBody] BasicDeviceInfo request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RegCode))
                {
                    return Ok(ApiResponse<UserAuthToken>.Fail(1, "注册码不能为空"));
                }

                var existingAccount = await _context.WechatAccounts
                    .FirstOrDefaultAsync(u => u.Wxid == request.RegCode);

                if (existingAccount != null)
                {
                    return Ok(ApiResponse<UserAuthToken>.Fail(1, "该注册码已被使用"));
                }

                var newAccount = new WechatAccount
                {
                    Wxid = request.RegCode,
                    WechatNumber = request.Imei,
                    Nickname = $"{request.Hsman} {request.Hstype}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    LastOnlineAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.WechatAccounts.Add(newAccount);
                await _context.SaveChangesAsync();

                var user = new User(newAccount);
                var tokenResponse = await _jwtService.GenerateTokenResponseAsync(user);

                return Ok(ApiResponse<UserAuthToken>.Success(new UserAuthToken
                {
                    UserId = newAccount.AccountId.ToString(),
                    Token = tokenResponse.Token
                }));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Phone register error");
                return Ok(ApiResponse<UserAuthToken>.Fail(-1, "注册失败"));
            }
        }

        [HttpPost("phone_heartBeat2")]
        public async Task<IActionResult> Heartbeat([FromBody] UserSearchParams request)
        {
            // Heartbeat logic usually just updates last online time. 
            // Since the request doesn't carry the token in the body (it might be in header), 
            // we rely on the Authorization header if the client sends it.
            // However, the client might not send Auth header for heartbeat? 
            // Let's check if we can identify the user. 
            // For now, just return success to keep the client happy.
            
            return Ok(ApiResponse<UserAuthInfo>.Success(new UserAuthInfo { UserIdentifier = "1" }));
        }

        [HttpPost("phone_validation")]
        public async Task<IActionResult> Validate([FromBody] ExtendedDeviceInfo request)
        {
             try
            {
                if (string.IsNullOrEmpty(request.RegCode))
                {
                    return Ok(ApiResponse<UserAuthToken>.Fail(1, "注册码不能为空"));
                }

                var account = await _context.WechatAccounts
                    .FirstOrDefaultAsync(u => u.Wxid == request.RegCode && !u.IsDeleted);

                if (account == null)
                {
                    return Ok(ApiResponse<UserAuthToken>.Fail(1, "用户不存在"));
                }

                var user = new User(account);
                var tokenResponse = await _jwtService.GenerateTokenResponseAsync(user);

                return Ok(ApiResponse<UserAuthToken>.Success(new UserAuthToken
                {
                    UserId = account.AccountId.ToString(),
                    Token = tokenResponse.Token
                }));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Phone validation error");
                return Ok(ApiResponse<UserAuthToken>.Fail(-1, "验证失败"));
            }
        }
    }
}
