using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace GroundUp.Services.Core;

internal sealed class AuthUrlBuilderService : IAuthUrlBuilderService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IConfiguration _configuration;
    private readonly ILoggingService _logger;

    public AuthUrlBuilderService(
        ITenantRepository tenantRepository,
        IConfiguration configuration,
        ILoggingService logger)
    {
        _tenantRepository = tenantRepository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> BuildLoginUrlAsync(string? domain, string redirectUri, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            _logger.LogInformation("No domain provided - using shared realm for standard tenant");
            var defaultState = new AuthCallbackState { Flow = "default", Realm = "groundup" };
            return BuildAuthOrRegistrationUrl("groundup", isRegistration: false, redirectUri, defaultState, loginHint: null);
        }

        // `ITenantRepository` exposes realm resolution by URL.
        // Treat the incoming `domain` as the URL/host value.
        var realmResult = await _tenantRepository.ResolveRealmByUrlAsync(domain);
        if (!realmResult.Success || realmResult.Data == null)
        {
            return $"ERROR:{realmResult.Message}";
        }

        var realm = realmResult.Data.Realm;

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

    public Task<string> BuildJoinLinkLoginUrlAsync(string realm, string joinToken, string redirectUri)
    {
        var state = new AuthCallbackState
        {
            Flow = "join_link",
            JoinToken = joinToken,
            Realm = realm
        };

        return Task.FromResult(BuildAuthOrRegistrationUrl(
            realm,
            isRegistration: false,
            redirectUri,
            state,
            loginHint: null));
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
