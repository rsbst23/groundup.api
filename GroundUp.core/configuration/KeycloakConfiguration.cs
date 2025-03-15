namespace GroundUp.core.configuration
{
    public class KeycloakConfiguration
    {
        public string AuthServerUrl { get; set; } = string.Empty;
        public string Realm { get; set; } = string.Empty;
        public string Resource { get; set; } = string.Empty; // Client ID
        public string Secret { get; set; } = string.Empty; // Client Secret
        public bool VerifyTokenAudience { get; set; } = true;
        public string SslRequired { get; set; } = "external";
        public bool UseResourceRoleMappings { get; set; } = true;
        public int ConfidentialPort { get; set; } = 0;

        // Added properties for admin access
        public string AdminClientId { get; set; } = string.Empty;
        public string AdminClientSecret { get; set; } = string.Empty;
    }
}