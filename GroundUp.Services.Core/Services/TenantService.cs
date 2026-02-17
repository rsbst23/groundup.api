using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core;
using GroundUp.Core.dtos;
using GroundUp.Core.dtos.tenants;
using GroundUp.Core.interfaces;
using Microsoft.AspNetCore.Http;

namespace GroundUp.Services.Core.Services;

public sealed class TenantService : ITenantService
{
    private const string DefaultRealm = "groundup";

    private readonly ITenantRepository _repo;

    public TenantService(ITenantRepository repo)
    {
        _repo = repo;
    }

    public Task<ApiResponse<TenantDetailDto>> GetByRealmAsync(string realmName) =>
        _repo.GetByRealmAsync(realmName);

    public Task<ApiResponse<PaginatedData<TenantListItemDto>>> GetAllAsync(FilterParams filterParams) =>
        _repo.GetAllAsync(filterParams);

    public Task<ApiResponse<TenantDetailDto>> GetByIdAsync(int id) =>
        _repo.GetByIdAsync(id);

    public Task<ApiResponse<TenantDetailDto>> AddAsync(CreateTenantDto dto) =>
        _repo.AddAsync(dto);

    public Task<ApiResponse<TenantDetailDto>> UpdateAsync(int id, UpdateTenantDto dto) =>
        _repo.UpdateAsync(id, dto);

    public Task<ApiResponse<bool>> DeleteAsync(int id) =>
        _repo.DeleteAsync(id);

    public Task<ApiResponse<byte[]>> ExportAsync(FilterParams filterParams, string format = "csv") =>
        _repo.ExportAsync(filterParams, format);

    public Task<ApiResponse<RealmResolutionResponseDto>> ResolveRealmByUrlAsync(string url) =>
        _repo.ResolveRealmByUrlAsync(url);

    public async Task<OperationResult<(string Realm, bool IsEnterprise)>> ResolveRealmFromDomainAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return OperationResult<(string Realm, bool IsEnterprise)>.Fail(
                "Domain is required.",
                StatusCodes.Status400BadRequest,
                ErrorCodes.ValidationFailed,
                null,
                (DefaultRealm, false));
        }

        // Accept inputs like "https://acme.example.com/path" or "acme.example.com".
        var normalized = domain.Trim();
        if (normalized.Contains("//", StringComparison.Ordinal))
        {
            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            {
                normalized = uri.Host;
            }
        }
        else
        {
            // Still might include a path.
            if (Uri.TryCreate($"https://{normalized}", UriKind.Absolute, out var uri))
            {
                normalized = uri.Host;
            }
        }

        var repoResult = await _repo.ResolveRealmByUrlAsync(normalized);

        if (!repoResult.Success || repoResult.Data is null)
        {
            // Don't block auth routing; fall back to default realm.
            return OperationResult<(string Realm, bool IsEnterprise)>.Ok((DefaultRealm, false));
        }

        var realm = string.IsNullOrWhiteSpace(repoResult.Data.Realm) ? DefaultRealm : repoResult.Data.Realm;
        return OperationResult<(string Realm, bool IsEnterprise)>.Ok((realm, repoResult.Data.IsEnterprise));
    }

    public Task<ApiResponse<TenantDetailDto>> CreateStandardTenantForUserAsync(string realmName, string organizationName) =>
        _repo.CreateStandardTenantForUserAsync(realmName, organizationName);
}
