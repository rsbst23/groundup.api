using AutoMapper;
using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.core;
using GroundUp.core.configuration;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.Data.Core.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroundUp.Data.Core.Repositories;

/// <summary>
/// Repository for user management
///
/// SCOPE: Local database operations + read-only sync from Keycloak
///
/// This repository:
/// - Queries users from local database (fast, tenant-filtered)
/// - Syncs users from Keycloak to local DB (after auth)
/// - DOES NOT create/update/delete users in Keycloak (handled by Keycloak Admin UI)
/// </summary>
public class UserRepository : BaseTenantRepository<User, UserSummaryDto>, IUserRepository
{
    private readonly IIdentityProviderAdminService _identityProvider;
    private readonly KeycloakConfiguration _keycloakConfig;

    public UserRepository(
        ApplicationDbContext context,
        IMapper mapper,
        ILoggingService logger,
        ITenantContext tenantContext,
        IIdentityProviderAdminService identityProvider,
        IOptions<KeycloakConfiguration> keycloakConfig)
        : base(context, mapper, logger, tenantContext)
    {
        _identityProvider = identityProvider;
        _keycloakConfig = keycloakConfig.Value;
    }

    #region User Query Operations

    /// <summary>
    /// Get user details by ID from Keycloak (source of truth)
    /// Automatically syncs to local database in background
    /// </summary>
    public async Task<ApiResponse<UserDetailsDto>> GetByIdAsync(string userId)
    {
        try
        {
            // Get fresh user data from Keycloak (source of truth)
            var keycloakUser = await _identityProvider.GetUserByIdAsync(userId);

            if (keycloakUser == null)
            {
                return new ApiResponse<UserDetailsDto>(
                    default!,
                    false,
                    $"User with ID '{userId}' not found",
                    null,
                    StatusCodes.Status404NotFound,
                    ErrorCodes.NotFound
                );
            }

            // Sync to database in background (non-blocking)
            _ = Task.Run(() => SyncUserToDatabaseAsync(keycloakUser));

            return new ApiResponse<UserDetailsDto>(keycloakUser);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving user {userId}: {ex.Message}", ex);
            return new ApiResponse<UserDetailsDto>(
                default!,
                false,
                "Failed to retrieve user",
                new List<string> { ex.Message },
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError
            );
        }
    }

    #endregion

    #region User Sync Operations (Internal - Called by Auth Callback)

    [Obsolete("Use EnsureLocalUserExistsAsync(Guid userId, string keycloakUserId, string realm). Keycloak sub should not be used as User.Id.")]
    public Task<ApiResponse<UserDetailsDto>> AddAsync(UserDetailsDto keycloakUser)
    {
        return Task.FromResult(new ApiResponse<UserDetailsDto>(
            default!,
            false,
            "Legacy sync method is disabled. Use EnsureLocalUserExistsAsync with a GroundUp user id.",
            new List<string> { "User.Id must not be derived from Keycloak user id." },
            StatusCodes.Status400BadRequest,
            ErrorCodes.ValidationFailed));
    }

    #endregion

    #region Helper Methods

    private async Task SyncUserToDatabaseAsync(UserDetailsDto keycloakUser)
    {
        try
        {
            // We intentionally do NOT sync by Keycloak sub into User.Id.
            // In this system, Keycloak sub is stored on UserTenant.ExternalUserId (realm-scoped),
            // and a single GroundUp user may have multiple external IDs across tenants/realms.
            _logger.LogInformation($"Skipping background user sync for Keycloak user {keycloakUser.Id} to avoid using Keycloak id as User.Id.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed during background user sync noop for {keycloakUser.Id}: {ex.Message}", ex);
        }
    }

    public async Task<ApiResponse<bool>> EnsureLocalUserExistsAsync(Guid userId, string keycloakUserId, string realm)
    {
        try
        {
            var existingUser = await _context.Set<User>().FindAsync(userId);
            if (existingUser != null)
            {
                return new ApiResponse<bool>(true, true, "User already exists.");
            }

            var keycloakUser = await _identityProvider.GetUserByIdAsync(keycloakUserId, realm);
            if (keycloakUser == null)
            {
                return new ApiResponse<bool>(false, false, "User not found in Keycloak.", null, StatusCodes.Status404NotFound, ErrorCodes.NotFound);
            }

            var newUser = new User
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

            _context.Set<User>().Add(newUser);
            await _context.SaveChangesAsync();

            return new ApiResponse<bool>(true, true, "User created.", null, StatusCodes.Status201Created);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to ensure local user exists for {userId}: {ex.Message}", ex);
            return new ApiResponse<bool>(false, false, "Failed to ensure local user exists.", new List<string> { ex.Message }, StatusCodes.Status500InternalServerError, ErrorCodes.InternalServerError);
        }
    }

    #endregion
}
