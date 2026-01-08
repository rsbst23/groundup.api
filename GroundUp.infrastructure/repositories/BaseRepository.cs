using AutoMapper;
using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using GroundUp.infrastructure.utilities;

namespace GroundUp.infrastructure.repositories
{
    public abstract class BaseRepository<T, TDto> where T : class
    {
        protected readonly DbContext _context;
        protected readonly IMapper _mapper;
        protected readonly DbSet<T> _dbSet;
        protected readonly ILoggingService _logger;

        protected BaseRepository(DbContext context, IMapper mapper, ILoggingService logger)
        {
            _context = context;
            _mapper = mapper;
            _dbSet = _context.Set<T>();
            _logger = logger;
        }

        #region Query Shaper Hooks (per method)

        /// <summary>
        /// Internal list pipeline that allows the caller (derived repository method) to shape the query
        /// (e.g. Include/ThenInclude) before the base filtering/sorting/paging is applied.
        /// </summary>
        protected virtual async Task<ApiResponse<PaginatedData<TDto>>> GetAllInternalAsync(
            FilterParams filterParams,
            Func<IQueryable<T>, IQueryable<T>>? queryShaper = null)
        {
            try
            {
                var query = _dbSet.AsQueryable();
                if (queryShaper != null)
                {
                    query = queryShaper(query);
                }

                query = ApplyFilters(query, filterParams);
                query = ExpressionHelper.ApplySorting(query, filterParams.SortBy);

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
                return new ApiResponse<PaginatedData<TDto>>(
                    default!,
                    false,
                    "An error occurred while retrieving data.",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError);
            }
        }

        /// <summary>
        /// Internal get-by-id pipeline that allows shaping. If no shaping is requested,
        /// uses FindAsync (fast path) when possible.
        /// </summary>
        protected virtual async Task<ApiResponse<TDto>> GetByIdInternalAsync(
            int id,
            Func<IQueryable<T>, IQueryable<T>>? queryShaper = null)
        {
            try
            {
                T? entity;

                if (queryShaper == null)
                {
                    entity = await _dbSet.FindAsync(id);
                }
                else
                {
                    var query = queryShaper(_dbSet.AsQueryable());
                    entity = await query.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
                }

                if (entity == null)
                {
                    return new ApiResponse<TDto>(default!, false, "Item not found", null, StatusCodes.Status404NotFound, ErrorCodes.NotFound);
                }

                return new ApiResponse<TDto>(_mapper.Map<TDto>(entity));
            }
            catch (Exception ex)
            {
                return new ApiResponse<TDto>(
                    default!,
                    false,
                    "An error occurred while retrieving the item.",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError);
            }
        }

        protected virtual async Task<ApiResponse<byte[]>> ExportInternalAsync(
            FilterParams filterParams,
            string format = "csv",
            Func<IQueryable<T>, IQueryable<T>>? queryShaper = null)
        {
            try
            {
                var query = _dbSet.AsQueryable();
                if (queryShaper != null)
                {
                    query = queryShaper(query);
                }

                query = ApplyFilters(query, filterParams);
                var orderedQuery = ExpressionHelper.ApplySorting(query, filterParams.SortBy);

                var items = await orderedQuery.ToListAsync();
                var mappedItems = _mapper.Map<List<TDto>>(items);
                _logger.LogInformation($"Exporting {items.Count} {typeof(T).Name} items in {format} format");

                byte[] fileContent;
                switch (format.ToLower())
                {
                    case "json":
                        fileContent = await GenerateJsonFileAsync(mappedItems);
                        break;
                    case "csv":
                    default:
                        fileContent = await GenerateCsvFileAsync(mappedItems);
                        break;
                }

                return new ApiResponse<byte[]>(fileContent, true, $"Exported {items.Count} items successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error exporting {typeof(T).Name} data: {ex.Message}", ex);
                return new ApiResponse<byte[]>
                (
                    new byte[0],
                    false,
                    "An error occurred while exporting data.",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        #endregion

        #region Query Helpers (for derived repositories)

        /// <summary>
        /// Applies the standard base filtering pipeline to a query.
        /// Exposed for derived repos that need custom shaping/projection but still want the same filtering behavior.
        /// </summary>
        protected IQueryable<T> ApplyFilterParams(IQueryable<T> query, FilterParams filterParams)
        {
            query = ApplyFilters(query, filterParams);
            query = ExpressionHelper.ApplySorting(query, filterParams.SortBy);
            return query;
        }

        /// <summary>
        /// Applies pagination for the given filter params.
        /// </summary>
        protected IQueryable<TQuery> ApplyPaging<TQuery>(IQueryable<TQuery> query, FilterParams filterParams)
        {
            return query
                .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);
        }

        #endregion

        // Get All with Pagination and Filtering
        public virtual Task<ApiResponse<PaginatedData<TDto>>> GetAllAsync(FilterParams filterParams)
            => GetAllInternalAsync(filterParams);

        // Get by ID
        public virtual Task<ApiResponse<TDto>> GetByIdAsync(int id)
            => GetByIdInternalAsync(id);

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

        public virtual Task<ApiResponse<byte[]>> ExportAsync(FilterParams filterParams, string format = "csv")
            => ExportInternalAsync(filterParams, format);

        // Helper method to generate CSV file
        private async Task<byte[]> GenerateCsvFileAsync<T>(List<T> items)
        {
            using var memoryStream = new MemoryStream();
            using var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8);
            if (items.Count > 0)
            {
                var properties = typeof(T).GetProperties();
                var headers = string.Join(",", properties.Select(p => $"\"{p.Name}\""));
                await streamWriter.WriteLineAsync(headers);
                foreach (var item in items)
                {
                    var values = properties.Select(p =>
                    {
                        var value = p.GetValue(item)?.ToString() ?? "";
                        return $"\"{value.Replace("\"", "\"\"")}\"";
                    });
                    await streamWriter.WriteLineAsync(string.Join(",", values));
                }
            }
            await streamWriter.FlushAsync();
            return memoryStream.ToArray();
        }

        // Helper method to generate JSON file
        private async Task<byte[]> GenerateJsonFileAsync<T>(List<T> items)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(items, options);
            await writer.WriteAsync(json);
            await writer.FlushAsync();
            return memoryStream.ToArray();
        }

        // Apply Filters Dynamically
        private IQueryable<T> ApplyFilters(IQueryable<T> query, FilterParams filterParams)
        {
            var entityType = typeof(T);
            foreach (var filter in filterParams.Filters ?? new Dictionary<string, string>())
            {
                var property = entityType.GetProperties()
                    .FirstOrDefault(p => string.Equals(p.Name, filter.Key, StringComparison.OrdinalIgnoreCase));
                if (property != null)
                {
                    query = query.Where(ExpressionHelper.BuildPredicate<T>(property, filter.Value));
                }
            }
            foreach (var filter in filterParams.ContainsFilters ?? new Dictionary<string, string>())
            {
                var property = entityType.GetProperties()
                    .FirstOrDefault(p => string.Equals(p.Name, filter.Key, StringComparison.OrdinalIgnoreCase));
                if (property != null)
                {
                    query = query.Where(ExpressionHelper.BuildContainsPredicate<T>(property, filter.Value));
                }
            }
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
                return mySqlEx.Number == 1062;
            }
            return false;
        }
    }
}
