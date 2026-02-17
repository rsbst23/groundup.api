using GroundUp.core.dtos;

namespace GroundUp.Data.Abstractions.Interfaces
{
    public interface ITenantJoinLinkRepository
    {
        // Tenant-scoped operations (use ITenantContext)
        Task<ApiResponse<PaginatedData<TenantJoinLinkDto>>> GetAllAsync(FilterParams filterParams, bool includeRevoked = false);
        Task<ApiResponse<TenantJoinLinkDto>> GetByIdAsync(int id);
        Task<ApiResponse<TenantJoinLinkDto>> CreateAsync(CreateTenantJoinLinkDto dto);
        Task<ApiResponse<bool>> RevokeAsync(int id);
        
        // Cross-tenant operation (for public join endpoint)
        Task<ApiResponse<TenantJoinLinkDto>> GetByTokenAsync(string token);
    }
}
