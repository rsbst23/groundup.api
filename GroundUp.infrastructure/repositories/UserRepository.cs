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
    public class UserRepository : BaseTenantRepository<User, UserSummaryDto>, IUserRepository
    {
        private readonly IIdentityProviderAdminService _identityProvider;
        private readonly IUserTenantRepository _userTenantRepository;
        private readonly KeycloakConfiguration _keycloakConfig;

        public UserRepository(
            ApplicationDbContext context,
            IMapper mapper,
            ILoggingService logger,
            ITenantContext tenantContext,
            IIdentityProviderAdminService identityProvider,
            IUserTenantRepository userTenantRepository,
            IOptions<KeycloakConfiguration> keycloakConfig)
            : base(context, mapper, logger, tenantContext)
        {
            _identityProvider = identityProvider;
            _userTenantRepository = userTenantRepository;
            _keycloakConfig = keycloakConfig.Value;
        }

        #region CRUD Operations

        // GetAllAsync - Query local database (efficient, tenant-filtered, paginated)
        // Inherits from BaseTenantRepository - no override needed unless you want custom logic

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

        public async Task<ApiResponse<UserDetailsDto>> AddAsync(CreateUserDto userDto)
        {
            try
            {
                // 1. Create user in Keycloak first
                _logger.LogInformation($"Creating user '{userDto.Username}' in Keycloak");
                var keycloakUser = await _identityProvider.CreateUserAsync(userDto);

                // 2. Assign default role to the new user
                if (!string.IsNullOrEmpty(_keycloakConfig.DefaultUserRole))
                {
                    _logger.LogInformation($"Assigning default role '{_keycloakConfig.DefaultUserRole}' to user {keycloakUser.Id}");
                    try
                    {
                        await _identityProvider.AssignRoleToUserAsync(keycloakUser.Id, _keycloakConfig.DefaultUserRole);
                    }
                    catch (Exception roleEx)
                    {
                        _logger.LogWarning($"Failed to assign default role '{_keycloakConfig.DefaultUserRole}' to user {keycloakUser.Id}: {roleEx.Message}");
                        // Don't fail the user creation if role assignment fails
                    }
                }

                // 3. Create local database record
                var dbUser = new User
                {
                    Id = Guid.Parse(keycloakUser.Id),
                    Username = keycloakUser.Username,
                    Email = keycloakUser.Email,
                    FirstName = keycloakUser.FirstName,
                    LastName = keycloakUser.LastName,
                    IsActive = keycloakUser.Enabled,
                    CreatedAt = DateTime.UtcNow,
                    TenantId = _tenantContext.TenantId // Assign to current tenant
                };

                _context.Users.Add(dbUser);
                await _context.SaveChangesAsync();

                // 4. Assign user to current tenant via UserTenantRepository
                await _userTenantRepository.AssignUserToTenantAsync(dbUser.Id, _tenantContext.TenantId);

                _logger.LogInformation($"Successfully created user '{userDto.Username}' with ID {keycloakUser.Id}");
                return new ApiResponse<UserDetailsDto>(
                    keycloakUser,
                    true,
                    "User created successfully",
                    null,
                    StatusCodes.Status201Created
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create user: {ex.Message}", ex);
                
                // TODO: Implement compensating transaction
                // If database save fails, consider deleting the user from Keycloak
                
                return new ApiResponse<UserDetailsDto>(
                    default!,
                    false,
                    "Failed to create user",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<UserDetailsDto>> UpdateAsync(string userId, UpdateUserDto userDto)
        {
            try
            {
                // 1. Update in Keycloak first
                var keycloakUser = await _identityProvider.UpdateUserAsync(userId, userDto);

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

                // 2. Update local database
                var dbUser = await _context.Users.FindAsync(Guid.Parse(userId));

                if (dbUser != null)
                {
                    dbUser.Email = keycloakUser.Email;
                    dbUser.FirstName = keycloakUser.FirstName;
                    dbUser.LastName = keycloakUser.LastName;
                    dbUser.IsActive = keycloakUser.Enabled;
                    dbUser.Username = keycloakUser.Username;

                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation($"Successfully updated user {userId}");
                return new ApiResponse<UserDetailsDto>(keycloakUser);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to update user {userId}: {ex.Message}", ex);
                return new ApiResponse<UserDetailsDto>(
                    default!,
                    false,
                    "Failed to update user",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(string userId)
        {
            try
            {
                // 1. Delete from Keycloak first
                var deleted = await _identityProvider.DeleteUserAsync(userId);

                if (!deleted)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        $"User with ID '{userId}' not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                // 2. Delete from database (cascade will remove UserTenants and UserRoles)
                var dbUser = await _context.Users.FindAsync(Guid.Parse(userId));

                if (dbUser != null)
                {
                    _context.Users.Remove(dbUser);
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation($"Successfully deleted user {userId}");
                return new ApiResponse<bool>(
                    true,
                    true,
                    "User deleted successfully"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete user {userId}: {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Failed to delete user",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        #endregion

        #region System Role Management

        public async Task<ApiResponse<List<SystemRoleDto>>> GetUserSystemRolesAsync(string userId)
        {
            try
            {
                var roles = await _identityProvider.GetUserRolesAsync(userId);
                return new ApiResponse<List<SystemRoleDto>>(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving system roles for user {userId}: {ex.Message}", ex);
                return new ApiResponse<List<SystemRoleDto>>(
                    new List<SystemRoleDto>(),
                    false,
                    "Failed to retrieve user system roles",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<bool>> AssignSystemRoleToUserAsync(string userId, string roleName)
        {
            try
            {
                var success = await _identityProvider.AssignRoleToUserAsync(userId, roleName);

                if (!success)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        $"Failed to assign role '{roleName}' to user",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed
                    );
                }

                _logger.LogInformation($"Successfully assigned role '{roleName}' to user {userId}");
                return new ApiResponse<bool>(
                    true,
                    true,
                    "Role assigned to user successfully"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to assign role '{roleName}' to user {userId}: {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Failed to assign role to user",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<bool>> RemoveSystemRoleFromUserAsync(string userId, string roleName)
        {
            try
            {
                var success = await _identityProvider.RemoveRoleFromUserAsync(userId, roleName);

                if (!success)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        $"Failed to remove role '{roleName}' from user",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed
                    );
                }

                _logger.LogInformation($"Successfully removed role '{roleName}' from user {userId}");
                return new ApiResponse<bool>(
                    true,
                    true,
                    "Role removed from user successfully"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to remove role '{roleName}' from user {userId}: {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Failed to remove role from user",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        #endregion

        #region User Management Operations

        public async Task<ApiResponse<bool>> SetUserEnabledAsync(string userId, bool enabled)
        {
            try
            {
                var success = await _identityProvider.SetUserEnabledAsync(userId, enabled);

                if (!success)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        $"Failed to set user enabled status",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed
                    );
                }

                // Update local database
                var dbUser = await _context.Users.FindAsync(Guid.Parse(userId));
                if (dbUser != null)
                {
                    dbUser.IsActive = enabled;
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation($"Successfully set user {userId} enabled status to {enabled}");
                return new ApiResponse<bool>(
                    true,
                    true,
                    $"User {(enabled ? "enabled" : "disabled")} successfully"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to set user {userId} enabled status: {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Failed to set user enabled status",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        public async Task<ApiResponse<bool>> SendPasswordResetEmailAsync(string userId)
        {
            try
            {
                var success = await _identityProvider.SendPasswordResetEmailAsync(userId);

                if (!success)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        $"Failed to send password reset email",
                        null,
                        StatusCodes.Status400BadRequest,
                        ErrorCodes.ValidationFailed
                    );
                }

                _logger.LogInformation($"Successfully sent password reset email to user {userId}");
                return new ApiResponse<bool>(
                    true,
                    true,
                    "Password reset email sent successfully"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send password reset email to user {userId}: {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Failed to send password reset email",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        #endregion

        #region Helper Methods

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
                        CreatedAt = DateTime.UtcNow,
                        TenantId = _tenantContext.TenantId
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
