using GroundUp.core.dtos;
using GroundUp.core.entities;

namespace GroundUp.core.interfaces
{
    /// <summary>
    /// Service for interacting with Keycloak Identity Provider
    /// 
    /// SCOPE: Read-only user queries + Realm management for enterprise tenants
    /// 
    /// What this service DOES:
    /// - Get user details from Keycloak (for syncing to local DB after auth)
    /// - Create/Delete/Get Keycloak realms (for enterprise tenant multi-realm support)
    /// - Create OAuth2 clients in realms (for enabling authentication)
    /// 
    /// What this service DOES NOT do (handled by Keycloak Admin UI):
    /// - Create/Update/Delete users (users created via OAuth flows)
    /// - Manage Keycloak roles (SYSTEMADMIN, TENANTADMIN, etc.)
    /// - Assign roles to users
    /// - Send password reset emails
    /// - Enable/disable users
    /// 
    /// Note: Application-level users, roles, and permissions are managed via:
    /// - IUserRepository (app database users)
    /// - IRoleRepository (app custom roles)
    /// - IPermissionRepository (app permissions)
    /// </summary>
    public interface IIdentityProviderAdminService
    {
        #region User Queries (Read-Only)

        /// <summary>
        /// Get user details from Keycloak by user ID
        /// Used for syncing users to local database after authentication
        /// Read-only - we never create/update/delete users in Keycloak from our app
        /// </summary>
        /// <param name="userId">The user's Keycloak ID (from JWT 'sub' claim)</param>
        /// <param name="realm">Optional realm name. If null, uses default from configuration.</param>
        /// <returns>User details or null if not found</returns>
        Task<UserDetailsDto?> GetUserByIdAsync(string userId, string? realm = null);

        #endregion

        #region Realm Management (For Enterprise Tenants)

        /// <summary>
        /// Creates a new Keycloak realm for an enterprise tenant
        /// Called by TenantRepository when creating enterprise tenant
        /// </summary>
        /// <param name="dto">Realm creation details (name, display name, enabled status)</param>
        /// <returns>ApiResponse with realm name if successful, error details if failed</returns>
        Task<ApiResponse<string>> CreateRealmAsync(CreateRealmDto dto);

        /// <summary>
        /// Creates a new Keycloak realm for an enterprise tenant with custom frontend URL
        /// Automatically creates the groundup-api client with configured redirect URIs
        /// </summary>
        /// <param name="dto">Realm creation details</param>
        /// <param name="frontendUrl">Frontend URL for this tenant (e.g., "acme.yourapp.com")</param>
        /// <returns>ApiResponse with realm name if successful</returns>
        Task<ApiResponse<string>> CreateRealmWithClientAsync(CreateRealmDto dto, string frontendUrl);

        /// <summary>
        /// Deletes a Keycloak realm
        /// Called by TenantRepository when deleting enterprise tenant
        /// WARNING: This is a destructive operation that cannot be undone
        /// </summary>
        /// <param name="realmName">The realm identifier to delete</param>
        /// <returns>True if realm was deleted successfully, false if not found</returns>
        Task<bool> DeleteRealmAsync(string realmName);

        /// <summary>
        /// Gets details about a specific Keycloak realm
        /// Used for verification/diagnostics
        /// </summary>
        /// <param name="realmName">The realm identifier</param>
        /// <returns>Realm details or null if not found</returns>
        Task<RealmDto?> GetRealmAsync(string realmName);

        #endregion

        #region Client Management (For Enterprise Realms)

        /// <summary>
        /// Creates an OAuth2 client in a Keycloak realm
        /// Called automatically by CreateRealmAsync for enterprise tenants
        /// Configures redirect URIs for both frontend and backend
        /// </summary>
        /// <param name="realmName">The realm to create the client in</param>
        /// <param name="dto">Client configuration (client ID, redirect URIs, etc.)</param>
        /// <returns>ApiResponse indicating success or failure</returns>
        Task<ApiResponse<bool>> CreateClientInRealmAsync(string realmName, CreateClientDto dto);

        #endregion
    }
}