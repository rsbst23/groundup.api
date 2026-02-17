using GroundUp.core.dtos;
using GroundUp.core.security;

namespace GroundUp.core.interfaces;

/// <summary>
/// Service boundary for error feedback.
/// Controllers call services only; authorization (if required) is enforced here.
/// </summary>
public interface IErrorFeedbackService
{
    [RequiresPermission("feedback.view", "SYSTEMADMIN")]
    Task<ApiResponse<PaginatedData<ErrorFeedbackDto>>> GetAllAsync(FilterParams filterParams);

    [RequiresPermission("feedback.view", "SYSTEMADMIN")]
    Task<ApiResponse<ErrorFeedbackDto>> GetByIdAsync(int id);

    // Public/anonymous creation endpoint.
    Task<ApiResponse<ErrorFeedbackDto>> AddAsync(ErrorFeedbackDto errorFeedbackDto);

    [RequiresPermission("feedback.delete", "SYSTEMADMIN")]
    Task<ApiResponse<bool>> DeleteAsync(int id);
}
