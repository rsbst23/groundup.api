using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;

namespace GroundUp.Services.Core.Services;

public sealed class ErrorFeedbackService : IErrorFeedbackService
{
    private readonly IErrorFeedbackRepository _repo;

    public ErrorFeedbackService(IErrorFeedbackRepository repo)
    {
        _repo = repo;
    }

    public Task<ApiResponse<PaginatedData<ErrorFeedbackDto>>> GetAllAsync(FilterParams filterParams) =>
        _repo.GetAllAsync(filterParams);

    public Task<ApiResponse<ErrorFeedbackDto>> GetByIdAsync(int id) =>
        _repo.GetByIdAsync(id);

    public Task<ApiResponse<ErrorFeedbackDto>> AddAsync(ErrorFeedbackDto errorFeedbackDto) =>
        _repo.AddAsync(errorFeedbackDto);

    public Task<ApiResponse<bool>> DeleteAsync(int id) =>
        _repo.DeleteAsync(id);
}
