using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SCRM.Models.Configurations;
using SCRM.Services.Data;
using SCRM.API.Models.Entities;
using SCRM.Services;
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

namespace SCRM.Services
{
    public class AuthService
    {
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

        private readonly ApplicationDbContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly IMemoryCache _cache;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ConnectionManager _connectionManager;

        public AuthService(
            ApplicationDbContext context,
            IOptions<JwtSettings> jwtSettings,
            IMemoryCache cache,
            UserManager<ApplicationUser> userManager,
            ConnectionManager connectionManager)
        {
            _context = context;
            _jwtSettings = jwtSettings.Value;
            _cache = cache;
            _userManager = userManager;
            _connectionManager = connectionManager;
        }

        #region JWT Logic

        public async Task<string> GenerateTokenAsync(ApplicationUser user)
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

            if (!string.IsNullOrEmpty(user.Email)) claims.Add(new Claim(ClaimTypes.Email, user.Email));

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            return CreateJwtToken(claims);
        }

        public async Task<string> GenerateTokenAsync(LegacyWechatUser user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            // Use cached permission/role logic
            var roles = await GetUserRolesAsync(user.Id);
            var permissions = await GetUserPermissionsAsync(user.Id);

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

            return CreateJwtToken(claims);
        }

        public string GenerateDeviceToken(WechatAccount device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, device.AccountId.ToString()),
                new Claim("device_imei", device.WechatNumber ?? ""),
                new Claim("device_uuid", device.ClientUuid ?? ""),
                new Claim("is_device", "true")
            };

            return CreateJwtToken(claims, DateTime.UtcNow.AddDays(365));
        }

        private string CreateJwtToken(List<Claim> claims, DateTime? expires = null)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiry = expires ?? DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: expiry,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshTokenAsync<T>(T user) where T : class
        {
            string userId = user switch
            {
                ApplicationUser au => au.Id,
                LegacyWechatUser lu => lu.Id.ToString(),
                _ => throw new ArgumentException("Unsupported user type")
            };

            var refreshToken = Guid.NewGuid().ToString("N");
            var cacheKey = $"refresh_token_{userId}";
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_jwtSettings.RefreshTokenExpiryDays)
            };
            _cache.Set(cacheKey, refreshToken, cacheOptions);
            _logger.Information("Generated refresh token for user {UserId}", userId);
            return refreshToken;
        }

        public bool ValidateRefreshTokenAsync(string userId, string refreshToken)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(refreshToken))
                return false;

            var cacheKey = $"refresh_token_{userId}";
            var storedRefreshToken = _cache.Get<string?>(cacheKey);

            var isValid = storedRefreshToken == refreshToken;

            if (!isValid)
            {
                _logger.Warning("Invalid refresh token for user {UserId}", userId);
            }

            return isValid;
        }

        public ClaimsPrincipal? ValidateToken(string token)
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
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return principal;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Token validation failed");
                return null;
            }
        }

        public void RevokeRefreshTokenAsync(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                var cacheKey = $"refresh_token_{userId}";
                _cache.Remove(cacheKey);
                _logger.Information("Revoked refresh token for user {UserId}", userId);
            }
        }

        // Consolidated from JwtService
        public async Task<TokenResponse> GenerateTokenResponseAsync(ApplicationUser user)
        {
            var token = await GenerateTokenAsync(user);
            var refreshToken = GenerateRefreshTokenAsync(user);
            var roles = await _userManager.GetRolesAsync(user);

            return new TokenResponse
            {
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
                User = new UserDto
                {
                    UserName = user.UserName,
                    Email = user.Email,
                    Roles = roles.ToList()
                }
            };
        }

        public async Task<TokenResponse> GenerateTokenResponseAsync(LegacyWechatUser user)
        {
            var token = await GenerateTokenAsync(user);
            var refreshToken = GenerateRefreshTokenAsync(user);

            var roles = await GetUserRolesAsync(user.Id);
            var permissions = await GetUserPermissionsAsync(user.Id);

            return new TokenResponse
            {
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
                User = new UserDto
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Roles = roles,
                    Permissions = permissions
                }
            };
        }

        #endregion

        #region Authorization Logic

        public async Task<bool> ValidateDeviceOwnershipAsync(ClaimsPrincipal userPrincipal, string connectionId)
        {
            var userId = userPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = userPrincipal?.Identity?.Name;
            var roles = userPrincipal?.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList() ?? new List<string>();

            if (string.IsNullOrEmpty(userId)) userId = userName; // Fallback

            var isAdmin = userPrincipal?.IsInRole("SuperAdmin") == true || userPrincipal?.IsInRole("Admin") == true;
            
            if (isAdmin) 
            {
                // _logger.Information("ValidateDeviceOwnership: User {UserId} is Admin/SuperAdmin. Allowed.", userId);
                return true;
            }

            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            if (connectionInfo == null) 
            {
                _logger.Warning("ValidateDeviceOwnership: Connection {ConnectionId} not found.", connectionId);
                return false;
            }

            if (long.TryParse(connectionInfo.UserId, out long accountId))
            {
                var ownerId = await _context.WechatAccounts
                    .Where(w => w.AccountId == accountId && !w.IsDeleted)
                    .Join(_context.SrClients, 
                          w => w.ClientUuid, 
                          c => c.uuid, 
                          (w, c) => c.OwnerId)
                    .FirstOrDefaultAsync();

                if (ownerId == userId || ownerId == null)
                {
                    return true;
                }
                
                _logger.Warning("ValidateDeviceOwnership: Forbidden. User {UserId} (Roles: {Roles}) does not own device connected at {ConnectionId}. DeviceOwner: {DeviceOwner}", userId, string.Join(",", roles), connectionId, ownerId);
                return false;
            }
            
            _logger.Warning("ValidateDeviceOwnership: Failed to parse Connection UserID '{ConnUserId}' as long.", connectionInfo.UserId);
            return false;
        }

        public ClaimsPrincipal? ValidateTransportToken(string token) => ValidateToken(token);

        #endregion

        #region Permission & Role Cache Logic (From PermissionService)

        public async Task<bool> HasPermissionAsync(int userId, string permissionCode)
        {
            if (userId <= 0 || string.IsNullOrEmpty(permissionCode)) return false;
            var permissions = await GetUserPermissionsAsync(userId);
            return permissions.Contains(permissionCode);
        }

        public async Task<bool> HasAnyPermissionAsync(int userId, params string[] permissionCodes)
        {
            if (userId <= 0 || permissionCodes == null || permissionCodes.Length == 0) return false;
            var permissions = await GetUserPermissionsAsync(userId);
            return permissionCodes.Any(code => permissions.Contains(code));
        }

        public async Task<bool> HasRoleAsync(int userId, string roleName)
        {
            if (userId <= 0 || string.IsNullOrEmpty(roleName)) return false;
            var roles = await GetUserRolesAsync(userId);
            return roles.Contains(roleName);
        }

        public async Task<List<string>> GetUserPermissionsAsync(long userId)
        {
            if (userId <= 0) return new List<string>();
            var cacheKey = $"user_permissions_{userId}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                var permissions = await _context.UserRoles
                    .Where(ur => ur.AccountId == userId && !ur.Account.IsDeleted)
                    .Include(ur => ur.Role)
                    .Where(ur => !ur.Role.IsDeleted)
                    .SelectMany(ur => ur.Role.RolePermissions)
                    .Include(rp => rp.Permission)
                    .Where(rp => !rp.Permission.IsDeleted)
                    .Select(rp => rp.Permission.Code)
                    .Distinct()
                    .ToListAsync();
                return permissions;
            });
        }

        public async Task<List<string>> GetUserRolesAsync(long userId)
        {
            if (userId <= 0) return new List<string>();
            var cacheKey = $"user_roles_{userId}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                var roles = await _context.UserRoles
                    .Where(ur => ur.AccountId == userId && !ur.Account.IsDeleted)
                    .Include(ur => ur.Role)
                    .Where(ur => !ur.Role.IsDeleted)
                    .Select(ur => ur.Role.Name)
                    .ToListAsync();
                return roles;
            });
        }

        public async Task<List<SCRM.API.Models.Entities.Permission>> GetAllPermissionsAsync()
        {
            return await _cache.GetOrCreateAsync("all_permissions", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return await _context.Permissions
                    .Where(p => !p.IsDeleted)
                    .OrderBy(p => p.Module)
                    .ThenBy(p => p.SortOrder)
                    .ToListAsync();
            });
        }

        public async Task<List<SCRM.API.Models.Entities.Role>> GetAllRolesAsync()
        {
            return await _cache.GetOrCreateAsync("all_roles", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return await _context.Roles
                    .Where(r => !r.IsDeleted)
                    .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                    .OrderBy(r => r.Name)
                    .ToListAsync();
            });
        }

        public async Task<UserPermissionInfo> GetUserPermissionInfoAsync(int userId)
        {
            if (userId <= 0) return new UserPermissionInfo();
            var cacheKey = $"user_permission_info_{userId}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                var user = await _context.WechatAccounts
                    .Where(u => u.AccountId == userId && !u.IsDeleted)
                    .Select(u => new UserDto { Id = u.AccountId, UserName = u.Wxid, FirstName = u.Nickname })
                    .FirstOrDefaultAsync();

                if (user == null) return new UserPermissionInfo();

                var roles = await GetUserRolesAsync(userId);
                var permissions = await GetUserPermissionsAsync(userId);

                return new UserPermissionInfo { User = user, Roles = roles, Permissions = permissions };
            });
        }

        public void ClearUserPermissionCache(int userId)
        {
            _cache.Remove($"user_permissions_{userId}");
            _cache.Remove($"user_roles_{userId}");
            _cache.Remove($"user_permission_info_{userId}");
            _logger.Debug("Cleared permission cache for user {UserId}", userId);
        }

        public void ClearAllCache()
        {
            _cache.Remove("all_permissions");
            _cache.Remove("all_roles");
        }

        public async Task<bool> HasAllPermissionsAsync(int userId, IEnumerable<string> permissions)
        {
            if (permissions == null || !permissions.Any()) return true;
            var userPermissions = await GetUserPermissionsAsync(userId);
            return permissions.All(p => userPermissions.Contains(p));
        }

        public async Task<bool> HasAnyPermissionAsync(int userId, IEnumerable<string> permissions)
        {
            if (permissions == null || !permissions.Any()) return false;
            var userPermissions = await GetUserPermissionsAsync(userId);
            return permissions.Any(p => userPermissions.Contains(p));
        }

        #endregion
    }
}
