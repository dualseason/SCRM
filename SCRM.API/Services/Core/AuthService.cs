using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SCRM.Models.Configurations;
using SCRM.Services.Data;
using SCRM.API.Models.Entities;
using SCRM.Services;
using SCRM.API.Services; 
using SCRM.UI.Services;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using SCRM.Models.Dtos;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using SCRM.SHARED.Models;

namespace SCRM.API.Services.Core
{
    /// <summary>
    /// 身份验证与权限服务
    /// <para>负责用户身份验证、JWT令牌生成与验证、以及基于角色的权限管理。</para>
    /// <para>核心职责：</para>
    /// <list type="bullet">
    /// <item>Identity 用户与 Legacy Wechat 用户鉴权</item>
    /// <item>JWT Token 生成、刷新与撤销</item>
    /// <item>设备所有权验证 (ValidateDeviceOwnership)</item>
    /// <item>权限与角色缓存管理</item>
    /// </list>
    /// </summary>
    public class AuthService
    {
        private readonly Microsoft.Extensions.Logging.ILogger<AuthService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly IMemoryCache _cache;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ConnectionManager _connectionManager;
        private readonly ISystemConfigService _configService;

        public AuthService(
            ApplicationDbContext context,
            IOptions<JwtSettings> jwtSettings,
            IMemoryCache cache,
            UserManager<ApplicationUser> userManager,
            ConnectionManager connectionManager,
            ISystemConfigService configService,
            Microsoft.Extensions.Logging.ILogger<AuthService> logger)
        {
            _context = context;
            _jwtSettings = jwtSettings.Value;
            _cache = cache;
            _userManager = userManager;
            _connectionManager = connectionManager;
            _configService = configService;
            _logger = logger;
        }

        private async Task<int> GetTokenExpiryMinutesAsync()
        {
            // Try cache first
            if (_cache.TryGetValue("config_tokenExpiryMinutes", out int cachedMinutes))
            {
                return cachedMinutes;
            }

            int expiryMinutes = 259200; // Default 180 days

            try 
            {
                var config = await _configService.GetConfigByKeyAsync("tokenExpiryMinutes");
                if (config != null && int.TryParse(config.value, out int val))
                {
                    expiryMinutes = val;
                }
                else
                {
                     // Seed default if missing
                     if (config == null)
                     {
                         await _configService.UpdateConfigAsync(new SystemConfig 
                         { 
                             key = "tokenExpiryMinutes", 
                             value = expiryMinutes.ToString(),
                             description = "Token Expiry in Minutes (Default 180 days)"
                         });
                     }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching token expiry from DB. Using default.");
            }

            // Cache for 10 minutes
            _cache.Set("config_tokenExpiryMinutes", expiryMinutes, TimeSpan.FromMinutes(10));
            return expiryMinutes;
        }

        #region JWT 逻辑

        /// <summary>
        /// 为 Standard Identity 用户生成令牌
        /// </summary>
        public async Task<string> GenerateTokenAsync(ApplicationUser user, string? deviceUuid = null)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            var roles = await _userManager.GetRolesAsync(user);
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? "Unknown"),
                new Claim("user_id", user.Id),
                new Claim("username", user.UserName ?? "Unknown")
            };

            if (!string.IsNullOrEmpty(deviceUuid))
            {
                claims.Add(new Claim("device_uuid", deviceUuid));
                _logger.LogInformation("Added device_uuid claim: {DeviceUuid}", deviceUuid);
            }
            else
            {
                _logger.LogWarning("GenerateTokenAsync called with empty deviceUuid for user {UserId}", user.Id);
            }

            if (!string.IsNullOrEmpty(user.Email)) claims.Add(new Claim(ClaimTypes.Email, user.Email));

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var expiryMinutes = await GetTokenExpiryMinutesAsync();
            return CreateJwtToken(claims, DateTime.UtcNow.AddMinutes(expiryMinutes));
        }

        /// <summary>
        /// 为旧版微信用户生成令牌
        /// </summary>
        public async Task<string> GenerateTokenAsync(LegacyWechatUser user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            var roles = await GetUserRolesAsync(user.Id.ToString());
            var permissions = await GetUserPermissionsAsync(user.Id.ToString());

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName ?? "Unknown"),
                new Claim("user_id", user.Id.ToString()),
                new Claim("username", user.UserName ?? "Unknown")
            };

            if (!string.IsNullOrEmpty(user.Email)) claims.Add(new Claim(ClaimTypes.Email, user.Email));
            if (!string.IsNullOrEmpty(user.FirstName)) claims.Add(new Claim("first_name", user.FirstName));
            if (!string.IsNullOrEmpty(user.LastName)) claims.Add(new Claim("last_name", user.LastName));

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            var expiryMinutes = await GetTokenExpiryMinutesAsync();
            return CreateJwtToken(claims, DateTime.UtcNow.AddMinutes(expiryMinutes));
        }

        /// <summary>
        /// 为设备账号生成长效令牌
        /// </summary>
        public string GenerateDeviceToken(WechatAccount device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, device.wxid),
                new Claim("device_imei", device.wechatNumber ?? ""),
                new Claim("device_uuid", device.clientUuid ?? ""),
                new Claim("is_device", "true")
            };

            return CreateJwtToken(claims, DateTime.UtcNow.AddDays(365));
        }

        private string CreateJwtToken(List<Claim> claims, DateTime? expires = null)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            
            // Enforce explicit expiry
            if (!expires.HasValue) 
            {
                 // Fallback to a safe default if somehow null (should not happen with updated callers)
                 expires = DateTime.UtcNow.AddDays(7); 
                 _logger.LogWarning("CreateJwtToken called without expiry. Using 7 days default.");
            }
            
            var expiry = expires.Value;

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: expiry,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// 生成刷新令牌
        /// </summary>
        public string GenerateRefreshTokenAsync<T>(T user) where T : class
        {
            string userId = user switch
            {
                ApplicationUser au => au.Id,
                LegacyWechatUser lu => lu.Id.ToString(),
                _ => throw new ArgumentException("不支持的用户类型")
            };

            var refreshToken = Guid.NewGuid().ToString("N");
            var cacheKey = $"refresh_token_{userId}";
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_jwtSettings.RefreshTokenExpiryDays)
            };
            _cache.Set(cacheKey, refreshToken, cacheOptions);
            _logger.LogInformation("已为用户 {UserId} 生成刷新令牌", userId);
            return refreshToken;
        }

        /// <summary>
        /// 验证刷新令牌
        /// </summary>
        public bool ValidateRefreshTokenAsync(string userId, string refreshToken)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(refreshToken))
                return false;

            var cacheKey = $"refresh_token_{userId}";
            var storedRefreshToken = _cache.Get<string?>(cacheKey);

            var isValid = storedRefreshToken == refreshToken;

            if (!isValid)
            {
                _logger.LogWarning("用户 {UserId} 的刷新令牌无效", userId);
            }

            return isValid;
        }

        /// <summary>
        /// 验证 JWT 令牌
        /// </summary>
        public ClaimsPrincipal? ValidateToken(string token)
        {
            return ValidateToken(token, validateLifetime: true);
        }

        /// <summary>
        /// 验证 JWT 令牌 (可选择忽略过期时间)
        /// </summary>
        public ClaimsPrincipal? ValidateToken(string token, bool validateLifetime)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    ValidateLifetime = validateLifetime, // Controlled by parameter
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                
                // Debug logging for claims
                var deviceClaim = principal.FindFirst("device_uuid");
                if (deviceClaim == null)
                {
                    _logger.LogWarning("Token validated but device_uuid claim is MISSING. Available claims: {Claims}", 
                        string.Join(", ", principal.Claims.Select(c => $"{c.Type}={c.Value}")));
                }

                return principal;
            }
            catch (Microsoft.IdentityModel.Tokens.SecurityTokenMalformedException)
            {
                _logger.LogWarning("令牌格式错误: {TokenPart}...", token.Length > 20 ? token.Substring(0, 20) : token);
                return null;
            }
            catch (Microsoft.IdentityModel.Tokens.SecurityTokenExpiredException)
            {
                _logger.LogWarning("令牌已过期 (Strict Mode)");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "令牌验证失败");
                return null;
            }
        }

        /// <summary>
        /// 撤销刷新令牌
        /// </summary>
        public void RevokeRefreshTokenAsync(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                var cacheKey = $"refresh_token_{userId}";
                _cache.Remove(cacheKey);
                _logger.LogInformation("已撤销用户 {UserId} 的刷新令牌", userId);
            }
        }

        /// <summary>
        /// 生成完整令牌响应 (Identity 用户)
        /// </summary>
        public async Task<TokenResponse> GenerateTokenResponseAsync(ApplicationUser user)
        {
            var token = await GenerateTokenAsync(user);
            var refreshToken = GenerateRefreshTokenAsync(user);
            var roles = await _userManager.GetRolesAsync(user);

            var expiryMinutes = await GetTokenExpiryMinutesAsync();

            return new TokenResponse
            {
                token = token,
                refreshToken = refreshToken,
                expiresAt = (long)(DateTime.UtcNow.AddMinutes(expiryMinutes) - new DateTime(1970, 1, 1)).TotalSeconds,
                user = new UserDto
                {
                    userName = user.UserName ?? string.Empty,
                    email = user.Email ?? string.Empty,
                    roles = roles.ToList()
                }
            };
        }

        /// <summary>
        /// 生成完整令牌响应 (旧版用户)
        /// </summary>
        public async Task<TokenResponse> GenerateTokenResponseAsync(LegacyWechatUser user)
        {
            var token = await GenerateTokenAsync(user);
            var refreshToken = GenerateRefreshTokenAsync(user);

            var roles = await GetUserRolesAsync(user.Id.ToString());
            var permissions = await GetUserPermissionsAsync(user.Id.ToString());

            var expiryMinutes = await GetTokenExpiryMinutesAsync();

            return new TokenResponse
            {
                token = token,
                refreshToken = refreshToken,
                expiresAt = (long)(DateTime.UtcNow.AddMinutes(expiryMinutes) - new DateTime(1970, 1, 1)).TotalSeconds,
                user = new UserDto
                {
                    id = user.Id.ToString(),
                    userName = user.UserName ?? string.Empty,
                    email = user.Email ?? string.Empty,
                    firstName = user.FirstName ?? string.Empty,
                    lastName = user.LastName ?? string.Empty,
                    roles = roles,
                    permissions = permissions
                }
            };
        }

        #endregion

        #region 权限与授权逻辑

        /// <summary>
        /// 验证用户是否有权操作指定连接的设备
        /// </summary>
        public async Task<bool> ValidateDeviceOwnershipAsync(ClaimsPrincipal userPrincipal, string connectionId)
        {
            var userId = userPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = userPrincipal?.Identity?.Name;
            var roles = userPrincipal?.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList() ?? new List<string>();

            if (string.IsNullOrEmpty(userId)) userId = userName;

            var isAdmin = userPrincipal?.IsInRole("SuperAdmin") == true || userPrincipal?.IsInRole("Admin") == true;
            
            if (isAdmin) 
            {
                return true;
            }

            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            if (connectionInfo == null) 
            {
                _logger.LogWarning("ValidateDeviceOwnership: 找不到连接 {ConnectionId}", connectionId);
                return false;
            }

            if (!string.IsNullOrEmpty(connectionInfo.userId))
            {
                var accountId = connectionInfo.userId;
                var ownerId = await _context.WechatAccounts
                    .Where(w => w.wxid == accountId && !w.isDeleted)
                    .Join(_context.SrClients, 
                          w => w.clientUuid, 
                          c => c.uuid, 
                          (w, c) => c.ownerId)
                    .FirstOrDefaultAsync();

                if (ownerId == userId || ownerId == null)
                {
                    return true;
                }
                
                _logger.LogWarning("ValidateDeviceOwnership: 拒绝访问。用户 {UserId} 无权操作连接 {ConnectionId} 的设备。设备所有者: {DeviceOwner}", userId, connectionId, ownerId);
                return false;
            }
            
            _logger.LogWarning("ValidateDeviceOwnership: 连接用户ID为空", connectionInfo.userId);
            return false;
        }

        public ClaimsPrincipal? ValidateTransportToken(string token) => ValidateToken(token);

        #region 设备管理

        /// <summary>
        /// 获取指定用户的可见设备列表
        /// </summary>
        public async Task<List<SrClient>> GetDevicesForUserAsync(string userId, bool isAdmin)
        {
            IQueryable<SrClient> query = _context.SrClients;

            if (!isAdmin && !string.IsNullOrEmpty(userId))
            {
                query = query.Where(c => c.ownerId == userId || c.ownerId == null);
            }

            var clientData = await query
                .GroupJoin(_context.WechatAccounts.Where(w => !w.isDeleted),
                    client => client.uuid,
                    account => account.clientUuid,
                    (client, accounts) => new { Client = client, Accounts = accounts })
                .SelectMany(
                    x => x.Accounts.DefaultIfEmpty(),
                    (x, account) => new { x.Client, Account = account })
                .ToListAsync();

            var result = new List<SrClient>();

            foreach (var item in clientData)
            {
                var client = item.Client;
                var account = item.Account;

                if (account != null)
                {
                    if (client.wx == null) client.wx = new Wx{ wechatAccount = account,srClient=client};
                    //client.weChatNick = account.nickname;
                    //client.wechatAccountId = account.accountId;

                    var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(client.uuid);
                    if (!string.IsNullOrEmpty(connectionId))
                    {
                        client.connectionId = connectionId;
                        client.isOnline = true;
                    }
                    else
                    {
                        client.connectionId = null;
                        client.isOnline = false;
                    }
                }

                if (!result.Any(r => r.uuid == client.uuid))
                {
                    result.Add(client);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取特定设备详情
        /// </summary>
        public async Task<SrClient?> GetDeviceAsync(string deviceUuid, string userId, bool isAdmin)
        {
            var client = await _context.SrClients.FirstOrDefaultAsync(c => c.uuid == deviceUuid);
            if (client == null) return null;

            if (!isAdmin && client.ownerId != null && client.ownerId != userId)
            {
                return null;
            }

            var account = await _context.WechatAccounts.FirstOrDefaultAsync(w => w.clientUuid == deviceUuid && !w.isDeleted);
            if (account != null)
            {

                //client.weChatNick = account.nickname;
                //client.wechatAccountId = account.accountId;

                var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
                if (!string.IsNullOrEmpty(connectionId))
                {
                    client.connectionId = connectionId;
                    client.isOnline = true;
                }
                else
                {
                    client.connectionId = null;
                    client.isOnline = false;
                }
            }

            return client;
        }

        #endregion

        #region 权限与角色缓存

        public async Task<bool> HasPermissionAsync(string userId, string permissionCode)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(permissionCode)) return false;
            var permissions = await GetUserPermissionsAsync(userId);
            return permissions.Contains(permissionCode);
        }

        public async Task<bool> HasAnyPermissionAsync(string userId, params string[] permissionCodes)
        {
            if (string.IsNullOrEmpty(userId) || permissionCodes == null || permissionCodes.Length == 0) return false;
            var permissions = await GetUserPermissionsAsync(userId);
            return permissionCodes.Any(code => permissions.Contains(code));
        }

        public async Task<bool> HasRoleAsync(string userId, string roleName)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleName)) return false;
            var roles = await GetUserRolesAsync(userId);
            return roles.Contains(roleName);
        }

        /// <summary>
        /// 获取用户所有权限（带缓存）
        /// </summary>
        public async Task<List<string>> GetUserPermissionsAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return new List<string>();
            var cacheKey = $"user_permissions_{userId}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                var permissions = await _context.userRoles
                    .Where(ur => ur.accountId == userId && ur.account != null && !ur.account.isDeleted)
                    .Include(ur => ur.role)
                    .Where(ur => ur.role != null && !ur.role.isDeleted)
                    .SelectMany(ur => ur.role!.rolePermissions)
                    .Include(rp => rp.permission)
                    .Where(rp => rp.permission != null && !rp.permission.isDeleted)
                    .Select(rp => rp.permission!.code)
                    .Distinct()
                    .ToListAsync();
                return permissions;
            }) ?? new List<string>();
        }

        /// <summary>
        /// 获取用户所有角色（带缓存）
        /// </summary>
        public async Task<List<string>> GetUserRolesAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return new List<string>();
            var cacheKey = $"user_roles_{userId}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                var roles = await _context.userRoles
                    .Where(ur => ur.accountId == userId && ur.account != null && !ur.account.isDeleted)
                    .Include(ur => ur.role)
                    .Where(ur => ur.role != null && !ur.role.isDeleted)
                    .Select(ur => ur.role!.roleName)
                    .ToListAsync();
                return roles;
            }) ?? new List<string>();
        }

        /// <summary>
        /// 获取所有可用权限（带缓存）
        /// </summary>
        public async Task<List<SCRM.API.Models.Entities.Permission>> GetAllPermissionsAsync()
        {
            return await _cache.GetOrCreateAsync("all_permissions", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return await _context.permissions
                    .Where(p => !p.isDeleted)
                    .OrderBy(p => p.module)
                    .ThenBy(p => p.sortOrder)
                    .ToListAsync();
            }) ?? new List<SCRM.API.Models.Entities.Permission>();
        }

        /// <summary>
        /// 获取所有角色（带缓存）
        /// </summary>
        public async Task<List<SCRM.API.Models.Entities.Role>> GetAllRolesAsync()
        {
            return await _cache.GetOrCreateAsync("all_roles", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return await _context.roles
                    .Where(r => !r.isDeleted)
                    .Include(r => r.rolePermissions)
                    .ThenInclude(rp => rp.permission)
                    .OrderBy(r => r.roleName)
                    .ToListAsync();
            }) ?? new List<SCRM.API.Models.Entities.Role>();
        }

        /// <summary>
        /// 获取完整用户信息及权限详情（带缓存）
        /// </summary>
        public async Task<UserPermissionInfo> GetUserPermissionInfoAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return new UserPermissionInfo();
            var cacheKey = $"user_permission_info_{userId}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                var user = await _context.WechatAccounts
                    .Where(u => u.wxid == userId && !u.isDeleted)
                    .Select(u => new UserDto 
                    { 
                        id = u.wxid, 
                        userName = u.wxid, 
                        firstName = u.nickname ?? string.Empty 
                    })
                    .FirstOrDefaultAsync();

                if (user == null) return new UserPermissionInfo();

                var roles = await GetUserRolesAsync(userId);
                var permissions = await GetUserPermissionsAsync(userId);

                return new UserPermissionInfo { user = user, roles = roles, permissions = permissions };
            }) ?? new UserPermissionInfo();
        }

        public void ClearUserPermissionCache(string userId)
        {
            _cache.Remove($"user_permissions_{userId}");
            _cache.Remove($"user_roles_{userId}");
            _cache.Remove($"user_permission_info_{userId}");
            _logger.LogDebug("已清除用户 {UserId} 的权限缓存", userId);
        }

        public void ClearAllCache()
        {
            _cache.Remove("all_permissions");
            _cache.Remove("all_roles");
        }

        public async Task<bool> HasAllPermissionsAsync(string userId, IEnumerable<string> permissions)
        {
            if (permissions == null || !permissions.Any()) return true;
            var userPermissions = await GetUserPermissionsAsync(userId);
            return permissions.All(p => userPermissions.Contains(p));
        }

        public async Task<bool> HasAnyPermissionAsync(string userId, IEnumerable<string> permissions)
        {
            if (permissions == null || !permissions.Any()) return false;
            var userPermissions = await GetUserPermissionsAsync(userId);
            return permissions.Any(p => userPermissions.Contains(p));
        }

        #endregion

        #endregion
    }
}

