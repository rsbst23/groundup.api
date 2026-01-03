using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace GroundUp.api.Controllers
{
    /// <summary>
    /// Unified controller for all invitation operations
    /// Handles both tenant-scoped admin operations and cross-tenant user operations
    /// Authorization enforced via repository layer
    /// </summary>
    [Route("api/invitations")]
    [ApiController]
    public class InvitationController : ControllerBase
    {
        private readonly ITenantInvitationRepository _invitationRepo;
        private readonly IUserTenantRepository _userTenantRepo;
        private readonly ILoggingService _logger;
        private readonly IConfiguration _configuration;
        private readonly IAuthUrlBuilderService _authUrlBuilder;

        public InvitationController(
            ITenantInvitationRepository invitationRepo,
            IUserTenantRepository userTenantRepo,
            ILoggingService logger,
            IConfiguration configuration,
            IAuthUrlBuilderService authUrlBuilder)
        {
            _invitationRepo = invitationRepo;
            _userTenantRepo = userTenantRepo;
            _logger = logger;
            _configuration = configuration;
            _authUrlBuilder = authUrlBuilder;
        }

        #region Tenant-Scoped Admin Operations

        /// <summary>
        /// Get all invitations for the current tenant (paginated)
        /// GET /api/invitations
        /// Tenant is automatically determined from ITenantContext
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<TenantInvitationDto>>>> Get(
            [FromQuery] FilterParams filterParams)
        {
            var result = await _invitationRepo.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get invitation by ID (tenant-scoped)
        /// GET /api/invitations/{id}
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<TenantInvitationDto>>> GetById(int id)
        {
            var result = await _invitationRepo.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Create a new invitation
        /// POST /api/invitations
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<TenantInvitationDto>>> Create(
            [FromBody] CreateTenantInvitationDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ApiResponse<TenantInvitationDto>(
                    default!,
                    false,
                    "Invalid invitation data",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            // Get the internal GroundUp user ID from the JWT claims
            var userIdClaim = User.FindFirst("ApplicationUserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogError($"Unable to parse user ID from claims. Claim value: {userIdClaim}");
                return Unauthorized(new ApiResponse<TenantInvitationDto>(
                    default!,
                    false,
                    "User not authenticated",
                    null,
                    StatusCodes.Status401Unauthorized,
                    ErrorCodes.Unauthorized
                ));
            }

            _logger.LogInformation($"Creating invitation with CreatedByUserId: {userId}");

            var result = await _invitationRepo.AddAsync(dto, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Update an invitation
        /// PUT /api/invitations/{id}
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<TenantInvitationDto>>> Update(
            int id,
            [FromBody] UpdateTenantInvitationDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ApiResponse<TenantInvitationDto>(
                    default!,
                    false,
                    "Invalid invitation data",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            if (id != dto.Id)
            {
                return BadRequest(new ApiResponse<TenantInvitationDto>(
                    default!,
                    false,
                    "ID mismatch",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.IdMismatch
                ));
            }

            var result = await _invitationRepo.UpdateAsync(id, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Delete an invitation
        /// DELETE /api/invitations/{id}
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _invitationRepo.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get pending invitations for current tenant
        /// GET /api/invitations/pending
        /// </summary>
        [HttpGet("pending")]
        public async Task<ActionResult<ApiResponse<List<TenantInvitationDto>>>> GetPending()
        {
            var result = await _invitationRepo.GetPendingInvitationsAsync();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Resend an invitation
        /// POST /api/invitations/{id}/resend
        /// </summary>
        [HttpPost("{id:int}/resend")]
        public async Task<ActionResult<ApiResponse<bool>>> Resend(int id, [FromQuery] int expirationDays = 7)
        {
            var result = await _invitationRepo.ResendInvitationAsync(id, expirationDays);
            return StatusCode(result.StatusCode, result);
        }

        #endregion

        #region Cross-Tenant User Operations

        /// <summary>
        /// Get invitations for the current user's email across all tenants
        /// GET /api/invitations/me
        /// </summary>
        [HttpGet("me")]
        public async Task<ActionResult<ApiResponse<List<TenantInvitationDto>>>> GetMyInvitations()
        {
            var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(emailClaim))
            {
                return BadRequest(new ApiResponse<List<TenantInvitationDto>>(
                    default!,
                    false,
                    "User email not found in token",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            var result = await _invitationRepo.GetInvitationsForEmailAsync(emailClaim);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Accept an invitation to join a tenant
        /// POST /api/invitations/accept
        /// </summary>
        [HttpPost("accept")]
        public async Task<ActionResult<ApiResponse<bool>>> AcceptInvitation([FromBody] AcceptInvitationDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ApiResponse<bool>(
                    false,
                    false,
                    "Invalid invitation data",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            var userIdClaim = User.FindFirst("ApplicationUserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ApiResponse<bool>(
                    false,
                    false,
                    "User not authenticated",
                    null,
                    StatusCodes.Status401Unauthorized,
                    ErrorCodes.Unauthorized
                ));
            }

            var result = await _invitationRepo.AcceptInvitationAsync(dto.InvitationToken, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get invitation details by token (for preview before accepting)
        /// GET /api/invitations/token/{token}
        /// No authentication required - users need to see this before registering
        /// </summary>
        [HttpGet("token/{token}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<TenantInvitationDto>>> GetByToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new ApiResponse<TenantInvitationDto>(
                    default!,
                    false,
                    "Invalid token",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            var result = await _invitationRepo.GetByTokenAsync(token);
            return StatusCode(result.StatusCode, result);
        }

        #endregion

        #region Public Invitation Flow Endpoint

        /// <summary>
        /// PUBLIC ENDPOINT: Validate invitation and return Keycloak registration URL
        /// GET /api/invitations/invite/{invitationToken}
        /// Works for both standard (shared realm) and enterprise (dedicated realm) invitations
        /// Realm is determined from the invitation's RealmName property
        /// 
        /// For NEW users: Returns registration URL
        /// For EXISTING users: Returns login URL
        /// 
        /// Client should redirect user to the returned AuthUrl
        /// </summary>
        [HttpGet("invite/{invitationToken}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<AuthUrlResponseDto>>> InviteRedirect(string invitationToken)
        {
            try
            {
                _logger.LogInformation($"Processing invite redirect for token: {invitationToken}");

                var invitationResult = await _invitationRepo.GetByTokenAsync(invitationToken);
                if (!invitationResult.Success || invitationResult.Data == null)
                {
                    _logger.LogWarning($"Invalid invitation token: {invitationToken}");
                    return BadRequest(new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        "Invalid invitation link",
                        new List<string> { "Invitation not found" },
                        StatusCodes.Status400BadRequest,
                        "INVALID_INVITATION"
                    ));
                }

                var invitation = invitationResult.Data;

                if (invitation.Status != "Pending")
                {
                    _logger.LogWarning($"Invitation {invitationToken} is not pending (Status: {invitation.Status})");
                    return BadRequest(new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        "Invitation is no longer valid",
                        new List<string> { $"Status: {invitation.Status}" },
                        StatusCodes.Status400BadRequest,
                        "INVITATION_NOT_VALID"
                    ));
                }

                if (invitation.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogWarning($"Invitation {invitationToken} has expired");
                    return BadRequest(new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        "Invitation has expired",
                        null,
                        StatusCodes.Status400BadRequest,
                        "INVITATION_EXPIRED"
                    ));
                }

                if (string.IsNullOrEmpty(invitation.RealmName))
                {
                    _logger.LogError($"RealmName missing for invitation {invitationToken}");
                    return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        "Tenant configuration error",
                        new List<string> { "Realm not configured for this tenant" },
                        StatusCodes.Status500InternalServerError,
                        "TENANT_CONFIG_ERROR"
                    ));
                }

                var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/callback";
                var loginUrl = await _authUrlBuilder.BuildInvitationLoginUrlAsync(
                    invitation.RealmName,
                    invitationToken,
                    invitation.Email,
                    redirectUri);

                if (loginUrl.StartsWith("ERROR:"))
                {
                    var errorMessage = loginUrl.Substring(6);
                    return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        errorMessage,
                        new List<string> { errorMessage },
                        StatusCodes.Status500InternalServerError,
                        "CONFIG_ERROR"
                    ));
                }

                _logger.LogInformation($"Generated login URL with login_hint for Keycloak realm {invitation.RealmName} for invitation {invitationToken}");

                var response = new ApiResponse<AuthUrlResponseDto>(
                    new AuthUrlResponseDto
                    {
                        AuthUrl = loginUrl,
                        Action = "invitation"
                    },
                    true,
                    "Invitation login URL generated successfully",
                    null,
                    StatusCodes.Status200OK
                );

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing invite redirect: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<AuthUrlResponseDto>(
                    null,
                    false,
                    "An error occurred processing the invitation",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    "INTERNAL_ERROR"
                ));
            }
        }

        #endregion
    }
}
