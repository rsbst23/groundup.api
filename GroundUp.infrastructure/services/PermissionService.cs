// PermissionService.cs
using System.Security.Claims;
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace GroundUp.infrastructure.services
{
    public class PermissionService : IPermissionService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILoggingService _logger;
        private readonly IPermissionRepository _permissionRepository;
        private readonly IRolePermissionRepository _rolePermissionRepository;
        private readonly IMemoryCache _cache;
        private readonly IKeycloakService _keycloakService;
        private static readonly ConcurrentDictionary<string, bool> _cacheKeys = new ConcurrentDictionary<string, bool>();


        private const string CACHE_KEY_PERMISSIONS = "UserPermissions_";
        private const int CACHE_DURATION_MINUTES = 15;

        public PermissionService(
            IHttpContextAccessor httpContextAccessor,
            ILoggingService logger,
            IPermissionRepository permissionRepository,
            IRolePermissionRepository rolePermissionRepository,
            IMemoryCache cache,
            IKeycloakService keycloakService)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _permissionRepository = permissionRepository;
            _rolePermissionRepository = rolePermissionRepository;
            _cache = cache;
            _keycloakService = keycloakService;
        }

        public async Task<bool> HasPermission(string userId, string permission)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null)
            {
                return false;
            }

            return true;
        }

        public async Task<bool> HasAnyPermission(string userId, string[] permissions)
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;
                if (user == null || !user.Identity?.IsAuthenticated == true)
                {
                    return false;
                }

                // Log user roles
                var roles = user.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                // Try direct claim-based permission check
                bool hasDirectPermission = permissions.Any(permission =>
                    user.HasClaim(ClaimTypes.Role, permission) || user.IsInRole(permission));

                if (hasDirectPermission)
                {
                    return true;
                }

                // Check from cache or DB
                var userPermissions = await GetUserPermissionsFromCacheOrDatabase(userId);
                bool hasPermissionFromDB = permissions.Any(p => userPermissions.Contains(p));

                return hasPermissionFromDB;
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        private async Task<List<string>> GetUserPermissionsFromCacheOrDatabase(string userId)
        {
            string cacheKey = $"{CACHE_KEY_PERMISSIONS}{userId}";

            // Try to get permissions from cache
            if (_cache.TryGetValue(cacheKey, out List<string> cachedPermissions))
            {
                return cachedPermissions;
            }

            // Get roles from user claims
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return new List<string>();

            var roles = user.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            // Get permissions for these roles from database
            var permissionsResult = await _permissionRepository.GetPermissionsByRoleNamesAsync(roles);
            var permissions = permissionsResult.Success ? permissionsResult.Data : new List<string>();

            // Cache the permissions
            _cache.Set(cacheKey, permissions, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            AddCacheKey(cacheKey);

            return permissions;
        }

        public async Task<IEnumerable<string>> GetUserPermissions(string userId)
        {
            return await GetUserPermissionsFromCacheOrDatabase(userId);
        }

        // Implement remaining methods...
        public async Task<ApiResponse<List<PermissionDto>>> GetAllPermissionsAsync()
        {
            var filterParams = new FilterParams { PageSize = 1000 }; // Get all permissions
            var result = await _permissionRepository.GetAllAsync(filterParams);
            return new ApiResponse<List<PermissionDto>>(
                result.Success ? result.Data.Items : new List<PermissionDto>(),
                result.Success,
                result.Message,
                result.Errors,
                result.StatusCode,
                result.ErrorCode
            );
        }

        public async Task<ApiResponse<PermissionDto>> GetPermissionByIdAsync(int id)
        {
            return await _permissionRepository.GetByIdAsync(id);
        }

        public async Task<ApiResponse<PermissionDto>> GetPermissionByNameAsync(string name)
        {
            return await _permissionRepository.GetByNameAsync(name);
        }

        public async Task<ApiResponse<PermissionDto>> CreatePermissionAsync(PermissionDto permissionDto)
        {
            return await _permissionRepository.AddAsync(permissionDto);
        }

        public async Task<ApiResponse<PermissionDto>> UpdatePermissionAsync(int id, PermissionDto permissionDto)
        {
            return await _permissionRepository.UpdateAsync(id, permissionDto);
        }

        public async Task<ApiResponse<bool>> DeletePermissionAsync(int id)
        {
            return await _permissionRepository.DeleteAsync(id);
        }

        public async Task<ApiResponse<List<RolePermissionDto>>> GetRolePermissionsAsync(string roleName)
        {
            return await _rolePermissionRepository.GetByRoleNameAsync(roleName);
        }

        public async Task<ApiResponse<RolePermissionDto>> AssignPermissionToRoleAsync(string roleName, int permissionId)
        {
            // First, check if the permission exists
            var permissionResult = await _permissionRepository.GetByIdAsync(permissionId);
            if (!permissionResult.Success)
            {
                return new ApiResponse<RolePermissionDto>(
                    default!,
                    false,
                    permissionResult.Message,
                    permissionResult.Errors,
                    permissionResult.StatusCode,
                    permissionResult.ErrorCode
                );
            }

            // Create role-permission mapping
            var rolePermissionDto = new RolePermissionDto
            {
                RoleName = roleName,
                PermissionId = permissionId,
                CreatedDate = DateTime.UtcNow
            };

            // Add the mapping
            var result = await _rolePermissionRepository.AddAsync(rolePermissionDto);

            // Clear cache for any user with this role
            ClearCacheForRole(roleName);

            return result;
        }

        public async Task<ApiResponse<bool>> RemovePermissionFromRoleAsync(string roleName, int permissionId)
        {
            var result = await _rolePermissionRepository.DeleteAsync(roleName, permissionId);

            // Clear cache for any user with this role
            if (result.Success)
            {
                ClearCacheForRole(roleName);
            }

            return result;
        }

        public async Task<ApiResponse<UserPermissionsDto>> GetUserPermissionsDetailedAsync(string userId)
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;
                if (user == null || !user.Identity?.IsAuthenticated == true)
                {
                    return new ApiResponse<UserPermissionsDto>(
                        new UserPermissionsDto { UserId = userId },
                        false,
                        "User not authenticated",
                        null,
                        StatusCodes.Status401Unauthorized,
                        ErrorCodes.Unauthorized
                    );
                }

                // Get roles from claims
                var roles = user.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                // Get permissions for these roles
                var permissions = await GetUserPermissionsFromCacheOrDatabase(userId);

                // Create result
                var result = new UserPermissionsDto
                {
                    UserId = userId,
                    Roles = roles,
                    Permissions = permissions
                };

                return new ApiResponse<UserPermissionsDto>(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting detailed permissions for user {userId}: {ex.Message}", ex);
                return new ApiResponse<UserPermissionsDto>(
                    new UserPermissionsDto { UserId = userId },
                    false,
                    "An error occurred while retrieving detailed permissions",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task SynchronizeRolesWithKeycloakAsync()
        {
            // This would be implemented to pull roles from Keycloak
            // For now, just log that this was attempted
            _logger.LogInformation("Synchronizing roles with Keycloak");

            // In a real implementation, you would:
            // 1. Fetch roles from Keycloak
            // 2. Update local role information
            // 3. Update permission mappings as needed
        }

        private void AddCacheKey(string key)
        {
            _cacheKeys.TryAdd(key, true);
        }

        private void ClearCacheForRole(string roleName)
        {
            foreach (var key in _cacheKeys.Keys.ToList())
            {
                if (key.StartsWith(CACHE_KEY_PERMISSIONS))
                {
                    _cache.Remove(key);
                    _cacheKeys.TryRemove(key, out _);
                }
            }
        }
    }
}