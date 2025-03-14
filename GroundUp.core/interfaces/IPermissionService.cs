using GroundUp.core.dtos;

namespace GroundUp.core.interfaces
{
    public interface IPermissionService
    {
        // Existing methods
        Task<bool> HasPermission(string userId, string permission);
        Task<bool> HasAnyPermission(string userId, string[] permissions);
        Task<IEnumerable<string>> GetUserPermissions(string userId);

        // Added methods for managing permissions
        Task<ApiResponse<List<PermissionDto>>> GetAllPermissionsAsync();
        Task<ApiResponse<PermissionDto>> GetPermissionByIdAsync(int id);
        Task<ApiResponse<PermissionDto>> GetPermissionByNameAsync(string name);
        Task<ApiResponse<PermissionDto>> CreatePermissionAsync(PermissionDto permissionDto);
        Task<ApiResponse<PermissionDto>> UpdatePermissionAsync(int id, PermissionDto permissionDto);
        Task<ApiResponse<bool>> DeletePermissionAsync(int id);

        // Methods for role-permission mappings
        Task<ApiResponse<List<RolePermissionDto>>> GetRolePermissionsAsync(string roleName);
        Task<ApiResponse<RolePermissionDto>> AssignPermissionToRoleAsync(string roleName, int permissionId);
        Task<ApiResponse<bool>> RemovePermissionFromRoleAsync(string roleName, int permissionId);

        // User-focused permission methods
        Task<ApiResponse<UserPermissionsDto>> GetUserPermissionsDetailedAsync(string userId);

        // Synchronization with Keycloak
        Task SynchronizeRolesWithKeycloakAsync();
    }
}