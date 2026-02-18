using AutoMapper;
using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core;
using GroundUp.Core.dtos;
using GroundUp.Core.entities;
using GroundUp.Core.interfaces;
using GroundUp.Data.Core.Data;

namespace GroundUp.Data.Core.Repositories;

public class ErrorFeedbackRepository : BaseTenantRepository<ErrorFeedback, ErrorFeedbackDto>, IErrorFeedbackRepository
{
    public ErrorFeedbackRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger, ITenantContext tenantContext)
        : base(context, mapper, logger, tenantContext) { }

    public override Task<ApiResponse<PaginatedData<ErrorFeedbackDto>>> GetAllAsync(FilterParams filterParams)
        => base.GetAllAsync(filterParams);

    public override Task<ApiResponse<ErrorFeedbackDto>> GetByIdAsync(int id)
        => base.GetByIdAsync(id);

    public override Task<ApiResponse<ErrorFeedbackDto>> AddAsync(ErrorFeedbackDto dto)
        => base.AddAsync(dto);

    public override Task<ApiResponse<ErrorFeedbackDto>> UpdateAsync(int id, ErrorFeedbackDto dto)
        => base.UpdateAsync(id, dto);

    public override Task<ApiResponse<bool>> DeleteAsync(int id)
        => base.DeleteAsync(id);
}
