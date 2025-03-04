using AutoMapper;
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using GroundUp.infrastructure.utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Linq.Expressions;

namespace GroundUp.infrastructure.repositories
{
    public abstract class BaseRepository<T, TDto> where T : class
    {
        protected readonly ApplicationDbContext _context;
        protected readonly IMapper _mapper;
        protected readonly DbSet<T> _dbSet;
        private readonly ILoggingService _logger;

        public BaseRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger)
        {
            _context = context;
            _mapper = mapper;
            _dbSet = _context.Set<T>();
            _logger = logger;
        }

        // Get All with Pagination and Filtering
        public virtual async Task<ApiResponse<PaginatedData<TDto>>> GetAllAsync(FilterParams filterParams)
        {
            try
            {
                var query = _dbSet.AsQueryable();

                // Apply filters and sorting
                query = ApplyFilters(query, filterParams);
                query = ExpressionHelper.ApplySorting(query, filterParams.SortBy);

                // Apply Pagination
                var totalRecords = await query.CountAsync();
                var pagedItems = await query
                    .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
                    .Take(filterParams.PageSize)
                    .ToListAsync();

                var mappedItems = _mapper.Map<List<TDto>>(pagedItems);
                var paginatedData = new PaginatedData<TDto>(mappedItems, filterParams.PageNumber, filterParams.PageSize, totalRecords);             

                return new ApiResponse<PaginatedData<TDto>>(paginatedData);
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedData<TDto>>(default!, false, "An error occurred while retrieving data.", new List<string> { ex.Message }, StatusCodes.Status500InternalServerError, ErrorCodes.InternalServerError);
            }
        }

        // Get by ID
        public virtual async Task<ApiResponse<TDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _dbSet.FindAsync(id);
                if (entity == null)
                {
                    return new ApiResponse<TDto>(default!, false, "Item not found", null, StatusCodes.Status404NotFound, ErrorCodes.NotFound);
                }
                return new ApiResponse<TDto>(_mapper.Map<TDto>(entity));
            }
            catch (Exception ex)
            {
                return new ApiResponse<TDto>(default!, false, "An error occurred while retrieving the item.", new List<string> { ex.Message }, StatusCodes.Status500InternalServerError, ErrorCodes.InternalServerError);
            }
        }

        // Add new entity
        public virtual async Task<ApiResponse<TDto>> AddAsync(TDto dto)
        {
            try
            {
                var entity = _mapper.Map<T>(dto);

                _dbSet.Add(entity);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Entity {typeof(T).Name} added successfully.");

                return new ApiResponse<TDto>(_mapper.Map<TDto>(entity), true, "Created successfully", null, StatusCodes.Status201Created);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                return new ApiResponse<TDto>(default!, false, "A record with this name already exists.", null, StatusCodes.Status400BadRequest, ErrorCodes.DuplicateEntry);
            }
        }

        // Update existing entity
        public virtual async Task<ApiResponse<TDto>> UpdateAsync(int id, TDto dto)
        {
            try
            {
                var existingEntity = await _dbSet.FindAsync(id);
                if (existingEntity == null)
                {
                    return new ApiResponse<TDto>(default!, false, "Item not found", null, StatusCodes.Status404NotFound, ErrorCodes.NotFound);
                }

                _mapper.Map(dto, existingEntity);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Entity {typeof(T).Name} edited successfully.");

                return new ApiResponse<TDto>(_mapper.Map<TDto>(existingEntity), true, "Updated successfully", null, StatusCodes.Status200OK);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                return new ApiResponse<TDto>(default!, false, "A record with this name already exists.", null, StatusCodes.Status400BadRequest, ErrorCodes.DuplicateEntry);
            }
            catch (Exception ex)
            {
                return new ApiResponse<TDto>(default!, false, "An error occurred while updating the entity.", new List<string> { ex.Message }, StatusCodes.Status500InternalServerError, ErrorCodes.InternalServerError);
            }
        }

        // Delete entity
        public virtual async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var entity = await _dbSet.FindAsync(id);
                if (entity == null)
                {
                    return new ApiResponse<bool>(false, false, "Item not found", null, StatusCodes.Status404NotFound, ErrorCodes.NotFound);
                }

                _dbSet.Remove(entity);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Entity {typeof(T).Name} deleted successfully.");

                return new ApiResponse<bool>(true, true, "Deleted successfully", null, StatusCodes.Status200OK);
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>(false, false, "An error occurred while deleting the entity.", new List<string> { ex.Message }, StatusCodes.Status500InternalServerError, ErrorCodes.InternalServerError);
            }
        }

        // Apply Filters Dynamically
        private IQueryable<T> ApplyFilters(IQueryable<T> query, FilterParams filterParams)
        {
            var entityType = typeof(T);

            // Normalize Exact Match Filters
            foreach (var filter in filterParams.Filters ?? new Dictionary<string, string>())
            {
                var property = entityType.GetProperties()
                    .FirstOrDefault(p => string.Equals(p.Name, filter.Key, StringComparison.OrdinalIgnoreCase));

                if (property != null)
                {
                    query = query.Where(ExpressionHelper.BuildPredicate<T>(property, filter.Value));
                }
            }

            // Normalize "Contains" Filters (for partial text search)
            foreach (var filter in filterParams.ContainsFilters ?? new Dictionary<string, string>())
            {
                var property = entityType.GetProperties()
                    .FirstOrDefault(p => string.Equals(p.Name, filter.Key, StringComparison.OrdinalIgnoreCase));

                if (property != null)
                {
                    query = query.Where(ExpressionHelper.BuildContainsPredicate<T>(property, filter.Value));
                }
            }

            // Normalize Min/Max Range Filters
            foreach (var filter in filterParams.MinFilters ?? new Dictionary<string, string>())
            {
                var property = entityType.GetProperties()
                    .FirstOrDefault(p => string.Equals(p.Name, filter.Key, StringComparison.OrdinalIgnoreCase));

                if (property != null)
                {
                    query = query.Where((property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
                        ? ExpressionHelper.BuildDateRangePredicate<T>(property, filter.Value, isMin: true)
                        : ExpressionHelper.BuildRangePredicate<T>(property, filter.Value, isMin: true));
                }
            }

            foreach (var filter in filterParams.MaxFilters ?? new Dictionary<string, string>())
            {
                var property = entityType.GetProperties()
                    .FirstOrDefault(p => string.Equals(p.Name, filter.Key, StringComparison.OrdinalIgnoreCase));

                if (property != null)
                {
                    query = query.Where((property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
                        ? ExpressionHelper.BuildDateRangePredicate<T>(property, filter.Value, isMin: false)
                        : ExpressionHelper.BuildRangePredicate<T>(property, filter.Value, isMin: false));
                }
            }

            return query;
        }

        private bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            if (ex.InnerException is MySqlException mySqlEx)
            {
                return mySqlEx.Number == 1062; // 1062 = Duplicate entry (unique constraint violation)
            }

            return false;
        }

    }
}