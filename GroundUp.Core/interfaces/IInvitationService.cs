using GroundUp.Core.dtos;
using GroundUp.Core.security;

namespace GroundUp.Core.interfaces;

/// <summary>
/// Service boundary for invitation workflows.
/// Controllers call services only; authorization is enforced here.
/// </summary>
public interface IInvitationService
{
    // Tenant-scoped admin operations
    [RequiresPermission("invitations.view")]
    Task<ApiResponse<PaginatedData<TenantInvitationDto>>> GetAllAsync(FilterParams filterParams);

    [RequiresPermission("invitations.view")]
    Task<ApiResponse<TenantInvitationDto>> GetByIdAsync(int id);

    [RequiresPermission("invitations.create")]
    Task<ApiResponse<TenantInvitationDto>> CreateAsync(CreateTenantInvitationDto dto, Guid createdByUserId);

    [RequiresPermission("invitations.update")]
    Task<ApiResponse<TenantInvitationDto>> UpdateAsync(int id, UpdateTenantInvitationDto dto);

    [RequiresPermission("invitations.delete")]
    Task<ApiResponse<bool>> DeleteAsync(int id);

    [RequiresPermission("invitations.view")]
    Task<ApiResponse<List<TenantInvitationDto>>> GetPendingAsync();

    [RequiresPermission("invitations.update")]
    Task<ApiResponse<bool>> ResendAsync(int id, int expirationDays = 7);

    // Cross-tenant user operations
    Task<ApiResponse<List<TenantInvitationDto>>> GetMyInvitationsAsync(string email);

    Task<ApiResponse<bool>> AcceptInvitationAsync(string invitationToken, Guid userId);

    // Public preview
    Task<ApiResponse<TenantInvitationDto>> GetByTokenAsync(string token);
}
