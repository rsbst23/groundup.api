using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core.dtos;
using GroundUp.Core.interfaces;

namespace GroundUp.Services.Core.Services;

public sealed class TenantJoinLinkService : ITenantJoinLinkService
{
    private readonly ITenantJoinLinkRepository _repo;

    public TenantJoinLinkService(ITenantJoinLinkRepository repo)
    {
        _repo = repo;
    }

    public Task<ApiResponse<PaginatedData<TenantJoinLinkDto>>> GetAllAsync(FilterParams filterParams, bool includeRevoked = false) =>
        _repo.GetAllAsync(filterParams, includeRevoked);

    public Task<ApiResponse<TenantJoinLinkDto>> GetByIdAsync(int id) =>
        _repo.GetByIdAsync(id);

    public Task<ApiResponse<TenantJoinLinkDto>> CreateAsync(CreateTenantJoinLinkDto dto) =>
        _repo.CreateAsync(dto);

    public Task<ApiResponse<bool>> RevokeAsync(int id) =>
        _repo.RevokeAsync(id);
}
