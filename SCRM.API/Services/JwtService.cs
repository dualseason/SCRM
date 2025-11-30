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

namespace SCRM.Services
{
    public class JwtService
    {
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

        private readonly ApplicationDbContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly IMemoryCache _cache;
        
        public JwtService(
            ApplicationDbContext context,
            IOptions<JwtSettings> jwtSettings,
            IMemoryCache cache)
        {
            _context = context;
            _jwtSettings = jwtSettings.Value;
            _cache = cache;        }

        public async Task<string> GenerateTokenAsync(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            // 获取用户角色和权限
            var roles = await GetUserRolesAsync(user.Id);
            var permissions = await GetUserPermissionsAsync(user.Id);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("first_name", user.FirstName ?? ""),
                new Claim("last_name", user.LastName ?? ""),
                new Claim("user_id", user.Id.ToString()),
                new Claim("username", user.UserName)
            };

            // 添加角色claims
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // 添加权限claims
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshTokenAsync(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            var refreshToken = Guid.NewGuid().ToString("N");

            // 将refresh token存储到缓存中，设置过期时间
            var cacheKey = $"refresh_token_{user.Id}";
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_jwtSettings.RefreshTokenExpiryDays)
            };

            _cache.Set(cacheKey, refreshToken, cacheOptions);

            _logger.Information("Generated refresh token for user {UserId}", user.Id);
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

        public ClaimsPrincipal? ValidateTokenAsync(string token)
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

        public async Task<TokenResponse> GenerateTokenResponseAsync(User user)
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

        private async Task<List<string>> GetUserRolesAsync(long userId)
        {
            return await _context.UserRoles
                .Where(ur => ur.AccountId == userId)
                .Include(ur => ur.Role)
                .Where(ur => !ur.Role.IsDeleted)
                .Select(ur => ur.Role.Name)
                .ToListAsync();
        }

        private async Task<List<string>> GetUserPermissionsAsync(long userId)
        {
            return await _context.UserRoles
                .Where(ur => ur.AccountId == userId)
                .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .Where(ur => !ur.Role.IsDeleted)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Where(rp => !rp.Permission.IsDeleted)
                .Select(rp => rp.Permission.Code)
                .Distinct()
                .ToListAsync();
        }
    }
}