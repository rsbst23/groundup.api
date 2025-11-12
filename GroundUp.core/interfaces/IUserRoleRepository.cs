using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.security;

namespace GroundUp.core.interfaces
{
    public interface IUserRoleRepository
    {
        [RequiresPermission("userroles.view", "SYSTEMADMIN")]
        Task<ApiResponse<PaginatedData<UserRoleDto>>> GetAllAsync(FilterParams filterParams);

        [RequiresPermission("userroles.view", "SYSTEMADMIN")]
        Task<ApiResponse<UserRoleDto>> GetByIdAsync(int id);

        [RequiresPermission("userroles.view", "SYSTEMADMIN")]
        Task<ApiResponse<UserRoleDto>> GetByNameAsync(string name);

        [RequiresPermission("userroles.create", "SYSTEMADMIN")]
        Task<ApiResponse<UserRoleDto>> AddAsync(UserRoleDto roleDto);

        [RequiresPermission("userroles.update", "SYSTEMADMIN")]
        Task<ApiResponse<UserRoleDto>> UpdateAsync(int id, UserRoleDto roleDto);

        [RequiresPermission("userroles.delete", "SYSTEMADMIN")]
        Task<ApiResponse<bool>> DeleteAsync(int id);
    }
}
