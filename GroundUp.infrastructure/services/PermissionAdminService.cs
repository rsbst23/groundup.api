using GroundUp.core.dtos;
using GroundUp.core.interfaces;

namespace GroundUp.infrastructure.services;

internal sealed class PermissionAdminService : IPermissionAdminService
{
    private readonly IPermissionRepository _repo;

    public PermissionAdminService(IPermissionRepository repo)
    {
        _repo = repo;
    }

    public async Task<OperationResult<PaginatedData<PermissionDto>>> GetAllAsync(FilterParams filterParams)
    {
        var api = await _repo.GetAllAsync(filterParams);
        return ToOperationResult(api);
    }

    public async Task<OperationResult<PermissionDto>> GetByIdAsync(int id)
    {
        var api = await _repo.GetByIdAsync(id);
        return ToOperationResult(api);
    }

    public async Task<OperationResult<PermissionDto>> AddAsync(PermissionDto permissionDto)
    {
        var api = await _repo.AddAsync(permissionDto);
        return ToOperationResult(api);
    }

    public async Task<OperationResult<PermissionDto>> UpdateAsync(int id, PermissionDto permissionDto)
    {
        var api = await _repo.UpdateAsync(id, permissionDto);
        return ToOperationResult(api);
    }

    public async Task<OperationResult<bool>> DeleteAsync(int id)
    {
        var api = await _repo.DeleteAsync(id);
        return ToOperationResult(api);
    }

    private static OperationResult<T> ToOperationResult<T>(ApiResponse<T> api)
        => new()
        {
            Data = api.Data,
            Success = api.Success,
            Message = api.Message,
            Errors = api.Errors,
            StatusCode = api.StatusCode,
            ErrorCode = api.ErrorCode
        };
}
