using GroundUp.core.dtos;
using GroundUp.core.dtos.tenants;
using GroundUp.core.enums;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace GroundUp.infrastructure.services
{
    internal class AuthFlowService : IAuthFlowService
    {
        private readonly ILoggingService _logger;
        private readonly IUserTenantRepository _userTenantRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly ITenantRepository _tenantRepository;
        private readonly ITenantJoinLinkRepository _tenantJoinLinkRepository;
        private readonly ITokenService _tokenService;
        private readonly IIdentityProviderService _identityProviderService;
        private readonly ITenantInvitationRepository _tenantInvitationRepository;
        private readonly IIdentityProviderAdminService _identityProviderAdminService;
        private readonly IUserRepository _userRepository;
        private readonly ApplicationDbContext _dbContext;

        public AuthFlowService(
            ILoggingService logger,
            IUserTenantRepository userTenantRepository,
            IUserRoleRepository userRoleRepository,
            ITenantRepository tenantRepository,
            ITenantJoinLinkRepository tenantJoinLinkRepository,
            ITokenService tokenService,
            IIdentityProviderService identityProviderService,
            ITenantInvitationRepository tenantInvitationRepository,
            IIdentityProviderAdminService identityProviderAdminService,
            IUserRepository userRepository,
            ApplicationDbContext dbContext)
        {
            _logger = logger;
            _userTenantRepository = userTenantRepository;
            _userRoleRepository = userRoleRepository;
            _tenantRepository = tenantRepository;
            _tenantJoinLinkRepository = tenantJoinLinkRepository;
            _tokenService = tokenService;
            _identityProviderService = identityProviderService;
            _tenantInvitationRepository = tenantInvitationRepository;
            _identityProviderAdminService = identityProviderAdminService;
            _userRepository = userRepository;
            _dbContext = dbContext;
        }

        public async Task<AuthCallbackResponseDto> HandleAuthCallbackAsync(
            string code,
            string? state,
            string redirectUri)
        {
            // 1) Parse state
            AuthCallbackState? callbackState = null;
            string? realm = null;

            if (!string.IsNullOrEmpty(state))
            {
                try
                {
                    callbackState = JsonSerializer.Deserialize<AuthCallbackState>(
                        System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(state)));
                    realm = callbackState?.Realm;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to parse state parameter: {ex.Message}");
                }
            }

            if (string.IsNullOrEmpty(realm))
            {
                realm = "groundup";
                _logger.LogInformation($"No realm in state, using default: {realm}");
            }

            // 2) Exchange auth code for tokens
            var tokenResponse = await _identityProviderService.ExchangeCodeForTokensAsync(code, redirectUri, realm);
            if (tokenResponse == null)
            {
                return new AuthCallbackResponseDto
                {
                    Success = false,
                    Flow = callbackState?.Flow ?? "unknown",
                    RequiresTenantSelection = false,
                    ErrorMessage = "Failed to exchange authorization code for tokens"
                };
            }

            // 3) Extract external user id (Keycloak sub)
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(tokenResponse.AccessToken);
            var keycloakUserId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            if (string.IsNullOrEmpty(keycloakUserId))
            {
                return new AuthCallbackResponseDto
                {
                    Success = false,
                    Flow = callbackState?.Flow ?? "unknown",
                    RequiresTenantSelection = false,
                    ErrorMessage = "Failed to extract user ID from token"
                };
            }

            _logger.LogInformation($"Keycloak user {keycloakUserId} authenticated in realm {realm}");

            // 4) Resolve existing membership
            var userTenant = await _userTenantRepository.GetByRealmAndExternalUserIdAsync(realm, keycloakUserId);
            Guid userId = userTenant?.UserId ?? Guid.NewGuid();

            // Note: user record is created inside flows when needed.

            // 5) Execute flow
            if (callbackState?.Flow == "invitation" && !string.IsNullOrEmpty(callbackState.InvitationToken))
            {
                return await HandleInvitationFlowAsync(userId, keycloakUserId, realm, callbackState.InvitationToken, tokenResponse.AccessToken);
            }

            if (callbackState?.Flow == "join_link" && !string.IsNullOrEmpty(callbackState.JoinToken))
            {
                return await HandleJoinLinkFlowAsync(userId, keycloakUserId, realm, callbackState.JoinToken, tokenResponse.AccessToken);
            }

            if (callbackState?.Flow == "enterprise_first_admin")
            {
                return await HandleEnterpriseFirstAdminFlowAsync(userId, keycloakUserId, realm, tokenResponse.AccessToken);
            }

            if (callbackState?.Flow == "new_org")
            {
                return await HandleNewOrganizationFlowAsync(userId, keycloakUserId, realm, tokenResponse.AccessToken);
            }

            return await HandleDefaultFlowAsync(userId, keycloakUserId, realm, tokenResponse.AccessToken);
        }

        private async Task EnsureLocalUserExistsAsync(Guid userId, string keycloakUserId, string realm)
        {
            var result = await _userRepository.EnsureLocalUserExistsAsync(userId, keycloakUserId, realm);
            if (!result.Success)
            {
                throw new InvalidOperationException(result.Message);
            }
        }

        private async Task<AuthCallbackResponseDto> HandleInvitationFlowAsync(
            Guid userId,
            string keycloakUserId,
            string realm,
            string invitationToken,
            string accessToken)
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    _logger.LogInformation($"Processing invitation flow for Keycloak user {keycloakUserId} in realm {realm}");

                    await EnsureLocalUserExistsAsync(userId, keycloakUserId, realm);

                    var accepted = await _tenantInvitationRepository.AcceptInvitationAsync(invitationToken, userId, keycloakUserId);
                    if (!accepted.Success)
                    {
                        await transaction.RollbackAsync();
                        return new AuthCallbackResponseDto
                        {
                            Success = false,
                            Flow = "invitation",
                            RequiresTenantSelection = false,
                            ErrorMessage = accepted.Message
                        };
                    }

                    await transaction.CommitAsync();

                    var userTenants = await _userTenantRepository.GetTenantsForUserAsync(userId);
                    if (userTenants.Count == 0)
                    {
                        return new AuthCallbackResponseDto
                        {
                            Success = false,
                            Flow = "invitation",
                            RequiresTenantSelection = false,
                            ErrorMessage = "No tenants found after accepting invitation"
                        };
                    }

                    var tenantId = userTenants[0].TenantId;
                    var customToken = await _tokenService.GenerateTokenAsync(userId, tenantId, ExtractClaims(accessToken));

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

                await EnsureLocalUserExistsAsync(userId, keycloakUserId, realm);

                var joinLinkResult = await _tenantJoinLinkRepository.GetByTokenAsync(joinToken);
                if (!joinLinkResult.Success || joinLinkResult.Data == null)
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

                // Enforce validity (repo returns by token even if revoked/expired; auth flow enforces access rules)
                if (joinLinkResult.Data.IsRevoked || joinLinkResult.Data.ExpiresAt <= DateTime.UtcNow)
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

                var tenantId = joinLinkResult.Data.TenantId;

                var existingMembership = await _userTenantRepository.GetUserTenantAsync(userId, tenantId);
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

                await _userTenantRepository.AssignUserToTenantAsync(userId, tenantId, isAdmin: false, externalUserId: keycloakUserId);

                await transaction.CommitAsync();

                var customToken = await _tokenService.GenerateTokenAsync(userId, tenantId, ExtractClaims(accessToken));

                return new AuthCallbackResponseDto
                {
                    Success = true,
                    Flow = "join_link",
                    Token = customToken,
                    TenantId = tenantId,
                    TenantName = joinLinkResult.Data.TenantName,
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
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    _logger.LogInformation($"Processing new organization flow for Keycloak user {keycloakUserId} in realm {realm}");

                    var ensureUser = await _userRepository.EnsureLocalUserExistsAsync(userId, keycloakUserId, realm);
                    if (!ensureUser.Success)
                    {
                        await transaction.RollbackAsync();
                        return new AuthCallbackResponseDto
                        {
                            Success = false,
                            Flow = "new_org",
                            RequiresTenantSelection = false,
                            ErrorMessage = ensureUser.Message
                        };
                    }

                    // Determine org name from local user record (now guaranteed to exist)
                    var user = await _dbContext.Users.FindAsync(userId);
                    var organizationName = !string.IsNullOrEmpty(user?.FirstName)
                        ? $"{user.FirstName}'s Organization"
                        : "My Organization";

                    var createTenant = await _tenantRepository.CreateStandardTenantForUserAsync(realm, organizationName);
                    if (!createTenant.Success || createTenant.Data == null)
                    {
                        await transaction.RollbackAsync();
                        return new AuthCallbackResponseDto
                        {
                            Success = false,
                            Flow = "new_org",
                            RequiresTenantSelection = false,
                            ErrorMessage = createTenant.Message
                        };
                    }

                    var tenantId = createTenant.Data.Id;

                    await _userTenantRepository.AssignUserToTenantAsync(userId, tenantId, isAdmin: true, externalUserId: keycloakUserId);

                    await transaction.CommitAsync();

                    var customToken = await _tokenService.GenerateTokenAsync(userId, tenantId, ExtractClaims(accessToken));

                    return new AuthCallbackResponseDto
                    {
                        Success = true,
                        Flow = "new_org",
                        Token = customToken,
                        TenantId = tenantId,
                        TenantName = createTenant.Data.Name,
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

        private async Task<AuthCallbackResponseDto> HandleDefaultFlowAsync(
            Guid userId,
            string keycloakUserId,
            string realm,
            string accessToken)
        {
            try
            {
                _logger.LogInformation($"Processing default flow for Keycloak user {keycloakUserId} in realm {realm}");

                var existingUser = await _dbContext.Users.FindAsync(userId);
                if (existingUser == null)
                {
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        await EnsureLocalUserExistsAsync(userId, keycloakUserId, realm);
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }

                var userTenants = await _userTenantRepository.GetTenantsForUserAsync(userId);

                if (userTenants.Count == 0)
                {
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

                    var tenantResult = await _tenantRepository.GetByRealmAsync(realm);
                    if (!tenantResult.Success || tenantResult.Data == null)
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

                    if (tenantResult.Data.TenantType == TenantType.Enterprise)
                    {
                        var (authorized, errorMessage) = await ValidateAndAssignSsoUserAsync(
                            userId,
                            keycloakUserId,
                            keycloakUser.Email,
                            tenantResult.Data,
                            realm);

                        if (!authorized)
                        {
                            _logger.LogWarning($"Unauthorized user {keycloakUserId} attempted access to enterprise tenant {tenantResult.Data.Name}.");
                            return new AuthCallbackResponseDto
                            {
                                Success = false,
                                Flow = "unauthorized_sso_access",
                                RequiresTenantSelection = false,
                                ErrorMessage = errorMessage
                            };
                        }

                        userTenants = await _userTenantRepository.GetTenantsForUserAsync(userId);
                    }
                    else
                    {
                        return await HandleNewOrganizationFlowAsync(userId, keycloakUserId, realm, accessToken);
                    }
                }

                if (userTenants.Count == 0)
                {
                    return new AuthCallbackResponseDto
                    {
                        Success = false,
                        Flow = "default",
                        RequiresTenantSelection = false,
                        ErrorMessage = "Unable to assign user to tenant. Please contact support."
                    };
                }

                if (userTenants.Count == 1)
                {
                    var token = await _tokenService.GenerateTokenAsync(userId, userTenants[0].TenantId, ExtractClaims(accessToken));

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
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    _logger.LogInformation($"Processing enterprise first admin flow for Keycloak user {keycloakUserId} in realm {realm}");

                    await EnsureLocalUserExistsAsync(userId, keycloakUserId, realm);

                    var tenantResult = await _tenantRepository.GetByRealmAsync(realm);
                    if (!tenantResult.Success || tenantResult.Data == null)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError($"No tenant found for realm {realm}");
                        return new AuthCallbackResponseDto
                        {
                            Success = false,
                            Flow = "enterprise_first_admin",
                            RequiresTenantSelection = false,
                            ErrorMessage = "Enterprise tenant not found for this realm"
                        };
                    }

                    var tenant = tenantResult.Data;

                    if (tenant.TenantType != core.enums.TenantType.Enterprise || !tenant.IsActive)
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

                    var hasAnyMembers = await _userTenantRepository.TenantHasAnyMembersAsync(tenant.Id);

                    if (hasAnyMembers)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogWarning($"Enterprise tenant {tenant.Id} already has member(s). First admin has already registered.");
                        return new AuthCallbackResponseDto
                        {
                            Success = false,
                            Flow = "enterprise_first_admin",
                            RequiresTenantSelection = false,
                            ErrorMessage = "This enterprise tenant already has an administrator. Please contact them for an invitation."
                        };
                    }

                    await _userTenantRepository.AssignUserToTenantAsync(userId, tenant.Id, isAdmin: true, externalUserId: keycloakUserId);

                    _logger.LogInformation($"Disabling registration for enterprise realm: {realm}");
                    var disabled = await _identityProviderAdminService.DisableRealmRegistrationAsync(realm);

                    if (!disabled)
                    {
                        _logger.LogWarning($"Failed to disable registration for enterprise realm: {realm}");
                    }

                    await transaction.CommitAsync();

                    var customToken = await _tokenService.GenerateTokenAsync(userId, tenant.Id, ExtractClaims(accessToken));

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

        private async Task<(bool authorized, string? errorMessage)> ValidateAndAssignSsoUserAsync(
            Guid userId,
            string keycloakUserId,
            string? userEmail,
            TenantDetailDto tenant,
            string realm)
        {
            _logger.LogInformation($"Validating SSO user {userEmail ?? "no-email"} for enterprise tenant {tenant.Name}");

            if (tenant.TenantType == TenantType.Enterprise && string.IsNullOrEmpty(userEmail))
            {
                _logger.LogWarning($"Enterprise login without email for realm {realm}");
                return (false, "Your authentication provider did not share your email address. Enterprise access requires a verified email.");
            }

            var userDomain = userEmail!.Split('@')[1].ToLowerInvariant();

            if (tenant.SsoAutoJoinDomains?.Contains(userDomain) == true)
            {
                _logger.LogInformation($"Auto-joining user {userId} from allowed domain {userDomain}");

                await _userTenantRepository.AssignUserToTenantAsync(userId, tenant.Id, isAdmin: false, keycloakUserId);

                var roleId = tenant.SsoAutoJoinRoleId;

                if (roleId == null)
                {
                    var roleLookup = await _userRoleRepository.GetRoleIdByNameAsync(tenant.Id, "Member");
                    roleId = roleLookup.Data;
                    if (roleId == null)
                    {
                        _logger.LogWarning($"No default role found for tenant {tenant.Id}");
                    }
                }

                if (roleId != null)
                {
                    var assignResult = await _userRoleRepository.AssignRoleAsync(userId, tenant.Id, roleId.Value);
                    if (!assignResult.Success)
                    {
                        _logger.LogWarning($"Role assignment failed for user {userId} tenant {tenant.Id}: {assignResult.Message}");
                      }
                  }
  
                  _logger.LogInformation($"User {userId} auto-joined tenant {tenant.Id} with role {roleId}");
                  return (true, null);
              }
  
              var pendingInvitations = await _tenantInvitationRepository.GetInvitationsForEmailAsync(userEmail!);
  
              var tenantInvitation = pendingInvitations.Data
                  ?.FirstOrDefault(i => i.TenantId == tenant.Id && i.Status == "Pending");
  
              if (tenantInvitation != null)
              {
                  _logger.LogInformation($"Auto-accepting invitation for user {userId}");
  
                  var acceptResult = await _tenantInvitationRepository.AcceptInvitationAsync(
                      tenantInvitation.InvitationToken,
                      userId,
                      keycloakUserId);
  
                  if (acceptResult.Success)
                  {
                      _logger.LogInformation($"User {userId} accepted invitation to tenant {tenant.Id}");
                      return (true, null);
                  }
  
                  _logger.LogError($"Failed to accept invitation: {acceptResult.Message}");
                  return (false, $"Failed to process invitation: {acceptResult.Message}");
              }
  
              _logger.LogWarning($"Unauthorized SSO login attempt for {userEmail} in tenant {tenant.Name}");
              return (false, "Access denied. Please request an invitation from your administrator.");
          }
  
          private static IEnumerable<Claim> ExtractClaims(string jwtToken)
          {
              var handler = new JwtSecurityTokenHandler();
              var token = handler.ReadJwtToken(jwtToken);
              return token.Claims;
          }
      }
  }
