using GroundUp.core.dtos;
using GroundUp.core.enums;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace GroundUp.infrastructure.services
{
    internal class AuthUrlBuilderService : IAuthUrlBuilderService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILoggingService _logger;

        public AuthUrlBuilderService(ApplicationDbContext dbContext, IConfiguration configuration, ILoggingService logger)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> BuildLoginUrlAsync(string? domain, string redirectUri, string? returnUrl = null)
        {
            var (realm, isEnterprise) = await ResolveRealmFromDomainAsync(domain);

            // login always goes through default flow
            var state = new AuthCallbackState
            {
                Flow = "default",
                Realm = realm
            };

            return BuildAuthOrRegistrationUrl(
                realm,
                isRegistration: false,
                redirectUri,
                state,
                loginHint: null);
        }

        public Task<string> BuildRegistrationUrlAsync(string redirectUri, string? returnUrl = null)
        {
            // Standard tenant registration - always uses shared realm
            var realm = "groundup";

            var state = new AuthCallbackState
            {
                Flow = "new_org",
                Realm = realm
            };

            return Task.FromResult(BuildAuthOrRegistrationUrl(
                realm,
                isRegistration: true,
                redirectUri,
                state,
                loginHint: null));
        }

        public Task<string> BuildInvitationLoginUrlAsync(string realm, string invitationToken, string invitationEmail, string redirectUri)
        {
            var state = new AuthCallbackState
            {
                Flow = "invitation",
                InvitationToken = invitationToken,
                Realm = realm
            };

            return Task.FromResult(BuildAuthOrRegistrationUrl(
                realm,
                isRegistration: false,
                redirectUri,
                state,
                loginHint: invitationEmail));
        }

        public Task<string> BuildEnterpriseFirstAdminRegistrationUrlAsync(string realm, string redirectUri)
        {
            var state = new AuthCallbackState
            {
                Flow = "enterprise_first_admin",
                Realm = realm
            };

            return Task.FromResult(BuildAuthOrRegistrationUrl(
                realm,
                isRegistration: true,
                redirectUri,
                state,
                loginHint: null));
        }

        private async Task<(string realm, bool isEnterprise)> ResolveRealmFromDomainAsync(string? domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                _logger.LogInformation("No domain provided - using shared realm for standard tenant");
                return ("groundup", false);
            }

            _logger.LogInformation($"Looking up tenant by domain: {domain}");

            var tenant = await _dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.CustomDomain == domain && t.IsActive);

            if (tenant == null)
            {
                _logger.LogWarning($"No active tenant found for domain: {domain}");
                return ($"ERROR:No tenant found for domain: {domain}", false);
            }

            var isEnterprise = tenant.TenantType == TenantType.Enterprise;
            _logger.LogInformation($"Domain {domain} maps to tenant '{tenant.Name}' using realm: {tenant.RealmName}");

            return (tenant.RealmName, isEnterprise);
        }

        private string BuildAuthOrRegistrationUrl(
            string realm,
            bool isRegistration,
            string redirectUri,
            AuthCallbackState state,
            string? loginHint)
        {
            var keycloakAuthUrl = Environment.GetEnvironmentVariable("KEYCLOAK_AUTH_SERVER_URL")
                ?? _configuration["Keycloak:AuthServerUrl"]
                ?? _configuration["KEYCLOAK_AUTH_SERVER_URL"];

            var clientId = Environment.GetEnvironmentVariable("KEYCLOAK_RESOURCE")
                ?? _configuration["Keycloak:Resource"]
                ?? _configuration["KEYCLOAK_RESOURCE"];

            if (string.IsNullOrEmpty(keycloakAuthUrl) || string.IsNullOrEmpty(clientId))
            {
                _logger.LogError("Keycloak configuration is missing");
                return "ERROR:Authentication service is not properly configured";
            }

            var stateJson = JsonSerializer.Serialize(state);
            var stateEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateJson));

            var baseUrl = isRegistration
                ? $"{keycloakAuthUrl}/realms/{realm}/protocol/openid-connect/registrations"
                : $"{keycloakAuthUrl}/realms/{realm}/protocol/openid-connect/auth";

            var url = $"{baseUrl}" +
                      $"?client_id={Uri.EscapeDataString(clientId)}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&response_type=code" +
                      $"&scope=openid%20email%20profile" +
                      $"&state={Uri.EscapeDataString(stateEncoded)}";

            if (!string.IsNullOrWhiteSpace(loginHint))
            {
                url += $"&login_hint={Uri.EscapeDataString(loginHint)}";
            }

            return url;
        }
    }
}
