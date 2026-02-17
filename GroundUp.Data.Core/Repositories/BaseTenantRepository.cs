using AutoMapper;
using GroundUp.Core;
using GroundUp.Core.dtos;
using GroundUp.Core.interfaces;
using GroundUp.Core.interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.Data.Core.Repositories;

public abstract class BaseTenantRepository<T, TDto> : BaseRepository<T, TDto>
    where T : class
    where TDto : class
{
    protected readonly ITenantContext _tenantContext;

    public BaseTenantRepository(DbContext context, IMapper mapper, ILoggingService logger, ITenantContext tenantContext)
        : base(context, mapper, logger)
    {
        _tenantContext = tenantContext;
    }

    protected override Task<ApiResponse<PaginatedData<TDto>>> GetAllInternalAsync(
        FilterParams filterParams,
        Func<IQueryable<T>, IQueryable<T>>? queryShaper = null)
    {
        Func<IQueryable<T>, IQueryable<T>> tenantShaper = query =>
        {
            if (typeof(ITenantEntity).IsAssignableFrom(typeof(T)))
            {
                var tenantId = _tenantContext.TenantId;
                query = query.Where(e => ((ITenantEntity)e).TenantId == tenantId);
            }

            return queryShaper != null ? queryShaper(query) : query;
        };

        return base.GetAllInternalAsync(filterParams, tenantShaper);
    }

    protected override async Task<ApiResponse<TDto>> GetByIdInternalAsync(
        int id,
        Func<IQueryable<T>, IQueryable<T>>? queryShaper = null)
    {
        Func<IQueryable<T>, IQueryable<T>> tenantShaper = query =>
        {
            if (typeof(ITenantEntity).IsAssignableFrom(typeof(T)))
            {
                var tenantId = _tenantContext.TenantId;
                query = query.Where(e => ((ITenantEntity)e).TenantId == tenantId);
            }

            return queryShaper != null ? queryShaper(query) : query;
        };

        var result = await base.GetByIdInternalAsync(id, tenantShaper);
        if (!result.Success || result.Data == null)
        {
            return result;
        }

        // Extra safety: if the model is tenant-bound, ensure it belongs to current tenant.
        var entity = await tenantShaper(_dbSet.AsQueryable()).FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
        if (entity is ITenantEntity tenantEntity && tenantEntity.TenantId != _tenantContext.TenantId)
        {
            return new ApiResponse<TDto>(default!, false, "Item not found", null, StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        return result;
    }

    protected override Task<ApiResponse<byte[]>> ExportInternalAsync(
        FilterParams filterParams,
        string format = "csv",
        Func<IQueryable<T>, IQueryable<T>>? queryShaper = null)
    {
        Func<IQueryable<T>, IQueryable<T>> tenantShaper = query =>
        {
            if (typeof(ITenantEntity).IsAssignableFrom(typeof(T)))
            {
                var tenantId = _tenantContext.TenantId;
                query = query.Where(e => ((ITenantEntity)e).TenantId == tenantId);
            }

            return queryShaper != null ? queryShaper(query) : query;
        };

        return base.ExportInternalAsync(filterParams, format, tenantShaper);
    }

    public override Task<ApiResponse<TDto>> AddAsync(TDto dto)
        => base.AddAsync(dto);

    public override async Task<ApiResponse<TDto>> UpdateAsync(int id, TDto dto)
    {
        // Ensure entity belongs to tenant before update.
        if (typeof(ITenantEntity).IsAssignableFrom(typeof(T)))
        {
            var existing = await _dbSet.FindAsync(id);
            if (existing is ITenantEntity tenantEntity && tenantEntity.TenantId != _tenantContext.TenantId)
            {
                return new ApiResponse<TDto>(default!, false, "Item not found", null, StatusCodes.Status404NotFound, ErrorCodes.NotFound);
            }
        }

        return await base.UpdateAsync(id, dto);
    }

    public override async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        // Ensure entity belongs to tenant before delete.
        if (typeof(ITenantEntity).IsAssignableFrom(typeof(T)))
        {
            var existing = await _dbSet.FindAsync(id);
            if (existing is ITenantEntity tenantEntity && tenantEntity.TenantId != _tenantContext.TenantId)
            {
                return new ApiResponse<bool>(false, false, "Item not found", null, StatusCodes.Status404NotFound, ErrorCodes.NotFound);
            }
        }

        return await base.DeleteAsync(id);
    }
}
