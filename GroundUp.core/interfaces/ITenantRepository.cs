using GroundUp.core.dtos;
using GroundUp.core.dtos.tenants;
using GroundUp.core.security;

namespace GroundUp.core.interfaces
{
    /// <summary>
    /// Repository interface for tenant management
    /// Requires SYSTEMADMIN role for all operations
    /// </summary>
    public interface ITenantRepository
    {
        /// <summary>
        /// Lookup tenant by realm name. Intended for internal/auth flows.
        /// Returns a detail DTO (flat) or NotFound.
        /// </summary>
        Task<ApiResponse<TenantDetailDto>> GetByRealmAsync(string realmName);

        [RequiresPermission("tenants.view", "SYSTEMADMIN")]
        Task<ApiResponse<PaginatedData<TenantListItemDto>>> GetAllAsync(FilterParams filterParams);

        [RequiresPermission("tenants.view", "SYSTEMADMIN")]
        Task<ApiResponse<TenantDetailDto>> GetByIdAsync(int id);

        [RequiresPermission("tenants.create", "SYSTEMADMIN")]
        Task<ApiResponse<TenantDetailDto>> AddAsync(CreateTenantDto dto);

        [RequiresPermission("tenants.update", "SYSTEMADMIN")]
        Task<ApiResponse<TenantDetailDto>> UpdateAsync(int id, UpdateTenantDto dto);

        [RequiresPermission("tenants.delete", "SYSTEMADMIN")]
        Task<ApiResponse<bool>> DeleteAsync(int id);

        [RequiresPermission("tenants.export", "SYSTEMADMIN")]
        Task<ApiResponse<byte[]>> ExportAsync(FilterParams filterParams, string format = "csv");

        /// <summary>
        /// Resolves which Keycloak realm to use based on the URL being accessed
        /// PUBLIC METHOD - No authentication required (called before user is authenticated)
        /// Used by frontend to determine which realm to use for authentication
        /// </summary>
        /// <param name="url">The URL being accessed (e.g., 'acme.myapp.com', 'app.myapp.com')</param>
        /// <returns>Realm information including realm name, tenant details, and enterprise status</returns>
        Task<ApiResponse<RealmResolutionResponseDto>> ResolveRealmByUrlAsync(string url);

        #region AuthFlow Helpers (Internal)

        /// <summary>
        /// Creates a standard tenant for self-service registration flows.
        /// Intended for internal auth workflows so services don't write to DbContext directly.
        /// </summary>
        Task<ApiResponse<TenantDetailDto>> CreateStandardTenantForUserAsync(string realmName, string organizationName);

        #endregion
    }
}
