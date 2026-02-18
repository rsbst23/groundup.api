using GroundUp.Core.dtos;
using GroundUp.Core.entities;
using GroundUp.Core.interfaces;

namespace GroundUp.Tests.Integration
{
    internal sealed class TestPermissionService : IPermissionService
    {
        public Task<bool> HasPermission(string userId, string permission) => Task.FromResult(true);
        public Task<bool> HasAnyPermission(string userId, string[] permissions) => Task.FromResult(true);
        public Task<IEnumerable<string>> GetUserPermissions(string userId) => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        public Task<ApiResponse<List<PermissionDto>>> GetAllPermissionsAsync() => Task.FromResult(new ApiResponse<List<PermissionDto>>(new List<PermissionDto>()));
        public Task<ApiResponse<PermissionDto>> GetPermissionByIdAsync(int id) => Task.FromResult(new ApiResponse<PermissionDto>(default!));
        public Task<ApiResponse<PermissionDto>> GetPermissionByNameAsync(string name) => Task.FromResult(new ApiResponse<PermissionDto>(default!));
        public Task<ApiResponse<PermissionDto>> CreatePermissionAsync(PermissionDto permissionDto) => Task.FromResult(new ApiResponse<PermissionDto>(permissionDto));
        public Task<ApiResponse<PermissionDto>> UpdatePermissionAsync(int id, PermissionDto permissionDto) => Task.FromResult(new ApiResponse<PermissionDto>(permissionDto));
        public Task<ApiResponse<bool>> DeletePermissionAsync(int id) => Task.FromResult(new ApiResponse<bool>(true));
        public Task<ApiResponse<UserPermissionsDto>> GetUserPermissionsDetailedAsync(string userId) => Task.FromResult(new ApiResponse<UserPermissionsDto>(default!));

        public void ClearPermissionCache() { }
        public void ClearPermissionCacheForRole(string roleName, RoleType roleType) { }
    }
}
