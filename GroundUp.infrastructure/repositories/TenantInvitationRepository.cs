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

                // Check if tenant exists and get realm
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

                // FOR LOCAL ACCOUNT INVITATIONS ONLY: Create Keycloak user and send execute actions email
                // FOR SSO INVITATIONS: Skip user creation - let the SSO provider (Google, Azure AD, etc.) create the user
                if (dto.IsLocalAccount && tenant.TenantType == core.enums.TenantType.Enterprise && !string.IsNullOrEmpty(tenant.RealmName))
                {
                    _logger.LogInformation($"Processing LOCAL ACCOUNT invitation for enterprise tenant {tenant.Name} in realm {tenant.RealmName}");
                    
                    // Check if user already exists in Keycloak
                    var existingUserId = await _identityProvider.GetUserIdByEmailAsync(tenant.RealmName, dto.Email);
                    
                    string keycloakUserId;
                    
                    if (existingUserId == null)
                    {
                        _logger.LogInformation($"User {dto.Email} does not exist in realm {tenant.RealmName}, creating new LOCAL ACCOUNT user...");
                        
                        // Create Keycloak user for local account
                        var createUserDto = new CreateUserDto
                        {
                            Username = dto.Email.Split('@')[0], // Use email prefix as username
                            Email = dto.Email,
                            FirstName = string.Empty, // Can be updated later
                            LastName = string.Empty,
                            Enabled = true,
                            EmailVerified = false,
                            SendWelcomeEmail = false // We'll send execute actions email instead
                        };
                        
                        keycloakUserId = await _identityProvider.CreateUserAsync(tenant.RealmName, createUserDto);
                        
                        if (keycloakUserId == null)
                        {
                            _logger.LogError($"Failed to create Keycloak user for invitation {invitation.Id}");
                            _logger.LogError($"CreateUserAsync returned null - check Keycloak logs for details");
                            // Don't fail the invitation creation, but log the error
                        }
                        else
                        {
                            _logger.LogInformation($"✅ Successfully created Keycloak LOCAL ACCOUNT user {keycloakUserId} for invitation {invitation.Id}");
                            
                            // Verify user was actually created by fetching it
                            var verifyUser = await _identityProvider.GetUserByIdAsync(keycloakUserId, tenant.RealmName);
                            if (verifyUser == null)
                            {
                                _logger.LogError($"❌ User creation verification failed! User {keycloakUserId} not found after creation");
                            }
                            else
                            {
                                _logger.LogInformation($"✅ User creation verified: {verifyUser.Email}, Enabled={verifyUser.Enabled}, EmailVerified={verifyUser.EmailVerified}");
                            }

                            // Send execute actions email with BOTH client_id and redirect_uri
                            // UPDATE_PROFILE: Prompts user to enter first name and last name
                            // UPDATE_PASSWORD: Prompts user to set their password
                            // VERIFY_EMAIL: Sends email verification (if SMTP configured)
                            //var actions = new List<string> { "UPDATE_PROFILE", "UPDATE_PASSWORD", "VERIFY_EMAIL" };
                            var actions = new List<string> { "UPDATE_PASSWORD", "VERIFY_EMAIL" };

                            // Build redirect URI to invitation acceptance endpoint
                            var apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5123";
                            var invitationUrl = $"{apiUrl}/api/invitations/invite/{invitation.InvitationToken}";
                            
                            // Get client ID from configuration
                            var clientId = Environment.GetEnvironmentVariable("KEYCLOAK_RESOURCE") ?? "groundup-api";
                            
                            _logger.LogInformation($"📧 Attempting to send execute-actions email...");
                            _logger.LogInformation($"   Realm: {tenant.RealmName}");
                            _logger.LogInformation($"   User ID: {keycloakUserId}");
                            _logger.LogInformation($"   Actions: {string.Join(", ", actions)}");
                            _logger.LogInformation($"   Client ID: {clientId}");
                            _logger.LogInformation($"   Redirect URI: {invitationUrl}");
                            
                            var emailSent = await _identityProvider.SendExecuteActionsEmailAsync(
                                tenant.RealmName,
                                keycloakUserId,
                                actions,
                                clientId,
                                invitationUrl
                            );
                            
                            if (!emailSent)
                            {
                                _logger.LogError($"❌ Failed to send execute actions email for invitation {invitation.Id}");
                                _logger.LogError($"   Check the SendExecuteActionsEmailAsync logs above for specific error details");
                                _logger.LogError($"   Common causes:");
                                _logger.LogError($"   1. SMTP not configured in realm '{tenant.RealmName}'");
                                _logger.LogError($"   2. Invalid client_id '{clientId}' for realm");
                                _logger.LogError($"   3. Invalid redirect_uri '{invitationUrl}' not in client's valid redirect URIs");
                                _logger.LogError($"   4. User email '{dto.Email}' is not set or invalid");
                            }
                            else
                            {
                                _logger.LogInformation($"✅ Successfully sent execute actions email for invitation {invitation.Id}");
                                _logger.LogInformation($"   User {dto.Email} should receive email to set password and verify email");
                                _logger.LogInformation($"   After completing actions, 'Back to application' link will redirect to: {invitationUrl}");
                            }
                        }
                    }
                    else
                    {
                        keycloakUserId = existingUserId;
                        _logger.LogInformation($"User already exists in Keycloak: {keycloakUserId} for invitation {invitation.Id}");
                        
                        // User exists - optionally send execute actions email if they haven't verified email
                        var keycloakUser = await _identityProvider.GetUserByIdAsync(keycloakUserId, tenant.RealmName);
                        if (keycloakUser != null && !keycloakUser.EmailVerified)
                        {
                            _logger.LogInformation($"Existing user {keycloakUserId} hasn't verified email, sending execute actions email");
							
                            var actions = new List<string> { "VERIFY_EMAIL" };
                            var apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5123";
                            var invitationUrl = $"{apiUrl}/api/invitations/invite/{invitation.InvitationToken}";
                            var clientId = Environment.GetEnvironmentVariable("KEYCLOAK_RESOURCE") ?? "groundup-api";
                            
                            await _identityProvider.SendExecuteActionsEmailAsync(
                                tenant.RealmName,
                                keycloakUserId,
                                actions,
                                clientId,
                                invitationUrl
                            );
                        }
                    }
                }
                else if (!dto.IsLocalAccount && tenant.TenantType == core.enums.TenantType.Enterprise)
                {
                    _logger.LogInformation($"Processing SSO invitation for enterprise tenant {tenant.Name} in realm {tenant.RealmName ?? "N/A"}");
                    _logger.LogInformation($"Skipping Keycloak user creation - user will authenticate via SSO (Google, Azure AD, etc.)");
                    _logger.LogInformation($"User will be created automatically by the SSO provider upon first login");
                }

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
