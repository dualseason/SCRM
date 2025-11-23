using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SCRM.Services.Data;
using SCRM.Models.Identity;
using SCRM.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SCRM.Models.Dtos;

namespace SCRM.Services
{
    public class PermissionService
    {
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        
        public PermissionService(
            ApplicationDbContext context,
            IMemoryCache cache)
        {
            _context = context;
            _cache = cache;        }

        public async Task<bool> HasPermissionAsync(int userId, string permissionCode)
        {
            if (userId <= 0 || string.IsNullOrEmpty(permissionCode))
                return false;

            var permissions = await GetUserPermissionsAsync(userId);
            return permissions.Contains(permissionCode);
        }

        public async Task<bool> HasAnyPermissionAsync(int userId, params string[] permissionCodes)
        {
            if (userId <= 0 || permissionCodes == null || permissionCodes.Length == 0)
                return false;

            var permissions = await GetUserPermissionsAsync(userId);
            return permissionCodes.Any(code => permissions.Contains(code));
        }

        public async Task<bool> HasAllPermissionsAsync(int userId, params string[] permissionCodes)
        {
            if (userId <= 0 || permissionCodes == null || permissionCodes.Length == 0)
                return false;

            var permissions = await GetUserPermissionsAsync(userId);
            return permissionCodes.All(code => permissions.Contains(code));
        }

        public async Task<bool> HasRoleAsync(int userId, string roleName)
        {
            if (userId <= 0 || string.IsNullOrEmpty(roleName))
                return false;

            var roles = await GetUserRolesAsync(userId);
            return roles.Contains(roleName);
        }

        public async Task<bool> HasAnyRoleAsync(int userId, params string[] roleNames)
        {
            if (userId <= 0 || roleNames == null || roleNames.Length == 0)
                return false;

            var roles = await GetUserRolesAsync(userId);
            return roleNames.Any(role => roles.Contains(role));
        }

        public async Task<List<string>> GetUserPermissionsAsync(int userId)
        {
            if (userId <= 0)
                return new List<string>();

            var cacheKey = $"user_permissions_{userId}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

                var permissions = await _context.UserRoles
                    .Where(ur => ur.UserId == userId && ur.User.IsActive)
                    .Include(ur => ur.Role)
                    .Where(ur => ur.Role.IsActive)
                    .SelectMany(ur => ur.Role.RolePermissions)
                    .Include(rp => rp.Permission)
                    .Where(rp => rp.Permission.IsActive)
                    .Select(rp => rp.Permission.Code)
                    .Distinct()
                    .ToListAsync();

                _logger.Debug("Loaded {Count} permissions for user {UserId}", permissions.Count, userId);
                return permissions;
            });
        }

        public async Task<List<string>> GetUserRolesAsync(int userId)
        {
            if (userId <= 0)
                return new List<string>();

            var cacheKey = $"user_roles_{userId}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

                var roles = await _context.UserRoles
                    .Where(ur => ur.UserId == userId && ur.User.IsActive)
                    .Include(ur => ur.Role)
                    .Where(ur => ur.Role.IsActive)
                    .Select(ur => ur.Role.Name)
                    .ToListAsync();

                _logger.Debug("Loaded {Count} roles for user {UserId}", roles.Count, userId);
                return roles;
            });
        }

        public async Task<List<Permission>> GetAllPermissionsAsync()
        {
            var cacheKey = "all_permissions";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

                var permissions = await _context.Permissions
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Module)
                    .ThenBy(p => p.SortOrder)
                    .ToListAsync();

                _logger.Debug("Loaded {Count} permissions from database", permissions.Count);
                return permissions;
            });
        }

        public async Task<List<Role>> GetAllRolesAsync()
        {
            var cacheKey = "all_roles";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

                var roles = await _context.Roles
                    .Where(r => r.IsActive)
                    .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                    .OrderBy(r => r.Name)
                    .ToListAsync();

                _logger.Debug("Loaded {Count} roles from database", roles.Count);
                return roles;
            });
        }

        public async Task<UserPermissionInfo> GetUserPermissionInfoAsync(int userId)
        {
            if (userId <= 0)
                return new UserPermissionInfo();

            var cacheKey = $"user_permission_info_{userId}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);

                var user = await _context.IdentityUsers
                    .Where(u => u.Id == userId && u.IsActive)
                    .Select(u => new UserDto
                    {
                        Id = u.Id,
                        UserName = u.UserName,
                        Email = u.Email,
                        FirstName = u.FirstName,
                        LastName = u.LastName
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return new UserPermissionInfo();

                var roles = await GetUserRolesAsync(userId);
                var permissions = await GetUserPermissionsAsync(userId);

                var userPermissionInfo = new UserPermissionInfo
                {
                    User = user,
                    Roles = roles,
                    Permissions = permissions
                };

                _logger.Debug("Loaded permission info for user {UserId}: {RolesCount} roles, {PermissionsCount} permissions",
                    userId, roles.Count, permissions.Count);

                return userPermissionInfo;
            });
        }

        public void ClearUserPermissionCache(int userId)
        {
            var keysToRemove = new[]
            {
                $"user_permissions_{userId}",
                $"user_roles_{userId}",
                $"user_permission_info_{userId}"
            };

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }

            _logger.Debug("Cleared permission cache for user {UserId}", userId);
        }

        public void ClearAllCache()
        {
            var keysToRemove = new[]
            {
                "all_permissions",
                "all_roles"
            };

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }

            _logger.Debug("Cleared all permission/role caches");
        }
    }
}