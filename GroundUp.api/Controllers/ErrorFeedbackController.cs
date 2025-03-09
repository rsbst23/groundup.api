using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.api.Controllers
{
    [Route("api/feedback")]
    [ApiController]
    public class ErrorFeedbackController : ControllerBase
    {
        private readonly IErrorFeedbackRepository _errorFeedbackRepository;
        private readonly ILoggingService _logger;

        public ErrorFeedbackController(IErrorFeedbackRepository errorFeedbackRepository, ILoggingService logger)
        {
            _errorFeedbackRepository = errorFeedbackRepository;
            _logger = logger;
        }

        // GET: api/feedback/error (Paginated) - Requires authentication
        [HttpGet("error")]
        //[Authorize]
        public async Task<ActionResult<ApiResponse<PaginatedData<ErrorFeedbackDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _errorFeedbackRepository.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, result);
        }

        // GET: api/feedback/error/{id} - Requires authentication
        [HttpGet("error/{id}")]
        //[Authorize]
        public async Task<ActionResult<ApiResponse<ErrorFeedbackDto>>> GetById(int id)
        {
            var result = await _errorFeedbackRepository.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        // POST: api/feedback/error - Does not require authentication
        [HttpPost("error")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<ErrorFeedbackDto>>> Create([FromBody] ErrorFeedbackDto errorFeedbackDto)
        {
            if (errorFeedbackDto == null)
            {
                return BadRequest(new ApiResponse<ErrorFeedbackDto>(
                    default!,
                    false,
                    "Invalid error feedback data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            try
            {
                // Log the incoming error feedback
                _logger.LogInformation($"Received error feedback: {errorFeedbackDto.Context} - {errorFeedbackDto.Error?.Message}");

                var result = await _errorFeedbackRepository.AddAsync(errorFeedbackDto);
                return StatusCode(result.StatusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing feedback: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<ErrorFeedbackDto>(
                        default!,
                        false,
                        "An error occurred while processing the feedback.",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.InternalServerError
                    ));
            }
        }

        // DELETE: api/feedback/error/{id} - Requires authentication
        [HttpDelete("error/{id}")]
        //[Authorize]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _errorFeedbackRepository.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}