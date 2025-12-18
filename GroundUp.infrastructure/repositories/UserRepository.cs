using AutoMapper;
using GroundUp.core;
using GroundUp.core.configuration;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroundUp.infrastructure.repositories
{
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

        // GetAllAsync - Inherited from BaseTenantRepository
        // Queries local database (fast, tenant-filtered, paginated)

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

        /// <summary>
        /// Syncs a user from Keycloak to local database
        /// INTERNAL USE ONLY - Called by auth callback handler after Keycloak authentication
        /// NOT exposed via controller endpoint
        /// </summary>
        /// <param name="keycloakUser">User details from Keycloak (already created via auth flow)</param>
        public async Task<ApiResponse<UserDetailsDto>> AddAsync(UserDetailsDto keycloakUser)
        {
            try
            {
                // User should already exist in Keycloak (created during registration/social auth)
                // This method just syncs them to local database

                _logger.LogInformation($"Syncing user '{keycloakUser.Username}' to local database");

                // Create local database record
                var dbUser = new User
                {
                    Id = Guid.Parse(keycloakUser.Id),
                    Username = keycloakUser.Username,
                    Email = keycloakUser.Email,
                    FirstName = keycloakUser.FirstName,
                    LastName = keycloakUser.LastName,
                    IsActive = keycloakUser.Enabled,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(dbUser);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully synced user '{keycloakUser.Username}' (ID: {keycloakUser.Id}) to local database");

                return new ApiResponse<UserDetailsDto>(
                    keycloakUser,
                    true,
                    "User synced to database successfully",
                    null,
                    StatusCodes.Status201Created
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to sync user to database: {ex.Message}", ex);

                return new ApiResponse<UserDetailsDto>(
                    default!,
                    false,
                    "Failed to sync user to database",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Background task to sync user data from Keycloak to local database
        /// Creates new user if doesn't exist, updates if exists
        /// </summary>
        private async Task SyncUserToDatabaseAsync(UserDetailsDto keycloakUser)
        {
            try
            {
                var userId = Guid.Parse(keycloakUser.Id);
                var existingUser = await _context.Users.FindAsync(userId);

                if (existingUser == null)
                {
                    // Create new user record
                    var newUser = new User
                    {
                        Id = userId,
                        Username = keycloakUser.Username,
                        Email = keycloakUser.Email,
                        FirstName = keycloakUser.FirstName,
                        LastName = keycloakUser.LastName,
                        IsActive = keycloakUser.Enabled,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Users.Add(newUser);
                }
                else
                {
                    // Update existing user record
                    existingUser.Username = keycloakUser.Username;
                    existingUser.Email = keycloakUser.Email;
                    existingUser.FirstName = keycloakUser.FirstName;
                    existingUser.LastName = keycloakUser.LastName;
                    existingUser.IsActive = keycloakUser.Enabled;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Successfully synced user {keycloakUser.Id} to database");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to sync user {keycloakUser.Id} to database: {ex.Message}", ex);
                // Don't throw - this is a background operation
            }
        }

        #endregion
    }
}
