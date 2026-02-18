namespace GroundUp.Core.dtos
{
    /// <summary>
    /// Request DTO for enterprise tenant signup
    /// </summary>
    public class EnterpriseSignupRequestDto
    {
        /// <summary>
        /// Company/Organization name
        /// Used for tenant name and realm display name
        /// </summary>
        public string CompanyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Contact email for first admin
        /// REQUIRED - used for invitation
        /// </summary>
        public string ContactEmail { get; set; } = string.Empty;
        
        /// <summary>
        /// Contact name for first admin
        /// </summary>
        public string ContactName { get; set; } = string.Empty;
        
        /// <summary>
        /// Requested subdomain identifier (e.g., "acme")
        /// Used for realm name generation: tenant_acme_xyz
        /// This is what appears in the Keycloak realm name, not what the user sees
        /// </summary>
        public string RequestedSubdomain { get; set; } = string.Empty;
        
        /// <summary>
        /// Domain where this tenant's application will be hosted
        /// Frontend is responsible for constructing this based on:
        /// - Subdomain mode: "acme.yourapp.com" (user types "acme", frontend appends base domain)
        /// - Custom domain mode: "app.acmecorp.com" (user provides full custom domain)
        /// 
        /// Used for:
        /// - Keycloak redirect URIs configuration
        /// - Realm resolution (URL-to-tenant lookup)
        /// - Email invitation links
        /// 
        /// Examples:
        /// - "acme.yourapp.com" (subdomain)
        /// - "app.acmecorp.com" (custom domain)
        /// 
        /// Note: Protocol (https://) should NOT be included - stored without protocol
        /// </summary>
        public string CustomDomain { get; set; } = string.Empty;
        
        /// <summary>
        /// Plan type (default: enterprise-trial)
        /// </summary>
        public string Plan { get; set; } = "enterprise-trial";
    }
    
    /// <summary>
    /// Response DTO for enterprise tenant signup
    /// </summary>
    public class EnterpriseSignupResponseDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string RealmName { get; set; } = string.Empty;
        public string CustomDomain { get; set; } = string.Empty;
        public string InvitationToken { get; set; } = string.Empty;
        public string InvitationUrl { get; set; } = string.Empty;
        public bool EmailSent { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
