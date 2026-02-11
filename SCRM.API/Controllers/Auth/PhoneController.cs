using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCRM.API.Filters;
using SCRM.API.Models.DTOs;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Core;
using SCRM.API.Utils;
using SCRM.Services;
using SCRM.Services.Data;
using SCRM.SHARED.Models;
using SCRM.UI.Services;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static SCRM.Models.Constants.Permissions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SCRM.Controllers.Auth
{
    [ApiController]
    [Route("")] // Root route to match /phone_login directly
    public class PhoneController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AuthService _authService;
        private readonly Microsoft.Extensions.Logging.ILogger<PhoneController> _logger;
        // _nettySettings removed
        private readonly ISystemConfigService _configService;

        public PhoneController(ApplicationDbContext context, AuthService authService, Microsoft.Extensions.Logging.ILogger<PhoneController> logger, ISystemConfigService configService)
        {
            _context = context;
            _authService = authService;
            _logger = logger;
            // _nettySettings removed
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

                // 严格从数据库获取配置
                var tcpHostCfg = await _configService.GetConfigByKeyAsync("tcpServerHost");
                var tcpPortCfg = await _configService.GetConfigByKeyAsync("server_port");
                string tcpHost = tcpHostCfg?.value ?? "";
                int tcpPort = 0;
                if (tcpPortCfg != null) int.TryParse(tcpPortCfg.value, out tcpPort);

                // 1. Find User by Email
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.userEmail);
                if (user == null)
                {
                    return Ok(ApiResponse<UserAuthToken>.Fail(1, "用户不存在"));
                }

                // 2. Find or Create SrClient
                var srClient = await _context.SrClients.FirstOrDefaultAsync(c => c.uuid == clientUuid);
                if (srClient == null)
                {
                    srClient = new SrClient
                    {
                        uuid = clientUuid,
                        createdAt = DateTime.UtcNow,
                        tcpHost = tcpHost,
                        tcpPort = tcpPort,
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
                    // 确保现有客户端的 TCP 配置也已更新!
                    srClient.tcpHost = tcpHost;
                    srClient.tcpPort = tcpPort;
                }

                // 3. Bind Client to User
                srClient.ownerId = user.Id.ToString();

                await _context.SaveChangesAsync();

                // 4. Return Token
                var token = new UserAuthToken
                {
                    userId = user.Id.ToString(),
                    token = await _authService.GenerateTokenAsync(user, clientUuid),
                    tcpHost = srClient.tcpHost,
                    tcpPort = srClient.tcpPort
                };

                // [修正] 将系统配置注入 customConfigs 以便客户端立即获取
                try
                {
                    var configs = await _configService.GetConfigsAsync();
                    var typedConfig = new Dictionary<string, object>();
                    foreach (var cfg in configs)
                    {
                        string k = cfg.key;
                        string value = cfg.value;

                        // 键映射 (兼容旧版)
                        if (k == "fileUploadUrl") k = "fileUpUrl";
                        else if (k == "tcpServerPort") k = "server_port";
                        else if (k == "httpApiBaseUrl") k = "apiBaseUrl";

                        // 类型推断 & 分组
                        if (bool.TryParse(value, out bool bVal)) typedConfig[k] = bVal;
                        else if (int.TryParse(value, out int iVal)) typedConfig[k] = iVal;
                        else typedConfig[k] = value;
                    }

                    // 无需兜底，因为如果 DB 存在我们已经有了。
                    // 但这里显式确保 "server_port" 和 "tcpServerHost" 与上面的严格查询一致
                    if (tcpPort > 0) typedConfig["server_port"] = tcpPort;
                    if (!string.IsNullOrEmpty(tcpHost)) typedConfig["tcpServerHost"] = tcpHost;

                    srClient.customConfigs = JsonSerializer.Serialize(typedConfig);
                    _logger.LogInformation("[HTTP登录] 已将系统配置植入 PhoneRegisterResponse.customConfigs 下发给客户端 (Host={Host}, Port={Port})", tcpHost, tcpPort);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[HTTP登录] 配置植入失败");
                }

                var phoneRegisterResponse = new PhoneRegisterResponse
                {
                    token = token,
                    config = srClient.customConfigs?? "{}"
                };

                _logger.LogInformation("[生成Token] 为客户端 {ClientUuid} 生成带 device_uuid 声明的Token (UserId: {UserId})", clientUuid, user.Id);

                return Ok(ApiResponse<PhoneRegisterResponse>.Success(phoneRegisterResponse));
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

                // 严格从数据库获取配置
                var tcpHostCfg = await _configService.GetConfigByKeyAsync("tcpServerHost");
                var tcpPortCfg = await _configService.GetConfigByKeyAsync("server_port");
                
                string tcpHost = tcpHostCfg?.value ?? "";
                int tcpPort = 0;
                if (tcpPortCfg != null) int.TryParse(tcpPortCfg.value, out tcpPort);

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
                //var srClient = await _context.SrClients.Include(c => c.accounts).FirstOrDefaultAsync(c => c.uuid == clientUuid);
                var srClient = await _context.SrClients.FirstOrDefaultAsync(c => c.uuid == clientUuid);

                if (srClient == null)
                {
                    srClient = new SrClient
                    {
                        uuid = clientUuid,
                        createdAt = DateTime.UtcNow
                    };
                    _context.SrClients.Add(srClient);
                }
                srClient.wx=new Wx { wechatAccount = account,srClient=srClient };

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
                
                // Inject DB Configs
                srClient.tcpHost = tcpHost;
                srClient.tcpPort = tcpPort;
                srClient.updatedAt = DateTime.UtcNow;
                srClient.ip = clientIp;
                srClient.lastLoginAt = DateTime.UtcNow;
                srClient.isOnline = true;
                srClient.status = 1;

                // [修正] 将系统配置注入 customConfigs 以便客户端立即获取
                try 
                {
                    var configs = await _configService.GetConfigsAsync();
                    var typedConfig = new Dictionary<string, object>();
                    foreach(var cfg in configs)
                    {
                        string k = cfg.key;
                        string value = cfg.value;

                        // 键映射 (兼容旧版)
                        if (k == "fileUploadUrl") k = "fileUpUrl";
                        else if (k == "tcpServerPort") k = "server_port";
                        else if (k == "httpApiBaseUrl") k = "apiBaseUrl";

                        // 类型推断 & 分组
                        if (bool.TryParse(value, out bool bVal)) typedConfig[k] = bVal;
                        else if (int.TryParse(value, out int iVal)) typedConfig[k] = iVal;
                        else typedConfig[k] = value;
                    }
                    
                    // 无需兜底，因为如果 DB 存在我们已经有了。
                    // 但这里显式确保 "server_port" 和 "tcpServerHost" 与上面的严格查询一致
                    if (tcpPort > 0) typedConfig["server_port"] = tcpPort;
                    if (!string.IsNullOrEmpty(tcpHost)) typedConfig["tcpServerHost"] = tcpHost;

                    srClient.customConfigs = JsonSerializer.Serialize(typedConfig);
                    _logger.LogInformation("[HTTP登录] 已将系统配置植入 customConfigs 下发给客户端 (Host={Host}, Port={Port})", tcpHost, tcpPort);
                }
                catch (Exception ex)
                {
                     _logger.LogError(ex, "[HTTP登录] 配置植入失败");
                }

                // 账户绑定现由 Wx 对象引用处理
                // 兼容性说明: 移除了旧版 'accounts' 列表 

                // 生成 Token
                ApplicationUser? user = null;
                if (!string.IsNullOrEmpty(srClient.ownerId))
                {
                    user = await _context.Users.FindAsync(srClient.ownerId);
                }
                
                // 兜底: 若无 owner，尝试通过 RegCode 如果是邮箱查找 (旧版)
                if (user == null && request.regCode.Contains("@"))
                {
                     user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.regCode);
                }

                if (user == null)
                {
                    _logger.LogInformation("[Login] Device {ClientUuid} has no owner. using fallback user for token.", clientUuid);
                    user = await _context.Users.OrderBy(u => u.Id).FirstOrDefaultAsync();
                    if (user != null && string.IsNullOrEmpty(srClient.ownerId))
                    {
                         srClient.ownerId = user.Id;
                    }
                }

                if (user is SCRM.SHARED.Models.ApplicationUser validUser)
                {
                    srClient.token = await _authService.GenerateTokenAsync(validUser, clientUuid);
                    _logger.LogInformation("[生成Token] Login Success. Token generated for User: {UserId}, Device: {ClientUuid}", validUser.Id, clientUuid);
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

                // 严格从数据库获取配置
                var tcpHostCfg = await _configService.GetConfigByKeyAsync("tcpServerHost");
                var tcpPortCfg = await _configService.GetConfigByKeyAsync("server_port");
                string tcpHost = tcpHostCfg?.value ?? "";
                int tcpPort = 0;
                if (tcpPortCfg != null) int.TryParse(tcpPortCfg.value, out tcpPort);

                var wx = new Wx { wechatAccount = account };
                var srClient = new SrClient
                {
                    uuid = "VALIDATION_SUCCESS",
                    tcpHost = tcpHost,
                    tcpPort = tcpPort,
                    wx = wx,
                };
                wx.srClient = srClient;

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
