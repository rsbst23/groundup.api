using GroundUp.core.configuration;
using GroundUp.core.interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace GroundUp.infrastructure.services
{
    public class IdentityProviderService : IIdentityProviderService
    {
        private readonly HttpClient _httpClient;
        private readonly KeycloakConfiguration _keycloakConfig;
        private readonly ILoggingService _logger;

        public IdentityProviderService(
            HttpClient httpClient,
            IOptions<KeycloakConfiguration> keycloakConfig,
            ILoggingService logger)
        {
            _httpClient = httpClient;
            _keycloakConfig = keycloakConfig.Value;
            _logger = logger;
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var url = $"{_keycloakConfig.AuthServerUrl}/realms/{_keycloakConfig.Realm}/protocol/openid-connect/userinfo";
                _logger.LogInformation($"Validating token against Keycloak: {url}");

                var response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error validating token: {ex.Message}", ex);
                return false;
            }
        }
    }
}