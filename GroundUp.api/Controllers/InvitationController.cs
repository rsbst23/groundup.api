using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GroundUp.Api.Controllers
{
    [Route("api/invitations")]
    [ApiController]
    public class InvitationController : ControllerBase
    {
        private readonly IInvitationService _invitationService;
        private readonly ILoggingService _logger;
        private readonly IAuthUrlBuilderService _authUrlBuilder;

        public InvitationController(
            IInvitationService invitationService,
            ILoggingService logger,
            IAuthUrlBuilderService authUrlBuilder)
        {
            _invitationService = invitationService;
            _logger = logger;
            _authUrlBuilder = authUrlBuilder;
        }

        #region Tenant-Scoped Admin Operations

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<TenantInvitationDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _invitationService.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<TenantInvitationDto>>> GetById(int id)
        {
            var result = await _invitationService.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<TenantInvitationDto>>> Create([FromBody] CreateTenantInvitationDto dto)
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

            var result = await _invitationService.CreateAsync(dto, userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<TenantInvitationDto>>> Update(int id, [FromBody] UpdateTenantInvitationDto dto)
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

            var result = await _invitationService.UpdateAsync(id, dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _invitationService.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("pending")]
        public async Task<ActionResult<ApiResponse<List<TenantInvitationDto>>>> GetPending()
        {
            var result = await _invitationService.GetPendingAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/resend")]
        public async Task<ActionResult<ApiResponse<bool>>> Resend(int id, [FromQuery] int expirationDays = 7)
        {
            var result = await _invitationService.ResendAsync(id, expirationDays);
            return StatusCode(result.StatusCode, result);
        }

        #endregion

        #region Cross-Tenant User Operations

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

            var result = await _invitationService.GetMyInvitationsAsync(emailClaim);
            return StatusCode(result.StatusCode, result);
        }

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

            var result = await _invitationService.AcceptInvitationAsync(dto.InvitationToken, userId);
            return StatusCode(result.StatusCode, result);
        }

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

            var result = await _invitationService.GetByTokenAsync(token);
            return StatusCode(result.StatusCode, result);
        }

        #endregion

        #region Public Invitation Flow Endpoint

        [HttpGet("invite/{invitationToken}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<AuthUrlResponseDto>>> InviteRedirect(string invitationToken)
        {
            try
            {
                _logger.LogInformation($"Processing invite redirect for token: {invitationToken}");

                var invitationResult = await _invitationService.GetByTokenAsync(invitationToken);
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
                    var errorMessage = loginUrl[6..];
                    return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<AuthUrlResponseDto>(
                        null,
                        false,
                        errorMessage,
                        new List<string> { errorMessage },
                        StatusCodes.Status500InternalServerError,
                        "CONFIG_ERROR"
                    ));
                }

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
