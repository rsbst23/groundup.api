using AutoMapper;
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.Repositories.Core.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.repositories
{
    public class TenantJoinLinkRepository : BaseTenantRepository<TenantJoinLink, TenantJoinLinkDto>, ITenantJoinLinkRepository
    {
        public TenantJoinLinkRepository(
            ApplicationDbContext context,
            IMapper mapper,
            ILoggingService logger,
            ITenantContext tenantContext)
            : base(context, mapper, logger, tenantContext) { }

        public async Task<ApiResponse<PaginatedData<TenantJoinLinkDto>>> GetAllAsync(FilterParams filterParams, bool includeRevoked = false)
        {
            try
            {
                var tenantId = _tenantContext.TenantId;
                
                var query = _dbSet
                    .Where(j => j.TenantId == tenantId);
                
                if (!includeRevoked)
                {
                    query = query.Where(j => !j.IsRevoked);
                }
                
                query = query.OrderByDescending(j => j.CreatedAt);
                
                var totalRecords = await query.CountAsync();
                var items = await query
                    .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
                    .Take(filterParams.PageSize)
                    .ToListAsync();
                
                var dtos = _mapper.Map<List<TenantJoinLinkDto>>(items);
                var paginatedData = new PaginatedData<TenantJoinLinkDto>(dtos, filterParams.PageNumber, filterParams.PageSize, totalRecords);
                
                return new ApiResponse<PaginatedData<TenantJoinLinkDto>>(paginatedData);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting join links: {ex.Message}", ex);
                return new ApiResponse<PaginatedData<TenantJoinLinkDto>>(
                    default!,
                    false,
                    "Failed to retrieve join links",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public override async Task<ApiResponse<TenantJoinLinkDto>> GetByIdAsync(int id)
        {
            try
            {
                var tenantId = _tenantContext.TenantId;
                var joinLink = await _dbSet.FirstOrDefaultAsync(j => j.Id == id && j.TenantId == tenantId);
                
                if (joinLink == null)
                {
                    return new ApiResponse<TenantJoinLinkDto>(
                        default!,
                        false,
                        "Join link not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }
                
                var dto = _mapper.Map<TenantJoinLinkDto>(joinLink);
                return new ApiResponse<TenantJoinLinkDto>(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting join link: {ex.Message}", ex);
                return new ApiResponse<TenantJoinLinkDto>(
                    default!,
                    false,
                    "Failed to retrieve join link",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<TenantJoinLinkDto>> CreateAsync(CreateTenantJoinLinkDto dto)
        {
            try
            {
                var tenantId = _tenantContext.TenantId;

                var joinLink = new TenantJoinLink
                {
                    TenantId = tenantId,
                    JoinToken = Guid.NewGuid().ToString("N"), // 32-char hex string
                    ExpiresAt = DateTime.UtcNow.AddDays(dto.ExpirationDays),
                    DefaultRoleId = dto.DefaultRoleId,
                    IsRevoked = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Set<TenantJoinLink>().Add(joinLink);
                await _context.SaveChangesAsync();

                var result = _mapper.Map<TenantJoinLinkDto>(joinLink);
                _logger.LogInformation($"Created join link {joinLink.Id} for tenant {tenantId}");

                return new ApiResponse<TenantJoinLinkDto>(result, true, "Join link created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating join link: {ex.Message}", ex);
                return new ApiResponse<TenantJoinLinkDto>(
                    default!,
                    false,
                    "Failed to create join link",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<bool>> RevokeAsync(int id)
        {
            try
            {
                var tenantId = _tenantContext.TenantId;
                var joinLink = await _dbSet.FirstOrDefaultAsync(j => j.Id == id && j.TenantId == tenantId);
                
                if (joinLink == null)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        "Join link not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }
                
                joinLink.IsRevoked = true;
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Revoked join link {id}");
                return new ApiResponse<bool>(true, true, "Join link revoked successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error revoking join link: {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Failed to revoke join link",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<TenantJoinLinkDto>> GetByTokenAsync(string token)
        {
            try
            {
                // Cross-tenant: no tenant filtering
                var joinLink = await _context.Set<TenantJoinLink>()
                    .Include(j => j.Tenant)
                    .FirstOrDefaultAsync(j => j.JoinToken == token);

                if (joinLink == null)
                {
                    return new ApiResponse<TenantJoinLinkDto>(
                        default!,
                        false,
                        "Join link not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                var dto = _mapper.Map<TenantJoinLinkDto>(joinLink);
                return new ApiResponse<TenantJoinLinkDto>(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting join link by token: {ex.Message}", ex);
                return new ApiResponse<TenantJoinLinkDto>(
                    default!,
                    false,
                    "Failed to retrieve join link",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }
    }
}
