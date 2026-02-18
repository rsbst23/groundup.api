using GroundUp.Core.dtos;

namespace GroundUp.Data.Abstractions.Interfaces
{
    public interface IErrorFeedbackRepository
    {
        Task<ApiResponse<PaginatedData<ErrorFeedbackDto>>> GetAllAsync(FilterParams filterParams);
        Task<ApiResponse<ErrorFeedbackDto>> GetByIdAsync(int id);
        Task<ApiResponse<ErrorFeedbackDto>> AddAsync(ErrorFeedbackDto errorFeedbackDto);
        Task<ApiResponse<ErrorFeedbackDto>> UpdateAsync(int id, ErrorFeedbackDto errorFeedbackDto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
    }
}