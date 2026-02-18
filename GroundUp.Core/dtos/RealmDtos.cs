namespace GroundUp.Core.dtos;

/// <summary>
/// DTO for realm resolution request
/// Frontend sends this to API to determine which realm to use
/// </summary>
public class RealmResolutionRequestDto
{
    /// <summary>
    /// The URL being accessed (e.g., 'acme.myapp.com', 'app.myapp.com')
    /// Will be normalized by the API (lowercased, protocol removed, etc.)
    /// </summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// DTO for realm resolution response
/// API returns this to tell frontend which realm to use
/// </summary>
public class RealmResolutionResponseDto
{
    /// <summary>
    /// The Keycloak realm to use for authentication
    /// </summary>
    public string Realm { get; set; } = string.Empty;
    
    /// <summary>
    /// The tenant ID (for analytics/tracking)
    /// Null if using default realm
    /// </summary>
    public int? TenantId { get; set; }
    
    /// <summary>
    /// The tenant name (for display purposes)
    /// Null if using default realm
    /// </summary>
    public string? TenantName { get; set; }
    
    /// <summary>
    /// Whether this is an enterprise tenant
    /// </summary>
    public bool IsEnterprise { get; set; }
}

/// <summary>
/// DTO for creating Keycloak realm (admin operation)
/// Used when creating enterprise tenants
/// </summary>
public class CreateRealmDto
{
    /// <summary>
    /// The realm identifier (lowercase, no spaces)
    /// </summary>
    public string Realm { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name for the realm
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the realm is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Allow user registration
    /// </summary>
    public bool RegistrationAllowed { get; set; } = true;
    
    /// <summary>
    /// Use email as username
    /// </summary>
    public bool RegistrationEmailAsUsername { get; set; } = false;
    
    /// <summary>
    /// Allow login with email
    /// </summary>
    public bool LoginWithEmailAllowed { get; set; } = true;
    
    /// <summary>
    /// Remember me functionality
    /// </summary>
    public bool RememberMe { get; set; } = true;
    
    /// <summary>
    /// Require email verification
    /// </summary>
    public bool VerifyEmail { get; set; } = true;
    
    /// <summary>
    /// Allow password reset
    /// </summary>
    public bool ResetPasswordAllowed { get; set; } = true;
    
    /// <summary>
    /// Allow username editing
    /// </summary>
    public bool EditUsernameAllowed { get; set; } = false;
}

/// <summary>
/// DTO for realm info (admin operation)
/// Used to retrieve realm details from Keycloak
/// </summary>
public class RealmDto
{
    /// <summary>
    /// The realm identifier
    /// </summary>
    public string Realm { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name for the realm
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the realm is enabled
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// DTO for creating a Keycloak client in a realm
/// Used during enterprise tenant provisioning to configure OAuth2 client
/// </summary>
public class CreateClientDto
{
    /// <summary>
    /// The client ID (e.g., 'groundup-api')
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this is a confidential client (requires client secret)
    /// </summary>
    public bool Confidential { get; set; } = true;
    
    /// <summary>
    /// Client secret (for confidential clients)
    /// </summary>
    public string? ClientSecret { get; set; }
    
    /// <summary>
    /// Valid redirect URIs for OAuth2 flow
    /// </summary>
    public List<string> RedirectUris { get; set; } = new();
    
    /// <summary>
    /// Valid web origins for CORS
    /// </summary>
    public List<string> WebOrigins { get; set; } = new();
    
    /// <summary>
    /// Enable standard OAuth2 flow (authorization code)
    /// </summary>
    public bool StandardFlowEnabled { get; set; } = true;
    
    /// <summary>
    /// Enable direct access grants (resource owner password credentials)
    /// </summary>
    public bool DirectAccessGrantsEnabled { get; set; } = false;
    
    /// <summary>
    /// Enable implicit flow
    /// </summary>
    public bool ImplicitFlowEnabled { get; set; } = false;
}
