using AutoMapper;
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using GroundUp.Core.interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.Repositories.Inventory.Repositories.Base
{
    /// <summary>
    /// Inventory-scoped tenant-aware base repository.
    /// See notes in `BaseRepository` about temporary duplication.
    /// </summary>
    public class BaseTenantRepository<TEntity, TDto> : BaseRepository<TEntity, TDto>
        where TEntity : class
        where TDto : class
    {
        protected readonly ITenantContext _tenantContext;

        public BaseTenantRepository(DbContext context, IMapper mapper, ILoggingService logger, ITenantContext tenantContext)
            : base(context, mapper, logger)
        {
            _tenantContext = tenantContext;
        }

        protected override Task<OperationResult<PaginatedData<TDto>>> GetAllInternalAsync(
            FilterParams filterParams,
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? queryShaper = null)
        {
            Func<IQueryable<TEntity>, IQueryable<TEntity>> tenantShaper = query =>
            {
                if (typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity)))
                {
                    var tenantId = _tenantContext.TenantId;
                    query = query.Where(e => ((ITenantEntity)e).TenantId == tenantId);
                }

                return queryShaper != null ? queryShaper(query) : query;
            };

            return base.GetAllInternalAsync(filterParams, tenantShaper);
        }

        protected override async Task<OperationResult<TDto>> GetByIdInternalAsync(
            int id,
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? queryShaper = null)
        {
            Func<IQueryable<TEntity>, IQueryable<TEntity>> tenantShaper = query =>
            {
                if (typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity)))
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
                return new OperationResult<TDto>
                {
                    Data = default,
                    Success = false,
                    Message = "Item not found",
                    StatusCode = StatusCodes.Status404NotFound,
                    ErrorCode = ErrorCodes.NotFound
                };
            }

            return result;
        }

        protected override Task<OperationResult<byte[]>> ExportInternalAsync(
            FilterParams filterParams,
            string format = "csv",
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? queryShaper = null)
        {
            Func<IQueryable<TEntity>, IQueryable<TEntity>> tenantShaper = query =>
            {
                if (typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity)))
                {
                    var tenantId = _tenantContext.TenantId;
                    query = query.Where(e => ((ITenantEntity)e).TenantId == tenantId);
                }

                return queryShaper != null ? queryShaper(query) : query;
            };

            return base.ExportInternalAsync(filterParams, format, tenantShaper);
        }

        public override async Task<OperationResult<TDto>> UpdateAsync(int id, TDto dto)
        {
            if (typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity)))
            {
                var existing = await _dbSet.FindAsync(id);
                if (existing is ITenantEntity tenantEntity && tenantEntity.TenantId != _tenantContext.TenantId)
                {
                    return new OperationResult<TDto>
                    {
                        Data = default,
                        Success = false,
                        Message = "Item not found",
                        StatusCode = StatusCodes.Status404NotFound,
                        ErrorCode = ErrorCodes.NotFound
                    };
                }
            }

            return await base.UpdateAsync(id, dto);
        }

        public override async Task<OperationResult<bool>> DeleteAsync(int id)
        {
            if (typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity)))
            {
                var existing = await _dbSet.FindAsync(id);
                if (existing is ITenantEntity tenantEntity && tenantEntity.TenantId != _tenantContext.TenantId)
                {
                    return new OperationResult<bool>
                    {
                        Data = false,
                        Success = false,
                        Message = "Item not found",
                        StatusCode = StatusCodes.Status404NotFound,
                        ErrorCode = ErrorCodes.NotFound
                    };
                }
            }

            return await base.DeleteAsync(id);
        }
    }
}
