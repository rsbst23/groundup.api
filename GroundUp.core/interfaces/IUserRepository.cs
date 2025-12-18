using GroundUp.core.dtos;
using GroundUp.core.security;

namespace GroundUp.core.interfaces
{
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
        [RequiresPermission("users.view")]
        Task<ApiResponse<PaginatedData<UserSummaryDto>>> GetAllAsync(FilterParams filterParams);

        /// <summary>
        /// Get user details by ID from Keycloak (source of truth)
        /// Automatically syncs to local database in background
        /// Use this when you need fresh user data
        /// </summary>
        [RequiresPermission("users.view")]
        Task<ApiResponse<UserDetailsDto>> GetByIdAsync(string userId);

        #endregion

        #region User Sync Operations (Internal - Called by Auth Callback)

        /// <summary>
        /// Syncs a user from Keycloak to local database
        /// INTERNAL USE ONLY - Called by auth callback handler after Keycloak authentication
        /// NOT exposed via controller endpoint
        /// 
        /// Users are created in Keycloak via:
        /// - OAuth flows (Google login, email/password registration)
        /// - Keycloak Admin UI
        /// 
        /// This method just ensures they exist in our local DB for relational integrity
        /// </summary>
        /// <param name="keycloakUser">User details from Keycloak (already created via auth flow)</param>
        Task<ApiResponse<UserDetailsDto>> AddAsync(UserDetailsDto keycloakUser);

        #endregion
    }
}
