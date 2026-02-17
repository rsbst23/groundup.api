using GroundUp.core.dtos;
using GroundUp.core.dtos.tenants;
using GroundUp.core.security;

namespace GroundUp.core.interfaces;

public interface ITenantService
{
    /// <summary>
    /// Lookup tenant by realm name. Intended for internal/auth flows.
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
    /// PUBLIC: Resolves which Keycloak realm to use based on the URL being accessed.
    /// </summary>
    Task<ApiResponse<RealmResolutionResponseDto>> ResolveRealmByUrlAsync(string url);

    /// <summary>
    /// Intended for auth URL generation (domain-based login) and similar routing.
    /// </summary>
    Task<OperationResult<(string Realm, bool IsEnterprise)>> ResolveRealmFromDomainAsync(string domain);

    /// <summary>
    /// Creates a standard tenant for self-service registration flows.
    /// Intended for internal auth workflows.
    /// </summary>
    Task<ApiResponse<TenantDetailDto>> CreateStandardTenantForUserAsync(string realmName, string organizationName);
}
