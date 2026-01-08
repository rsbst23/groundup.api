using GroundUp.core.dtos;

namespace GroundUp.core.interfaces
{
    public interface IPermissionRepository
    {
        Task<ApiResponse<PaginatedData<PermissionDto>>> GetAllAsync(FilterParams filterParams);

        Task<ApiResponse<PermissionDto>> GetByIdAsync(int id);

        Task<ApiResponse<PermissionDto>> AddAsync(PermissionDto permissionDto);

        Task<ApiResponse<PermissionDto>> UpdateAsync(int id, PermissionDto permissionDto);

        Task<ApiResponse<bool>> DeleteAsync(int id);

        // Task<ApiResponse<List<string>>> GetPermissionsByRoleNamesAsync(List<string> roleNames);
    }
}
