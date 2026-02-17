using GroundUp.Core.dtos;
using GroundUp.Core.security;

namespace GroundUp.Core.interfaces;

public interface IPolicyService
{
    [RequiresPermission("policies.view", "SYSTEMADMIN")]
    Task<ApiResponse<PaginatedData<PolicyDto>>> GetAllAsync(FilterParams filterParams);

    [RequiresPermission("policies.view", "SYSTEMADMIN")]
    Task<ApiResponse<PolicyDto>> GetByIdAsync(int id);

    [RequiresPermission("policies.view", "SYSTEMADMIN")]
    Task<ApiResponse<PolicyDto>> GetByNameAsync(string name);

    [RequiresPermission("policies.create", "SYSTEMADMIN")]
    Task<ApiResponse<PolicyDto>> AddAsync(PolicyDto policyDto);

    [RequiresPermission("policies.update", "SYSTEMADMIN")]
    Task<ApiResponse<PolicyDto>> UpdateAsync(int id, PolicyDto policyDto);

    [RequiresPermission("policies.delete", "SYSTEMADMIN")]
    Task<ApiResponse<bool>> DeleteAsync(int id);

    [RequiresPermission("policies.permissions.view", "SYSTEMADMIN")]
    Task<ApiResponse<List<PermissionDto>>> GetPolicyPermissionsAsync(int policyId);

    [RequiresPermission("policies.permissions.assign", "SYSTEMADMIN")]
    Task<ApiResponse<bool>> AssignPermissionsToPolicyAsync(int policyId, List<int> permissionIds);

    [RequiresPermission("policies.permissions.remove", "SYSTEMADMIN")]
    Task<ApiResponse<bool>> RemovePermissionFromPolicyAsync(int policyId, int permissionId);
}
