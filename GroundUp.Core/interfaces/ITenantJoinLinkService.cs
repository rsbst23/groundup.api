using GroundUp.Core.dtos;
using GroundUp.Core.security;

namespace GroundUp.Core.interfaces;

/// <summary>
/// Service boundary for tenant join-link CRUD operations.
/// Controllers must call services only. Authorization is enforced here.
/// </summary>
public interface ITenantJoinLinkService
{
    [RequiresPermission("joinlinks.view")]
    Task<ApiResponse<PaginatedData<TenantJoinLinkDto>>> GetAllAsync(FilterParams filterParams, bool includeRevoked = false);

    [RequiresPermission("joinlinks.view")]
    Task<ApiResponse<TenantJoinLinkDto>> GetByIdAsync(int id);

    [RequiresPermission("joinlinks.create")]
    Task<ApiResponse<TenantJoinLinkDto>> CreateAsync(CreateTenantJoinLinkDto dto);

    [RequiresPermission("joinlinks.revoke")]
    Task<ApiResponse<bool>> RevokeAsync(int id);
}
