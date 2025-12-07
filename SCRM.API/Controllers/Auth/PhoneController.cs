using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.DTOs;
using SCRM.API.Models.Entities;
using SCRM.API.Utils;
using SCRM.Services;
using SCRM.Services.Data;
using SCRM.API.Filters;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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

        private async Task<T> GetDecryptedBody<T>()
        {
            try
            {
                using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
                {
                    string encryptedBody = await reader.ReadToEndAsync();
                    if (string.IsNullOrEmpty(encryptedBody)) return default;

                    // Decrypt
                    string json = EncryptionHelper.DecryptDefault(encryptedBody);
                    if (string.IsNullOrEmpty(json))
                    {
                        _logger.Warning($"Failed to decrypt body. Length: {encryptedBody.Length}");
                        return default;
                    }

                    // Deserialize
                    return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error reading/decrypting body");
                return default;
            }
        }

        [HttpPost("phone_reg")]
        [ClientApiKeyAuth]
        public async Task<IActionResult> Register()
        {
            var request = await GetDecryptedBody<PhoneRegDto>();
            if (request == null) return Ok(ApiResponse<UserAuthToken>.Fail(1, "无效的请求数据"));

            string clientUuid = HttpContext.Items["ClientUuid"]?.ToString();
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                if (string.IsNullOrEmpty(request.UserEmail))
                {
                    if (!string.IsNullOrEmpty(request.RegCode) && request.RegCode.Contains("@"))
                    {
                        request.UserEmail = request.RegCode;
                    }
                    else
                    {
                        return Ok(ApiResponse<UserAuthToken>.Fail(1, "用户邮箱不能为空"));
                    }
                }

                // 1. Find User by Email
                // Note: User entity mapping might be complex, but we try to query by Email.
                // If User.Email maps to WechatAccount.MobilePhone, this query effectively searches WechatAccounts.
                // But we use _context.Users to be consistent with DbContext.
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.UserEmail);
                if (user == null)
                {
                    return Ok(ApiResponse<UserAuthToken>.Fail(1, "用户不存在"));
                }

                // 2. Find or Create SrClient
                var srClient = await _context.SrClients.Include(c => c.Accounts).FirstOrDefaultAsync(c => c.uuid == clientUuid);
                if (srClient == null)
                {
                    srClient = new SrClient
                    {
                        uuid = clientUuid,
                        createdAt = DateTime.UtcNow,
                        tcpHost = "192.168.1.226", // TODO: Get from config
                        tcpPort = 8647,
                        status = 1,
                        isOnline = true,
                        lastLoginAt = DateTime.UtcNow,
                        ip = clientIp
                    };
                    _context.SrClients.Add(srClient);
                }
                else
                {
                    srClient.updatedAt = DateTime.UtcNow;
                    srClient.ip = clientIp;
                    srClient.lastLoginAt = DateTime.UtcNow;
                    srClient.isOnline = true;
                }

                // 3. Bind Client to User
                // We convert long Id to string because OwnerId is string?
                srClient.OwnerId = user.Id.ToString();

                await _context.SaveChangesAsync();

                // 4. Return Token
                var token = new UserAuthToken
                {
                    userId = user.Id.ToString(),
                    token = "API_KEY_AUTH",
                    tcpHost = srClient.tcpHost,
                    tcpPort = srClient.tcpPort
                };

                return Ok(ApiResponse<UserAuthToken>.Success(token));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Phone registration error");
                return Ok(ApiResponse<UserAuthToken>.Fail(-1, "服务器内部错误"));
            }
        }

        [HttpPost("phone_login")]
        [ClientApiKeyAuth]
        public async Task<IActionResult> Login()
        {
            var request = await GetDecryptedBody<SCRM.API.Models.DTOs.Device>();
            if (request == null) return Ok(ApiResponse<SrClient>.Fail(1, "无效的请求数据"));

            // Get Client UUID from Header (Validated by Filter)
            string clientUuid = HttpContext.Items["ClientUuid"].ToString();
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                if (string.IsNullOrEmpty(request.regCode))
                {
                    return Ok(ApiResponse<SrClient>.Fail(1, "注册码不能为空"));
                }

                WechatAccount account = null;

                // 1. Try to find by UUID (Primary)
                if (!string.IsNullOrEmpty(clientUuid))
                {
                    account = await _context.WechatAccounts
                        .FirstOrDefaultAsync(u => u.ClientUuid == clientUuid && !u.IsDeleted);
                }

                // 2. Fallback: Try to find by IMEI (Legacy/Migration)
                if (account == null)
                {
                    // Handle Auto-Registration Logic
                    if (request.regCode == "AUTO_REG_CODE")
                    {
                        account = await _context.WechatAccounts
                            .FirstOrDefaultAsync(u => u.WechatNumber == request.imei && !u.IsDeleted);

                        if (account == null)
                        {
                            // Auto-create new account
                            account = new WechatAccount
                            {
                                WechatNumber = request.imei,
                                ClientUuid = clientUuid, // Bind UUID immediately
                                Nickname = $"{request.hsman} {request.hstype}",
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                IsActive = true,
                                Wxid = Guid.NewGuid().ToString("N"),
                                VipExpiryDate = DateTime.UtcNow.AddDays(7)
                            };
                            _context.WechatAccounts.Add(account);
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            // Found by IMEI, but UUID was missing or different. Update it.
                            if (!string.IsNullOrEmpty(clientUuid) && account.ClientUuid != clientUuid)
                            {
                                account.ClientUuid = clientUuid;
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                    else
                    {
                        // Regular Login with RegCode (Wxid)
                        account = await _context.WechatAccounts
                            .FirstOrDefaultAsync(u => u.Wxid == request.regCode && !u.IsDeleted);
                        
                        // If found, bind UUID
                        if (account != null && !string.IsNullOrEmpty(clientUuid) && account.ClientUuid != clientUuid)
                        {
                            account.ClientUuid = clientUuid;
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                if (account == null)
                {
                    return Ok(ApiResponse<SrClient>.Fail(1, "用户未注册"));
                }

                // Update device info
                if (account.WechatNumber != request.imei)
                {
                     account.WechatNumber = request.imei;
                }
                account.Nickname = $"{request.hsman} {request.hstype}";
                account.LastOnlineAt = DateTime.UtcNow;
                
                // Find or Create SrClient
                var srClient = await _context.SrClients.Include(c => c.Accounts).FirstOrDefaultAsync(c => c.uuid == clientUuid);
                if (srClient == null)
                {
                    srClient = new SrClient
                    {
                        uuid = clientUuid,
                        createdAt = DateTime.UtcNow
                    };
                    _context.SrClients.Add(srClient);
                }

                srClient.device = request;
                srClient.tcpHost = "192.168.1.226";
                srClient.tcpPort = 8647;
                srClient.updatedAt = DateTime.UtcNow;
                srClient.ip = clientIp;
                srClient.lastLoginAt = DateTime.UtcNow;
                srClient.isOnline = true;
                srClient.status = 1;

                // Ensure account is in the list
                if (!srClient.Accounts.Any(a => a.AccountId == account.AccountId))
                {
                    srClient.Accounts.Add(account);
                }

                await _context.SaveChangesAsync();

                return Ok(ApiResponse<SrClient>.Success(srClient));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Phone login error");
                return Ok(ApiResponse<SrClient>.Fail(-1, "服务器内部错误"));
            }
        }

        [HttpPost("phone_heartBeat2")]
        public async Task<IActionResult> Heartbeat()
        {
            var request = await GetDecryptedBody<UserSearchParams>();
            // request might be null if decryption fails, but for heartbeat we might be lenient or just return error
            if (request == null) return Ok(ApiResponse<UserAuthInfo>.Fail(1, "无效的请求数据"));
            // Heartbeat logic usually just updates last online time. 
            // Since the request doesn't carry the token in the body (it might be in header), 
            // we rely on the Authorization header if the client sends it.
            // However, the client might not send Auth header for heartbeat? 
            // Let's check if we can identify the user. 
            // For now, just return success to keep the client happy.
            
            return Ok(ApiResponse<UserAuthInfo>.Success(new UserAuthInfo { userIdentifier = "1" }));
        }

        [HttpPost("phone_validation")]
        public async Task<IActionResult> Validate()
        {
            var request = await GetDecryptedBody<ExtendedDeviceInfo>();
            if (request == null) return Ok(ApiResponse<SrClient>.Fail(1, "无效的请求数据"));

             try
            {
                if (string.IsNullOrEmpty(request.regCode))
                {
                    return Ok(ApiResponse<SrClient>.Fail(1, "注册码不能为空"));
                }

                var account = await _context.WechatAccounts
                    .FirstOrDefaultAsync(u => u.Wxid == request.regCode && !u.IsDeleted);

                if (account == null)
                {
                    return Ok(ApiResponse<SrClient>.Fail(1, "用户不存在"));
                }

                var srClient = new SrClient
                {
                    uuid = "VALIDATION_SUCCESS",
                    tcpHost = "192.168.1.226",
                    tcpPort = 8647,
                    Accounts = new List<WechatAccount> { account }
                };

                return Ok(ApiResponse<SrClient>.Success(srClient));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Phone validation error");
                return Ok(ApiResponse<SrClient>.Fail(-1, "验证失败"));
            }
        }
    }
}
