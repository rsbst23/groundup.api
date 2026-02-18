using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core.dtos;
using GroundUp.Core.interfaces;

namespace GroundUp.Services.Core.Services;

public sealed class RolePolicyService : IRolePolicyService
{
    private readonly IRolePolicyRepository _repo;

    public RolePolicyService(IRolePolicyRepository repo)
    {
        _repo = repo;
    }

    public Task<ApiResponse<PaginatedData<RolePolicyDto>>> GetAllAsync(FilterParams filterParams) =>
        _repo.GetAllAsync(filterParams);

    public Task<ApiResponse<RolePolicyDto>> GetByIdAsync(int id) =>
        _repo.GetByIdAsync(id);

    public Task<ApiResponse<RolePolicyDto>> AddAsync(RolePolicyDto rolePolicyDto) =>
        _repo.AddAsync(rolePolicyDto);

    public Task<ApiResponse<RolePolicyDto>> UpdateAsync(int id, RolePolicyDto rolePolicyDto) =>
        _repo.UpdateAsync(id, rolePolicyDto);

    public Task<ApiResponse<bool>> DeleteAsync(int id) =>
        _repo.DeleteAsync(id);
}
