using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.api.Controllers
{
    [Route("api/tenant-join-links")]
    [ApiController]
    [Authorize] // Requires authentication
    public class TenantJoinLinkController : ControllerBase
    {
        private readonly ITenantJoinLinkRepository _repository;
        private readonly ILoggingService _logger;

        public TenantJoinLinkController(ITenantJoinLinkRepository repository, ILoggingService logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <summary>
        /// List join links for current tenant
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<TenantJoinLinkDto>>>> GetAll(
            [FromQuery] FilterParams filterParams,
            [FromQuery] bool includeRevoked = false)
        {
            var result = await _repository.GetAllAsync(filterParams, includeRevoked);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get join link by ID (tenant-scoped)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<TenantJoinLinkDto>>> GetById(int id)
        {
            var result = await _repository.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Create new join link for current tenant
        /// Requires admin permission
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<TenantJoinLinkDto>>> Create([FromBody] CreateTenantJoinLinkDto dto)
        {
            // TODO: Add admin check via UserTenant.IsAdmin or permission system
            var result = await _repository.CreateAsync(dto);
            
            // Add full join URL to response
            if (result.Success && result.Data != null)
            {
                result.Data.JoinUrl = $"{Request.Scheme}://{Request.Host}/api/join/{result.Data.JoinToken}";
            }
            
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Revoke a join link (tenant-scoped)
        /// Requires admin permission
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<bool>>> Revoke(int id)
        {
            // TODO: Add admin check
            var result = await _repository.RevokeAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
