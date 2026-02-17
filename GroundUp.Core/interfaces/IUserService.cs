using GroundUp.core.dtos;
using GroundUp.core.security;

namespace GroundUp.core.interfaces;

/// <summary>
/// Service boundary for user queries.
/// Controllers call services only; authorization is enforced here.
/// </summary>
public interface IUserService
{
    [RequiresPermission("users.view")]
    Task<ApiResponse<PaginatedData<UserSummaryDto>>> GetAllAsync(FilterParams filterParams);

    [RequiresPermission("users.view")]
    Task<ApiResponse<UserDetailsDto>> GetByIdAsync(string userId);

    // Internal/auth-flow support
    Task<ApiResponse<bool>> EnsureLocalUserExistsAsync(Guid userId, string keycloakUserId, string realm);
}
