using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.enums;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace GroundUp.api.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ILoggingService _logger;
        private readonly IUserTenantRepository _userTenantRepository;
        private readonly ITokenService _tokenService;
        private readonly IIdentityProviderService _identityProviderService;
        private readonly IUserRepository _userRepository;
        private readonly ITenantInvitationRepository _tenantInvitationRepository;
        private readonly IIdentityProviderAdminService _identityProviderAdminService;
        private readonly ITenantRepository _tenantRepository;
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _configuration;

        public AuthController(
            ILoggingService logger, 
            IUserTenantRepository userTenantRepository, 
            ITokenService tokenService,
            IIdentityProviderService identityProviderService,
            IUserRepository userRepository,
            ITenantInvitationRepository tenantInvitationRepository,
            IIdentityProviderAdminService identityProviderAdminService,
            ITenantRepository tenantRepository,
            ApplicationDbContext dbContext,
            IConfiguration configuration)
        {
            _logger = logger;
            _userTenantRepository = userTenantRepository;
            _tokenService = tokenService;
            _identityProviderService = identityProviderService;
            _userRepository = userRepository;
            _tenantInvitationRepository = tenantInvitationRepository;
            _identityProviderAdminService = identityProviderAdminService;
            _tenantRepository = tenantRepository;
            _dbContext = dbContext;
            _configuration = configuration;
        }

        /// <summary>
        /// Handles OAuth callback - processes authentication and returns result as JSON
        /// React frontend is responsible for navigation based on the response
        /// </summary>
        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<AuthCallbackResponseDto>>> AuthCallback(
            [FromQuery] string code,
            [FromQuery] string? state)
        {
            try
            {
                // 1. Parse state to get realm (if provided)
                AuthCallbackState? callbackState = null;
                string? realm = null;
                
                if (!string.IsNullOrEmpty(state))
                {
                    try
                    {
                        callbackState = JsonSerializer.Deserialize<AuthCallbackState>(
                            System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(state)));
                        realm = callbackState?.Realm;
                        
                        if (!string.IsNullOrEmpty(realm))
                        {
                            _logger.LogInformation($"Auth callback using realm from state: {realm}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to parse state parameter: {ex.Message}");
                    }
                }

                // Default to shared realm if not specified
                if (string.IsNullOrEmpty(realm))
                {
                    realm = "groundup"; // TODO: Get from configuration
                    _logger.LogInformation($"No realm in state, using default: {realm}");
                }

                // 2. Exchange authorization code for tokens (with optional realm)
                var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/callback";
                var tokenResponse = await _identityProviderService.ExchangeCodeForTokensAsync(code, redirectUri, realm);

                if (tokenResponse == null)
                {
                    _logger.LogError("Failed to exchange authorization code for tokens");
                    var response = new ApiResponse<AuthCallbackResponseDto>(
                        default!,
                        false,
                        "Failed to exchange authorization code for tokens",
                        new List<string> { "Token exchange failed" },
                        StatusCodes.Status400BadRequest,
                        "TOKEN_EXCHANGE_FAILED"
                    );
                    return StatusCode(response.StatusCode, response);
                }

                // 3. Extract Keycloak user ID from JWT
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(tokenResponse.AccessToken);
                var keycloakUserId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

                if (string.IsNullOrEmpty(keycloakUserId))
                {
                    _logger.LogError("Failed to extract user ID from JWT token");
                    var response = new ApiResponse<AuthCallbackResponseDto>(
                        default!,
                        false,
                        "Failed to extract user ID from token",
                        new List<string> { "Invalid token structure" },
                        StatusCodes.Status400BadRequest,
                        "INVALID_TOKEN"
                    );
                    return StatusCode(response.StatusCode, response);
                }

                _logger.LogInformation($"Keycloak user {keycloakUserId} authenticated in realm {realm}");

                // 4. IDENTITY RESOLUTION using UserTenant.ExternalUserId
                // Query: UserTenant WHERE Tenant.RealmName = @realm AND ExternalUserId = @keycloakUserId
                var userTenant = await _userTenantRepository.GetByRealmAndExternalUserIdAsync(realm, keycloakUserId);
                Guid? userId = userTenant?.UserId;

                if (userId == null)
                {
                    // First time seeing this identity - need to create user
                    _logger.LogInformation($"First login for Keycloak user {keycloakUserId} in realm {realm}");
                    
                    // Get full user details from Keycloak
                    var keycloakUser = await _identityProviderAdminService.GetUserByIdAsync(keycloakUserId, realm);
                    if (keycloakUser == null)
                    {
                        _logger.LogError($"User {keycloakUserId} not found in Keycloak realm {realm}");
                        var response = new ApiResponse<AuthCallbackResponseDto>(
                            default!,
                            false,
                            "User not found in authentication system",
                            new List<string> { $"User {keycloakUserId} not found in realm {realm}" },
                            StatusCodes.Status404NotFound,
                            "USER_NOT_FOUND"
                        );
                        return StatusCode(response.StatusCode, response);
                    }

                    // Create new global GroundUp user (UserTenant will be created in flow handlers)
                    userId = Guid.NewGuid();
                    
                    _logger.LogInformation($"Creating new GroundUp user {userId} for Keycloak user {keycloakUserId} in realm {realm}");
                }
                else
                {
                    _logger.LogInformation($"Resolved Keycloak user {keycloakUserId} in realm {realm} to GroundUp user {userId}");
                }

                // 5. Handle different flows based on state
                AuthCallbackResponseDto responseDto;
                
                if (callbackState?.Flow == "invitation" && !string.IsNullOrEmpty(callbackState.InvitationToken))
                {
                    // Flow: User clicked invitation link
                    responseDto = await HandleInvitationFlowAsync(userId.Value, keycloakUserId, realm, callbackState.InvitationToken, tokenResponse.AccessToken);
                }
                else if (callbackState?.Flow == "join_link" && !string.IsNullOrEmpty(callbackState.JoinToken))
                {
                    // Flow: User clicked join link
                    responseDto = await HandleJoinLinkFlowAsync(userId.Value, keycloakUserId, realm, callbackState.JoinToken, tokenResponse.AccessToken);
                }
                else if (callbackState?.Flow == "enterprise_first_admin")
                {
                    // Flow: Enterprise tenant first admin registration
                    responseDto = await HandleEnterpriseFirstAdminFlowAsync(userId.Value, keycloakUserId, realm, tokenResponse.AccessToken);
                }
                else if (callbackState?.Flow == "new_org")
                {
                    // Flow: New organization signup (standard tenant)
                    responseDto = await HandleNewOrganizationFlowAsync(userId.Value, keycloakUserId, realm, tokenResponse.AccessToken);
                }
                else
                {
                    // Flow: Default - check for existing tenants or pending invitations
                    responseDto = await HandleDefaultFlowAsync(userId.Value, keycloakUserId, realm, tokenResponse.AccessToken);
                }

                // Return appropriate status code based on flow result
                var statusCode = responseDto.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest;
                var finalResponse = new ApiResponse<AuthCallbackResponseDto>(
                    responseDto,
                    responseDto.Success,
                    responseDto.Success ? "Authentication successful" : "Authentication failed",
                    responseDto.ErrorMessage != null ? new List<string> { responseDto.ErrorMessage } : null,
                    statusCode,
                    responseDto.Success ? null : "AUTH_FLOW_FAILED"
                );
                
                return StatusCode(finalResponse.StatusCode, finalResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in auth callback: {ex.Message}", ex);
                var response = new ApiResponse<AuthCallbackResponseDto>(
                    default!,
                    false,
                    "An unexpected error occurred during authentication",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    "UNEXPECTED_ERROR"
                );
                return StatusCode(response.StatusCode, response);
            }
        }

        private async Task<AuthCallbackResponseDto> HandleInvitationFlowAsync(
            Guid userId, 
            string keycloakUserId, 
            string realm, 
            string invitationToken, 
            string accessToken)
        {
            // Use execution strategy for retry support with manual transactions
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    _logger.LogInformation($"Processing invitation flow for Keycloak user {keycloakUserId} in realm {realm}");

                    // Check if user already exists in local DB
                    var existingUser = await _dbContext.Users.FindAsync(userId);
                    
                    if (existingUser == null)
                    {
                        // User doesn't exist yet - create user
                        var keycloakUser = await _identityProviderAdminService.GetUserByIdAsync(keycloakUserId, realm);
                        if (keycloakUser == null)
                        {
                            await transaction.RollbackAsync();
                            return new AuthCallbackResponseDto
                            {
                                Success = false,
                                Flow = "invitation",
                                RequiresTenantSelection = false,
                                ErrorMessage = "User not found in Keycloak"
                            };
                        }

                        // Create new user
                        var newUser = new core.entities.User
                        {
                            Id = userId,
                            DisplayName = !string.IsNullOrEmpty(keycloakUser.FirstName) 
                                ? $"{keycloakUser.FirstName} {keycloakUser.LastName}".Trim() 
                                : keycloakUser.Username ?? "Unknown",
                            Email = keycloakUser.Email,
                            Username = keycloakUser.Username,
                            FirstName = keycloakUser.FirstName,
                            LastName = keycloakUser.LastName,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        _dbContext.Users.Add(newUser);
                        await _dbContext.SaveChangesAsync();

                        _logger.LogInformation($"Created new user {userId} for realm {realm}");
                    }

                    // Accept the invitation - this creates UserTenant with ExternalUserId
                    var accepted = await _tenantInvitationRepository.AcceptInvitationAsync(invitationToken, userId, keycloakUserId);

                    if (!accepted.Success)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogWarning($"Failed to accept invitation: {accepted.Message}");
                        return new AuthCallbackResponseDto
                        {
                            Success = false,
                            Flow = "invitation",
                            RequiresTenantSelection = false,
                            ErrorMessage = accepted.Message
                        };
                    }

                    await transaction.CommitAsync();

                    // Get the user's tenants
                    var userTenants = await _userTenantRepository.GetTenantsForUserAsync(userId);

                    if (userTenants.Count == 0)
                    {
                        _logger.LogWarning($"User {userId} has no tenants after accepting invitation");
                        return new AuthCallbackResponseDto
                        {
                            Success = false,
                            Flow = "invitation",
                            RequiresTenantSelection = false,
                            ErrorMessage = "No tenants found after accepting invitation"
                        };
                    }

                    // Generate tenant-scoped token
                    var tenantId = userTenants[0].TenantId;
                    var customToken = await _tokenService.GenerateTokenAsync(userId, tenantId, ExtractClaims(accessToken));

                    // Set cookie
                    SetAuthCookie(customToken);

                    _logger.LogInformation($"User {userId} successfully added to tenant {tenantId} via invitation");
                    
                    return new AuthCallbackResponseDto
                    {
                        Success = true,
                        Flow = "invitation",
                        Token = customToken,
                        TenantId = tenantId,
                        TenantName = userTenants[0].Tenant?.Name,
                        RequiresTenantSelection = false,
                        Message = "Invitation accepted successfully"
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Error handling invitation flow: {ex.Message}", ex);
                    return new AuthCallbackResponseDto
                    {
                        Success = false,
                        Flow = "invitation",
                        RequiresTenantSelection = false,
                        ErrorMessage = "An unexpected error occurred while accepting invitation"
                    };
                }
            });
        }

        private async Task<AuthCallbackResponseDto> HandleJoinLinkFlowAsync(
            Guid userId, 
            string keycloakUserId, 
            string realm, 
            string joinToken, 
            string accessToken)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation($"Processing join link flow for Keycloak user {keycloakUserId} in realm {realm}");

                // Check if user already exists in local DB
                var existingUser = await _dbContext.Users.FindAsync(userId);
                
                if (existingUser == null)
                {
                    // User doesn't exist yet - create user
                    var keycloakUser = await _identityProviderAdminService.GetUserByIdAsync(keycloakUserId, realm);
                    if (keycloakUser == null)
                    {
                        await transaction.RollbackAsync();
                        return new AuthCallbackResponseDto
                        {
                            Success = false,
                            Flow = "join_link",
                            RequiresTenantSelection = false,
                            ErrorMessage = "User not found in Keycloak"
                        };
                    }

                    // Create new user
                    var newUser = new core.entities.User
                    {
                        Id = userId,
                        DisplayName = !string.IsNullOrEmpty(keycloakUser.FirstName) 
                            ? $"{keycloakUser.FirstName} {keycloakUser.LastName}".Trim() 
                            : keycloakUser.Username ?? "Unknown",
                        Email = keycloakUser.Email,
                        Username = keycloakUser.Username,
                        FirstName = keycloakUser.FirstName,
                        LastName = keycloakUser.LastName,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    _dbContext.Users.Add(newUser);
                    await _dbContext.SaveChangesAsync();

                    _logger.LogInformation($"Created new user {userId} for realm {realm}");
                }

                // Get join link from database
                var joinLink = await _dbContext.TenantJoinLinks
                    .Include(j => j.Tenant)
                    .FirstOrDefaultAsync(j => j.JoinToken == joinToken && !j.IsRevoked && j.ExpiresAt > DateTime.UtcNow);

                if (joinLink == null)
                {
                    await transaction.RollbackAsync();
                    return new AuthCallbackResponseDto
                    {
                        Success = false,
                        Flow = "join_link",
                        RequiresTenantSelection = false,
                        ErrorMessage = "Join link is invalid, revoked, or expired"
                    };
                }

                // Check if user is already a member of this tenant
                var existingMembership = await _userTenantRepository.GetUserTenantAsync(userId, joinLink.TenantId);
                if (existingMembership != null)
                {
                    await transaction.RollbackAsync();
                    return new AuthCallbackResponseDto
                    {
                        Success = false,
                        Flow = "join_link",
                        RequiresTenantSelection = false,
                        ErrorMessage = "You are already a member of this tenant"
                    };
                }

                // Assign user to tenant with default role (not admin)
                await _userTenantRepository.AssignUserToTenantAsync(
                    userId,
                    joinLink.TenantId,
                    isAdmin: false,
                    externalUserId: keycloakUserId);

                await transaction.CommitAsync();

                // Get the user's tenants
                var userTenants = await _userTenantRepository.GetTenantsForUserAsync(userId);

                // Generate tenant-scoped token
                var customToken = await _tokenService.GenerateTokenAsync(userId, joinLink.TenantId, ExtractClaims(accessToken));

                // Set cookie
                SetAuthCookie(customToken);

                _logger.LogInformation($"User {userId} successfully joined tenant {joinLink.TenantId} via join link");
                
                return new AuthCallbackResponseDto
                {
                    Success = true,
                    Flow = "join_link",
                    Token = customToken,
                    TenantId = joinLink.TenantId,
                    TenantName = joinLink.Tenant?.Name,
                    RequiresTenantSelection = false,
                    Message = "Successfully joined tenant"
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error handling join link flow: {ex.Message}", ex);
                return new AuthCallbackResponseDto
                {
                    Success = false,
                    Flow = "join_link",
                    RequiresTenantSelection = false,
                    ErrorMessage = "An unexpected error occurred while joining tenant"
                };
            }
        }

        private async Task<AuthCallbackResponseDto> HandleNewOrganizationFlowAsync(
            Guid userId, 
            string keycloakUserId, 
            string realm, 
            string accessToken)
        {
            // Use execution strategy for retry support with manual transactions
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    _logger.LogInformation($"Processing new organization flow for Keycloak user {keycloakUserId} in realm {realm}");

                    // This flow is ONLY for standard tenant creation
                    // Enterprise tenants use the 'enterprise_first_admin' flow

                    // Check if user already exists in local DB
                    var existingUser = await _dbContext.Users.FindAsync(userId);
                    
                    if (existingUser == null)
                    {
                        // User doesn't exist yet - create user
                        var keycloakUser = await _identityProviderAdminService.GetUserByIdAsync(keycloakUserId, realm);
                        if (keycloakUser == null)
                        {
                            await transaction.RollbackAsync();
                            return new AuthCallbackResponseDto
                            {
                                Success = false,
                                Flow = "new_org",
                                RequiresTenantSelection = false,
                                ErrorMessage = "User not found in Keycloak"
                            };
                        }

                        // Create new user
                        var newUser = new core.entities.User
                        {
                            Id = userId,
                            DisplayName = !string.IsNullOrEmpty(keycloakUser.FirstName) 
                                ? $"{keycloakUser.FirstName} {keycloakUser.LastName}".Trim() 
                                : keycloakUser.Username ?? "Unknown",
                            Email = keycloakUser.Email,
                            Username = keycloakUser.Username,
                            FirstName = keycloakUser.FirstName,
                            LastName = keycloakUser.LastName,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        _dbContext.Users.Add(newUser);
                        await _dbContext.SaveChangesAsync();

                        _logger.LogInformation($"Created new user {userId} for realm {realm}");
                    }
                    else
                    {
                        // User already exists - check if they already have a tenant
                        var existingTenants = await _userTenantRepository.GetTenantsForUserAsync(userId);
                        if (existingTenants.Count > 0)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogWarning($"User {userId} already has tenant(s), cannot create new organization");
                            return new AuthCallbackResponseDto
                            {
                                Success = false,
                                Flow = "new_org",
                                RequiresTenantSelection = false,
                                ErrorMessage = "User already has an organization"
                            };
                        }
                    }

                    // Get user display name for organization name
                    var user = await _dbContext.Users.FindAsync(userId);
                    var organizationName = !string.IsNullOrEmpty(user?.FirstName) 
                        ? $"{user.FirstName}'s Organization" 
                        : "My Organization";

                    // Create new STANDARD tenant using shared realm
                    var tenant = new core.entities.Tenant
                    {
                        Name = organizationName,
                        Description = "Created via self-service registration",
                        IsActive = true,
                        TenantType = core.enums.TenantType.Standard,
                        RealmName = realm, // Should be "groundup" for standard tenants
                        CreatedAt = DateTime.UtcNow
                    };

                    _dbContext.Tenants.Add(tenant);
                    await _dbContext.SaveChangesAsync();
                    
                    _logger.LogInformation($"Created standard tenant: {tenant.Name} (ID: {tenant.Id}) using realm: {realm}");

                    // Assign user to tenant as admin with ExternalUserId
                    await _userTenantRepository.AssignUserToTenantAsync(
                        userId,
                        tenant.Id,
                        isAdmin: true,
                        externalUserId: keycloakUserId);

                    await transaction.CommitAsync();

                    // Generate tenant-scoped token
                    var customToken = await _tokenService.GenerateTokenAsync(
                        userId, 
                        tenant.Id, 
                        ExtractClaims(accessToken));

                    // Set cookie
                    SetAuthCookie(customToken);

                    _logger.LogInformation($"User {userId} successfully created organization {tenant.Name} (ID: {tenant.Id})");
                    
                    return new AuthCallbackResponseDto
                    {
                        Success = true,
                        Flow = "new_org",
                        Token = customToken,
                        TenantId = tenant.Id,
                        TenantName = tenant.Name,
                        RequiresTenantSelection = false,
                        IsNewOrganization = true,
                        Message = "Organization created successfully"
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Error handling new organization flow: {ex.Message}", ex);
                    return new AuthCallbackResponseDto
                    {
                        Success = false,
                        Flow = "new_org",
                        RequiresTenantSelection = false,
                        ErrorMessage = "An unexpected error occurred while creating organization"
                    };
                }
            });
        }

        private async Task<AuthCallbackResponseDto> HandleDefaultFlowAsync(Guid userId, string keycloakUserId, string realm, string accessToken)
        {
            try
            {
                _logger.LogInformation($"Processing default flow for Keycloak user {keycloakUserId} in realm {realm}");

                // Check if user exists in local DB, if not create them
                var existingUser = await _dbContext.Users.FindAsync(userId);
                
                if (existingUser == null)
                {
                    // First time login - create user
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        var keycloakUser = await _identityProviderAdminService.GetUserByIdAsync(keycloakUserId, realm);
                        if (keycloakUser == null)
                        {
                            await transaction.RollbackAsync();
                            return new AuthCallbackResponseDto
                            {
                                Success = false,
                                Flow = "default",
                                RequiresTenantSelection = false,
                                ErrorMessage = "User not found in Keycloak"
                            };
                        }

                        // Create new user
                        var newUser = new core.entities.User
                        {
                            Id = userId,
                            DisplayName = !string.IsNullOrEmpty(keycloakUser.FirstName) 
                                ? $"{keycloakUser.FirstName} {keycloakUser.LastName}".Trim() 
                                : keycloakUser.Username ?? "Unknown",
                            Email = keycloakUser.Email,
                            Username = keycloakUser.Username,
                            FirstName = keycloakUser.FirstName,
                            LastName = keycloakUser.LastName,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        _dbContext.Users.Add(newUser);
                        await _dbContext.SaveChangesAsync();

                        await transaction.CommitAsync();
                        _logger.LogInformation($"Created new user {userId} for realm {realm}");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }

                // Resolve tenant memberships
                var userTenants = await _userTenantRepository.GetTenantsForUserAsync(userId);

                // Handle users with no tenant memberships
                if (userTenants.Count == 0)
                {
                    // Get Keycloak user details first (needed for email validation)
                    var keycloakUser = await _identityProviderAdminService.GetUserByIdAsync(keycloakUserId, realm);
                    if (keycloakUser == null)
                    {
                        return new AuthCallbackResponseDto
                        {
                            Success = false,
                            Flow = "default",
                            RequiresTenantSelection = false,
                            ErrorMessage = "User not found in authentication system"
                        };
                    }

                    // Determine tenant from realm
                    var tenant = await _dbContext.Tenants
                        .Include(t => t.SsoAutoJoinRole)
                        .FirstOrDefaultAsync(t => t.RealmName == realm);

                    if (tenant == null)
                    {
                        _logger.LogError($"No tenant found for realm {realm}");
                        return new AuthCallbackResponseDto
                        {
                            Success = false,
                            Flow = "default",
                            RequiresTenantSelection = false,
                            ErrorMessage = "Tenant configuration error"
                        };
                    }

                    if (tenant.TenantType == TenantType.Enterprise)
                    {
                        // ENTERPRISE TENANT: Validate and potentially auto-join
                        var (authorized, errorMessage) = await ValidateAndAssignSsoUserAsync(
                            userId,
                            keycloakUserId,
                            keycloakUser.Email,
                            tenant,
                            realm
                        );

                        if (!authorized)
                        {
                            // TODO: Delete Keycloak user (cleanup orphaned account)
                            // await _identityProviderAdminService.DeleteUserAsync(keycloakUserId, realm);
                            _logger.LogWarning($"Unauthorized user {keycloakUserId} attempted access to enterprise tenant {tenant.Name}. Keycloak account should be manually deleted.");

                            return new AuthCallbackResponseDto
                            {
                                Success = false,
                                Flow = "unauthorized_sso_access",
                                RequiresTenantSelection = false,
                                ErrorMessage = errorMessage
                            };
                        }

                        // User was authorized and assigned - refresh memberships
                        userTenants = await _userTenantRepository.GetTenantsForUserAsync(userId);
                    }
                    else
                    {
                        // STANDARD TENANT: Auto-create tenant for them (existing new_org flow)
                        return await HandleNewOrganizationFlowAsync(userId, keycloakUserId, realm, accessToken);
                    }
                }

                // At this point, userTenants.Count should be > 0
                if (userTenants.Count == 0)
                {
                    // This should only happen if something went wrong
                    return new AuthCallbackResponseDto
                    {
                        Success = false,
                        Flow = "default",
                        RequiresTenantSelection = false,
                        ErrorMessage = "Unable to assign user to tenant. Please contact support."
                    };
                }

                // User has tenants - check for tenant selection requirement
                if (userTenants.Count == 1)
                {
                    // Automatically select the tenant
                    var token = await _tokenService.GenerateTokenAsync(userId, userTenants[0].TenantId, ExtractClaims(accessToken));
                    SetAuthCookie(token);

                    return new AuthCallbackResponseDto
                    {
                        Success = true,
                        Flow = "default",
                        Token = token,
                        TenantId = userTenants[0].TenantId,
                        TenantName = userTenants[0].Tenant?.Name,
                        RequiresTenantSelection = false,
                        Message = "User authenticated successfully"
                    };
                }
                else
                {
                    // Multiple tenants - require tenant selection
                    return new AuthCallbackResponseDto
                    {
                        Success = true,
                        Flow = "default",
                        RequiresTenantSelection = true,
                        AvailableTenants = userTenants.Select(ut => new TenantSelectionDto
                        {
                            TenantId = ut.TenantId,
                            TenantName = ut.Tenant?.Name ?? "Unknown",
                            IsAdmin = ut.IsAdmin
                        }).ToList(),
                        Message = "Please select a tenant"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling default flow: {ex.Message}", ex);
                return new AuthCallbackResponseDto
                {
                    Success = false,
                    Flow = "default",
                    RequiresTenantSelection = false,
                    ErrorMessage = "An unexpected error occurred during authentication"
                };
            }
        }

        private async Task<AuthCallbackResponseDto> HandleEnterpriseFirstAdminFlowAsync(
            Guid userId, 
            string keycloakUserId, 
            string realm, 
            string accessToken)
        {
            // Use execution strategy for retry support with manual transactions
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    _logger.LogInformation($"Processing enterprise first admin flow for Keycloak user {keycloakUserId} in realm {realm}");

                    // Check if user already exists in local DB
                    var existingUser = await _dbContext.Users.FindAsync(userId);
                    
                    if (existingUser == null)
                    {
                        // User doesn't exist yet - create user
                        var keycloakUser = await _identityProviderAdminService.GetUserByIdAsync(keycloakUserId, realm);
                        if (keycloakUser == null)
                        {
                            await transaction.RollbackAsync();
                            return new AuthCallbackResponseDto
                            {
                                Success = false,
                                Flow = "enterprise_first_admin",
                                RequiresTenantSelection = false,
                                ErrorMessage = "User not found in authentication system"
                            };
                        }

                        // Create new user
                        var newUser = new core.entities.User
                        {
                            Id = userId,
                            DisplayName = !string.IsNullOrEmpty(keycloakUser.FirstName) 
                                ? $"{keycloakUser.FirstName} {keycloakUser.LastName}".Trim() 
                                : keycloakUser.Username ?? "Unknown",
                            Email = keycloakUser.Email,
                            Username = keycloakUser.Username,
                            FirstName = keycloakUser.FirstName,
                            LastName = keycloakUser.LastName,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        _dbContext.Users.Add(newUser);
                        await _dbContext.SaveChangesAsync();

                        _logger.LogInformation($"Created new user {userId} for realm {realm}");
                    }

                    // Find the enterprise tenant that was pre-created for this realm
                    var tenant = await _dbContext.Tenants
                        .FirstOrDefaultAsync(t => t.RealmName == realm && t.TenantType == core.enums.TenantType.Enterprise && t.IsActive);

                    if (tenant == null)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError($"No enterprise tenant found for realm {realm}");
                        return new AuthCallbackResponseDto
                        {
                            Success = false,
                            Flow = "enterprise_first_admin",
                            RequiresTenantSelection = false,
                            ErrorMessage = "Enterprise tenant not found for this realm"
                        };
                    }

                    // Check if tenant already has any users (race condition check)
                    var existingMembers = await _dbContext.UserTenants
                        .Where(ut => ut.TenantId == tenant.Id)
                        .CountAsync();

                    if (existingMembers > 0)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogWarning($"Enterprise tenant {tenant.Id} already has {existingMembers} member(s). First admin has already registered.");
                        return new AuthCallbackResponseDto
                        {
                            Success = false,
                            Flow = "enterprise_first_admin",
                            RequiresTenantSelection = false,
                            ErrorMessage = "This enterprise tenant already has an administrator. Please contact them for an invitation."
                        };
                    }

                    // Assign user to tenant as admin with ExternalUserId
                    await _userTenantRepository.AssignUserToTenantAsync(
                        userId,
                        tenant.Id,
                        isAdmin: true,
                        externalUserId: keycloakUserId);

                    // Disable registration for enterprise realm after first admin
                    _logger.LogInformation($"Disabling registration for enterprise realm: {realm}");
                    var disabled = await _identityProviderAdminService.DisableRealmRegistrationAsync(realm);
                    
                    if (!disabled)
                    {
                        _logger.LogWarning($"Failed to disable registration for enterprise realm: {realm}");
                    }
                    else
                    {
                        _logger.LogInformation($"Successfully disabled registration for enterprise realm: {realm}");
                    }

                    await transaction.CommitAsync();

                    // Generate tenant-scoped token
                    var customToken = await _tokenService.GenerateTokenAsync(
                        userId, 
                        tenant.Id, 
                        ExtractClaims(accessToken));

                    // Set cookie
                    SetAuthCookie(customToken);

                    _logger.LogInformation($"User {userId} successfully became first admin of enterprise tenant {tenant.Name} (ID: {tenant.Id})");
                    
                    return new AuthCallbackResponseDto
                    {
                        Success = true,
                        Flow = "enterprise_first_admin",
                        Token = customToken,
                        TenantId = tenant.Id,
                        TenantName = tenant.Name,
                        RequiresTenantSelection = false,
                        IsNewOrganization = false,
                        Message = "Successfully joined enterprise organization as administrator"
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Error handling enterprise first admin flow: {ex.Message}", ex);
                    return new AuthCallbackResponseDto
                    {
                        Success = false,
                        Flow = "enterprise_first_admin",
                        RequiresTenantSelection = false,
                        ErrorMessage = "An unexpected error occurred while joining enterprise organization"
                    };
                }
            });
        }

        /// <summary>
        /// Validates SSO user and handles auto-join or invitation acceptance for enterprise tenants
        /// Returns true if user was authorized and assigned to tenant
        /// Returns false if unauthorized (caller should delete Keycloak user and return error)
        /// </summary>
        private async Task<(bool authorized, string? errorMessage)> ValidateAndAssignSsoUserAsync(
            Guid userId,
            string keycloakUserId,
            string? userEmail,
            Tenant tenant,
            string realm)
        {
            _logger.LogInformation($"Validating SSO user {userEmail ?? "no-email"} for enterprise tenant {tenant.Name}");

            // 1. For ENTERPRISE tenants, email is REQUIRED
            //    (Needed for invitation matching and domain-based auto-join)
            if (tenant.TenantType == TenantType.Enterprise && string.IsNullOrEmpty(userEmail))
            {
                _logger.LogWarning($"Enterprise login without email for realm {realm}");
                return (false, "Your authentication provider did not share your email address. Enterprise access requires a verified email.");
            }

            // 2. Email validation passed - continue with authorization checks

            // 3. Check domain-based auto-join
            var userDomain = userEmail!.Split('@')[1].ToLowerInvariant();

            if (tenant.SsoAutoJoinDomains?.Contains(userDomain) == true)
            {
                _logger.LogInformation($"Auto-joining user {userId} from allowed domain {userDomain}");

                // Assign to tenant
                await _userTenantRepository.AssignUserToTenantAsync(
                    userId,
                    tenant.Id,
                    isAdmin: false,
                    keycloakUserId
                );

                // Assign default role
                var roleId = tenant.SsoAutoJoinRoleId;

                if (roleId == null)
                {
                    // Fallback to default "Member" role
                    var defaultRole = await _dbContext.Roles
                        .FirstOrDefaultAsync(r => r.TenantId == tenant.Id && r.Name == "Member");

                    roleId = defaultRole?.Id;

                    if (roleId == null)
                    {
                        _logger.LogWarning($"No default role found for tenant {tenant.Id}");
                    }
                }

                if (roleId != null)
                {
                    await _dbContext.UserRoles.AddAsync(new UserRole
                    {
                        UserId = userId,
                        RoleId = roleId.Value,
                        TenantId = tenant.Id
                    });

                    await _dbContext.SaveChangesAsync();
                }

                _logger.LogInformation($"User {userId} auto-joined tenant {tenant.Id} with role {roleId}");
                return (true, null);
            }

            // 4. Check for pending invitation
            var pendingInvitations = await _tenantInvitationRepository
                .GetInvitationsForEmailAsync(userEmail!);

            var tenantInvitation = pendingInvitations.Data
                ?.FirstOrDefault(i => i.TenantId == tenant.Id && i.Status == "Pending");

            if (tenantInvitation != null)
            {
                _logger.LogInformation($"Auto-accepting invitation for user {userId}");

                // Accept the invitation (creates UserTenant and assigns roles)
                var acceptResult = await _tenantInvitationRepository.AcceptInvitationAsync(
                    tenantInvitation.InvitationToken,
                    userId,
                    keycloakUserId
                );

                if (acceptResult.Success)
                {
                    _logger.LogInformation($"User {userId} accepted invitation to tenant {tenant.Id}");
                    return (true, null);
                }
                else
                {
                    _logger.LogError($"Failed to accept invitation: {acceptResult.Message}");
                    return (false, $"Failed to process invitation: {acceptResult.Message}");
                }
            }

            // 5. No authorization found - user is not allowed
            _logger.LogWarning($"Unauthorized SSO login attempt for {userEmail} in tenant {tenant.Name}");
            return (false, "Access denied. Please request an invitation from your administrator.");
        }

        private IEnumerable<Claim> ExtractClaims(string jwtToken)
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwtToken);
            return token.Claims;
        }

        private void SetAuthCookie(string token, string cookieName = "AuthToken")
        {
            Response.Cookies.Append(cookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            });
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("AuthToken");
            _logger.LogInformation("User logged out - cookie cleared");
            
            var response = new ApiResponse<string>(
                "Logged out successfully.",
                true,
                "User logged out successfully",
                null,
                StatusCodes.Status200OK
            );
            
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Get login URL for authentication
        /// Returns the Keycloak login URL - client should redirect user to this URL
        /// </summary>
        [HttpGet("login")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<AuthUrlResponseDto>>> GetLoginUrl(
            [FromQuery] string? domain = null, 
            [FromQuery] string? returnUrl = null)
        {
            try
            {
                var authUrl = await BuildAuthUrlAsync(domain, action: null, returnUrl);
                
                if (authUrl.StartsWith("ERROR:"))
                {
                    var errorMessage = authUrl.Substring(6);
                    return BadRequest(new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        errorMessage,
                        new List<string> { errorMessage },
                        StatusCodes.Status400BadRequest,
                        "INVALID_DOMAIN"
                    ));
                }
                
                var response = new ApiResponse<AuthUrlResponseDto>(
                    new AuthUrlResponseDto { AuthUrl = authUrl, Action = "login" },
                    true,
                    "Login URL generated successfully",
                    null,
                    StatusCodes.Status200OK
                );
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating login URL: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<AuthUrlResponseDto>(
                    null,
                    false,
                    "Failed to generate login URL",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    "LOGIN_URL_GENERATION_FAILED"
                ));
            }
        }

        /// <summary>
        /// Get registration URL for new user signup (standard tenants only)
        /// Returns the Keycloak registration URL in shared realm
        /// Enterprise tenants get registration URL directly from enterprise signup endpoint
        /// </summary>
        [HttpGet("register")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<AuthUrlResponseDto>>> GetRegisterUrl(
            [FromQuery] string? returnUrl = null)
        {
            try
            {
                // Standard tenant registration - always uses shared realm
                var authUrl = await BuildAuthUrlAsync(domain: null, action: "register", returnUrl);
                
                if (authUrl.StartsWith("ERROR:"))
                {
                    var errorMessage = authUrl.Substring(6);
                    return BadRequest(new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        errorMessage,
                        new List<string> { errorMessage },
                        StatusCodes.Status400BadRequest,
                        "INVALID_DOMAIN"
                    ));
                }
                
                var response = new ApiResponse<AuthUrlResponseDto>(
                    new AuthUrlResponseDto { AuthUrl = authUrl, Action = "register" },
                    true,
                    "Registration URL generated successfully",
                    null,
                    StatusCodes.Status200OK
                );
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating registration URL: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<AuthUrlResponseDto>(
                    null,
                    false,
                    "Failed to generate registration URL",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    "REGISTRATION_URL_GENERATION_FAILED"
                ));
            }
        }

        /// <summary>
        /// Helper method to build Keycloak auth URL
        /// </summary>
        private async Task<string> BuildAuthUrlAsync(string? domain, string? action, string? returnUrl)
        {
            string targetRealm;
            bool isEnterpriseLogin = false;
            
            if (string.IsNullOrEmpty(domain))
            {
                // No domain provided - standard tenant flow
                targetRealm = "groundup"; // Shared realm
                _logger.LogInformation("No domain provided - using shared realm for standard tenant");
            }
            else
            {
                // Domain provided - look up tenant by CustomDomain
                _logger.LogInformation($"Looking up tenant by domain: {domain}");
                
                var tenant = await _dbContext.Tenants
                    .FirstOrDefaultAsync(t => t.CustomDomain == domain && t.IsActive);
                
                if (tenant == null)
                {
                    _logger.LogWarning($"No active tenant found for domain: {domain}");
                    return $"ERROR:No tenant found for domain: {domain}";
                }
                
                targetRealm = tenant.RealmName;
                isEnterpriseLogin = tenant.TenantType == core.enums.TenantType.Enterprise;
                
                _logger.LogInformation($"Domain {domain} maps to tenant '{tenant.Name}' using realm: {targetRealm}");
            }
            
            // Determine action - normalize to lowercase
            var normalizedAction = action?.ToLowerInvariant();
            var isRegistration = normalizedAction == "register" || normalizedAction == "signup";
            
            _logger.LogInformation($"Building {(isRegistration ? "registration" : "login")} URL for realm: {targetRealm}");
            
            // Determine flow based on action and login type
            // Registration = new_org flow (user creates new organization)
            // Enterprise login = default flow (user logs into existing enterprise tenant)
            // Standard login = default flow (user logs into existing standard tenant)
            string flow;
            if (isRegistration && !isEnterpriseLogin)
            {
                // New user registering for standard tenant - create new organization
                flow = "new_org";
            }
            else
            {
                // Existing user login (standard or enterprise) - default flow
                flow = "default";
            }
            
            // Build state
            var state = new AuthCallbackState
            {
                Flow = flow,
                Realm = targetRealm
            };
            
            var stateJson = JsonSerializer.Serialize(state);
            var stateEncoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(stateJson));
            
            // Read from environment variables
            var keycloakAuthUrl = Environment.GetEnvironmentVariable("KEYCLOAK_AUTH_SERVER_URL") 
                ?? _configuration["Keycloak:AuthServerUrl"];
            var clientId = Environment.GetEnvironmentVariable("KEYCLOAK_RESOURCE") 
                ?? _configuration["Keycloak:Resource"];
            
            if (string.IsNullOrEmpty(keycloakAuthUrl) || string.IsNullOrEmpty(clientId))
            {
                _logger.LogError("Keycloak configuration is missing");
                return "ERROR:Authentication service is not properly configured";
            }
            
            var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/callback";
            
            // For registration, use the dedicated registration endpoint
            // For login, use the standard auth endpoint
            string authUrl;
            
            if (isRegistration)
            {
                // Use Keycloak's registration endpoint
                authUrl = $"{keycloakAuthUrl}/realms/{targetRealm}/protocol/openid-connect/registrations" +
                          $"?client_id={Uri.EscapeDataString(clientId!)}" +
                          $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                          $"&response_type=code" +
                          $"&scope=openid%20email%20profile" +
                          $"&state={Uri.EscapeDataString(stateEncoded)}";
            }
            else
            {
                // Use standard auth endpoint for login
                authUrl = $"{keycloakAuthUrl}/realms/{targetRealm}/protocol/openid-connect/auth" +
                          $"?client_id={Uri.EscapeDataString(clientId!)}" +
                          $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                          $"&response_type=code" +
                          $"&scope=openid%20email%20profile" +
                          $"&state={Uri.EscapeDataString(stateEncoded)}";
            }
            
            return authUrl;
        }

        [HttpGet("me")]
        [Authorize]
        public ActionResult<ApiResponse<UserProfileDto>> GetUserProfile()
        {
            var userId = User.FindFirstValue("ApplicationUserId");
            if (userId == null)
            {
                var response = new ApiResponse<UserProfileDto>(
                    default!,
                    false,
                    "Unauthorized access.",
                    null,
                    StatusCodes.Status401Unauthorized,
                    ErrorCodes.Unauthorized
                );
                return StatusCode(response.StatusCode, response);
            }

            var userProfile = new UserProfileDto
            {
                Id = userId,
                Email = User.FindFirstValue(ClaimTypes.Email) ?? "",
                Username = User.FindFirstValue("preferred_username") ??
                           User.FindFirstValue(ClaimTypes.Name) ?? "",
                FullName = User.FindFirstValue("name") ?? "",
                Roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
            };

            _logger.LogInformation($"User profile retrieved for {userProfile.Username}");
            
            var successResponse = new ApiResponse<UserProfileDto>(
                userProfile,
                true,
                "User profile retrieved successfully",
                null,
                StatusCodes.Status200OK
            );
            
            return StatusCode(successResponse.StatusCode, successResponse);
        }

        [HttpGet("debug-token")]
        [Authorize]
        public ActionResult<ApiResponse<Dictionary<string, string>>> DebugToken()
        {
            var claims = User.Claims.ToDictionary(
                c => c.Type,
                c => c.Value
            );

            var response = new ApiResponse<Dictionary<string, string>>(
                claims,
                true,
                "Token claims retrieved successfully",
                null,
                StatusCodes.Status200OK
            );

            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("set-tenant")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<SetTenantResponseDto>>> SetTenant([FromBody] SetTenantRequestDto request)
        {
            ApiResponse<SetTenantResponseDto> response;

            var userIdStr = User.FindFirstValue("ApplicationUserId");
            
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                _logger.LogWarning("Failed to parse userId as Guid");
                response = new ApiResponse<SetTenantResponseDto>(default!, false, "Unauthorized access.", null, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized);
                return StatusCode(response.StatusCode, response);
            }

            var userTenants = await _userTenantRepository.GetTenantsForUserAsync(userId);

            if (request.TenantId == null)
            {
                if (userTenants.Count == 1)
                {
                    var token = await _tokenService.GenerateTokenAsync(userId, userTenants[0].TenantId, User.Claims);
                    
                    Response.Cookies.Append("AuthToken", token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddHours(1)
                    });

                    _logger.LogInformation("Token generated and cookie set");
                    
                    response = new ApiResponse<SetTenantResponseDto>(
                        new SetTenantResponseDto {
                            SelectionRequired = false,
                            AvailableTenants = null,
                            Token = token
                        },
                        true,
                        "Tenant set and token issued.",
                        null,
                        StatusCodes.Status200OK
                    );
                }
                else
                {
                    response = new ApiResponse<SetTenantResponseDto>(
                        new SetTenantResponseDto {
                            SelectionRequired = true,
                            AvailableTenants = userTenants.Select(ut => new TenantDto
                            {
                                Id = ut.Tenant.Id,
                                Name = ut.Tenant.Name,
                                Description = ut.Tenant.Description
                            }).ToList(),
                            Token = null
                        },
                        true,
                        "Tenant selection required.",
                        null,
                        StatusCodes.Status200OK
                    );
                }
            }
            else
            {
                var userTenant = await _userTenantRepository.GetUserTenantAsync(userId, request.TenantId.Value);
                if (userTenant == null)
                {
                    _logger.LogWarning("User is not assigned to the selected tenant");
                    response = new ApiResponse<SetTenantResponseDto>(
                        default!, false, "User is not assigned to the selected tenant.", null, StatusCodes.Status403Forbidden, ErrorCodes.Unauthorized);
                }
                else
                {
                    var token = await _tokenService.GenerateTokenAsync(userId, request.TenantId.Value, User.Claims);
                    
                    Response.Cookies.Append("AuthToken", token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddHours(1)
                    });

                    _logger.LogInformation("Token generated and cookie set");
                    
                    response = new ApiResponse<SetTenantResponseDto>(
                        new SetTenantResponseDto {
                            SelectionRequired = false,
                            AvailableTenants = null,
                            Token = token
                        },
                        true,
                        "Tenant set and token issued.",
                        null,
                        StatusCodes.Status200OK
                    );
                }
            }
            
            return StatusCode(response.StatusCode, response);
        }
    }
}