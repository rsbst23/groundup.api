using GroundUp.core.configuration;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace GroundUp.infrastructure.services
{
    public class KeycloakAdminService : IKeycloakAdminService
    {
        private readonly HttpClient _httpClient;
        private readonly KeycloakConfiguration _keycloakConfig;
        private readonly ILoggingService _logger;
        private string? _accessToken;
        private DateTime _tokenExpiresAt = DateTime.MinValue;

        public KeycloakAdminService(HttpClient httpClient, IOptions<KeycloakConfiguration> keycloakConfig, ILoggingService logger)
        {
            _httpClient = httpClient;
            _keycloakConfig = keycloakConfig.Value;
            _logger = logger;
        }

        #region Role Management

        public async Task<List<RoleDto>> GetAllRolesAsync()
        {
            await EnsureAdminTokenAsync();

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/roles";

            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();

            var roles = await response.Content.ReadFromJsonAsync<List<RoleDto>>() ?? new List<RoleDto>();
            return roles;
        }

        public async Task<RoleDto?> GetRoleByNameAsync(string name)
        {
            await EnsureAdminTokenAsync();

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/roles/{name}";

            var response = await _httpClient.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }
                response.EnsureSuccessStatusCode(); // Throw for other errors
            }

            var role = await response.Content.ReadFromJsonAsync<RoleDto>();
            return role;
        }

        public async Task<RoleDto> CreateRoleAsync(CreateRoleDto roleDto)
        {
            await EnsureAdminTokenAsync();

            string requestUrl;
            var payload = new
            {
                name = roleDto.Name,
                description = roleDto.Description
            };

            if (roleDto.IsClientRole && !string.IsNullOrEmpty(roleDto.ClientId))
            {
                requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/clients/{roleDto.ClientId}/roles";
            }
            else
            {
                requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/roles";
            }

            var response = await _httpClient.PostAsJsonAsync(requestUrl, payload);
            response.EnsureSuccessStatusCode();

            // Keycloak doesn't return the created role, so we need to fetch it
            var createdRole = await GetRoleByNameAsync(roleDto.Name);
            if (createdRole == null)
            {
                throw new Exception($"Role '{roleDto.Name}' was created but could not be retrieved");
            }

            return createdRole;
        }

        public async Task<RoleDto?> UpdateRoleAsync(string name, UpdateRoleDto roleDto)
        {
            await EnsureAdminTokenAsync();

            // First, get the existing role
            var existingRole = await GetRoleByNameAsync(name);
            if (existingRole == null)
            {
                return null;
            }

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/roles/{name}";

            var payload = new
            {
                name = name,
                description = roleDto.Description ?? existingRole.Description
            };

            var response = await _httpClient.PutAsJsonAsync(requestUrl, payload);
            response.EnsureSuccessStatusCode();

            // Keycloak doesn't return the updated role, so we need to fetch it
            return await GetRoleByNameAsync(name);
        }

        public async Task<bool> DeleteRoleAsync(string name)
        {
            await EnsureAdminTokenAsync();

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/roles/{name}";

            var response = await _httpClient.DeleteAsync(requestUrl);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();
            return true;
        }

        #endregion

        #region User-Role Management

        public async Task<List<RoleDto>> GetUserRolesAsync(string userId)
        {
            await EnsureAdminTokenAsync();

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/users/{userId}/role-mappings/realm";

            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();

            var roles = await response.Content.ReadFromJsonAsync<List<RoleDto>>() ?? new List<RoleDto>();
            return roles;
        }

        public async Task<bool> AssignRoleToUserAsync(string userId, string roleName)
        {
            await EnsureAdminTokenAsync();

            // Get the role first
            var role = await GetRoleByNameAsync(roleName);
            if (role == null)
            {
                _logger.LogWarning($"Role '{roleName}' not found");
                return false;
            }

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/users/{userId}/role-mappings/realm";

            var payload = new List<RoleDto> { role };
            var response = await _httpClient.PostAsJsonAsync(requestUrl, payload);
            response.EnsureSuccessStatusCode();

            return true;
        }

        public async Task<bool> AssignRolesToUserAsync(string userId, List<string> roleNames)
        {
            await EnsureAdminTokenAsync();

            // Get all the roles first
            var rolesToAssign = new List<RoleDto>();

            foreach (var roleName in roleNames)
            {
                var role = await GetRoleByNameAsync(roleName);
                if (role != null)
                {
                    rolesToAssign.Add(role);
                }
                else
                {
                    _logger.LogWarning($"Role '{roleName}' not found");
                }
            }

            if (rolesToAssign.Count == 0)
            {
                _logger.LogWarning("No valid roles to assign");
                return false;
            }

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/users/{userId}/role-mappings/realm";

            var response = await _httpClient.PostAsJsonAsync(requestUrl, rolesToAssign);
            response.EnsureSuccessStatusCode();

            return true;
        }

        public async Task<bool> RemoveRoleFromUserAsync(string userId, string roleName)
        {
            await EnsureAdminTokenAsync();

            // Get the role first
            var role = await GetRoleByNameAsync(roleName);
            if (role == null)
            {
                _logger.LogWarning($"Role '{roleName}' not found");
                return false;
            }

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/users/{userId}/role-mappings/realm";

            var content = new StringContent(
                JsonSerializer.Serialize(new List<RoleDto> { role }),
                Encoding.UTF8,
                "application/json"
            );

            var request = new HttpRequestMessage(HttpMethod.Delete, requestUrl)
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return true;
        }

        #endregion

        #region User Management

        public async Task<List<UserSummaryDto>> GetAllUsersAsync()
        {
            await EnsureAdminTokenAsync();

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/users";

            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();

            var users = await response.Content.ReadFromJsonAsync<List<UserSummaryDto>>() ?? new List<UserSummaryDto>();
            return users;
        }

        public async Task<UserDetailsDto?> GetUserByIdAsync(string userId)
        {
            await EnsureAdminTokenAsync();

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/users/{userId}";

            var response = await _httpClient.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }
                response.EnsureSuccessStatusCode(); // Throw for other errors
            }

            var user = await response.Content.ReadFromJsonAsync<UserDetailsDto>();
            return user;
        }

        public async Task<UserDetailsDto?> GetUserByUsernameAsync(string username)
        {
            await EnsureAdminTokenAsync();

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/users?username={Uri.EscapeDataString(username)}&exact=true";

            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();

            var users = await response.Content.ReadFromJsonAsync<List<UserDetailsDto>>() ?? new List<UserDetailsDto>();
            return users.FirstOrDefault();
        }

        #endregion

        #region Authentication

        private async Task EnsureAdminTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-1))
            {
                // Token is still valid
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                return;
            }

            // Get a new token using password grant
            var tokenEndpoint = $"{_keycloakConfig.AuthServerUrl}/realms/master/protocol/openid-connect/token";
            _logger.LogInformation($"Requesting admin token from: {tokenEndpoint}");

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _keycloakConfig.AdminClientId),
                new KeyValuePair<string, string>("client_secret", _keycloakConfig.AdminClientSecret)
            });

            try
            {
                var response = await _httpClient.PostAsync(tokenEndpoint, formContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Token request failed with status {response.StatusCode}: {errorContent}");
                    throw new Exception($"Failed to obtain admin token: {errorContent}");
                }

                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
                if (tokenResponse != null)
                {
                    _accessToken = tokenResponse.AccessToken;
                    _tokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    _logger.LogInformation("Successfully obtained admin token");
                }
                else
                {
                    throw new Exception("Failed to parse token response from Keycloak");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception during token request: {ex.Message}");
                throw;
            }
        }

        private class TokenResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("refresh_expires_in")]
            public int RefreshExpiresIn { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("token_type")]
            public string TokenType { get; set; } = string.Empty;
        }

        #endregion
    }
}