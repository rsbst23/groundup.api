using GroundUp.Core.configuration;
using GroundUp.Core.dtos;
using GroundUp.Core.interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GroundUp.Services.Core.Services;

public sealed class IdentityProviderService : IIdentityProviderService
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
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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

    public async Task<TokenResponseDto?> ExchangeCodeForTokensAsync(string code, string redirectUri, string? realm = null)
    {
        try
        {
            var keycloakRealm = realm ?? _keycloakConfig.Realm;

            var tokenEndpoint = $"{_keycloakConfig.AuthServerUrl}/realms/{keycloakRealm}/protocol/openid-connect/token";
            _logger.LogInformation($"Exchanging code for tokens (realm: {keycloakRealm})");

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("client_id", _keycloakConfig.Resource),
                new KeyValuePair<string, string>("client_secret", _keycloakConfig.Secret),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri)
            });

            var response = await _httpClient.PostAsync(tokenEndpoint, formContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Token exchange failed (realm: {keycloakRealm}) with status {response.StatusCode}: {errorContent}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);

            if (tokenResponse == null)
            {
                _logger.LogError("Failed to parse token response from Keycloak");
                return null;
            }

            var result = new TokenResponseDto
            {
                AccessToken = tokenResponse.TryGetValue("access_token", out var at) ? at.GetString() ?? string.Empty : string.Empty,
                RefreshToken = tokenResponse.TryGetValue("refresh_token", out var rt) ? rt.GetString() ?? string.Empty : string.Empty,
                ExpiresIn = tokenResponse.TryGetValue("expires_in", out var ei) ? ei.GetInt32() : 0,
                RefreshExpiresIn = tokenResponse.TryGetValue("refresh_expires_in", out var rei) ? rei.GetInt32() : 0,
                TokenType = tokenResponse.TryGetValue("token_type", out var tt) ? tt.GetString() ?? "Bearer" : "Bearer",
                IdToken = tokenResponse.TryGetValue("id_token", out var idt) ? idt.GetString() ?? string.Empty : string.Empty
            };

            _logger.LogInformation($"Successfully exchanged authorization code for tokens (realm: {keycloakRealm})");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exchanging code for tokens: {ex.Message}", ex);
            return null;
        }
    }
}
