using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.core.security;
using GroundUp.infrastructure.data;
using System.Text.Json;

namespace GroundUp.infrastructure.repositories
{
    public class ErrorFeedbackRepository : BaseRepository<ErrorFeedback, ErrorFeedbackDto>, IErrorFeedbackRepository
    {
        public ErrorFeedbackRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger)
            : base(context, mapper, logger) { }

        //[RequiresPermission("errors.view")]
        public override Task<ApiResponse<PaginatedData<ErrorFeedbackDto>>> GetAllAsync(FilterParams filterParams)
            => base.GetAllAsync(filterParams);

        //[RequiresPermission("errors.view")]
        public override Task<ApiResponse<ErrorFeedbackDto>> GetByIdAsync(int id)
            => base.GetByIdAsync(id);

        // Allow error feedback submission without authentication
        //public override async Task<ApiResponse<ErrorFeedbackDto>> AddAsync(ErrorFeedbackDto dto)
        //{
        //    // Set the timestamp if not already set
        //    if (dto.Timestamp == default)
        //    {
        //        dto.Timestamp = DateTime.UtcNow;
        //    }

        //    // Set created date
        //    dto.CreatedDate = DateTime.UtcNow;

        //    return await base.AddAsync(dto);
        //}

        public override Task<ApiResponse<ErrorFeedbackDto>> AddAsync(ErrorFeedbackDto dto)
            => base.AddAsync(dto);

        //[RequiresPermission("errors.update")]
        public override Task<ApiResponse<ErrorFeedbackDto>> UpdateAsync(int id, ErrorFeedbackDto dto)
            => base.UpdateAsync(id, dto);

        //[RequiresPermission("errors.delete")]
        public override Task<ApiResponse<bool>> DeleteAsync(int id)
            => base.DeleteAsync(id);
    }
}