using GroundUp.core.dtos;

namespace GroundUp.core.interfaces
{
    public interface ISystemRoleRepository
    {
        Task<ApiResponse<List<RoleDto>>> GetAllAsync();
        Task<ApiResponse<RoleDto>> GetByNameAsync(string name);
        Task<ApiResponse<List<PolicyDto>>> GetRolePoliciesAsync(string roleName);
        Task<ApiResponse<RolePolicyDto>> AssignPolicyToRoleAsync(string roleName, int policyId);
        Task<ApiResponse<bool>> RemovePolicyFromRoleAsync(string roleName, int policyId);
    }
}
