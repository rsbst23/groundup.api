using GroundUp.core.dtos;
using GroundUp.core.security;

namespace GroundUp.core.interfaces;

public interface IRolePolicyService
{
    [RequiresPermission("rolepolicies.view", "SYSTEMADMIN")]
    Task<ApiResponse<PaginatedData<RolePolicyDto>>> GetAllAsync(FilterParams filterParams);

    [RequiresPermission("rolepolicies.view", "SYSTEMADMIN")]
    Task<ApiResponse<RolePolicyDto>> GetByIdAsync(int id);

    [RequiresPermission("rolepolicies.create", "SYSTEMADMIN")]
    Task<ApiResponse<RolePolicyDto>> AddAsync(RolePolicyDto rolePolicyDto);

    [RequiresPermission("rolepolicies.update", "SYSTEMADMIN")]
    Task<ApiResponse<RolePolicyDto>> UpdateAsync(int id, RolePolicyDto rolePolicyDto);

    [RequiresPermission("rolepolicies.delete", "SYSTEMADMIN")]
    Task<ApiResponse<bool>> DeleteAsync(int id);
}
