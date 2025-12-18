using AutoMapper;
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.enums;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.repositories
{
    /// <summary>
    /// Repository for tenant management
    /// Inherits from BaseRepository for common CRUD operations
    /// Overrides methods requiring tenant-specific logic (navigation properties, realm management)
    /// </summary>
    public class TenantRepository : BaseRepository<Tenant, TenantDto>, ITenantRepository
    {
        private readonly IIdentityProviderAdminService _identityProviderAdminService;

        public TenantRepository(
            ApplicationDbContext context,
            IMapper mapper,
            ILoggingService logger,
            IIdentityProviderAdminService identityProviderAdminService)
            : base(context, mapper, logger)
        {
            _identityProviderAdminService = identityProviderAdminService;
        }

        /// <summary>
        /// Override GetAllAsync to include ParentTenant navigation property
        /// </summary>
        public override async Task<ApiResponse<PaginatedData<TenantDto>>> GetAllAsync(FilterParams filterParams)
        {
            try
            {
                var query = _dbSet
                    .Include(t => t.ParentTenant)
                    .AsQueryable();

                // Apply sorting
                query = GroundUp.infrastructure.utilities.ExpressionHelper.ApplySorting(query, filterParams.SortBy);

                var totalRecords = await query.CountAsync();
                var pagedItems = await query
                    .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
                    .Take(filterParams.PageSize)
                    .ToListAsync();

                var mappedItems = pagedItems.Select(t => new TenantDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    ParentTenantId = t.ParentTenantId,
                    CreatedAt = t.CreatedAt,
                    IsActive = t.IsActive,
                    ParentTenantName = t.ParentTenant?.Name,
                    TenantType = t.TenantType,
                    CustomDomain = t.CustomDomain,
                    RealmName = t.RealmName
                }).ToList();

                var paginatedData = new PaginatedData<TenantDto>(
                    mappedItems,
                    filterParams.PageNumber,
                    filterParams.PageSize,
                    totalRecords
                );

                return new ApiResponse<PaginatedData<TenantDto>>(paginatedData);
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedData<TenantDto>>(
                    default!,
                    false,
                    "An error occurred while retrieving tenants.",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        /// <summary>
        /// Override GetByIdAsync to include ParentTenant navigation property
        /// </summary>
        public override async Task<ApiResponse<TenantDto>> GetByIdAsync(int id)
        {
            try
            {
                var tenant = await _dbSet
                    .Include(t => t.ParentTenant)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (tenant == null)
                {
                    return new ApiResponse<TenantDto>(
                        default!,
                        false,
                        "Tenant not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                var dto = new TenantDto
                {
                    Id = tenant.Id,
                    Name = tenant.Name,
                    Description = tenant.Description,
                    ParentTenantId = tenant.ParentTenantId,
                    CreatedAt = tenant.CreatedAt,
                    IsActive = tenant.IsActive,
                    ParentTenantName = tenant.ParentTenant?.Name,
                    TenantType = tenant.TenantType,
                    CustomDomain = tenant.CustomDomain,
                    RealmName = tenant.RealmName
                };

                return new ApiResponse<TenantDto>(dto);
            }
            catch (Exception ex)
            {
                return new ApiResponse<TenantDto>(
                    default!,
                    false,
                    "An error occurred while retrieving the tenant.",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        /// <summary>
        /// Override AddAsync to handle CreateTenantDto and implement realm creation logic
        /// ITenantRepository expects CreateTenantDto, not TenantDto
        /// </summary>
        public async Task<ApiResponse<TenantDto>> AddAsync(CreateTenantDto dto)
        {
            try
            {
                // Validate parent tenant exists if specified
                if (dto.ParentTenantId.HasValue)
                {
                    var parentExists = await _dbSet.AnyAsync(t => t.Id == dto.ParentTenantId.Value);
                    if (!parentExists)
                    {
                        return new ApiResponse<TenantDto>(
                            default!,
                            false,
                            "Parent tenant not found",
                            null,
                            StatusCodes.Status400BadRequest,
                            ErrorCodes.ValidationFailed
                        );
                    }
                }

                // Validate CustomDomain for enterprise tenants
                if (dto.TenantType == TenantType.Enterprise && string.IsNullOrWhiteSpace(dto.CustomDomain))
                {
                    return new ApiResponse<TenantDto>(
                        default!,
                        false,
                        "CustomDomain is required for enterprise tenants",
                        new List<string> { "CustomDomain cannot be empty for enterprise tenants" },
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed
                    );
                }

                // For enterprise tenants, create Keycloak realm first (before database)
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
                        return new ApiResponse<TenantDto>(
                            default!,
                            false,
                            "Failed to create Keycloak realm for enterprise tenant",
                            new List<string> { "Realm creation failed" },
                            StatusCodes.Status500InternalServerError,
                            "REALM_CREATION_FAILED"
                        );
                    }
                }

                // Create tenant entity
                var tenant = new Tenant
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    ParentTenantId = dto.ParentTenantId,
                    IsActive = dto.IsActive,
                    TenantType = dto.TenantType,
                    CustomDomain = dto.CustomDomain,
                    RealmName = dto.CustomDomain ?? "groundup",
                    CreatedAt = DateTime.UtcNow
                };

                _dbSet.Add(tenant);
                await _context.SaveChangesAsync();

                // Reload with parent tenant for response
                var created = await _dbSet
                    .Include(t => t.ParentTenant)
                    .FirstAsync(t => t.Id == tenant.Id);

                var resultDto = new TenantDto
                {
                    Id = created.Id,
                    Name = created.Name,
                    Description = created.Description,
                    ParentTenantId = created.ParentTenantId,
                    CreatedAt = created.CreatedAt,
                    IsActive = created.IsActive,
                    ParentTenantName = created.ParentTenant?.Name,
                    TenantType = created.TenantType,
                    CustomDomain = created.CustomDomain,
                    RealmName = created.RealmName
                };

                return new ApiResponse<TenantDto>(
                    resultDto,
                    true,
                    "Tenant created successfully",
                    null,
                    StatusCodes.Status201Created
                );
            }
            catch (DbUpdateException ex)
            {
                // Rollback: Delete Keycloak realm if it was created
                if (dto.TenantType == TenantType.Enterprise && !string.IsNullOrWhiteSpace(dto.Name))
                {
                    await DeleteKeycloakRealmAsync(dto.Name.ToLowerInvariant());
                }

                return new ApiResponse<TenantDto>(
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
                // Rollback: Delete Keycloak realm if it was created
                if (dto.TenantType == TenantType.Enterprise && !string.IsNullOrWhiteSpace(dto.Name))
                {
                    await DeleteKeycloakRealmAsync(dto.Name.ToLowerInvariant());
                }

                return new ApiResponse<TenantDto>(
                    default!,
                    false,
                    "An error occurred while creating the tenant.",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        /// <summary>
        /// Override UpdateAsync to handle UpdateTenantDto and reload with navigation properties
        /// ITenantRepository expects UpdateTenantDto, not TenantDto
        /// </summary>
        public async Task<ApiResponse<TenantDto>> UpdateAsync(int id, UpdateTenantDto dto)
        {
            try
            {
                var tenant = await _dbSet.FindAsync(id);
                if (tenant == null)
                {
                    return new ApiResponse<TenantDto>(
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
                // Note: TenantType cannot be changed after creation

                await _context.SaveChangesAsync();

                // Reload with parent tenant for response
                var updated = await _dbSet
                    .Include(t => t.ParentTenant)
                    .FirstAsync(t => t.Id == id);

                var resultDto = new TenantDto
                {
                    Id = updated.Id,
                    Name = updated.Name,
                    Description = updated.Description,
                    ParentTenantId = updated.ParentTenantId,
                    CreatedAt = updated.CreatedAt,
                    IsActive = updated.IsActive,
                    ParentTenantName = updated.ParentTenant?.Name,
                    TenantType = updated.TenantType,
                    CustomDomain = updated.CustomDomain,
                    RealmName = updated.RealmName
                };

                return new ApiResponse<TenantDto>(
                    resultDto,
                    true,
                    "Tenant updated successfully",
                    null,
                    StatusCodes.Status200OK
                );
            }
            catch (DbUpdateException ex)
            {
                return new ApiResponse<TenantDto>(
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
                return new ApiResponse<TenantDto>(
                    default!,
                    false,
                    "An error occurred while updating the tenant.",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        /// <summary>
        /// Override DeleteAsync to add custom validation and realm deletion
        /// </summary>
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

                // Check if tenant has child tenants
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

                // Check if tenant has users
                var hasUsers = await _context.UserTenants.AnyAsync(ut => ut.TenantId == id);
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

                // If enterprise tenant, delete Keycloak realm first
                if (tenant.TenantType == TenantType.Enterprise)
                {
                    var realmDeleted = await DeleteKeycloakRealmAsync(tenant.RealmName ?? tenant.Name.ToLowerInvariant());
                    
                    if (!realmDeleted)
                    {
                        // Continue anyway - realm might not exist
                    }
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

        /// <summary>
        /// Override ExportAsync to include ParentTenant navigation property
        /// </summary>
        public override async Task<ApiResponse<byte[]>> ExportAsync(FilterParams filterParams, string format = "csv")
        {
            try
            {
                var query = _dbSet
                    .Include(t => t.ParentTenant)
                    .AsQueryable();

                query = GroundUp.infrastructure.utilities.ExpressionHelper.ApplySorting(query, filterParams.SortBy);

                var items = await query.ToListAsync();

                var mappedItems = items.Select(t => new TenantDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    ParentTenantId = t.ParentTenantId,
                    CreatedAt = t.CreatedAt,
                    IsActive = t.IsActive,
                    ParentTenantName = t.ParentTenant?.Name,
                    TenantType = t.TenantType,
                    CustomDomain = t.CustomDomain,
                    RealmName = t.RealmName
                }).ToList();

                // Use base class CSV/JSON generation
                byte[] fileContent;
                if (format.ToLower() == "json")
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(mappedItems, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    fileContent = System.Text.Encoding.UTF8.GetBytes(json);
                }
                else
                {
                    fileContent = GenerateCsvFile(mappedItems);
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
                    new byte[0],
                    false,
                    "An error occurred while exporting data.",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        /// <summary>
        /// Resolves which Keycloak realm to use based on the URL being accessed
        /// PUBLIC METHOD - No authentication required (called before user is authenticated)
        /// </summary>
        public async Task<ApiResponse<RealmResolutionResponseDto>> ResolveRealmByUrlAsync(string url)
        {
            try
            {
                // Normalize URL (remove protocol, trailing slash, etc.)
                var normalizedUrl = NormalizeUrl(url);

                // Look up tenant by CustomDomain
                var tenant = await _dbSet
                    .Where(t => t.IsActive && t.CustomDomain == normalizedUrl)
                    .FirstOrDefaultAsync();

                if (tenant == null)
                {
                    // No specific tenant found - return default realm
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

                // Tenant found - return its realm
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
                // On error, return default realm to avoid blocking authentication
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

        /// <summary>
        /// Normalizes URL for consistent comparison
        /// </summary>
        private string NormalizeUrl(string url)
        {
            return url.ToLowerInvariant()
                .Replace("https://", "")
                .Replace("http://", "")
                .TrimEnd('/');
        }

        #region Private Helper Methods

        /// <summary>
        /// Custom CSV generation to maintain specific column order
        /// </summary>
        private byte[] GenerateCsvFile(List<TenantDto> items)
        {
            using var memoryStream = new MemoryStream();
            using var streamWriter = new StreamWriter(memoryStream, System.Text.Encoding.UTF8);

            if (items.Count > 0)
            {
                // Write headers
                streamWriter.WriteLine("\"Id\",\"Name\",\"Description\",\"ParentTenantId\",\"ParentTenantName\",\"TenantType\",\"CustomDomain\",\"IsActive\",\"CreatedAt\"");

                // Write data
                foreach (var item in items)
                {
                    streamWriter.WriteLine($"\"{item.Id}\",\"{item.Name}\",\"{item.Description ?? ""}\",\"{item.ParentTenantId?.ToString() ?? ""}\",\"{item.ParentTenantName ?? ""}\",\"{item.TenantType}\",\"{item.CustomDomain ?? ""}\",\"{item.IsActive}\",\"{item.CreatedAt:yyyy-MM-dd HH:mm:ss}\"");
                }
            }

            streamWriter.Flush();
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Helper method to create Keycloak realm
        /// </summary>
        private async Task<bool> CreateKeycloakRealmAsync(CreateRealmDto dto)
        {
            var result = await _identityProviderAdminService.CreateRealmAsync(dto);
            return result.Success;
        }

        /// <summary>
        /// Helper method to delete Keycloak realm (rollback)
        /// </summary>
        private async Task<bool> DeleteKeycloakRealmAsync(string realmName)
        {
            return await _identityProviderAdminService.DeleteRealmAsync(realmName);
        }

        #endregion
    }
}
