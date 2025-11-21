using Microsoft.AspNetCore.Authorization;
using SCRM.Authorization;

namespace SCRM.Attributes
{
    public class RequirePermissionAttribute : AuthorizeAttribute
    {
        public RequirePermissionAttribute(string permission) : base($"Permission:{permission}")
        {
            Permission = permission;
        }

        public string Permission { get; }
    }

    public class RequirePermissionsAttribute : AuthorizeAttribute
    {
        public RequirePermissionsAttribute(params string[] permissions) : base($"Permissions:{string.Join(",", permissions)}")
        {
            Permissions = permissions;
        }

        public string[] Permissions { get; }
    }

    public class RequireRoleAttribute : AuthorizeAttribute
    {
        public RequireRoleAttribute(params string[] roles) : base($"Roles:{string.Join(",", roles)}")
        {
            Roles = roles;
        }

        public new string[] Roles { get; }
    }
}