using GroundUp.core.dtos;

namespace GroundUp.Data.Abstractions.Interfaces
{
    /// <summary>
    /// Repository for managing tenant invitations
    /// Follows standard CRUD pattern with tenant-scoped operations
    /// </summary>
    public interface ITenantInvitationRepository
    {
        #region Standard CRUD Operations

        /// <summary>
        /// Get all invitations for current tenant (paginated)
        /// Tenant is determined from ITenantContext
        /// </summary>
        Task<ApiResponse<PaginatedData<TenantInvitationDto>>> GetAllAsync(FilterParams filterParams);

        /// <summary>
        /// Get invitation by ID (tenant-scoped)
        /// </summary>
        Task<ApiResponse<TenantInvitationDto>> GetByIdAsync(int id);

        /// <summary>
        /// Create a new invitation for current tenant
        /// Tenant is determined from ITenantContext
        /// </summary>
        Task<ApiResponse<TenantInvitationDto>> AddAsync(CreateTenantInvitationDto dto, Guid createdByUserId);

        /// <summary>
        /// Update an existing invitation
        /// </summary>
        Task<ApiResponse<TenantInvitationDto>> UpdateAsync(int id, UpdateTenantInvitationDto dto);

        /// <summary>
        /// Delete (revoke) an invitation
        /// </summary>
        Task<ApiResponse<bool>> DeleteAsync(int id);

        #endregion

        #region Invitation-Specific Operations

        /// <summary>
        /// Get only pending (valid, not accepted) invitations for current tenant
        /// Tenant is determined from ITenantContext
        /// </summary>
        Task<ApiResponse<List<TenantInvitationDto>>> GetPendingInvitationsAsync();

        /// <summary>
        /// Resend an invitation (extends expiration)
        /// </summary>
        Task<ApiResponse<bool>> ResendInvitationAsync(int id, int expirationDays = 7);

        #endregion

        #region Cross-Tenant Operations (No Tenant Filter)

        /// <summary>
        /// Get invitation by token (works across all tenants)
        /// </summary>
        Task<ApiResponse<TenantInvitationDto>> GetByTokenAsync(string token);

        /// <summary>
        /// Get invitations for a specific email (works across all tenants)
        /// </summary>
        Task<ApiResponse<List<TenantInvitationDto>>> GetInvitationsForEmailAsync(string email);

        /// <summary>
        /// Accept an invitation and assign user to tenant (works across all tenants)
        /// </summary>
        /// <param name="token">Invitation token</param>
        /// <param name="userId">GroundUp user ID</param>
        /// <param name="externalUserId">Optional external identity (Keycloak sub) to store in UserTenant for membership resolution</param>
        Task<ApiResponse<bool>> AcceptInvitationAsync(string token, Guid userId, string? externalUserId = null);

        #endregion
    }
}
