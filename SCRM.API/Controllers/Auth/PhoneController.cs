using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.DTOs;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;
using SCRM.API.Utils;
using SCRM.Services;
using SCRM.API.Services.Core;
using SCRM.Services.Data;
using SCRM.UI.Services;
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
        private readonly AuthService _authService;
        private readonly Microsoft.Extensions.Logging.ILogger<PhoneController> _logger;
        private readonly SCRM.Models.Configurations.NettySettings _nettySettings;
        private readonly ISystemConfigService _configService;

        public PhoneController(ApplicationDbContext context, AuthService authService, Microsoft.Extensions.Logging.ILogger<PhoneController> logger, Microsoft.Extensions.Options.IOptions<SCRM.Models.Configurations.NettySettings> options, ISystemConfigService configService)
        {
            _context = context;
            _authService = authService;
            _logger = logger;
            _nettySettings = options.Value;
            _configService = configService;
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
                        _logger.LogWarning($"Failed to decrypt body. Length: {encryptedBody.Length}");
                        return default;
                    }

                    // Deserialize
                    return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading/decrypting body");
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
            _logger.LogInformation("[HTTP接入] 收到手机注册请求 - IP: {Ip}, 邮箱: {Email}", clientIp, request.userEmail);

            try
            {
                if (string.IsNullOrEmpty(request.userEmail))
                {
                    if (!string.IsNullOrEmpty(request.regCode) && request.regCode.Contains("@"))
                    {
                        request.userEmail = request.regCode;
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
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.userEmail);
                if (user == null)
                {
                    return Ok(ApiResponse<UserAuthToken>.Fail(1, "用户不存在"));
                }

                // 2. Find or Create SrClient
                var srClient = await _context.SrClients.Include(c => c.accounts).FirstOrDefaultAsync(c => c.uuid == clientUuid);
                if (srClient == null)
                {
                    srClient = new SrClient
                    {
                        uuid = clientUuid,
                        createdAt = DateTime.UtcNow,
                        tcpHost = _nettySettings.Host,
                        tcpPort = _nettySettings.Port,
                        status = 1,
                        isOnline = true,
                        lastLoginAt = DateTime.UtcNow,
                        ip = clientIp,
                        device = new Jubo.JuLiao.IM.Wx.Proto.PostDeviceInfoNoticeMessage()
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
                // We convert long Id to string because ownerId is string?
                srClient.ownerId = user.Id.ToString();

                await _context.SaveChangesAsync();

                // 4. Return Token
                var token = new UserAuthToken
                {
                    userId = user.Id.ToString(),
                    token = await _authService.GenerateTokenAsync(user),
                    tcpHost = srClient.tcpHost,
                    tcpPort = srClient.tcpPort
                };

                _logger.LogInformation("[生成Token] 为客户端 {ClientUuid} 生成Token (UserId: {UserId})...", clientUuid, user.Id);

                return Ok(ApiResponse<UserAuthToken>.Success(token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Phone registration error");
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
            _logger.LogInformation("[HTTP接入] 收到手机登录请求 - IP: {Ip}, RegCode: {RegCode}, IMEI: {BMI}", clientIp, request.regCode, request.imei);

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
                        .FirstOrDefaultAsync(u => u.clientUuid == clientUuid && !u.isDeleted);
                }

                // 2. Fallback: Try to find by IMEI (Legacy/Migration)
                if (account == null)
                {
                    // Handle Auto-Registration Logic
                    if (request.regCode == "AUTO_REG_CODE")
                    {
                        account = await _context.WechatAccounts
                            .FirstOrDefaultAsync(u => u.wechatNumber == request.imei && !u.isDeleted);

                        if (account == null)
                        {
                            // Auto-create new account
                            account = new WechatAccount
                            {
                                wechatNumber = request.imei,
                                clientUuid = clientUuid, // Bind UUID immediately
                                nickname = $"{request.hsman} {request.hstype}",
                                createdAt = DateTime.UtcNow,
                                updatedAt = DateTime.UtcNow,
                                isActive = true,
                                wxid = Guid.NewGuid().ToString("N"),
                                vipExpiryDate = DateTime.UtcNow.AddDays(7)
                            };
                            _context.WechatAccounts.Add(account);
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            // Found by IMEI, but UUID was missing or different. Update it.
                            if (!string.IsNullOrEmpty(clientUuid) && account.clientUuid != clientUuid)
                            {
                                account.clientUuid = clientUuid;
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                    else
                    {
                        // Regular Login with RegCode (wxid)
                        account = await _context.WechatAccounts
                            .FirstOrDefaultAsync(u => u.wxid == request.regCode && !u.isDeleted);
                        
                        // If found, bind UUID
                        if (account != null && !string.IsNullOrEmpty(clientUuid) && account.clientUuid != clientUuid)
                        {
                            account.clientUuid = clientUuid;
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                if (account == null)
                {
                    return Ok(ApiResponse<SrClient>.Fail(1, "用户未注册"));
                }

                // Update device info
                if (account.wechatNumber != request.imei)
                {
                     account.wechatNumber = request.imei;
                }
                account.nickname = $"{request.hsman} {request.hstype}";
                account.lastOnlineAt = DateTime.UtcNow;
                
                // Find or Create SrClient
                var srClient = await _context.SrClients.Include(c => c.accounts).FirstOrDefaultAsync(c => c.uuid == clientUuid);
                if (srClient == null)
                {
                    srClient = new SrClient
                    {
                        uuid = clientUuid,
                        createdAt = DateTime.UtcNow
                    };
                    _context.SrClients.Add(srClient);
                }

                srClient.device = new Jubo.JuLiao.IM.Wx.Proto.PostDeviceInfoNoticeMessage
                {
                    IMEI = request.imei ?? "",
                    PhoneBrand = request.hsman ?? "",
                    PhoneModel = request.hstype ?? "",
                    OSVerNumber = int.TryParse(request.androidApi, out int v) ? v : 0
                };
                
                if (!string.IsNullOrEmpty(request.packageName))
                {
                    srClient.device.AppInfos.Add(new Jubo.JuLiao.IM.Wx.Proto.PostDeviceInfoNoticeMessage.Types.DeviceAppInfoMessage
                    {
                        PackageName = request.packageName,
                        VerNumber = request.versionCode
                    });
                }
                srClient.tcpHost = _nettySettings.Host;
                srClient.tcpPort = _nettySettings.Port;
                srClient.updatedAt = DateTime.UtcNow;
                srClient.ip = clientIp;
                srClient.lastLoginAt = DateTime.UtcNow;
                srClient.isOnline = true;
                srClient.status = 1;

                // [Fix] Inject System Configs into customConfigs so client gets them immediately
                // Convert List<SystemConfig> to Dictionary<string, object> for serialization
                // Or just a simple wrapper if client expects specific format. 
                // Assuming client parses customConfigs as a dictionary or checks for specific keys.
                // We'll mimic the PushConfig format roughly - a dictionary of Key/Value
                try 
                {
                    var configs = await _configService.GetConfigsAsync();
                    var configDict = configs.ToDictionary(c => c.key, c => (object)c.value);
                    
                    // Specific mapping if needed (e.g. key aliases)
                    // The client likely merges `customConfigs` into its settings.
                    // We must ensure integers are integers if client JSON parser is strict.
                    var typedConfig = new Dictionary<string, object>();
                    foreach(var cfg in configs)
                    {
                        string k = cfg.key;
                        string confVal = cfg.value;
                         if (k == "fileUploadUrl") typedConfig["fileUpUrl"] = confVal;
                         else if (k == "tcpServerPort") typedConfig["server_port"] = int.Parse(confVal);
                         else if (k == "httpApiBaseUrl") typedConfig["apiBaseUrl"] = confVal;
                         else if (k == "keepWake" || k == "tokenExpiryMinutes") 
                         {
                             if(int.TryParse(confVal, out int iVal)) typedConfig[k] = iVal;
                             else typedConfig[k] = confVal;
                         }
                         else if (k == "autoLogin" || k == "autoPic" || k == "silentFunc" || k == "forceRun")
                         {
                             if(bool.TryParse(confVal, out bool bVal)) typedConfig[k] = bVal;
                             else typedConfig[k] = confVal;
                         }
                         else typedConfig[k] = confVal;
                    }
                    
                    // Add Netty settings as fallback
                    if (!typedConfig.ContainsKey("server_port")) typedConfig["server_port"] = _nettySettings.Port;
                    if (!typedConfig.ContainsKey("tcpServerHost")) typedConfig["tcpServerHost"] = _nettySettings.Host;

                    srClient.customConfigs = JsonSerializer.Serialize(typedConfig);
                    _logger.LogInformation("[HTTP登录] 已将系统配置植入 customConfigs 下发给客户端 (包含 keepWake={KeepWake})", typedConfig.ContainsKey("keepWake") ? typedConfig["keepWake"] : "N/A");
                }
                catch (Exception ex)
                {
                     _logger.LogError(ex, "[HTTP登录] 配置植入失败");
                }


                // Ensure account is in the list
                if (!srClient.accounts.Any(a => a.accountId == account.accountId))
                {
                    srClient.accounts.Add(account);
                }

                // Generate Token
                ApplicationUser? user = null;
                if (!string.IsNullOrEmpty(srClient.ownerId))
                {
                    user = await _context.Users.FindAsync(srClient.ownerId);
                }
                
                // Fallback: If no owner, try to find by RegCode if it's an email (Legacy)
                if (user == null && request.regCode.Contains("@"))
                {
                     user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.regCode);
                }

                if (user is SCRM.SHARED.Models.ApplicationUser validUser)
                {
                    srClient.token = await _authService.GenerateTokenAsync(validUser);
                     _logger.LogInformation("[生成Token] Login Success. Token generated for User: {UserId}", validUser.Id);
                }
                else
                {
                    _logger.LogWarning("[Login] User not found for Client: {ClientUuid}. Token generation skipped.", clientUuid);
                    // Optional: Create a temporary guest user? Or fail? 
                    // For now, let's log warning. If Client needs token, this will fail TCP.
                }

                await _context.SaveChangesAsync();

                return Ok(ApiResponse<SrClient>.Success(srClient));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Phone login error");
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
                    .FirstOrDefaultAsync(u => u.wxid == request.regCode && !u.isDeleted);

                if (account == null)
                {
                    return Ok(ApiResponse<SrClient>.Fail(1, "用户不存在"));
                }

                var srClient = new SrClient
                {
                    uuid = "VALIDATION_SUCCESS",
                    tcpHost = _nettySettings.Host,
                    tcpPort = _nettySettings.Port,
                    accounts = new List<WechatAccount> { account }
                };

                return Ok(ApiResponse<SrClient>.Success(srClient));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Phone validation error");
                return Ok(ApiResponse<SrClient>.Fail(-1, "验证失败"));
            }
        }
    }
}
