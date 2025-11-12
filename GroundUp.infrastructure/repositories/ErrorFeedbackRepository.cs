using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.core.security;
using GroundUp.infrastructure.data;

namespace GroundUp.infrastructure.repositories
{
    public class ErrorFeedbackRepository : BaseTenantRepository<ErrorFeedback, ErrorFeedbackDto>, IErrorFeedbackRepository
    {
        public ErrorFeedbackRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger, ITenantContext tenantContext)
            : base(context, mapper, logger, tenantContext) { }

        [RequiresPermission("errors.view")]
        public override Task<ApiResponse<PaginatedData<ErrorFeedbackDto>>> GetAllAsync(FilterParams filterParams)
            => base.GetAllAsync(filterParams);

        [RequiresPermission("errors.view")]
        public override Task<ApiResponse<ErrorFeedbackDto>> GetByIdAsync(int id)
            => base.GetByIdAsync(id);

        public override Task<ApiResponse<ErrorFeedbackDto>> AddAsync(ErrorFeedbackDto dto)
            => base.AddAsync(dto);

        [RequiresPermission("errors.update")]
        public override Task<ApiResponse<ErrorFeedbackDto>> UpdateAsync(int id, ErrorFeedbackDto dto)
            => base.UpdateAsync(id, dto);

        [RequiresPermission("errors.delete")]
        public override Task<ApiResponse<bool>> DeleteAsync(int id)
            => base.DeleteAsync(id);
    }
}