using GroundUp.core.dtos;

namespace GroundUp.core.interfaces
{
    public interface IPolicyRepository
    {
        Task<ApiResponse<PaginatedData<PolicyDto>>> GetAllAsync(FilterParams filterParams);
        Task<ApiResponse<PolicyDto>> GetByIdAsync(int id);
        Task<ApiResponse<PolicyDto>> GetByNameAsync(string name);
        Task<ApiResponse<PolicyDto>> AddAsync(PolicyDto policyDto);
        Task<ApiResponse<PolicyDto>> UpdateAsync(int id, PolicyDto policyDto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<List<PermissionDto>>> GetPolicyPermissionsAsync(int policyId);
        Task<ApiResponse<bool>> AssignPermissionsToPolicyAsync(int policyId, List<int> permissionIds);
        Task<ApiResponse<bool>> RemovePermissionFromPolicyAsync(int policyId, int permissionId);
    }
}
