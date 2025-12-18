using AutoMapper;
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.infrastructure.repositories
{
    /// <summary>
    /// Repository for managing tenant invitations
    /// Extends BaseTenantRepository for tenant-scoped operations
    /// </summary>
    public class TenantInvitationRepository : BaseTenantRepository<TenantInvitation, TenantInvitationDto>, ITenantInvitationRepository
    {
        private readonly IUserTenantRepository _userTenantRepo;
        private readonly IIdentityProviderAdminService _identityProvider;

        public TenantInvitationRepository(
            ApplicationDbContext context,
            IMapper mapper,
            ILoggingService logger,
            ITenantContext tenantContext,
            IUserTenantRepository userTenantRepo,
            IIdentityProviderAdminService identityProvider)
            : base(context, mapper, logger, tenantContext)
        {
            _userTenantRepo = userTenantRepo;
            _identityProvider = identityProvider;
        }

        #region Standard CRUD Operations

        public async Task<ApiResponse<PaginatedData<TenantInvitationDto>>> GetAllAsync(FilterParams filterParams)
        {
            try
            {
                // Get tenant from context (same pattern as BaseTenantRepository)
                var tenantId = _tenantContext.TenantId;

                // Tenant-scoped query
                var query = _dbSet
                    .Include(ti => ti.Tenant)
                    .Include(ti => ti.CreatedByUser)
                    .Include(ti => ti.AcceptedByUser)
                    .Where(ti => ti.TenantId == tenantId)
                    .AsQueryable();

                // Apply sorting
                query = GroundUp.infrastructure.utilities.ExpressionHelper.ApplySorting(query, filterParams.SortBy);

                var totalRecords = await query.CountAsync();
                var pagedItems = await query
                    .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
                    .Take(filterParams.PageSize)
                    .ToListAsync();

                var mappedItems = _mapper.Map<List<TenantInvitationDto>>(pagedItems);
                var paginatedData = new PaginatedData<TenantInvitationDto>(
                    mappedItems,
                    filterParams.PageNumber,
                    filterParams.PageSize,
                    totalRecords
                );

                return new ApiResponse<PaginatedData<TenantInvitationDto>>(paginatedData);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving invitations: {ex.Message}", ex);
                return new ApiResponse<PaginatedData<TenantInvitationDto>>(
                    default!,
                    false,
                    "Failed to retrieve invitations",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public override async Task<ApiResponse<TenantInvitationDto>> GetByIdAsync(int id)
        {
            try
            {
                // Use base repository's tenant-scoped query with includes
                var invitation = await _dbSet
                    .Include(ti => ti.Tenant)
                    .Include(ti => ti.CreatedByUser)
                    .Include(ti => ti.AcceptedByUser)
                    .FirstOrDefaultAsync(ti => ti.Id == id);

                if (invitation == null)
                {
                    return new ApiResponse<TenantInvitationDto>(
                        default!,
                        false,
                        "Invitation not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                var invitationDto = _mapper.Map<TenantInvitationDto>(invitation);
                return new ApiResponse<TenantInvitationDto>(invitationDto);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving invitation by ID: {ex.Message}", ex);
                return new ApiResponse<TenantInvitationDto>(
                    default!,
                    false,
                    "Failed to retrieve invitation",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<TenantInvitationDto>> AddAsync(CreateTenantInvitationDto dto, Guid createdByUserId)
        {
            try
            {
                // Get tenant from context (same pattern as BaseTenantRepository)
                var tenantId = _tenantContext.TenantId;

                _logger.LogInformation($"Creating invitation for {dto.Email} to tenant {tenantId} by user {createdByUserId}");

                // Verify that the user exists in the database
                var creatingUser = await _context.Users.FindAsync(createdByUserId);
                if (creatingUser == null)
                {
                    _logger.LogError($"User {createdByUserId} not found in Users table");
                    return new ApiResponse<TenantInvitationDto>(
                        default!,
                        false,
                        $"User with ID {createdByUserId} not found",
                        new List<string> { "The creating user does not exist in the database" },
                        StatusCodes.Status404NotFound,
                        ErrorCodes.UserNotFound
                    );
                }

                // Set TenantId from context (not from DTO)
                dto.TenantId = tenantId;

                // Check if tenant exists
                var tenant = await _context.Tenants.FindAsync(tenantId);
                if (tenant == null)
                {
                    return new ApiResponse<TenantInvitationDto>(
                        default!,
                        false,
                        $"Tenant with ID {tenantId} not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                // Create invitation
                var invitation = new TenantInvitation
                {
                    ContactEmail = dto.Email.ToLowerInvariant(),
                    TenantId = tenantId,
                    InvitationToken = Guid.NewGuid().ToString("N"), // 32-character hex string
                    ExpiresAt = DateTime.UtcNow.AddDays(dto.ExpirationDays),
                    RoleId = null,
                    Status = InvitationStatus.Pending,
                    IsAdmin = dto.IsAdmin,
                    CreatedByUserId = createdByUserId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.TenantInvitations.Add(invitation);
                await _context.SaveChangesAsync();

                // Reload with navigation properties
                var created = await _context.TenantInvitations
                    .Include(ti => ti.Tenant)
                    .Include(ti => ti.CreatedByUser)
                    .FirstAsync(ti => ti.Id == invitation.Id);

                var invitationDto = _mapper.Map<TenantInvitationDto>(created);

                _logger.LogInformation($"Created invitation ID {invitation.Id} with token {invitation.InvitationToken}");
                return new ApiResponse<TenantInvitationDto>(
                    invitationDto,
                    true,
                    "Invitation created successfully"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating invitation: {ex.Message}", ex);
                return new ApiResponse<TenantInvitationDto>(
                    default!,
                    false,
                    "Failed to create invitation",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<TenantInvitationDto>> UpdateAsync(int id, UpdateTenantInvitationDto dto)
        {
            try
            {
                // Tenant-scoped: Only update invitations in current tenant
                var invitation = await _dbSet
                    .Include(ti => ti.Tenant)
                    .Include(ti => ti.CreatedByUser)
                    .FirstOrDefaultAsync(ti => ti.Id == id);

                if (invitation == null)
                {
                    return new ApiResponse<TenantInvitationDto>(
                        default!,
                        false,
                        "Invitation not found or not in current tenant",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                if (invitation.Status == InvitationStatus.Accepted)
                {
                    return new ApiResponse<TenantInvitationDto>(
                        default!,
                        false,
                        "Cannot update invitation - already accepted",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed
                    );
                }

                // Update allowed fields
                invitation.IsAdmin = dto.IsAdmin;
                invitation.ExpiresAt = DateTime.UtcNow.AddDays(dto.ExpirationDays);

                await _context.SaveChangesAsync();

                var invitationDto = _mapper.Map<TenantInvitationDto>(invitation);
                return new ApiResponse<TenantInvitationDto>(
                    invitationDto,
                    true,
                    "Invitation updated successfully"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating invitation: {ex.Message}", ex);
                return new ApiResponse<TenantInvitationDto>(
                    default!,
                    false,
                    "Failed to update invitation",
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
                // Tenant-scoped: Only delete invitations in current tenant
                var invitation = await _dbSet.FirstOrDefaultAsync(ti => ti.Id == id);

                if (invitation == null)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        "Invitation not found or not in current tenant",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                if (invitation.Status == InvitationStatus.Accepted)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        "Cannot delete invitation - already accepted",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed
                    );
                }

                _context.TenantInvitations.Remove(invitation);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Deleted invitation {id}");
                return new ApiResponse<bool>(
                    true,
                    true,
                    "Invitation deleted successfully"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting invitation: {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Failed to delete invitation",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        #endregion

        #region Invitation-Specific Operations

        public async Task<ApiResponse<List<TenantInvitationDto>>> GetPendingInvitationsAsync()
        {
            try
            {
                // Get tenant from context (same pattern as BaseTenantRepository)
                var tenantId = _tenantContext.TenantId;

                var now = DateTime.UtcNow;
                // Tenant-scoped: Only get pending invitations for current tenant
                var invitations = await _dbSet
                    .Include(ti => ti.Tenant)
                    .Include(ti => ti.CreatedByUser)
                    .Where(ti => ti.TenantId == tenantId 
                        && ti.Status == InvitationStatus.Pending 
                        && ti.ExpiresAt > now)
                    .OrderByDescending(ti => ti.CreatedAt)
                    .ToListAsync();

                var invitationDtos = _mapper.Map<List<TenantInvitationDto>>(invitations);
                return new ApiResponse<List<TenantInvitationDto>>(
                    invitationDtos,
                    true,
                    $"Found {invitationDtos.Count} pending invitation(s)"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving pending invitations: {ex.Message}", ex);
                return new ApiResponse<List<TenantInvitationDto>>(
                    default!,
                    false,
                    "Failed to retrieve pending invitations",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<bool>> ResendInvitationAsync(int id, int expirationDays = 7)
        {
            try
            {
                // Tenant-scoped: Only resend invitations in current tenant
                var invitation = await _dbSet.FirstOrDefaultAsync(ti => ti.Id == id);

                if (invitation == null)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        "Invitation not found or not in current tenant",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                if (invitation.Status == InvitationStatus.Accepted)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        "Cannot resend invitation - already accepted",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed
                    );
                }

                // Update expiration date
                invitation.ExpiresAt = DateTime.UtcNow.AddDays(expirationDays);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Resent invitation {id} with new expiration {invitation.ExpiresAt}");
                return new ApiResponse<bool>(
                    true,
                    true,
                    "Invitation resent successfully"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error resending invitation: {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Failed to resend invitation",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        #endregion

        #region Cross-Tenant Operations (No Tenant Filter)

        public async Task<ApiResponse<TenantInvitationDto>> GetByTokenAsync(string token)
        {
            try
            {
                // Special case: Token lookup should work across ALL tenants
                // This allows users without a tenant to accept invitations
                var invitation = await _context.TenantInvitations
                    .Include(ti => ti.Tenant)
                    .Include(ti => ti.CreatedByUser)
                    .Include(ti => ti.AcceptedByUser)
                    .FirstOrDefaultAsync(ti => ti.InvitationToken == token);

                if (invitation == null)
                {
                    return new ApiResponse<TenantInvitationDto>(
                        default!,
                        false,
                        "Invitation not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                var invitationDto = _mapper.Map<TenantInvitationDto>(invitation);
                return new ApiResponse<TenantInvitationDto>(invitationDto);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving invitation by token: {ex.Message}", ex);
                return new ApiResponse<TenantInvitationDto>(
                    default!,
                    false,
                    "Failed to retrieve invitation",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<List<TenantInvitationDto>>> GetInvitationsForEmailAsync(string email)
        {
            try
            {
                // Special case: Email lookup should work across ALL tenants
                // This allows users to see all their invitations regardless of current tenant context
                var normalizedEmail = email.ToLowerInvariant();
                var invitations = await _context.TenantInvitations
                    .Include(ti => ti.Tenant)
                    .Include(ti => ti.CreatedByUser)
                    .Include(ti => ti.AcceptedByUser)
                    .Where(ti => ti.ContactEmail == normalizedEmail)
                    .OrderByDescending(ti => ti.CreatedAt)
                    .ToListAsync();

                var invitationDtos = _mapper.Map<List<TenantInvitationDto>>(invitations);
                return new ApiResponse<List<TenantInvitationDto>>(
                    invitationDtos,
                    true,
                    $"Found {invitationDtos.Count} invitation(s)"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving invitations for email: {ex.Message}", ex);
                return new ApiResponse<List<TenantInvitationDto>>(
                    default!,
                    false,
                    "Failed to retrieve invitations",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<bool>> AcceptInvitationAsync(string token, Guid userId, string? externalUserId = null)
        {
            try
            {
                _logger.LogInformation($"User {userId} attempting to accept invitation with token {token}");

                // Get invitation
                var invitationResult = await GetByTokenAsync(token);
                if (!invitationResult.Success)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        invitationResult.Message,
                        invitationResult.Errors,
                        invitationResult.StatusCode,
                        invitationResult.ErrorCode
                    );
                }

                // Get the actual entity for updating
                var invitation = await _context.TenantInvitations
                    .Include(ti => ti.Tenant)
                    .FirstOrDefaultAsync(ti => ti.InvitationToken == token);

                if (invitation == null || invitation.Status != InvitationStatus.Pending || invitation.IsExpired)
                {
                    _logger.LogWarning($"Invitation is not valid (Status: {invitation?.Status}, Expired: {invitation?.IsExpired})");
                    return new ApiResponse<bool>(
                        false,
                        false,
                        "Invalid or expired invitation",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed
                    );
                }

                // Get user to verify email matches
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning($"User {userId} not found");
                    return new ApiResponse<bool>(
                        false,
                        false,
                        "User not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.UserNotFound
                    );
                }

                // Verify email matches (case-insensitive)
                if (!string.IsNullOrEmpty(user.Email) && 
                    !user.Email.Equals(invitation.ContactEmail, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning($"User email {user.Email} does not match invitation email {invitation.ContactEmail}");
                    return new ApiResponse<bool>(
                        false,
                        false,
                        "Email mismatch - this invitation is for a different email address",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed
                    );
                }

                // Assign user to tenant with IsAdmin flag and ExternalUserId
                // ExternalUserId is used for realm-based membership resolution (realm + sub -> tenant)
                await _userTenantRepo.AssignUserToTenantAsync(
                    userId,
                    invitation.TenantId,
                    invitation.IsAdmin,
                    externalUserId
                );

                // Mark invitation as accepted
                invitation.Status = InvitationStatus.Accepted;
                invitation.AcceptedAt = DateTime.UtcNow;
                invitation.AcceptedByUserId = userId;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId} successfully accepted invitation {invitation.Id} (ExternalUserId: {externalUserId})");
                return new ApiResponse<bool>(
                    true,
                    true,
                    "Invitation accepted successfully. You now have access to the tenant."
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error accepting invitation: {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Failed to accept invitation",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        #endregion
    }
}
