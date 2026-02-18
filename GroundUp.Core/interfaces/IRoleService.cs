using GroundUp.Core.dtos;
using GroundUp.Core.security;

namespace GroundUp.Core.interfaces;

public interface IRoleService
{
    [RequiresPermission("roles.view", "SYSTEMADMIN")]
    Task<ApiResponse<PaginatedData<RoleDto>>> GetAllAsync(FilterParams filterParams);

    [RequiresPermission("roles.view", "SYSTEMADMIN")]
    Task<ApiResponse<RoleDto>> GetByIdAsync(int id);

    [RequiresPermission("roles.create", "SYSTEMADMIN")]
    Task<ApiResponse<RoleDto>> AddAsync(RoleDto roleDto);

    [RequiresPermission("roles.update", "SYSTEMADMIN")]
    Task<ApiResponse<RoleDto>> UpdateAsync(int id, RoleDto roleDto);

    [RequiresPermission("roles.delete", "SYSTEMADMIN")]
    Task<ApiResponse<bool>> DeleteAsync(int id);
}
