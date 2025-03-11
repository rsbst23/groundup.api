// GroundUp.infrastructure/services/PermissionService.cs
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
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity?.IsAuthenticated == true)
            {
                return false;
            }

            // With Keycloak, permissions are usually mapped to roles
            // Check if the user has a role that matches the permission
            var hasRole = user.HasClaim(ClaimTypes.Role, permission);

            // For more complex permission checks, you might want to map
            // between roles and permissions in a more sophisticated way

            _logger.LogInformation($"Checking permission '{permission}' for user {userId}: {hasRole}");

            return hasRole;
        }
    }
}