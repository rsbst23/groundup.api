using GroundUp.Core.dtos;

namespace GroundUp.Data.Abstractions.Interfaces;

/// <summary>
/// Repository interface for user management
/// 
/// SCOPE: Local database operations only - Keycloak is the source of truth
/// 
/// What this repository DOES:
/// - Query users from local database (efficient, paginated queries)
/// - Sync users from Keycloak to local DB (after authentication)
/// - Support tenant-based user queries
/// 
/// What this repository DOES NOT do (handled by Keycloak Admin UI):
/// - Create users (users created via OAuth flows - Google login, email registration)
/// - Update user profiles (managed in Keycloak)
/// - Manage user passwords (Keycloak handles password reset)
/// - Enable/disable users (managed in Keycloak)
/// - Assign Keycloak roles (SYSTEMADMIN, TENANTADMIN managed in Keycloak)
/// 
/// Note: Application-level roles/permissions are managed via:
/// - IRoleRepository (custom application roles)
/// - IUserRoleRepository (assign app roles to users)
/// - IPermissionRepository (custom permissions)
/// </summary>
public interface IUserRepository
{
    #region User Query Operations (Local Database)

    /// <summary>
    /// Get all users from local database (paginated, tenant-filtered)
    /// Fast queries against local DB - use this for lists/grids
    /// </summary>
    Task<ApiResponse<PaginatedData<UserSummaryDto>>> GetAllAsync(FilterParams filterParams);

    /// <summary>
    /// Get user details by ID from Keycloak (source of truth)
    /// Automatically syncs to local database in background
    /// Use this when you need fresh user data
    /// </summary>
    Task<ApiResponse<UserDetailsDto>> GetByIdAsync(string userId);

    #endregion

    #region User Sync Operations (Internal - Called by Auth Callback)

    /// <summary>
    /// LEGACY: Syncs a user from Keycloak to local database assuming User.Id == Guid.Parse(keycloakUser.Id).
    /// This conflicts with our multi-tenant identity model where Keycloak sub is stored on UserTenant.ExternalUserId.
    /// Prefer <see cref="EnsureLocalUserExistsAsync"/>.
    /// </summary>
    [Obsolete("Use EnsureLocalUserExistsAsync(Guid userId, string keycloakUserId, string realm). Keycloak sub should not be used as User.Id.")]
    Task<ApiResponse<UserDetailsDto>> AddAsync(UserDetailsDto keycloakUser);

    /// <summary>
    /// Ensures a local User record exists for the given GroundUp user id.
    /// Intended for internal auth workflows so services don't write to DbContext directly.
    /// </summary>
    Task<ApiResponse<bool>> EnsureLocalUserExistsAsync(Guid userId, string keycloakUserId, string realm);

    #endregion
}
