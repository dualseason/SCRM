using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using SCRM.Services;
using System.Threading.Tasks;
using System.Linq;

namespace SCRM.Services.Auth
{
    // 统一的 Requirement
    public class SCRMRequirement : IAuthorizationRequirement
    {
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

        public string[] Permissions { get; }
        public string[] Roles { get; }

        public SCRMRequirement(string[] permissions = null, string[] roles = null)
        {
            Permissions = permissions ?? Array.Empty<string>();
            Roles = roles ?? Array.Empty<string>();
        }
    }

    // 统一的 AuthorizationHandler
    public class SCRMAuthorizationHandler : AuthorizationHandler<SCRMRequirement>
    {
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

        private readonly IPermissionService _permissionService;
        
        public SCRMAuthorizationHandler(
            IPermissionService permissionService)
        {
            _permissionService = permissionService;        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            SCRMRequirement requirement)
        {
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.Warning("User ID claim not found or invalid");
                context.Fail();
                return;
            }

            bool authorized = false;

            // 检查权限
            if (requirement.Permissions.Any())
            {
                if (await _permissionService.HasAnyPermissionAsync(userId, requirement.Permissions))
                {
                    authorized = true;
                }
            }

            // 检查角色 (如果尚未授权)
            if (!authorized && requirement.Roles.Any())
            {
                if (await _permissionService.HasAnyRoleAsync(userId, requirement.Roles))
                {
                    authorized = true;
                }
            }

            if (authorized)
            {
                context.Succeed(requirement);
            }
            else
            {
                _logger.Warning("User {UserId} failed authorization. Required Permissions: {Permissions}, Roles: {Roles}",
                    userId, string.Join(",", requirement.Permissions), string.Join(",", requirement.Roles));
                context.Fail();
            }
        }
    }
}
