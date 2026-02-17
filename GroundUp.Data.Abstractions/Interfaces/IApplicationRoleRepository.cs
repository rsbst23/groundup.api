using GroundUp.Core.dtos;

namespace GroundUp.Data.Abstractions.Interfaces;

public interface IApplicationRoleRepository
{
    Task<ApiResponse<PaginatedData<RoleDto>>> GetAllAsync(FilterParams filterParams);
    Task<ApiResponse<RoleDto>> GetByIdAsync(int id);
    Task<ApiResponse<RoleDto>> GetByNameAsync(string name);
    Task<ApiResponse<RoleDto>> AddAsync(RoleDto roleDto);
    Task<ApiResponse<RoleDto>> UpdateAsync(int id, RoleDto roleDto);
    Task<ApiResponse<bool>> DeleteAsync(int id);
    Task<ApiResponse<List<PolicyDto>>> GetRolePoliciesAsync(string roleName);
    Task<ApiResponse<RolePolicyDto>> AssignPolicyToRoleAsync(string roleName, int policyId);
    Task<ApiResponse<bool>> RemovePolicyFromRoleAsync(string roleName, int policyId);
}
