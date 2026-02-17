using AutoMapper;
using GroundUp.Core;
using GroundUp.Core.dtos;
using GroundUp.Core.interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;

namespace GroundUp.Repositories.Inventory.Repositories.Base
{
    /// <summary>
    /// Inventory-scoped base repository.
    /// Duplicated here temporarily to avoid referencing `GroundUp.infrastructure` from `GroundUp.Repositories.Inventory`.
    /// </summary>
    public abstract class BaseRepository<TEntity, TDto>
        where TEntity : class
        where TDto : class
    {
        protected readonly DbContext _context;
        protected readonly IMapper _mapper;
        protected readonly DbSet<TEntity> _dbSet;
        protected readonly ILoggingService _logger;

        protected BaseRepository(DbContext context, IMapper mapper, ILoggingService logger)
        {
            _context = context;
            _mapper = mapper;
            _dbSet = _context.Set<TEntity>();
            _logger = logger;
        }

        protected virtual async Task<OperationResult<PaginatedData<TDto>>> GetAllInternalAsync(
            FilterParams filterParams,
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? queryShaper = null)
        {
            try
            {
                var query = _dbSet.AsQueryable();
                if (queryShaper != null)
                {
                    query = queryShaper(query);
                }

                query = ApplyFilters(query, filterParams);
                query = ApplySorting(query, filterParams.SortBy);

                var totalRecords = await query.CountAsync();

                var pagedItems = await query
                    .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
                    .Take(filterParams.PageSize)
                    .ToListAsync();

                var mappedItems = _mapper.Map<List<TDto>>(pagedItems);
                var paginatedData = new PaginatedData<TDto>(mappedItems, filterParams.PageNumber, filterParams.PageSize, totalRecords);

                return new OperationResult<PaginatedData<TDto>>
                {
                    Data = paginatedData,
                    Success = true,
                    StatusCode = StatusCodes.Status200OK
                };
            }
            catch (Exception ex)
            {
                return new OperationResult<PaginatedData<TDto>>
                {
                    Data = default,
                    Success = false,
                    Message = "An error occurred while retrieving data.",
                    Errors = new List<string> { ex.Message },
                    StatusCode = StatusCodes.Status500InternalServerError,
                    ErrorCode = ErrorCodes.InternalServerError
                };
            }
        }

        protected virtual async Task<OperationResult<TDto>> GetByIdInternalAsync(
            int id,
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? queryShaper = null)
        {
            try
            {
                TEntity? entity;

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
                    return new OperationResult<TDto>
                    {
                        Data = default,
                        Success = false,
                        Message = "Item not found",
                        StatusCode = StatusCodes.Status404NotFound,
                        ErrorCode = ErrorCodes.NotFound
                    };
                }

                return new OperationResult<TDto>
                {
                    Data = _mapper.Map<TDto>(entity),
                    Success = true,
                    StatusCode = StatusCodes.Status200OK
                };
            }
            catch (Exception ex)
            {
                return new OperationResult<TDto>
                {
                    Data = default,
                    Success = false,
                    Message = "An error occurred while retrieving the item.",
                    Errors = new List<string> { ex.Message },
                    StatusCode = StatusCodes.Status500InternalServerError,
                    ErrorCode = ErrorCodes.InternalServerError
                };
            }
        }

        protected virtual async Task<OperationResult<byte[]>> ExportInternalAsync(
            FilterParams filterParams,
            string format = "csv",
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? queryShaper = null)
        {
            try
            {
                var query = _dbSet.AsQueryable();
                if (queryShaper != null)
                {
                    query = queryShaper(query);
                }

                query = ApplyFilters(query, filterParams);
                var orderedQuery = ApplySorting(query, filterParams.SortBy);

                var items = await orderedQuery.ToListAsync();
                var mappedItems = _mapper.Map<List<TDto>>(items);
                _logger.LogInformation($"Exporting {items.Count} {typeof(TEntity).Name} items in {format} format");

                byte[] fileContent = format.ToLower() switch
                {
                    "json" => await GenerateJsonFileAsync(mappedItems),
                    _ => await GenerateCsvFileAsync(mappedItems)
                };

                return new OperationResult<byte[]>
                {
                    Data = fileContent,
                    Success = true,
                    Message = $"Exported {items.Count} items successfully.",
                    StatusCode = StatusCodes.Status200OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error exporting {typeof(TEntity).Name} data: {ex.Message}", ex);
                return new OperationResult<byte[]>
                {
                    Data = Array.Empty<byte>(),
                    Success = false,
                    Message = "An error occurred while exporting data.",
                    Errors = new List<string> { ex.Message },
                    StatusCode = StatusCodes.Status500InternalServerError,
                    ErrorCode = ErrorCodes.InternalServerError
                };
            }
        }

        public virtual Task<OperationResult<PaginatedData<TDto>>> GetAllAsync(FilterParams filterParams)
            => GetAllInternalAsync(filterParams);

        public virtual Task<OperationResult<TDto>> GetByIdAsync(int id)
            => GetByIdInternalAsync(id);

        public virtual async Task<OperationResult<TDto>> AddAsync(TDto dto)
        {
            try
            {
                var entity = _mapper.Map<TEntity>(dto);
                _dbSet.Add(entity);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Entity {typeof(TEntity).Name} added successfully.");

                return new OperationResult<TDto>
                {
                    Data = _mapper.Map<TDto>(entity),
                    Success = true,
                    Message = "Created successfully",
                    StatusCode = StatusCodes.Status201Created
                };
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                return new OperationResult<TDto>
                {
                    Data = default,
                    Success = false,
                    Message = "A record with this name already exists.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    ErrorCode = ErrorCodes.DuplicateEntry
                };
            }
        }

        public virtual async Task<OperationResult<TDto>> UpdateAsync(int id, TDto dto)
        {
            try
            {
                var existingEntity = await _dbSet.FindAsync(id);
                if (existingEntity == null)
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

                _mapper.Map(dto, existingEntity);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Entity {typeof(TEntity).Name} edited successfully.");

                return new OperationResult<TDto>
                {
                    Data = _mapper.Map<TDto>(existingEntity),
                    Success = true,
                    Message = "Updated successfully",
                    StatusCode = StatusCodes.Status200OK
                };
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                return new OperationResult<TDto>
                {
                    Data = default,
                    Success = false,
                    Message = "A record with this name already exists.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    ErrorCode = ErrorCodes.DuplicateEntry
                };
            }
            catch (Exception ex)
            {
                return new OperationResult<TDto>
                {
                    Data = default,
                    Success = false,
                    Message = "An error occurred while updating the entity.",
                    Errors = new List<string> { ex.Message },
                    StatusCode = StatusCodes.Status500InternalServerError,
                    ErrorCode = ErrorCodes.InternalServerError
                };
            }
        }

        public virtual async Task<OperationResult<bool>> DeleteAsync(int id)
        {
            try
            {
                var entity = await _dbSet.FindAsync(id);
                if (entity == null)
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

                _dbSet.Remove(entity);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Entity {typeof(TEntity).Name} deleted successfully.");

                return new OperationResult<bool>
                {
                    Data = true,
                    Success = true,
                    Message = "Deleted successfully",
                    StatusCode = StatusCodes.Status200OK
                };
            }
            catch (Exception ex)
            {
                return new OperationResult<bool>
                {
                    Data = false,
                    Success = false,
                    Message = "An error occurred while deleting the entity.",
                    Errors = new List<string> { ex.Message },
                    StatusCode = StatusCodes.Status500InternalServerError,
                    ErrorCode = ErrorCodes.InternalServerError
                };
            }
        }

        public virtual Task<OperationResult<byte[]>> ExportAsync(FilterParams filterParams, string format = "csv")
            => ExportInternalAsync(filterParams, format);

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

        private async Task<byte[]> GenerateJsonFileAsync<T>(List<T> items)
        {
            using var memoryStream = new MemoryStream();
            await using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(items, options);
            await writer.WriteAsync(json);
            await writer.FlushAsync();
            return memoryStream.ToArray();
        }

        private IQueryable<TEntity> ApplySorting(IQueryable<TEntity> query, string? sortBy)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
            {
                return query;
            }

            // Supports: "Name" or "-Name" (descending)
            var descending = sortBy.StartsWith('-');
            var propName = descending ? sortBy[1..] : sortBy;

            var prop = typeof(TEntity).GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, propName, StringComparison.OrdinalIgnoreCase));

            if (prop == null)
            {
                return query;
            }

            var param = Expression.Parameter(typeof(TEntity), "x");
            var body = Expression.Convert(Expression.Property(param, prop), typeof(object));
            var lambda = Expression.Lambda<Func<TEntity, object>>(body, param);

            return descending ? query.OrderByDescending(lambda) : query.OrderBy(lambda);
        }

        private IQueryable<TEntity> ApplyFilters(IQueryable<TEntity> query, FilterParams filterParams)
        {
            var entityType = typeof(TEntity);

            foreach (var filter in filterParams.Filters ?? new Dictionary<string, string>())
            {
                var property = entityType.GetProperties()
                    .FirstOrDefault(p => string.Equals(p.Name, filter.Key, StringComparison.OrdinalIgnoreCase));
                if (property != null)
                {
                    query = query.Where(BuildPredicate(property, filter.Value));
                }
            }

            foreach (var filter in filterParams.ContainsFilters ?? new Dictionary<string, string>())
            {
                var property = entityType.GetProperties()
                    .FirstOrDefault(p => string.Equals(p.Name, filter.Key, StringComparison.OrdinalIgnoreCase));
                if (property != null)
                {
                    query = query.Where(BuildContainsPredicate(property, filter.Value));
                }
            }

            return query;
        }

        private static Expression<Func<TEntity, bool>> BuildPredicate(System.Reflection.PropertyInfo property, string value)
        {
            var parameter = Expression.Parameter(typeof(TEntity), "x");
            Expression member = Expression.Property(parameter, property);

            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            object? converted;
            try
            {
                converted = Convert.ChangeType(value, targetType);
            }
            catch
            {
                // If conversion fails, don't filter.
                return _ => true;
            }

            var constant = Expression.Constant(converted, targetType);

            // Always use a convert so the expression types match (MemberExpression vs UnaryExpression).
            member = Expression.Convert(member, targetType);

            var equal = Expression.Equal(member, constant);
            return Expression.Lambda<Func<TEntity, bool>>(equal, parameter);
        }

        private static Expression<Func<TEntity, bool>> BuildContainsPredicate(System.Reflection.PropertyInfo property, string value)
        {
            var parameter = Expression.Parameter(typeof(TEntity), "x");
            var member = Expression.Property(parameter, property);

            if (property.PropertyType != typeof(string))
            {
                return _ => true;
            }

            var constant = Expression.Constant(value);
            var containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
            if (containsMethod == null)
            {
                return _ => true;
            }

            var body = Expression.Call(member, containsMethod, constant);
            return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
            => ex.InnerException is MySqlException { Number: 1062 };
    }
}
