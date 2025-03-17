using GroundUp.core.dtos;

namespace GroundUp.core.interfaces
{
    public interface IRoleRepository
    {
        Task<ApiResponse<PaginatedData<RoleDto>>> GetAllAsync(FilterParams filterParams);
        Task<ApiResponse<RoleDto>> GetByNameAsync(string name);
        Task<ApiResponse<RoleDto>> CreateAsync(CreateRoleDto roleDto);
        Task<ApiResponse<RoleDto>> UpdateAsync(string name, UpdateRoleDto roleDto);
        Task<ApiResponse<bool>> DeleteAsync(string name);
    }
}
