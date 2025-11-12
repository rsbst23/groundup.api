using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace GroundUp.infrastructure.services
{
    public class PermissionService : IPermissionService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILoggingService _logger;
        private readonly ApplicationDbContext _context;
        private readonly IIdentityProviderAdminService _identityProviderAdminService;
        private readonly IMemoryCache _cache;
        private static readonly ConcurrentDictionary<string, bool> _cacheKeys = new ConcurrentDictionary<string, bool>();

        private const string CACHE_KEY_PERMISSIONS = "UserPermissions_";
        private const int CACHE_DURATION_MINUTES = 15;

        public PermissionService(
            IHttpContextAccessor httpContextAccessor,
            ILoggingService logger,
            ApplicationDbContext context,
            IMemoryCache cache,
            IIdentityProviderAdminService identityProviderAdminService)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _context = context;
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

            // Get roles for the user from the database
            var userRoles = await _context.UserRoles
                .Where(ur => ur.UserId.ToString() == userId)
                .Include(ur => ur.Role)
                .Select(ur => ur.Role.Name)
                .ToListAsync();

            // Get permissions for these roles through policies
            var permissions = await _context.RolePolicies
                .Where(rp => userRoles.Contains(rp.RoleName) && rp.RoleType == RoleType.System)
                .Join(_context.PolicyPermissions,
                      rp => rp.PolicyId,
                      pp => pp.PolicyId,
                      (rp, pp) => pp.PermissionId)
                .Join(_context.Permissions,
                      permId => permId,
                      perm => perm.Id,
                      (permId, perm) => perm.Name)
                .Distinct()
                .ToListAsync();

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
                var permissions = await _context.Permissions
                    .OrderBy(p => p.Group)
                    .ThenBy(p => p.Name)
                    .ToListAsync();

                var permissionDtos = permissions.Select(p => new PermissionDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Group = p.Group
                }).ToList();

                return new ApiResponse<List<PermissionDto>>(permissionDtos);
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
                var permission = await _context.Permissions.FindAsync(id);
                if (permission == null)
                {
                    return new ApiResponse<PermissionDto>(
                        default!,
                        false,
                        $"Permission with ID {id} not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                var permissionDto = new PermissionDto
                {
                    Id = permission.Id,
                    Name = permission.Name,
                    Description = permission.Description,
                    Group = permission.Group
                };

                return new ApiResponse<PermissionDto>(permissionDto);
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
                var permission = await _context.Permissions
                    .FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower());

                if (permission == null)
                {
                    return new ApiResponse<PermissionDto>(
                        default!,
                        false,
                        $"Permission with name '{name}' not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                var permissionDto = new PermissionDto
                {
                    Id = permission.Id,
                    Name = permission.Name,
                    Description = permission.Description,
                    Group = permission.Group
                };

                return new ApiResponse<PermissionDto>(permissionDto);
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

        public async Task<ApiResponse<PermissionDto>> CreatePermissionAsync(PermissionDto permissionDto)
        {
            try
            {
                // Check if permission with same name already exists
                var existingPermission = await _context.Permissions
                    .FirstOrDefaultAsync(p => p.Name.ToLower() == permissionDto.Name.ToLower());

                if (existingPermission != null)
                {
                    return new ApiResponse<PermissionDto>(
                        default!,
                        false,
                        $"Permission with name '{permissionDto.Name}' already exists",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.DuplicateEntry
                    );
                }

                var permission = new Permission
                {
                    Name = permissionDto.Name,
                    Description = permissionDto.Description,
                    Group = permissionDto.Group
                };

                _context.Permissions.Add(permission);
                await _context.SaveChangesAsync();

                permissionDto.Id = permission.Id;
                return new ApiResponse<PermissionDto>(
                    permissionDto,
                    true,
                    "Permission created successfully",
                    null,
                    StatusCodes.Status201Created
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating permission: {ex.Message}", ex);
                return new ApiResponse<PermissionDto>(
                    default!,
                    false,
                    "Failed to create permission",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<PermissionDto>> UpdatePermissionAsync(int id, PermissionDto permissionDto)
        {
            try
            {
                var permission = await _context.Permissions.FindAsync(id);
                if (permission == null)
                {
                    return new ApiResponse<PermissionDto>(
                        default!,
                        false,
                        $"Permission with ID {id} not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                // Check if new name conflicts with existing permission
                if (permission.Name != permissionDto.Name)
                {
                    var existingPermission = await _context.Permissions
                        .FirstOrDefaultAsync(p => p.Name.ToLower() == permissionDto.Name.ToLower() && p.Id != id);

                    if (existingPermission != null)
                    {
                        return new ApiResponse<PermissionDto>(
                            default!,
                            false,
                            $"Permission with name '{permissionDto.Name}' already exists",
                            null,
                            StatusCodes.Status400BadRequest,
                            ErrorCodes.DuplicateEntry
                        );
                    }
                }

                permission.Name = permissionDto.Name;
                permission.Description = permissionDto.Description;
                permission.Group = permissionDto.Group;

                await _context.SaveChangesAsync();

                // Clear cache since permission details have changed
                ClearPermissionCache();

                return new ApiResponse<PermissionDto>(
                    permissionDto,
                    true,
                    "Permission updated successfully"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating permission with ID {id}: {ex.Message}", ex);
                return new ApiResponse<PermissionDto>(
                    default!,
                    false,
                    "Failed to update permission",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<bool>> DeletePermissionAsync(int id)
        {
            try
            {
                var permission = await _context.Permissions.FindAsync(id);
                if (permission == null)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        $"Permission with ID {id} not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                // Check if permission is used in any policies
                var isUsed = await _context.PolicyPermissions
                    .AnyAsync(pp => pp.PermissionId == id);

                if (isUsed)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        "Cannot delete permission that is used in policies",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed
                    );
                }

                _context.Permissions.Remove(permission);
                await _context.SaveChangesAsync();

                // Clear cache since permissions have changed
                ClearPermissionCache();

                return new ApiResponse<bool>(
                    true,
                    true,
                    "Permission deleted successfully"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting permission with ID {id}: {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Failed to delete permission",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
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

        public async Task SynchronizeSystemRolesAsync()
        {
            try
            {
                _logger.LogInformation("Starting synchronization of roles with Keycloak");

                // Get all roles from Keycloak
                var keycloakRoles = await _identityProviderAdminService.GetAllRolesAsync();

                foreach (var role in keycloakRoles)
                {
                    // Check if this role has any policies assigned
                    var existingPolicies = await _context.RolePolicies
                        .AnyAsync(rp => rp.RoleName == role.Name && rp.RoleType == RoleType.System);

                    if (!existingPolicies)
                    {
                        _logger.LogInformation($"Detected System role '{role.Name}' without policy assignments");
                    }
                }

                _logger.LogInformation("Role synchronization with Keycloak completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error synchronizing roles with Keycloak: {ex.Message}", ex);
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