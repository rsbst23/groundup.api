using GroundUp.core.dtos;
using GroundUp.core.security;

namespace GroundUp.core.interfaces
{
    public interface IRolePolicyRepository
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

        // [RequiresPermission("rolepolicies.view", "SYSTEMADMIN")]
        // Task<ApiResponse<List<RolePolicyDto>>> GetByRoleNameAsync(string roleName, RoleType roleType);
        // [RequiresPermission("rolepolicies.view", "SYSTEMADMIN")]
        // Task<ApiResponse<List<PolicyDto>>> GetPoliciesByRoleAsync(string roleName, RoleType roleType);
        // [RequiresPermission("rolepolicies.assign", "SYSTEMADMIN")]
        // Task<ApiResponse<RolePolicyDto>> AssignPolicyToRoleAsync(RolePolicyDto rolePolicyDto);
        // [RequiresPermission("rolepolicies.remove", "SYSTEMADMIN")]
        // Task<ApiResponse<bool>> RemovePolicyFromRoleAsync(string roleName, RoleType roleType, int policyId);
    }
}
