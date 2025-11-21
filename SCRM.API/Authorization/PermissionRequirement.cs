using Microsoft.AspNetCore.Authorization;

namespace SCRM.Authorization
{
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string Permission { get; }

        public PermissionRequirement(string permission)
        {
            Permission = permission ?? throw new ArgumentNullException(nameof(permission));
        }
    }

    public class PermissionsRequirement : IAuthorizationRequirement
    {
        public string[] Permissions { get; }

        public PermissionsRequirement(params string[] permissions)
        {
            Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        }
    }

    public class RolesRequirement : IAuthorizationRequirement
    {
        public string[] Roles { get; }

        public RolesRequirement(params string[] roles)
        {
            Roles = roles ?? throw new ArgumentNullException(nameof(roles));
        }
    }
}