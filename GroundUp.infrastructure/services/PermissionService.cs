using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace GroundUp.infrastructure.services
{
    public class PermissionService : IPermissionService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILoggingService _logger;
        private readonly IPermissionQueryRepository _permissionQueryRepository;
        private readonly IPermissionRepository _permissionRepository;
        private readonly IIdentityProviderAdminService _identityProviderAdminService;
        private readonly IMemoryCache _cache;
        private static readonly ConcurrentDictionary<string, bool> _cacheKeys = new ConcurrentDictionary<string, bool>();

        private const string CACHE_KEY_PERMISSIONS = "UserPermissions_";
        private const int CACHE_DURATION_MINUTES = 15;

        public PermissionService(
            IHttpContextAccessor httpContextAccessor,
            ILoggingService logger,
            IPermissionQueryRepository permissionQueryRepository,
            IPermissionRepository permissionRepository,
            IMemoryCache cache,
            IIdentityProviderAdminService identityProviderAdminService)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _permissionQueryRepository = permissionQueryRepository;
            _permissionRepository = permissionRepository;
            _cache = cache;
            _identityProviderAdminService = identityProviderAdminService;
        }

        public async Task<bool> HasPermission(string userId, string permission)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity?.IsAuthenticated == true)
            {
                return false;
            }

            // Check direct claim-based permission
            if (user.HasClaim(ClaimTypes.Role, permission) || user.IsInRole(permission))
            {
                return true;
            }

            // Check from database-stored permissions through policies
            var userPermissions = await GetUserPermissionsFromCacheOrDatabase(userId);
            return userPermissions.Contains(permission);
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
                _logger.LogError($"Error checking permissions: {ex.Message}", ex);
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

            var permissions = await _permissionQueryRepository.GetUserPermissionsAsync(userId);

            // Cache the permissions
            _cache.Set(cacheKey, permissions, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            AddCacheKey(cacheKey);

            return permissions;
        }

        public async Task<IEnumerable<string>> GetUserPermissions(string userId)
        {
            return await GetUserPermissionsFromCacheOrDatabase(userId);
        }

        public async Task<ApiResponse<List<PermissionDto>>> GetAllPermissionsAsync()
        {
            try
            {
                return await _permissionQueryRepository.GetAllPermissionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving all permissions: {ex.Message}", ex);
                return new ApiResponse<List<PermissionDto>>(
                    new List<PermissionDto>(),
                    false,
                    "Failed to retrieve permissions",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<PermissionDto>> GetPermissionByIdAsync(int id)
        {
            try
            {
                return await _permissionQueryRepository.GetPermissionByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving permission with ID {id}: {ex.Message}", ex);
                return new ApiResponse<PermissionDto>(
                    default!,
                    false,
                    "Failed to retrieve permission",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<PermissionDto>> GetPermissionByNameAsync(string name)
        {
            try
            {
                return await _permissionQueryRepository.GetPermissionByNameAsync(name);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving permission with name {name}: {ex.Message}", ex);
                return new ApiResponse<PermissionDto>(
                    default!,
                    false,
                    "Failed to retrieve permission",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public Task<ApiResponse<PermissionDto>> CreatePermissionAsync(PermissionDto permissionDto)
        {
            // Legacy contract; actual admin endpoints use `PermissionAdminService`.
            return _permissionRepository.AddAsync(permissionDto);
        }

        public async Task<ApiResponse<PermissionDto>> UpdatePermissionAsync(int id, PermissionDto permissionDto)
        {
            var result = await _permissionRepository.UpdateAsync(id, permissionDto);
            if (result.Success)
            {
                ClearPermissionCache();
            }

            return result;
        }

        public async Task<ApiResponse<bool>> DeletePermissionAsync(int id)
        {
            var result = await _permissionRepository.DeleteAsync(id);
            if (result.Success)
            {
                ClearPermissionCache();
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

                // Get permissions for these roles through policies
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

        public void ClearPermissionCache()
        {
            foreach (var key in _cacheKeys.Keys.ToList())
            {
                if (key.StartsWith(CACHE_KEY_PERMISSIONS))
                {
                    _cache.Remove(key);
                    _cacheKeys.TryRemove(key, out _);
                }
            }
            _logger.LogInformation("Permission cache cleared");
        }

        public void ClearPermissionCacheForRole(string roleName, RoleType roleType)
        {
            // For System roles, a more targeted approach could be implemented
            // but for now, we'll clear all caches to be safe
            ClearPermissionCache();
            _logger.LogInformation($"Permission cache cleared for role {roleName} of type {roleType}");
        }

        private void AddCacheKey(string key)
        {
            _cacheKeys.TryAdd(key, true);
        }
    }
}