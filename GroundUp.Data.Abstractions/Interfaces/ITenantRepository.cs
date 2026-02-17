using GroundUp.core.dtos;
using GroundUp.core.dtos.tenants;

namespace GroundUp.Data.Abstractions.Interfaces;

/// <summary>
/// Repository interface for tenant persistence operations.
/// Authorization is enforced at the service boundary.
/// </summary>
public interface ITenantRepository
{
    /// <summary>
    /// Lookup tenant by realm name. Intended for internal/auth flows.
    /// Returns a detail DTO (flat) or NotFound.
    /// </summary>
    Task<ApiResponse<TenantDetailDto>> GetByRealmAsync(string realmName);

    Task<ApiResponse<PaginatedData<TenantListItemDto>>> GetAllAsync(FilterParams filterParams);

    Task<ApiResponse<TenantDetailDto>> GetByIdAsync(int id);

    Task<ApiResponse<TenantDetailDto>> AddAsync(CreateTenantDto dto);

    Task<ApiResponse<TenantDetailDto>> UpdateAsync(int id, UpdateTenantDto dto);

    Task<ApiResponse<bool>> DeleteAsync(int id);

    Task<ApiResponse<byte[]>> ExportAsync(FilterParams filterParams, string format = "csv");

    /// <summary>
    /// Resolves which Keycloak realm to use based on the URL being accessed.
    /// PUBLIC METHOD - No authentication required (called before user is authenticated).
    /// Used by frontend to determine which realm to use for authentication.
    /// </summary>
    Task<ApiResponse<RealmResolutionResponseDto>> ResolveRealmByUrlAsync(string url);

    #region AuthFlow Helpers (Internal)

    /// <summary>
    /// Creates a standard tenant for self-service registration flows.
    /// Intended for internal auth workflows so services don't write to DbContext directly.
    /// </summary>
    Task<ApiResponse<TenantDetailDto>> CreateStandardTenantForUserAsync(string realmName, string organizationName);

    #endregion
}
