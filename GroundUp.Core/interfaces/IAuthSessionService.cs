using GroundUp.Core.dtos;
using GroundUp.Core.dtos.auth;
using GroundUp.Core.dtos.tenants;

namespace GroundUp.Core.interfaces;

/// <summary>
/// Service boundary for session/tenant selection actions.
/// Controllers call services only; authorization is enforced here.
/// </summary>
public interface IAuthSessionService
{
    /// <summary>
    /// Implements POST /api/auth/set-tenant.
    /// If <paramref name="request"/> has no tenant selected and the user has multiple tenants,
    /// returns the list requiring selection.
    /// If a tenant is selected (or only one exists), issues an updated auth token.
    /// </summary>
    Task<ApiResponse<SetTenantResponseDto>> SetTenantAsync(
        Guid userId,
        SetTenantRequestDto request,
        IEnumerable<System.Security.Claims.Claim> existingClaims);

    /// <summary>
    /// Sliding-expiration refresh path used by JWT bearer events.
    /// Validates that the user still has access to the tenant, and if so issues a new token.
    /// Returns <c>null</c> if no refresh should occur or validation fails.
    /// </summary>
    Task<string?> TryRefreshAuthTokenAsync(Guid userId, int tenantId, IEnumerable<System.Security.Claims.Claim> existingClaims);
}
