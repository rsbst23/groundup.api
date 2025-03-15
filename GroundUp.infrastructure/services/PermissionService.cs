using GroundUp.core;
using GroundUp.core.dtos;
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
        private readonly IPermissionRepository _permissionRepository;
        private readonly IRolePermissionRepository _rolePermissionRepository;
        private readonly IKeycloakAdminService _keycloakAdminService;
        private readonly IMemoryCache _cache;
        private static readonly ConcurrentDictionary<string, bool> _cacheKeys = new ConcurrentDictionary<string, bool>();


        private const string CACHE_KEY_PERMISSIONS = "UserPermissions_";
        private const int CACHE_DURATION_MINUTES = 15;

        public PermissionService(
            IHttpContextAccessor httpContextAccessor,
            ILoggingService logger,
            IPermissionRepository permissionRepository,
            IRolePermissionRepository rolePermissionRepository,
            IMemoryCache cache,
            IKeycloakAdminService keycloakAdminService)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _permissionRepository = permissionRepository;
            _rolePermissionRepository = rolePermissionRepository;
            _cache = cache;
            _keycloakAdminService = keycloakAdminService;
        }

        public async Task<bool> HasPermission(string userId, string permission)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null)
            {
                return false;
            }

            // Check direct claim-based permission
            if (user.HasClaim(ClaimTypes.Role, permission) || user.IsInRole(permission))
            {
                return true;
            }

            // Check from database-stored permissions
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

        #region Permission CRUD Operations

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

        #endregion

        #region Role-Permission Mapping Operations

        public async Task<ApiResponse<List<RolePermissionMappingDto>>> GetAllRolePermissionMappingsAsync()
        {
            try
            {
                // Get all roles from Keycloak
                var roles = await _keycloakAdminService.GetAllRolesAsync();

                var mappings = new List<RolePermissionMappingDto>();

                // For each role, get its permissions
                foreach (var role in roles)
                {
                    var permissionsResult = await _rolePermissionRepository.GetByRoleNameAsync(role.Name);

                    var permissionDtos = new List<PermissionDto>();
                    if (permissionsResult.Success && permissionsResult.Data.Any())
                    {
                        // Get the details of each permission
                        foreach (var rolePermission in permissionsResult.Data)
                        {
                            var permissionResult = await _permissionRepository.GetByIdAsync(rolePermission.PermissionId);
                            if (permissionResult.Success && permissionResult.Data != null)
                            {
                                permissionDtos.Add(permissionResult.Data);
                            }
                        }
                    }

                    mappings.Add(new RolePermissionMappingDto
                    {
                        RoleName = role.Name,
                        Permissions = permissionDtos
                    });
                }

                return new ApiResponse<List<RolePermissionMappingDto>>(mappings);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting all role-permission mappings: {ex.Message}", ex);
                return new ApiResponse<List<RolePermissionMappingDto>>(
                    new List<RolePermissionMappingDto>(),
                    false,
                    "Failed to retrieve role-permission mappings",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
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

        public async Task<ApiResponse<bool>> AssignMultiplePermissionsToRoleAsync(string roleName, List<int> permissionIds)
        {
            try
            {
                // Check if the role exists in Keycloak
                var role = await _keycloakAdminService.GetRoleByNameAsync(roleName);
                if (role == null)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        $"Role '{roleName}' not found in Keycloak",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                // Check if all permissions exist
                var validPermissionIds = new List<int>();
                foreach (var permissionId in permissionIds)
                {
                    var permissionResult = await _permissionRepository.GetByIdAsync(permissionId);
                    if (permissionResult.Success)
                    {
                        validPermissionIds.Add(permissionId);
                    }
                    else
                    {
                        _logger.LogWarning($"Permission with ID {permissionId} not found, skipping");
                    }
                }

                if (validPermissionIds.Count == 0)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        "No valid permissions found to assign",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed
                    );
                }

                // Assign each permission
                int successCount = 0;
                foreach (var permissionId in validPermissionIds)
                {
                    var result = await AssignPermissionToRoleAsync(roleName, permissionId);
                    if (result.Success)
                    {
                        successCount++;
                    }
                }

                // Clear cache for any user with this role
                ClearCacheForRole(roleName);

                return new ApiResponse<bool>(
                    true,
                    true,
                    $"Successfully assigned {successCount} of {validPermissionIds.Count} permissions to role '{roleName}'"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error assigning multiple permissions to role '{roleName}': {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    $"Failed to assign permissions to role '{roleName}'",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
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

        #endregion

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
            try
            {
                _logger.LogInformation("Starting synchronization of roles with Keycloak");

                // Get all roles from Keycloak
                var keycloakRoles = await _keycloakAdminService.GetAllRolesAsync();

                // Get all role-permission mappings from the database
                var allMappingsResponse = await GetAllRolePermissionMappingsAsync();

                if (!allMappingsResponse.Success)
                {
                    _logger.LogError("Failed to retrieve existing role-permission mappings");
                    return;
                }

                var existingMappings = allMappingsResponse.Data;

                // Find roles in Keycloak that don't have entries in our database
                var missingRoles = keycloakRoles
                    .Where(kr => !existingMappings.Any(em => em.RoleName == kr.Name))
                    .ToList();

                _logger.LogInformation($"Found {missingRoles.Count} Keycloak roles not yet mapped in the database");

                // For each missing role, create an empty role-permission mapping entry
                foreach (var role in missingRoles)
                {
                    _logger.LogInformation($"Creating empty permission mapping for role '{role.Name}'");
                    // We don't need to create any specific permissions; just having the role name
                    // in our database is sufficient for future mappings
                }

                // Optionally, find roles in our database that don't exist in Keycloak (deleted roles)
                var deletedRoles = existingMappings
                    .Where(em => !keycloakRoles.Any(kr => kr.Name == em.RoleName))
                    .ToList();

                _logger.LogInformation($"Found {deletedRoles.Count} mapped roles that no longer exist in Keycloak");

                // For each deleted role, remove its permission mappings
                foreach (var mapping in deletedRoles)
                {
                    _logger.LogInformation($"Removing permission mappings for deleted role '{mapping.RoleName}'");
                    await _rolePermissionRepository.DeleteByRoleNameAsync(mapping.RoleName);
                }

                _logger.LogInformation("Role synchronization with Keycloak completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error synchronizing roles with Keycloak: {ex.Message}", ex);
            }
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