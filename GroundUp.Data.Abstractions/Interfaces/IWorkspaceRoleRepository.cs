using GroundUp.Core.dtos;

namespace GroundUp.Data.Abstractions.Interfaces;

public interface IWorkspaceRoleRepository
{
    Task<ApiResponse<PaginatedData<RoleDto>>> GetAllAsync(string workspaceId, FilterParams filterParams);
    Task<ApiResponse<RoleDto>> GetByIdAsync(int id);
    Task<ApiResponse<RoleDto>> GetByNameAsync(string name, string workspaceId);
    Task<ApiResponse<RoleDto>> AddAsync(RoleDto roleDto);
    Task<ApiResponse<RoleDto>> UpdateAsync(int id, RoleDto roleDto);
    Task<ApiResponse<bool>> DeleteAsync(int id);
    Task<ApiResponse<List<PolicyDto>>> GetRolePoliciesAsync(string roleName, string workspaceId);
    Task<ApiResponse<RolePolicyDto>> AssignPolicyToRoleAsync(string roleName, string workspaceId, int policyId);
    Task<ApiResponse<bool>> RemovePolicyFromRoleAsync(string roleName, string workspaceId, int policyId);
}
