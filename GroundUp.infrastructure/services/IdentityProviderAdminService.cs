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

        #region Role Management

        public async Task<List<SystemRoleDto>> GetAllRolesAsync()
        {
            await EnsureAdminTokenAsync();

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/roles";

            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();

            // Get the raw roles from Keycloak
            var keycloakRoles = await response.Content.ReadFromJsonAsync<List<dynamic>>() ?? new List<dynamic>();

            // Convert to our SystemRoleDto
            var systemRoles = new List<SystemRoleDto>();
            foreach (var role in keycloakRoles)
            {
                systemRoles.Add(new SystemRoleDto
                {
                    Id = role.id?.ToString() ?? string.Empty,
                    Name = role.name?.ToString() ?? string.Empty,
                    Description = role.description?.ToString(),
                    IsClientRole = role.clientRole != null ? (bool)role.clientRole : false,
                    ContainerId = role.containerId?.ToString(),
                    Composite = role.composite != null ? (bool)role.composite : false
                });
            }

            return systemRoles;
        }

        public async Task<SystemRoleDto?> GetRoleByNameAsync(string name)
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

            // Parse the response
            var responseContent = await response.Content.ReadFromJsonAsync<JsonElement>();

            // Convert to our SystemRoleDto
            var systemRole = new SystemRoleDto
            {
                Id = responseContent.GetProperty("id").GetString() ?? string.Empty,
                Name = responseContent.GetProperty("name").GetString() ?? string.Empty,
                Description = responseContent.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                IsClientRole = responseContent.TryGetProperty("clientRole", out var clientRole) && clientRole.GetBoolean(),
                ContainerId = responseContent.TryGetProperty("containerId", out var containerId) ? containerId.GetString() : null,
                Composite = responseContent.TryGetProperty("composite", out var composite) && composite.GetBoolean()
            };

            return systemRole;
        }

        public async Task<SystemRoleDto> CreateRoleAsync(CreateSystemRoleDto roleDto)
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

        public async Task<SystemRoleDto?> UpdateRoleAsync(string name, UpdateRoleDto roleDto)
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

        public async Task<List<SystemRoleDto>> GetUserRolesAsync(string userId)
        {
            await EnsureAdminTokenAsync();

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/users/{userId}/role-mappings/realm";

            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();

            // Get the raw roles from Keycloak
            var keycloakRoles = await response.Content.ReadFromJsonAsync<List<dynamic>>() ?? new List<dynamic>();

            // Convert to our SystemRoleDto
            var systemRoles = new List<SystemRoleDto>();
            foreach (var role in keycloakRoles)
            {
                systemRoles.Add(new SystemRoleDto
                {
                    Id = role.id?.ToString() ?? string.Empty,
                    Name = role.name?.ToString() ?? string.Empty,
                    Description = role.description?.ToString(),
                    IsClientRole = role.clientRole != null ? (bool)role.clientRole : false,
                    ContainerId = role.containerId?.ToString(),
                    Composite = role.composite != null ? (bool)role.composite : false
                });
            }

            return systemRoles;
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

            // Create payload with Keycloak's expected format
            var payload = new List<object> {
                new {
                    id = role.Id,
                    name = role.Name
                }
            };

            var response = await _httpClient.PostAsJsonAsync(requestUrl, payload);
            response.EnsureSuccessStatusCode();

            return true;
        }

        public async Task<bool> AssignRolesToUserAsync(string userId, List<string> roleNames)
        {
            await EnsureAdminTokenAsync();

            // Get all the roles first
            var rolesToAssign = new List<object>();

            foreach (var roleName in roleNames)
            {
                var role = await GetRoleByNameAsync(roleName);
                if (role != null)
                {
                    rolesToAssign.Add(new
                    {
                        id = role.Id,
                        name = role.Name
                    });
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

            // Create payload with Keycloak's expected format
            var payload = new List<object> {
                new {
                    id = role.Id,
                    name = role.Name
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
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

        public async Task<UserDetailsDto> CreateUserAsync(CreateUserDto userDto)
        {
            await EnsureAdminTokenAsync();

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/users";

            // Generate a strong temporary password (user will be required to change it)
            var temporaryPassword = GenerateSecurePassword();

            // Build Keycloak user payload
            var payload = new
            {
                username = userDto.Username,
                email = userDto.Email,
                firstName = userDto.FirstName,
                lastName = userDto.LastName,
                enabled = userDto.Enabled,
                emailVerified = userDto.EmailVerified,
                credentials = new[]
                {
                    new
                    {
                        type = "password",
                        value = temporaryPassword,
                        temporary = true // Force user to change password on first login
                    }
                },
                requiredActions = new[] { "UPDATE_PASSWORD" }, // Require password update
                attributes = userDto.Attributes
            };

            var response = await _httpClient.PostAsJsonAsync(requestUrl, payload);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to create user: {errorContent}");
                throw new Exception($"Failed to create user in Keycloak: {errorContent}");
            }

            // Keycloak returns 201 Created with Location header containing the user ID
            var locationHeader = response.Headers.Location?.ToString();
            if (string.IsNullOrEmpty(locationHeader))
            {
                throw new Exception("Failed to get user ID from Keycloak response");
            }

            // Extract user ID from location header (e.g., .../users/12345-67890-...)
            var userId = locationHeader.Split('/').Last();

            // Send password setup email if requested
            if (userDto.SendWelcomeEmail)
            {
                try
                {
                    await SendPasswordResetEmailAsync(userId);
                    _logger.LogInformation($"Sent password setup email to user '{userDto.Username}'");
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning($"Failed to send password setup email to user '{userDto.Username}': {emailEx.Message}");
                    // Don't fail user creation if email fails
                }
            }

            // Fetch the created user
            var createdUser = await GetUserByIdAsync(userId);
            if (createdUser == null)
            {
                throw new Exception($"User was created but could not be retrieved");
            }

            _logger.LogInformation($"Successfully created user '{userDto.Username}' with ID {userId}");
            return createdUser;
        }

        public async Task<UserDetailsDto?> UpdateUserAsync(string userId, UpdateUserDto userDto)
        {
            await EnsureAdminTokenAsync();

            // First, get the existing user
            var existingUser = await GetUserByIdAsync(userId);
            if (existingUser == null)
            {
                _logger.LogWarning($"User with ID '{userId}' not found");
                return null;
            }

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/users/{userId}";

            // Build update payload (only include fields that are provided)
            var payload = new
            {
                email = userDto.Email ?? existingUser.Email,
                firstName = userDto.FirstName ?? existingUser.FirstName,
                lastName = userDto.LastName ?? existingUser.LastName,
                enabled = userDto.Enabled ?? existingUser.Enabled,
                emailVerified = userDto.EmailVerified ?? existingUser.EmailVerified,
                attributes = userDto.Attributes ?? existingUser.Attributes
            };

            var response = await _httpClient.PutAsJsonAsync(requestUrl, payload);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to update user {userId}: {errorContent}");
                throw new Exception($"Failed to update user in Keycloak: {errorContent}");
            }

            // Fetch the updated user
            var updatedUser = await GetUserByIdAsync(userId);
            _logger.LogInformation($"Successfully updated user with ID {userId}");
            return updatedUser;
        }

        public async Task<bool> DeleteUserAsync(string userId)
        {
            await EnsureAdminTokenAsync();

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/users/{userId}";

            var response = await _httpClient.DeleteAsync(requestUrl);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"User with ID '{userId}' not found");
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to delete user {userId}: {errorContent}");
                throw new Exception($"Failed to delete user from Keycloak: {errorContent}");
            }

            _logger.LogInformation($"Successfully deleted user with ID {userId}");
            return true;
        }

        public async Task<bool> SetUserEnabledAsync(string userId, bool enabled)
        {
            await EnsureAdminTokenAsync();

            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/users/{userId}";

            var payload = new { enabled };

            var response = await _httpClient.PutAsJsonAsync(requestUrl, payload);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to set user {userId} enabled status: {errorContent}");
                return false;
            }

            _logger.LogInformation($"Successfully set user {userId} enabled status to {enabled}");
            return true;
        }

        public async Task<bool> SendPasswordResetEmailAsync(string userId)
        {
            await EnsureAdminTokenAsync();

            // Trigger Keycloak to send password reset email
            var requestUrl = $"{_keycloakConfig.AuthServerUrl}/admin/realms/{_keycloakConfig.Realm}/users/{userId}/execute-actions-email";

            var payload = new[] { "UPDATE_PASSWORD" };

            var response = await _httpClient.PutAsJsonAsync(requestUrl, payload);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to send password reset email to user {userId}: {errorContent}");
                return false;
            }

            _logger.LogInformation($"Successfully sent password reset email to user {userId}");
            return true;
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

        #region Helper Methods

        /// <summary>
        /// Generates a cryptographically secure random password
        /// </summary>
        private string GenerateSecurePassword()
        {
            const string upperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowerCase = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string special = "!@#$%^&*()-_=+[]{}|;:,.<>?";
            const int passwordLength = 16;

            var random = new Random();
            var password = new char[passwordLength];

            // Ensure at least one character from each category
            password[0] = upperCase[random.Next(upperCase.Length)];
            password[1] = lowerCase[random.Next(lowerCase.Length)];
            password[2] = digits[random.Next(digits.Length)];
            password[3] = special[random.Next(special.Length)];

            // Fill the rest randomly
            var allChars = upperCase + lowerCase + digits + special;
            for (int i = 4; i < passwordLength; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }

            // Shuffle the password to avoid predictable patterns
            for (int i = passwordLength - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }

            return new string(password);
        }

        #endregion
    }
}