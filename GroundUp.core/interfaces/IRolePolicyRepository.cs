using GroundUp.core.dtos;
using GroundUp.core.entities;

namespace GroundUp.core.interfaces
{
    public interface IRolePolicyRepository
    {
        Task<ApiResponse<List<RolePolicyDto>>> GetByRoleNameAsync(string roleName, RoleType roleType);
        Task<ApiResponse<List<PolicyDto>>> GetPoliciesByRoleAsync(string roleName, RoleType roleType);
        Task<ApiResponse<RolePolicyDto>> AssignPolicyToRoleAsync(RolePolicyDto rolePolicyDto);
        Task<ApiResponse<bool>> RemovePolicyFromRoleAsync(string roleName, RoleType roleType, int policyId);
    }
}
