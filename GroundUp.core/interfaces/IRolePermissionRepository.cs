using GroundUp.core.dtos;

namespace GroundUp.core.interfaces
{
    public interface IRolePermissionRepository
    {
        Task<ApiResponse<List<RolePermissionDto>>> GetByRoleNameAsync(string roleName);
        Task<ApiResponse<RolePermissionDto>> AddAsync(RolePermissionDto rolePermissionDto);
        Task<ApiResponse<bool>> DeleteAsync(string roleName, int permissionId);
        Task<ApiResponse<bool>> DeleteByRoleNameAsync(string roleName);
    }
}
