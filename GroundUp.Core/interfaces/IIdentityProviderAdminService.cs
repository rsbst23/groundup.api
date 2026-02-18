using GroundUp.Core.dtos;

namespace GroundUp.Core.interfaces
{
    /// <summary>
    /// Service for interacting with Keycloak Identity Provider Admin API
    /// 
    /// SCOPE: Read-only user queries + Realm management for enterprise tenants
    /// 
    /// This service provides minimal interaction with Keycloak Admin API:
    /// - Get user details (for syncing to local DB)
    /// - Create/Delete/Get realms (for enterprise multi-realm support)
    /// - Create clients in realms
    /// - Disable realm registration
    /// </summary>
    public interface IIdentityProviderAdminService
    {
        #region User Queries (Read-Only)

        /// <summary>
        /// Get user details from Keycloak by user ID
        /// Used for syncing users to local database after authentication
        /// </summary>
        /// <param name="userId">The Keycloak user ID (sub claim)</param>
        /// <param name="realm">Optional realm name (defaults to configured realm if not provided)</param>
        /// <returns>User details or null if not found</returns>
        Task<UserDetailsDto?> GetUserByIdAsync(string userId, string? realm = null);

        /// <summary>
        /// Get Keycloak user ID by email address
        /// Used to check if user already exists before creating invitation
        /// </summary>
        /// <param name="realm">The realm to search in</param>
        /// <param name="email">Email address to search for</param>
        /// <returns>User ID if found, null otherwise</returns>
        Task<string?> GetUserIdByEmailAsync(string realm, string email);

        #endregion

        #region User Management

        /// <summary>
        /// Creates a new user in Keycloak with required actions
        /// Used when inviting users who don't have Keycloak accounts yet
        /// </summary>
        /// <param name="realm">The realm to create user in</param>
        /// <param name="dto">User creation details</param>
        /// <returns>Created user ID (sub claim) or null if failed</returns>
        Task<string?> CreateUserAsync(string realm, CreateUserDto dto);

        /// <summary>
        /// Sends Keycloak execute actions email (password setup, email verification, etc.)
        /// IMPORTANT: Both client_id and redirect_uri should be provided together for proper redirects
        /// Keycloak requires both parameters to associate the action with your client and enable "back to app" functionality
        /// </summary>
        /// <param name="realm">The realm</param>
        /// <param name="userId">The Keycloak user ID</param>
        /// <param name="actions">Required actions (e.g., ["UPDATE_PASSWORD", "VERIFY_EMAIL"])</param>
        /// <param name="clientId">Your OAuth client ID (e.g., "groundup-api")</param>
        /// <param name="redirectUri">URI to redirect to after completing actions (must be valid for the client)</param>
        /// <returns>True if email sent successfully</returns>
        Task<bool> SendExecuteActionsEmailAsync(
            string realm, 
            string userId, 
            List<string> actions, 
            string? clientId = null,
            string? redirectUri = null);

        #endregion

        #region Realm Management

        /// <summary>
        /// Creates a new Keycloak realm for an enterprise tenant
        /// Uses master realm admin credentials to create the realm
        /// </summary>
        /// <param name="dto">Realm configuration</param>
        /// <returns>API response with realm name if successful</returns>
        Task<ApiResponse<string>> CreateRealmAsync(CreateRealmDto dto);

        /// <summary>
        /// Deletes a Keycloak realm
        /// WARNING: This is a destructive operation that cannot be undone
        /// </summary>
        /// <param name="realmName">The realm to delete</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteRealmAsync(string realmName);

        /// <summary>
        /// Gets details about a specific Keycloak realm
        /// </summary>
        /// <param name="realmName">The realm name</param>
        /// <returns>Realm details or null if not found</returns>
        Task<RealmDto?> GetRealmAsync(string realmName);

        /// <summary>
        /// Disables user registration for a realm
        /// Called after first enterprise tenant user completes registration
        /// </summary>
        /// <param name="realmName">The realm identifier</param>
        /// <returns>True if registration was disabled successfully</returns>
        Task<bool> DisableRealmRegistrationAsync(string realmName);

        #endregion

        #region Client Management

        /// <summary>
        /// Creates an OAuth2 client in a Keycloak realm
        /// </summary>
        /// <param name="realmName">The realm to create the client in</param>
        /// <param name="dto">Client configuration</param>
        /// <returns>API response indicating success or failure</returns>
        Task<ApiResponse<bool>> CreateClientInRealmAsync(string realmName, CreateClientDto dto);

        /// <summary>
        /// Creates a new Keycloak realm for an enterprise tenant with custom frontend URL
        /// Automatically creates the groundup-api client with configured redirect URIs
        /// </summary>
        /// <param name="dto">Realm configuration</param>
        /// <param name="frontendUrl">The tenant's custom frontend URL</param>
        /// <returns>API response with realm name if successful</returns>
        Task<ApiResponse<string>> CreateRealmWithClientAsync(CreateRealmDto dto, string frontendUrl);

        #endregion
    }
}
