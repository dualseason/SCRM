using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace SCRM.Services.Auth
{
    public class PermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        public DefaultAuthorizationPolicyProvider FallbackPolicyProvider { get; }

        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        {
            FallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => FallbackPolicyProvider.GetDefaultPolicyAsync();

        public Task<AuthorizationPolicy> GetFallbackPolicyAsync() => FallbackPolicyProvider.GetFallbackPolicyAsync();

        public Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
        {
            if (policyName.StartsWith("Permission:", StringComparison.OrdinalIgnoreCase))
            {
                var permission = policyName.Substring("Permission:".Length);
                var policy = new AuthorizationPolicyBuilder();
                policy.AddRequirements(new SCRMRequirement(permissions: new[] { permission }));
                return Task.FromResult(policy.Build());
            }

            if (policyName.StartsWith("Permissions:", StringComparison.OrdinalIgnoreCase))
            {
                var permissions = policyName.Substring("Permissions:".Length).Split(',');
                var policy = new AuthorizationPolicyBuilder();
                policy.AddRequirements(new SCRMRequirement(permissions: permissions));
                return Task.FromResult(policy.Build());
            }

            if (policyName.StartsWith("Roles:", StringComparison.OrdinalIgnoreCase))
            {
                var roles = policyName.Substring("Roles:".Length).Split(',');
                var policy = new AuthorizationPolicyBuilder();
                policy.AddRequirements(new SCRMRequirement(roles: roles));
                return Task.FromResult(policy.Build());
            }

            return FallbackPolicyProvider.GetPolicyAsync(policyName);
        }
    }
}
