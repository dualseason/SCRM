using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using SCRM.Services;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.Authorization
{
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IPermissionService _permissionService;
        private readonly ILogger<PermissionAuthorizationHandler> _logger;

        public PermissionAuthorizationHandler(
            IPermissionService permissionService,
            ILogger<PermissionAuthorizationHandler> logger)
        {
            _permissionService = permissionService;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("User ID claim not found or invalid");
                context.Fail();
                return;
            }

            var hasPermission = await _permissionService.HasPermissionAsync(userId, requirement.Permission);

            if (hasPermission)
            {
                _logger.LogDebug("User {UserId} has permission {Permission}", userId, requirement.Permission);
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("User {UserId} does not have permission {Permission}", userId, requirement.Permission);
                context.Fail();
            }
        }
    }

    public class PermissionsAuthorizationHandler : AuthorizationHandler<PermissionsRequirement>
    {
        private readonly IPermissionService _permissionService;
        private readonly ILogger<PermissionsAuthorizationHandler> _logger;

        public PermissionsAuthorizationHandler(
            IPermissionService permissionService,
            ILogger<PermissionsAuthorizationHandler> logger)
        {
            _permissionService = permissionService;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionsRequirement requirement)
        {
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("User ID claim not found or invalid");
                context.Fail();
                return;
            }

            var hasPermission = await _permissionService.HasAnyPermissionAsync(userId, requirement.Permissions);

            if (hasPermission)
            {
                _logger.LogDebug("User {UserId} has one of required permissions: {Permissions}",
                    userId, string.Join(", ", requirement.Permissions));
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("User {UserId} does not have any of required permissions: {Permissions}",
                    userId, string.Join(", ", requirement.Permissions));
                context.Fail();
            }
        }
    }

    public class RolesAuthorizationHandler : AuthorizationHandler<RolesRequirement>
    {
        private readonly IPermissionService _permissionService;
        private readonly ILogger<RolesAuthorizationHandler> _logger;

        public RolesAuthorizationHandler(
            IPermissionService permissionService,
            ILogger<RolesAuthorizationHandler> logger)
        {
            _permissionService = permissionService;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            RolesRequirement requirement)
        {
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("User ID claim not found or invalid");
                context.Fail();
                return;
            }

            var hasRole = await _permissionService.HasAnyRoleAsync(userId, requirement.Roles);

            if (hasRole)
            {
                _logger.LogDebug("User {UserId} has one of required roles: {Roles}",
                    userId, string.Join(", ", requirement.Roles));
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("User {UserId} does not have any of required roles: {Roles}",
                    userId, string.Join(", ", requirement.Roles));
                context.Fail();
            }
        }
    }
}