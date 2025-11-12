using GroundUp.core.dtos;
using GroundUp.core.security;

namespace GroundUp.core.interfaces
{
    public interface IPermissionRepository
    {
        [RequiresPermission("permissions.view", "SYSTEMADMIN")]
        Task<ApiResponse<PaginatedData<PermissionDto>>> GetAllAsync(FilterParams filterParams);

        [RequiresPermission("permissions.view", "SYSTEMADMIN")]
        Task<ApiResponse<PermissionDto>> GetByIdAsync(int id);

        [RequiresPermission("permissions.create", "SYSTEMADMIN")]
        Task<ApiResponse<PermissionDto>> AddAsync(PermissionDto permissionDto);

        [RequiresPermission("permissions.update", "SYSTEMADMIN")]
        Task<ApiResponse<PermissionDto>> UpdateAsync(int id, PermissionDto permissionDto);

        [RequiresPermission("permissions.delete", "SYSTEMADMIN")]
        Task<ApiResponse<bool>> DeleteAsync(int id);

        // [RequiresPermission("permissions.view", "SYSTEMADMIN")]
        // Task<ApiResponse<List<string>>> GetPermissionsByRoleNamesAsync(List<string> roleNames);
    }
}
