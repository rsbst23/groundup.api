using AutoMapper;
using AutoMapper.QueryableExtensions;
using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.dtos.tenants;
using GroundUp.core.entities;
using GroundUp.core.enums;
using GroundUp.core.interfaces;
using GroundUp.Data.Core.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.Data.Core.Repositories;

/// <summary>
/// Repository for tenant management
/// Inherits from BaseRepository for common CRUD operations
/// Overrides methods requiring tenant-specific logic (navigation properties, realm management)
/// </summary>
public class TenantRepository : BaseTenantRepository<Tenant, TenantDetailDto>, ITenantRepository
{
    private readonly IIdentityProviderAdminService _identityProviderAdminService;

    public TenantRepository(
        ApplicationDbContext context,
        IMapper mapper,
        ILoggingService logger,
        ITenantContext tenantContext,
        IIdentityProviderAdminService identityProviderAdminService)
        : base(context, mapper, logger, tenantContext)
    {
        _identityProviderAdminService = identityProviderAdminService;
    }

    private static IQueryable<Tenant> WithParentAndSsoRole(IQueryable<Tenant> query)
        => query
            .Include(t => t.ParentTenant)
            .Include(t => t.SsoAutoJoinRole);

    public async Task<ApiResponse<PaginatedData<TenantListItemDto>>> GetAllAsync(FilterParams filterParams)
    {
        try
        {
            var query = WithParentAndSsoRole(_dbSet.AsQueryable());

            // Use base pipeline for filtering + sorting.
            query = ApplyFilterParams(query, filterParams);

            var totalRecords = await query.CountAsync();

            var items = await ApplyPaging(query, filterParams)
                .ProjectTo<TenantListItemDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            var paginatedData = new PaginatedData<TenantListItemDto>(items, filterParams.PageNumber, filterParams.PageSize, totalRecords);
            return new ApiResponse<PaginatedData<TenantListItemDto>>(paginatedData);
        }
        catch (Exception ex)
        {
            return new ApiResponse<PaginatedData<TenantListItemDto>>(
                default!,
                false,
                "An error occurred while retrieving tenants.",
                new List<string> { ex.Message },
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError
            );
        }
    }

    public async Task<ApiResponse<TenantDetailDto>> GetByIdAsync(int id)
    {
        try
        {
            var dto = await WithParentAndSsoRole(_dbSet.AsQueryable())
                .Where(t => t.Id == id)
                .ProjectTo<TenantDetailDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            if (dto == null)
            {
                return new ApiResponse<TenantDetailDto>(
                    default!,
                    false,
                    "Tenant not found",
                    null,
                    StatusCodes.Status404NotFound,
                    ErrorCodes.NotFound
                );
            }

            return new ApiResponse<TenantDetailDto>(dto);
        }
        catch (Exception ex)
        {
            return new ApiResponse<TenantDetailDto>(
                default!,
                false,
                "An error occurred while retrieving the tenant.",
                new List<string> { ex.Message },
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError
            );
        }
    }

    public async Task<ApiResponse<TenantDetailDto>> AddAsync(CreateTenantDto dto)
    {
        try
        {
            if (dto.ParentTenantId.HasValue)
            {
                var parentExists = await _dbSet.AnyAsync(t => t.Id == dto.ParentTenantId.Value);
                if (!parentExists)
                {
                    return new ApiResponse<TenantDetailDto>(
                        default!,
                        false,
                        "Parent tenant not found",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed
                    );
                }
            }

            if (dto.TenantType == TenantType.Enterprise && string.IsNullOrWhiteSpace(dto.CustomDomain))
            {
                return new ApiResponse<TenantDetailDto>(
                    default!,
                    false,
                    "CustomDomain is required for enterprise tenants",
                    new List<string> { "CustomDomain cannot be empty for enterprise tenants" },
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                );
            }

            if (dto.TenantType == TenantType.Enterprise)
            {
                var realmDto = new CreateRealmDto
                {
                    Realm = dto.Name.ToLowerInvariant(),
                    DisplayName = dto.Description ?? dto.Name,
                    Enabled = dto.IsActive
                };

                var realmCreated = await CreateKeycloakRealmAsync(realmDto);

                if (!realmCreated)
                {
                    return new ApiResponse<TenantDetailDto>(
                        default!,
                        false,
                        "Failed to create Keycloak realm for enterprise tenant",
                        new List<string> { "Realm creation failed" },
                        StatusCodes.Status500InternalServerError,
                        "REALM_CREATION_FAILED"
                    );
                }
            }

            var tenant = new Tenant
            {
                Name = dto.Name,
                Description = dto.Description,
                ParentTenantId = dto.ParentTenantId,
                IsActive = dto.IsActive,
                TenantType = dto.TenantType,
                CustomDomain = dto.CustomDomain,
                RealmName = dto.CustomDomain ?? "groundup",
                SsoAutoJoinDomains = dto.SsoAutoJoinDomains,
                SsoAutoJoinRoleId = dto.SsoAutoJoinRoleId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Set<Tenant>().Add(tenant);
            await _context.SaveChangesAsync();

            var createdDto = await WithParentAndSsoRole(_dbSet.AsQueryable())
                .Where(t => t.Id == tenant.Id)
                .ProjectTo<TenantDetailDto>(_mapper.ConfigurationProvider)
                .FirstAsync();

            return new ApiResponse<TenantDetailDto>(
                createdDto,
                true,
                "Tenant created successfully",
                null,
                StatusCodes.Status201Created
            );
        }
        catch (DbUpdateException ex)
        {
            if (dto.TenantType == TenantType.Enterprise && !string.IsNullOrWhiteSpace(dto.Name))
            {
                await DeleteKeycloakRealmAsync(dto.Name.ToLowerInvariant());
            }

            return new ApiResponse<TenantDetailDto>(
                default!,
                false,
                "A tenant with this name may already exist.",
                new List<string> { ex.Message },
                StatusCodes.Status400BadRequest,
                ErrorCodes.DuplicateEntry
            );
        }
        catch (Exception ex)
        {
            if (dto.TenantType == TenantType.Enterprise && !string.IsNullOrWhiteSpace(dto.Name))
            {
                await DeleteKeycloakRealmAsync(dto.Name.ToLowerInvariant());
            }

            return new ApiResponse<TenantDetailDto>(
                default!,
                false,
                "An error occurred while creating the tenant.",
                new List<string> { ex.Message },
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError
            );
        }
    }

    public async Task<ApiResponse<TenantDetailDto>> UpdateAsync(int id, UpdateTenantDto dto)
    {
        try
        {
            var tenant = await _dbSet.FindAsync(id);
            if (tenant == null)
            {
                return new ApiResponse<TenantDetailDto>(
                    default!,
                    false,
                    "Tenant not found",
                    null,
                    StatusCodes.Status404NotFound,
                    ErrorCodes.NotFound
                );
            }

            tenant.Name = dto.Name;
            tenant.Description = dto.Description;
            tenant.IsActive = dto.IsActive;
            tenant.CustomDomain = dto.CustomDomain;
            tenant.SsoAutoJoinDomains = dto.SsoAutoJoinDomains;
            tenant.SsoAutoJoinRoleId = dto.SsoAutoJoinRoleId;

            await _context.SaveChangesAsync();

            var updatedDto = await WithParentAndSsoRole(_dbSet.AsQueryable())
                .Where(t => t.Id == id)
                .ProjectTo<TenantDetailDto>(_mapper.ConfigurationProvider)
                .FirstAsync();

            return new ApiResponse<TenantDetailDto>(
                updatedDto,
                true,
                "Tenant updated successfully",
                null,
                StatusCodes.Status200OK
            );
        }
        catch (DbUpdateException ex)
        {
            return new ApiResponse<TenantDetailDto>(
                default!,
                false,
                "A tenant with this name may already exist.",
                new List<string> { ex.Message },
                StatusCodes.Status400BadRequest,
                ErrorCodes.DuplicateEntry
            );
        }
        catch (Exception ex)
        {
            return new ApiResponse<TenantDetailDto>(
                default!,
                false,
                "An error occurred while updating the tenant.",
                new List<string> { ex.Message },
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError
            );
        }
    }

    public override async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        try
        {
            var tenant = await _dbSet.FindAsync(id);
            if (tenant == null)
            {
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Tenant not found",
                    null,
                    StatusCodes.Status404NotFound,
                    ErrorCodes.NotFound
                );
            }

            var hasChildren = await _dbSet.AnyAsync(t => t.ParentTenantId == id);
            if (hasChildren)
            {
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Cannot delete tenant with child tenants",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                );
            }

            var hasUsers = await _context.Set<UserTenant>().AnyAsync(ut => ut.TenantId == id);
            if (hasUsers)
            {
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Cannot delete tenant with assigned users",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                );
            }

            if (tenant.TenantType == TenantType.Enterprise)
            {
                await DeleteKeycloakRealmAsync(tenant.RealmName ?? tenant.Name.ToLowerInvariant());
            }

            _dbSet.Remove(tenant);
            await _context.SaveChangesAsync();

            return new ApiResponse<bool>(
                true,
                true,
                "Tenant deleted successfully",
                null,
                StatusCodes.Status200OK
            );
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>(
                false,
                false,
                "An error occurred while deleting the tenant.",
                new List<string> { ex.Message },
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError
            );
        }
    }

    public override async Task<ApiResponse<byte[]>> ExportAsync(FilterParams filterParams, string format = "csv")
    {
        try
        {
            var query = WithParentAndSsoRole(_dbSet.AsQueryable());
            query = GroundUp.Data.Core.Utilities.ExpressionHelper.ApplySorting(query, filterParams.SortBy);
            var items = await query
                .ProjectTo<TenantDetailDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            byte[] fileContent;
            if (format.ToLower() == "json")
            {
                var json = System.Text.Json.JsonSerializer.Serialize(items, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                fileContent = System.Text.Encoding.UTF8.GetBytes(json);
            }
            else
            {
                fileContent = GenerateCsvFile(items);
            }

            return new ApiResponse<byte[]>(
                fileContent,
                true,
                $"Exported {items.Count} tenants successfully."
            );
        }
        catch (Exception ex)
        {
            return new ApiResponse<byte[]>(
                Array.Empty<byte>(),
                false,
                "An error occurred while exporting data.",
                new List<string> { ex.Message },
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError
            );
        }
    }

    public async Task<ApiResponse<RealmResolutionResponseDto>> ResolveRealmByUrlAsync(string url)
    {
        try
        {
            var normalizedUrl = NormalizeUrl(url);

            var tenant = await _dbSet
                .Where(t => t.IsActive && t.CustomDomain == normalizedUrl)
                .FirstOrDefaultAsync();

            if (tenant == null)
            {
                return new ApiResponse<RealmResolutionResponseDto>(
                    new RealmResolutionResponseDto
                    {
                        Realm = "groundup",
                        TenantId = null,
                        TenantName = null,
                        IsEnterprise = false
                    },
                    true,
                    "Using default realm"
                );
            }

            var response = new RealmResolutionResponseDto
            {
                Realm = tenant.RealmName,
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                IsEnterprise = tenant.TenantType == TenantType.Enterprise
            };

            return new ApiResponse<RealmResolutionResponseDto>(response, true, "Realm resolved successfully");
        }
        catch (Exception ex)
        {
            return new ApiResponse<RealmResolutionResponseDto>(
                new RealmResolutionResponseDto
                {
                    Realm = "groundup",
                    TenantId = null,
                    TenantName = null,
                    IsEnterprise = false
                },
                false,
                "Error resolving realm, using default",
                new List<string> { ex.Message },
                StatusCodes.Status200OK
            );
        }
    }

    private static string NormalizeUrl(string url)
    {
        return url.ToLowerInvariant()
            .Replace("https://", "")
            .Replace("http://", "")
            .TrimEnd('/');
    }

    private byte[] GenerateCsvFile(List<TenantDetailDto> items)
    {
        using var memoryStream = new MemoryStream();
        using var streamWriter = new StreamWriter(memoryStream, System.Text.Encoding.UTF8);

        if (items.Count > 0)
        {
            streamWriter.WriteLine("\"Id\",\"Name\",\"Description\",\"ParentTenantId\",\"ParentTenantName\",\"TenantType\",\"CustomDomain\",\"IsActive\",\"CreatedAt\"");

            foreach (var item in items)
            {
                streamWriter.WriteLine($"\"{item.Id}\",\"{item.Name}\",\"{item.Description ?? ""}\",\"{item.ParentTenantId?.ToString() ?? ""}\",\"{item.ParentTenantName ?? ""}\",\"{item.TenantType}\",\"{item.CustomDomain ?? ""}\",\"{item.IsActive}\",\"{item.CreatedAt:yyyy-MM-dd HH:mm:ss}\"");
            }
        }

        streamWriter.Flush();
        return memoryStream.ToArray();
    }

    private async Task<bool> CreateKeycloakRealmAsync(CreateRealmDto dto)
    {
        var result = await _identityProviderAdminService.CreateRealmAsync(dto);
        return result.Success;
    }

    private async Task<bool> DeleteKeycloakRealmAsync(string realmName)
    {
        return await _identityProviderAdminService.DeleteRealmAsync(realmName);
    }

    public async Task<ApiResponse<TenantDetailDto>> GetByRealmAsync(string realmName)
    {
        try
        {
            var dto = await WithParentAndSsoRole(_dbSet.AsQueryable())
                .Where(t => t.RealmName == realmName)
                .ProjectTo<TenantDetailDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            if (dto == null)
            {
                return new ApiResponse<TenantDetailDto>(
                    default!,
                    false,
                    "Tenant not found",
                    null,
                    StatusCodes.Status404NotFound,
                    ErrorCodes.NotFound);
            }

            return new ApiResponse<TenantDetailDto>(dto);
        }
        catch (Exception ex)
        {
            return new ApiResponse<TenantDetailDto>(
                default!,
                false,
                "An error occurred while retrieving the tenant.",
                new List<string> { ex.Message },
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError);
        }
    }

    public async Task<ApiResponse<TenantDetailDto>> CreateStandardTenantForUserAsync(string realmName, string organizationName)
    {
        try
        {
            var tenant = new Tenant
            {
                Name = organizationName,
                Description = "Created via self-service registration",
                IsActive = true,
                TenantType = TenantType.Standard,
                RealmName = realmName,
                CreatedAt = DateTime.UtcNow
            };

            _context.Set<Tenant>().Add(tenant);
            await _context.SaveChangesAsync();

            var dto = _mapper.Map<TenantDetailDto>(tenant);
            return new ApiResponse<TenantDetailDto>(dto, true, "Organization created successfully", null, StatusCodes.Status201Created);
        }
        catch (Exception ex)
        {
            return new ApiResponse<TenantDetailDto>(
                default!,
                false,
                "An unexpected error occurred while creating organization",
                new List<string> { ex.Message },
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError);
        }
    }

    public async Task<OperationResult<(string Realm, bool IsEnterprise)>> ResolveRealmFromDomainAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return OperationResult<(string Realm, bool IsEnterprise)>.Ok(("groundup", false));
        }

        var normalized = NormalizeUrl(domain);

        var tenant = await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.CustomDomain == normalized && t.IsActive);

        if (tenant == null)
        {
            return OperationResult<(string Realm, bool IsEnterprise)>.Fail(
                $"No active tenant found for domain: {normalized}",
                StatusCodes.Status404NotFound,
                ErrorCodes.NotFound);
        }

        return OperationResult<(string Realm, bool IsEnterprise)>.Ok((tenant.RealmName, tenant.TenantType == TenantType.Enterprise));
    }
}
