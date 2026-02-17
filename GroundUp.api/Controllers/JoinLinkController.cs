using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.Api.Controllers
{
    /// <summary>
    /// Unified controller for all join link operations.
    /// Handles both tenant-scoped admin operations and public join-link redirect.
    /// </summary>
    [Route("api/join")]
    [ApiController]
    public class JoinLinkController : ControllerBase
    {
        private readonly ITenantJoinLinkService _tenantJoinLinkService;
        private readonly IJoinLinkService _joinLinkService;

        public JoinLinkController(ITenantJoinLinkService tenantJoinLinkService, IJoinLinkService joinLinkService)
        {
            _tenantJoinLinkService = tenantJoinLinkService;
            _joinLinkService = joinLinkService;
        }

        #region Tenant-Scoped Admin Operations

        /// <summary>
        /// List join links for current tenant
        /// GET /api/join
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<ApiResponse<PaginatedData<TenantJoinLinkDto>>>> GetAll(
            [FromQuery] FilterParams filterParams,
            [FromQuery] bool includeRevoked = false)
        {
            var result = await _tenantJoinLinkService.GetAllAsync(filterParams, includeRevoked);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get join link by ID (tenant-scoped)
        /// GET /api/join/{id}
        /// </summary>
        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<TenantJoinLinkDto>>> GetById(int id)
        {
            var result = await _tenantJoinLinkService.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Create new join link for current tenant
        /// POST /api/join
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ApiResponse<TenantJoinLinkDto>>> Create([FromBody] CreateTenantJoinLinkDto dto)
        {
            var result = await _tenantJoinLinkService.CreateAsync(dto);

            // Add full join URL to response
            if (result.Success && result.Data != null)
            {
                result.Data.JoinUrl = $"{Request.Scheme}://{Request.Host}/api/join/{result.Data.JoinToken}";
            }

            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Revoke a join link (tenant-scoped)
        /// DELETE /api/join/{id}
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<bool>>> Revoke(int id)
        {
            var result = await _tenantJoinLinkService.RevokeAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        #endregion

        #region Public Join Flow Endpoint

        /// <summary>
        /// PUBLIC ENDPOINT: Validate join link and return Keycloak login URL
        /// GET /api/join/{joinToken}
        /// Client should redirect user to the returned AuthUrl.
        /// </summary>
        [HttpGet("{joinToken}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<AuthUrlResponseDto>>> JoinRedirect(string joinToken)
        {
            var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/callback";
            var result = await _joinLinkService.BuildJoinAuthUrlAsync(joinToken, redirectUri);
            return StatusCode(result.StatusCode, result);
        }

        #endregion
    }
}
