using GroundUp.core.dtos;
using GroundUp.core.security;

namespace GroundUp.core.interfaces
{
    public interface ISystemRoleRepository
    {
        [RequiresPermission("systemroles.view", "SYSTEMADMIN")]
        Task<ApiResponse<List<RoleDto>>> GetAllAsync();

        [RequiresPermission("systemroles.view", "SYSTEMADMIN")]
        Task<ApiResponse<RoleDto>> GetByNameAsync(string name);

        [RequiresPermission("systemroles.policies.view", "SYSTEMADMIN")]
        Task<ApiResponse<List<PolicyDto>>> GetRolePoliciesAsync(string roleName);

        [RequiresPermission("systemroles.policies.assign", "SYSTEMADMIN")]
        Task<ApiResponse<RolePolicyDto>> AssignPolicyToRoleAsync(string roleName, int policyId);

        [RequiresPermission("systemroles.policies.remove", "SYSTEMADMIN")]
        Task<ApiResponse<bool>> RemovePolicyFromRoleAsync(string roleName, int policyId);
    }
}
