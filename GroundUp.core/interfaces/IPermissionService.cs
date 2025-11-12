using GroundUp.core.dtos;
using GroundUp.core.entities;

namespace GroundUp.core.interfaces
{
    public interface IPermissionService
    {
        // Core permission checking methods
        Task<bool> HasPermission(string userId, string permission);
        Task<bool> HasAnyPermission(string userId, string[] permissions);
        Task<IEnumerable<string>> GetUserPermissions(string userId);

        // Permission management methods
        Task<ApiResponse<List<PermissionDto>>> GetAllPermissionsAsync();
        Task<ApiResponse<PermissionDto>> GetPermissionByIdAsync(int id);
        Task<ApiResponse<PermissionDto>> GetPermissionByNameAsync(string name);
        Task<ApiResponse<PermissionDto>> CreatePermissionAsync(PermissionDto permissionDto);
        Task<ApiResponse<PermissionDto>> UpdatePermissionAsync(int id, PermissionDto permissionDto);
        Task<ApiResponse<bool>> DeletePermissionAsync(int id);

        // User-focused permission methods
        Task<ApiResponse<UserPermissionsDto>> GetUserPermissionsDetailedAsync(string userId);

        // System maintenance methods
        Task SynchronizeSystemRolesAsync();

        // Cache management
        void ClearPermissionCache();
        void ClearPermissionCacheForRole(string roleName, RoleType roleType);
    }
}