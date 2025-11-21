using SCRM.Models.Identity;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public interface IPermissionService
    {
        Task<bool> HasPermissionAsync(int userId, string permissionCode);
        Task<bool> HasAnyPermissionAsync(int userId, params string[] permissionCodes);
        Task<bool> HasAllPermissionsAsync(int userId, params string[] permissionCodes);
        Task<bool> HasRoleAsync(int userId, string roleName);
        Task<bool> HasAnyRoleAsync(int userId, params string[] roleNames);
        Task<List<string>> GetUserPermissionsAsync(int userId);
        Task<List<string>> GetUserRolesAsync(int userId);
        Task<List<Permission>> GetAllPermissionsAsync();
        Task<List<Role>> GetAllRolesAsync();
        Task<UserPermissionInfo> GetUserPermissionInfoAsync(int userId);
    }

    public class UserPermissionInfo
    {
        public UserDto User { get; set; } = null!;
        public List<string> Permissions { get; set; } = new();
        public List<string> Roles { get; set; } = new();
    }
}