using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace GroundUp.api.Controllers
{
    /// <summary>
    /// Controller for tenant join link operations
    /// Handles public join link acceptance (open registration)
    /// </summary>
    [Route("api/join")]
    [ApiController]
    public class JoinLinkController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILoggingService _logger;
        private readonly IConfiguration _configuration;

        public JoinLinkController(
            ApplicationDbContext dbContext,
            ILoggingService logger,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// PUBLIC ENDPOINT: Validate join link and redirect to Keycloak
        /// GET /api/join/{joinToken}
        /// Called when user clicks join link (open registration)
        /// </summary>
        [HttpGet("{joinToken}")]
        [AllowAnonymous]
        public async Task<IActionResult> JoinRedirect(string joinToken)
        {
            try
            {
                _logger.LogInformation($"Processing join link redirect for token: {joinToken}");

                // Validate join link exists and is active
                var joinLink = await _dbContext.TenantJoinLinks
                    .Include(j => j.Tenant)
                    .FirstOrDefaultAsync(j => j.JoinToken == joinToken);

                if (joinLink == null)
                {
                    _logger.LogWarning($"Invalid join link token: {joinToken}");
                    return BadRequest(new ApiResponse<string>(
                        null,
                        false,
                        "Invalid join link",
                        new List<string> { "Join link not found" },
                        StatusCodes.Status400BadRequest,
                        "INVALID_JOIN_LINK"
                    ));
                }

                // Check if join link is revoked
                if (joinLink.IsRevoked)
                {
                    _logger.LogWarning($"Join link {joinToken} has been revoked");
                    return BadRequest(new ApiResponse<string>(
                        null,
                        false,
                        "Join link has been revoked",
                        null,
                        StatusCodes.Status400BadRequest,
                        "JOIN_LINK_REVOKED"
                    ));
                }

                // Check if join link has expired
                if (joinLink.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogWarning($"Join link {joinToken} has expired");
                    return BadRequest(new ApiResponse<string>(
                        null,
                        false,
                        "Join link has expired",
                        null,
                        StatusCodes.Status400BadRequest,
                        "JOIN_LINK_EXPIRED"
                    ));
                }

                // Get tenant realm (join links use tenant's realm)
                var realmName = joinLink.Tenant?.RealmName;
                if (string.IsNullOrEmpty(realmName))
                {
                    // Fallback to shared realm if tenant doesn't have dedicated realm
                    realmName = "groundup";
                    _logger.LogInformation($"Tenant {joinLink.TenantId} has no RealmName, using shared realm: {realmName}");
                }

                // Create OIDC state with join link flow metadata
                var state = new AuthCallbackState
                {
                    Flow = "join_link",
                    JoinToken = joinToken,
                    Realm = realmName
                };

                var stateJson = JsonSerializer.Serialize(state);
                var stateEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateJson));

                // Build Keycloak authorization URL
                var keycloakAuthUrl = _configuration["Keycloak:AuthServerUrl"];
                var clientId = _configuration["Keycloak:ClientId"];
                var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/callback";

                var authUrl = $"{keycloakAuthUrl}/realms/{realmName}/protocol/openid-connect/auth" +
                              $"?client_id={Uri.EscapeDataString(clientId!)}" +
                              $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                              $"&response_type=code" +
                              $"&scope=openid%20email%20profile" +
                              $"&state={Uri.EscapeDataString(stateEncoded)}";

                _logger.LogInformation($"Redirecting to Keycloak realm {realmName} for join link: {joinToken}");
                return Redirect(authUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing join link redirect: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<string>(
                    null,
                    false,
                    "An error occurred processing the join link",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    "INTERNAL_ERROR"
                ));
            }
        }
    }
}
