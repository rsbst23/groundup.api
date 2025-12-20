using GroundUp.core;
using GroundUp.core.configuration;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace GroundUp.infrastructure.services
{
    /// <summary>
    /// Service for interacting with Keycloak Identity Provider
    /// 
    /// SCOPE: Read-only user queries + Realm management for enterprise tenants
    /// 
    /// This service provides minimal interaction with Keycloak Admin API:
    /// - Get user details (for syncing to local DB)
    /// - Create/Delete/Get realms (for enterprise multi-realm support)
    /// 
    /// User management (create/update/delete/roles) is handled by Keycloak Admin UI
    /// </summary>
    public class IdentityProviderAdminService : IIdentityProviderAdminService
    {
        private readonly HttpClient _httpClient;
        private readonly KeycloakConfiguration _keycloakConfig;
        private readonly ILoggingService _logger;
        private string? _accessToken;
        private DateTime _tokenExpiresAt = DateTime.MinValue;

        public IdentityProviderAdminService(HttpClient httpClient, IOptions<KeycloakConfiguration> keycloakConfig, ILoggingService logger)
        {
            _httpClient = httpClient;
            _keycloakConfig = keycloakConfig.Value;
            _logger = logger;
        }

        #region User Queries (Read-Only)

        /// <summary>
        /// Get user details from Keycloak by user ID
        /// Used for syncing users to local database after authentication
        /// </summary>
        public async Task<UserDetailsDto?> GetUserByIdAsync(string userId, string? realm = null)
        {
            await EnsureAdminTokenAsync();

            // Use provided realm or default from configuration
            var targetRealm = realm ?? _keycloakConfig.Realm;
            
            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{targetRealm}/users/{userId}";

            var response = await _httpClient.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"User {userId} not found in realm {targetRealm}");
                    return null;
                }
                response.EnsureSuccessStatusCode(); // Throw for other errors
            }

            var user = await response.Content.ReadFromJsonAsync<UserDetailsDto>();
            _logger.LogInformation($"Retrieved user {userId} from realm {targetRealm}");
            return user;
        }

        /// <summary>
        /// Get Keycloak user ID by email address
        /// Used to check if user already exists before creating invitation
        /// </summary>
        public async Task<string?> GetUserIdByEmailAsync(string realm, string email)
        {
            try
            {
                await EnsureAdminTokenAsync();
                
                var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{realm}/users?email={Uri.EscapeDataString(email)}&exact=true";

                _logger.LogInformation($"Searching for user by email in realm {realm}: {email}");
                
                var response = await _httpClient.GetAsync(requestUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to search for user by email: {response.StatusCode}");
                    return null;
                }
                
                var users = await response.Content.ReadFromJsonAsync<List<UserDetailsDto>>();
                var user = users?.FirstOrDefault();
                
                if (user != null)
                {
                    _logger.LogInformation($"Found existing user with email {email}: {user.Id}");
                    return user.Id;
                }
                
                _logger.LogInformation($"No user found with email {email} in realm {realm}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error searching for user by email: {ex.Message}", ex);
                return null;
            }
        }

        #endregion

        #region User Management

        /// <summary>
        /// Creates a new user in Keycloak with required actions
        /// Used when inviting users who don't have Keycloak accounts yet
        /// </summary>
        public async Task<string?> CreateUserAsync(string realm, CreateUserDto dto)
        {
            try
            {
                await EnsureAdminTokenAsync();
                
                _logger.LogInformation($"Creating user in realm {realm}: {dto.Email}");
                
                var userPayload = new
                {
                    username = dto.Username,
                    email = dto.Email,
                    firstName = dto.FirstName,
                    lastName = dto.LastName,
                    enabled = dto.Enabled,
                    emailVerified = dto.EmailVerified,
                    attributes = dto.Attributes ?? new Dictionary<string, List<string>>()
                    // NOTE: Do NOT set requiredActions here - we'll send execute-actions email explicitly
                    // Setting requiredActions during user creation does NOT trigger email automatically
                };
                
                var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{realm}/users";
                var response = await _httpClient.PostAsJsonAsync(requestUrl, userPayload);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to create user in Keycloak: {error}");
                    return null;
                }
                
                // Get user ID from Location header
                var location = response.Headers.Location?.ToString();
                if (string.IsNullOrEmpty(location))
                {
                    _logger.LogError("No Location header returned after user creation");
                    return null;
                }
                
                var userId = location.Split('/').Last();
                _logger.LogInformation($"Successfully created user {userId} in realm {realm}");
                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating user in realm {realm}: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Sends Keycloak execute actions email (password setup, email verification, etc.)
        /// IMPORTANT: Both client_id and redirect_uri must be provided together for proper redirects
        /// </summary>
        public async Task<bool> SendExecuteActionsEmailAsync(
            string realm,
            string userId,
            List<string> actions,
            string? clientId = null,
            string? redirectUri = null)
        {
            try
            {
                await EnsureAdminTokenAsync();
                
                _logger.LogInformation($"Sending execute actions email to user {userId} in realm {realm}");
                _logger.LogInformation($"Actions: {string.Join(", ", actions)}");
                
                // Build query parameters - BOTH client_id and redirect_uri must be provided together
                var queryParams = new List<string>();
                
                if (!string.IsNullOrEmpty(clientId))
                {
                    queryParams.Add($"client_id={Uri.EscapeDataString(clientId)}");
                    _logger.LogInformation($"Client ID: {clientId}");
                }
                
                if (!string.IsNullOrEmpty(redirectUri))
                {
                    queryParams.Add($"redirect_uri={Uri.EscapeDataString(redirectUri)}");
                    _logger.LogInformation($"Redirect URI: {redirectUri}");
                }
                
                var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
                
                var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{realm}/users/{userId}/execute-actions-email{queryString}";
                
                _logger.LogInformation($"Execute actions email URL: {requestUrl}");
                _logger.LogInformation($"Actions payload: {string.Join(", ", actions)}");
                
                var response = await _httpClient.PutAsJsonAsync(requestUrl, actions);
                
                _logger.LogInformation($"Execute actions email response status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to send execute actions email. Status: {response.StatusCode}, Error: {error}");
                    _logger.LogError($"This usually means: 1) SMTP not configured in realm '{realm}', 2) User email not set, or 3) Invalid client_id/redirect_uri");
                    return false;
                }
                
                _logger.LogInformation($"Successfully sent execute actions email to user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending execute actions email: {ex.Message}", ex);
                return false;
            }
        }

        #endregion

        #region Realm Management

        /// <summary>
        /// Creates a new Keycloak realm for an enterprise tenant
        /// Uses master realm admin credentials to create the realm
        /// </summary>
        public async Task<ApiResponse<string>> CreateRealmAsync(CreateRealmDto dto)
        {
            try
            {
                await EnsureAdminTokenAsync();

                var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms";

                var payload = new
                {
                    realm = dto.Realm,
                    displayName = dto.DisplayName,
                    enabled = dto.Enabled,
                    registrationAllowed = dto.RegistrationAllowed,
                    registrationEmailAsUsername = dto.RegistrationEmailAsUsername,
                    loginWithEmailAllowed = dto.LoginWithEmailAllowed,
                    duplicateEmailsAllowed = false,
                    resetPasswordAllowed = dto.ResetPasswordAllowed,
                    editUsernameAllowed = dto.EditUsernameAllowed,
                    rememberMe = dto.RememberMe,
                    verifyEmail = dto.VerifyEmail,
                    bruteForceProtected = true,
                    accessTokenLifespan = 300,
                    ssoSessionIdleTimeout = 1800,
                    ssoSessionMaxLifespan = 36000,
                    revokeRefreshToken = false,
                    refreshTokenMaxReuse = 0
                };

                var response = await _httpClient.PostAsJsonAsync(requestUrl, payload);

                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogWarning($"Realm {dto.Realm} already exists");
                    return new ApiResponse<string>(
                        string.Empty,
                        false,
                        "Realm already exists",
                        new List<string> { $"Realm '{dto.Realm}' already exists in Keycloak" },
                        Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict,
                        ErrorCodes.Conflict
                    );
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to create realm {dto.Realm}: {errorContent}");
                    return new ApiResponse<string>(
                        string.Empty,
                        false,
                        "Failed to create Keycloak realm",
                        new List<string> { errorContent },
                        Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    );
                }

                _logger.LogInformation($"Successfully created Keycloak realm: {dto.Realm}");
                return new ApiResponse<string>(
                    dto.Realm,
                    true,
                    "Keycloak realm created successfully"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating Keycloak realm: {ex.Message}", ex);
                return new ApiResponse<string>(
                    string.Empty,
                    false,
                    "Error creating Keycloak realm",
                    new List<string> { ex.Message },
                    Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        /// <summary>
        /// Deletes a Keycloak realm
        /// WARNING: This is a destructive operation that cannot be undone
        /// </summary>
        public async Task<bool> DeleteRealmAsync(string realmName)
        {
            await EnsureAdminTokenAsync();

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{realmName}";

            var response = await _httpClient.DeleteAsync(requestUrl);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"Realm {realmName} not found");
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to delete realm {realmName}: {errorContent}");
                return false;
            }

            _logger.LogInformation($"Successfully deleted Keycloak realm: {realmName}");
            return true;
        }

        /// <summary>
        /// Gets details about a specific Keycloak realm
        /// </summary>
        public async Task<RealmDto?> GetRealmAsync(string realmName)
        {
            await EnsureAdminTokenAsync();

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{realmName}";

            var response = await _httpClient.GetAsync(requestUrl);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"Realm {realmName} not found");
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to get realm {realmName}: {errorContent}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var realmData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);

            if (realmData == null)
            {
                _logger.LogError($"Failed to parse realm data for {realmName}");
                return null;
            }

            var realmDto = new RealmDto
            {
                Realm = realmData.TryGetValue("realm", out var r) ? r.GetString() ?? string.Empty : string.Empty,
                DisplayName = realmData.TryGetValue("displayName", out var dn) ? dn.GetString() ?? string.Empty : string.Empty,
                Enabled = realmData.TryGetValue("enabled", out var e) && e.GetBoolean()
            };

            _logger.LogInformation($"Successfully retrieved realm details for: {realmName}");
            return realmDto;
        }

        /// <summary>
        /// Disables user registration for a realm
        /// Called after first enterprise tenant user completes registration
        /// </summary>
        public async Task<bool> DisableRealmRegistrationAsync(string realmName)
        {
            try
            {
                await EnsureAdminTokenAsync();

                var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{realmName}";

                // Update realm to disable registration
                var payload = new
                {
                    registrationAllowed = false
                };

                var response = await _httpClient.PutAsJsonAsync(requestUrl, payload);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"Realm {realmName} not found");
                    return false;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to disable registration for realm {realmName}: {errorContent}");
                    return false;
                }

                _logger.LogInformation($"Successfully disabled registration for realm: {realmName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error disabling registration for realm {realmName}: {ex.Message}", ex);
                return false;
            }
        }

        #endregion

        #region Client Management

        /// <summary>
        /// Creates an OAuth2 client in a Keycloak realm
        /// </summary>
        public async Task<ApiResponse<bool>> CreateClientInRealmAsync(string realmName, CreateClientDto dto)
        {
            try
            {
                await EnsureAdminTokenAsync();

                var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{realmName}/clients";

                var payload = new
                {
                    clientId = dto.ClientId,
                    enabled = true,
                    clientAuthenticatorType = "client-secret",
                    secret = dto.ClientSecret,
                    redirectUris = dto.RedirectUris.ToArray(),
                    webOrigins = dto.WebOrigins.ToArray(),
                    publicClient = !dto.Confidential,
                    protocol = "openid-connect",
                    standardFlowEnabled = dto.StandardFlowEnabled,
                    implicitFlowEnabled = dto.ImplicitFlowEnabled,
                    directAccessGrantsEnabled = dto.DirectAccessGrantsEnabled,
                    serviceAccountsEnabled = dto.Confidential,
                    authorizationServicesEnabled = false,
                    fullScopeAllowed = true,
                    attributes = new
                    {
                        pkce_code_challenge_method = "S256"
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(requestUrl, payload);

                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogWarning($"Client {dto.ClientId} already exists in realm {realmName}");
                    return new ApiResponse<bool>(
                        false,
                        false,
                        "Client already exists",
                        new List<string> { $"Client '{dto.ClientId}' already exists in realm '{realmName}'" },
                        Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict,
                        ErrorCodes.Conflict
                    );
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to create client {dto.ClientId} in realm {realmName}: {errorContent}");
                    return new ApiResponse<bool>(
                        false,
                        false,
                        "Failed to create Keycloak client",
                        new List<string> { errorContent },
                        Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    );
                }

                _logger.LogInformation($"Successfully created Keycloak client: {dto.ClientId} in realm {realmName}");
                return new ApiResponse<bool>(
                    true,
                    true,
                    "Keycloak client created successfully"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating Keycloak client: {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    "Error creating Keycloak client",
                    new List<string> { ex.Message },
                    Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        /// <summary>
        /// Creates a new Keycloak realm for an enterprise tenant with custom frontend URL
        /// Automatically creates the groundup-api client with configured redirect URIs
        /// </summary>
        public async Task<ApiResponse<string>> CreateRealmWithClientAsync(CreateRealmDto dto, string frontendUrl)
        {
            try
            {
                await EnsureAdminTokenAsync();

                var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms";

                // Build SMTP configuration from environment variables
                var smtpServer = BuildSmtpConfiguration();

                // Enable email verification if SMTP is configured and dto requests it
                var enableEmailVerification = smtpServer != null && dto.VerifyEmail;

                _logger.LogInformation($"Creating realm {dto.Realm} with email verification: {enableEmailVerification} (SMTP Configured: {smtpServer != null})");

                var payload = new
                {
                    realm = dto.Realm,
                    displayName = dto.DisplayName,
                    enabled = dto.Enabled,
                    registrationAllowed = dto.RegistrationAllowed,
                    registrationEmailAsUsername = dto.RegistrationEmailAsUsername,
                    loginWithEmailAllowed = dto.LoginWithEmailAllowed,
                    duplicateEmailsAllowed = false,
                    resetPasswordAllowed = dto.ResetPasswordAllowed,
                    editUsernameAllowed = dto.EditUsernameAllowed,
                    rememberMe = dto.RememberMe,
                    verifyEmail = enableEmailVerification,
                    bruteForceProtected = true,
                    accessTokenLifespan = 300,
                    ssoSessionIdleTimeout = 1800,
                    ssoSessionMaxLifespan = 36000,
                    revokeRefreshToken = false,
                    refreshTokenMaxReuse = 0,
                    smtpServer = smtpServer
                };

                var response = await _httpClient.PostAsJsonAsync(requestUrl, payload);

                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogWarning($"Realm {dto.Realm} already exists");
                    return new ApiResponse<string>(
                        string.Empty,
                        false,
                        "Realm already exists",
                        new List<string> { $"Realm '{dto.Realm}' already exists in Keycloak" },
                        Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict,
                        ErrorCodes.Conflict
                    );
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to create realm {dto.Realm}: {errorContent}");
                    return new ApiResponse<string>(
                        string.Empty,
                        false,
                        "Failed to create Keycloak realm",
                        new List<string> { errorContent },
                        Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    );
                }

                _logger.LogInformation($"Successfully created Keycloak realm: {dto.Realm}");

                // Create the groundup-api client with tenant's frontend URL
                var clientDto = BuildClientConfiguration(frontendUrl);
                var clientResult = await CreateClientInRealmAsync(dto.Realm, clientDto);

                if (!clientResult.Success)
                {
                    _logger.LogWarning($"Realm {dto.Realm} created but client creation failed: {clientResult.Message}");
                }
                else
                {
                    _logger.LogInformation($"Successfully created client in realm {dto.Realm} with frontend URL: {frontendUrl}");
                }

                return new ApiResponse<string>(
                    dto.Realm,
                    true,
                    "Keycloak realm and client created successfully"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating Keycloak realm: {ex.Message}", ex);
                return new ApiResponse<string>(
                    string.Empty,
                    false,
                    "Error creating Keycloak realm",
                    new List<string> { ex.Message },
                    Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        #endregion

        #region Authentication

        /// <summary>
        /// Builds the client configuration from environment variables
        /// </summary>
        private CreateClientDto BuildClientConfiguration(string? tenantFrontendUrl = null)
        {
            var apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5123";
            var apiCallbackPath = Environment.GetEnvironmentVariable("API_CALLBACK_PATH") ?? "/api/auth/callback";
            var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:5173";
            
            var redirectUris = new List<string>();
            var webOrigins = new List<string>();

            // Add API callback URL (for OAuth login flow)
            redirectUris.Add($"{apiUrl}{apiCallbackPath}");
            webOrigins.Add(apiUrl);
            
            // Add invitation acceptance URL (for execute-actions email redirect)
            redirectUris.Add($"{apiUrl}/api/invitations/invite/*");

            if (!string.IsNullOrEmpty(tenantFrontendUrl))
            {
                var normalizedUrl = NormalizeFrontendUrl(tenantFrontendUrl);
                redirectUris.Add($"{normalizedUrl}/auth/callback");
                redirectUris.Add($"{normalizedUrl}/*");
                webOrigins.Add(normalizedUrl);
            }
            else
            {
                redirectUris.Add($"{frontendUrl}/auth/callback");
                redirectUris.Add($"{frontendUrl}/*");
                webOrigins.Add(frontendUrl);
            }

            return new CreateClientDto
            {
                ClientId = _keycloakConfig.Resource,
                Confidential = true,
                ClientSecret = _keycloakConfig.Secret,
                RedirectUris = redirectUris.Distinct().ToList(),
                WebOrigins = webOrigins.Distinct().ToList(),
                StandardFlowEnabled = true,
                DirectAccessGrantsEnabled = false,
                ImplicitFlowEnabled = false
            };
        }

        private string NormalizeFrontendUrl(string url)
        {
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                return url.TrimEnd('/');
            }
            return $"https://{url}".TrimEnd('/');
        }

        /// <summary>
        /// Builds SMTP configuration from environment variables
        /// </summary>
        private object? BuildSmtpConfiguration()
        {
            try
            {
                var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
                var smtpUsername = Environment.GetEnvironmentVariable("SMTP_USERNAME");
                var smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD");

                if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
                {
                    _logger.LogInformation("SMTP not configured - email verification will be disabled");
                    return null;
                }

                var smtpPortStr = Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587";
                var smtpFrom = Environment.GetEnvironmentVariable("SMTP_FROM") ?? "noreply@groundup.com";
                var smtpFromDisplayName = Environment.GetEnvironmentVariable("SMTP_FROM_DISPLAY_NAME") ?? "GroundUp";
                var smtpReplyTo = Environment.GetEnvironmentVariable("SMTP_REPLY_TO") ?? "";
                var smtpEnvelopeFrom = Environment.GetEnvironmentVariable("SMTP_ENVELOPE_FROM") ?? "";
                var smtpAuthEnabled = Environment.GetEnvironmentVariable("SMTP_AUTH_ENABLED") ?? "true";
                var smtpStartTlsEnabled = Environment.GetEnvironmentVariable("SMTP_STARTTLS_ENABLED") ?? "true";
                var smtpSslEnabled = Environment.GetEnvironmentVariable("SMTP_SSL_ENABLED") ?? "false";

                int.TryParse(smtpPortStr, out var smtpPort);

                var smtpConfig = new Dictionary<string, string>
                {
                    ["host"] = smtpHost,
                    ["port"] = smtpPort.ToString(),
                    ["from"] = smtpFrom,
                    ["fromDisplayName"] = smtpFromDisplayName,
                    ["replyTo"] = smtpReplyTo,
                    ["envelopeFrom"] = smtpEnvelopeFrom,
                    ["auth"] = smtpAuthEnabled,
                    ["starttls"] = smtpStartTlsEnabled,
                    ["ssl"] = smtpSslEnabled,
                    ["user"] = smtpUsername,
                    ["password"] = smtpPassword
                };

                _logger.LogInformation($"SMTP configured - using {smtpHost}:{smtpPort}");
                return smtpConfig;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error building SMTP configuration: {ex.Message}");
                return null;
            }
        }

        private async Task EnsureAdminTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-1))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                return;
            }

            var tokenEndpoint = $"{_keycloakConfig.AuthServerUrl}/realms/master/protocol/openid-connect/token";
            _logger.LogInformation($"Requesting admin token from: {tokenEndpoint}");

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _keycloakConfig.AdminClientId),
                new KeyValuePair<string, string>("client_secret", _keycloakConfig.AdminClientSecret)
            });

            var response = await _httpClient.PostAsync(tokenEndpoint, formContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to retrieve admin token: {errorContent}");
                throw new Exception("Failed to retrieve admin token from Keycloak");
            }

            var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await response.Content.ReadAsStringAsync());
            if (tokenResponse == null || !tokenResponse.TryGetValue("access_token", out var accessTokenElement))
            {
                throw new Exception("Invalid token response from Keycloak");
            }

            _accessToken = accessTokenElement.GetString();
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse["expires_in"].GetInt32());

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            _logger.LogInformation("Successfully obtained admin access token for Keycloak");
        }

        #endregion
    }
}