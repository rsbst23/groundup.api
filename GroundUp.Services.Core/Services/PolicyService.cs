using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;

namespace GroundUp.Services.Core.Services;

public sealed class PolicyService : IPolicyService
{
    private readonly IPolicyRepository _repo;

    public PolicyService(IPolicyRepository repo)
    {
        _repo = repo;
    }

    public Task<ApiResponse<PaginatedData<PolicyDto>>> GetAllAsync(FilterParams filterParams) =>
        _repo.GetAllAsync(filterParams);

    public Task<ApiResponse<PolicyDto>> GetByIdAsync(int id) =>
        _repo.GetByIdAsync(id);

    public Task<ApiResponse<PolicyDto>> GetByNameAsync(string name) =>
        _repo.GetByNameAsync(name);

    public Task<ApiResponse<PolicyDto>> AddAsync(PolicyDto policyDto) =>
        _repo.AddAsync(policyDto);

    public Task<ApiResponse<PolicyDto>> UpdateAsync(int id, PolicyDto policyDto) =>
        _repo.UpdateAsync(id, policyDto);

    public Task<ApiResponse<bool>> DeleteAsync(int id) =>
        _repo.DeleteAsync(id);

    public Task<ApiResponse<List<PermissionDto>>> GetPolicyPermissionsAsync(int policyId) =>
        _repo.GetPolicyPermissionsAsync(policyId);

    public Task<ApiResponse<bool>> AssignPermissionsToPolicyAsync(int policyId, List<int> permissionIds) =>
        _repo.AssignPermissionsToPolicyAsync(policyId, permissionIds);

    public Task<ApiResponse<bool>> RemovePermissionFromPolicyAsync(int policyId, int permissionId) =>
        _repo.RemovePermissionFromPolicyAsync(policyId, permissionId);
}
