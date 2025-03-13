using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace GroundUp.infrastructure.services
{
    public class PermissionService : IPermissionService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILoggingService _logger;

        public PermissionService(
            IHttpContextAccessor httpContextAccessor,
            ILoggingService logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<bool> HasPermission(string userId, string permission)
        {
            return await HasAnyPermission(userId, new[] { permission });
        }

        public async Task<bool> HasAnyPermission(string userId, string[] permissions)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity?.IsAuthenticated == true)
            {
                _logger.LogInformation($"User not authenticated while checking permissions");
                return false;
            }

            // Check if any of the permissions match user's roles or claims
            bool hasPermission = permissions.Any(permission =>
                user.HasClaim(ClaimTypes.Role, permission) ||
                user.IsInRole(permission));

            _logger.LogInformation($"Checking permissions '{string.Join(", ", permissions)}' for user {userId}: {hasPermission}");

            return hasPermission;
        }

        public async Task<IEnumerable<string>> GetUserPermissions(string userId)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity?.IsAuthenticated == true)
            {
                return Enumerable.Empty<string>();
            }

            // Extract roles and claims as permissions
            return user.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct()
                .ToList();
        }

        // Placeholder methods for future implementation
        public async Task AssignPermissionToRole(string roleName, string permission)
        {
            // This would typically interact with Keycloak's role management
            _logger.LogInformation($"Attempted to assign permission {permission} to role {roleName}");
            throw new NotImplementedException("Role permission management is not implemented in this version.");
        }

        public async Task RemovePermissionFromRole(string roleName, string permission)
        {
            // This would typically interact with Keycloak's role management
            _logger.LogInformation($"Attempted to remove permission {permission} from role {roleName}");
            throw new NotImplementedException("Role permission management is not implemented in this version.");
        }

        public async Task<IEnumerable<string>> GetRolePermissions(string roleName)
        {
            // This would typically retrieve permissions associated with a role
            _logger.LogInformation($"Attempted to retrieve permissions for role {roleName}");
            throw new NotImplementedException("Role permission retrieval is not implemented in this version.");
        }
    }
}