namespace GroundUp.Core.dtos
{
    /// <summary>
    /// Response from Keycloak token endpoint
    /// </summary>
    public class TokenResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public int RefreshExpiresIn { get; set; }
        public string TokenType { get; set; } = "Bearer";
        public string IdToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// State parameter for auth callback
    /// Determines the flow after successful authentication
    /// </summary>
    public class AuthCallbackState
    {
        public string Flow { get; set; } = "default"; // "invitation", "join_link", "new_org", "default"
        public string? InvitationToken { get; set; }
        public string? JoinToken { get; set; }
        public string? RedirectUrl { get; set; }
        
        /// <summary>
        /// The Keycloak realm used for authentication
        /// Frontend determines this via database lookup before initiating auth
        /// Backend uses this to exchange token with the correct realm
        /// </summary>
        public string? Realm { get; set; }
    }

    /// <summary>
    /// Response from auth callback endpoint
    /// React uses this to determine navigation and display
    /// </summary>
    public class AuthCallbackResponseDto
    {
        public bool Success { get; set; }
        public string Flow { get; set; } = string.Empty; // "invitation", "new_org", "default"
        public string? Token { get; set; }
        public int? TenantId { get; set; }
        public string? TenantName { get; set; }
        public bool RequiresTenantSelection { get; set; }
        public List<TenantSelectionDto>? AvailableTenants { get; set; }
        public bool HasPendingInvitations { get; set; }
        public int? PendingInvitationsCount { get; set; }
        public bool IsNewOrganization { get; set; }
        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Tenant information for selection
    /// </summary>
    public class TenantSelectionDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }

    /// <summary>
    /// Response for login/registration URL endpoints
    /// Returns the Keycloak auth URL to redirect the user to
    /// </summary>
    public class AuthUrlResponseDto
    {
        public string AuthUrl { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // "login" or "register"
    }
}
