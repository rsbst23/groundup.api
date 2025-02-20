using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.infrastructure.data;
using GroundUp.infrastructure.utilities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace GroundUp.infrastructure.repositories
{
    public abstract class BaseRepository<T, TDto> where T : class
    {
        protected readonly ApplicationDbContext _context;
        protected readonly IMapper _mapper;
        protected readonly DbSet<T> _dbSet;

        public BaseRepository(ApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
            _dbSet = _context.Set<T>();
        }

        // Generic Get All with Filtering, Sorting & Pagination
        public async Task<PaginatedResponse<TDto>> GetAllAsync(FilterParams filterParams)
        {
            var query = _dbSet.AsQueryable();

            // Apply Filters
            query = ApplyFilters(query, filterParams);

            // Apply Sorting
            query = ExpressionHelper.ApplySorting(query, filterParams.SortBy);

            // Apply Pagination
            return await ApplyPagination(query, filterParams);
        }

        // Generic Get By ID
        public async Task<TDto?> GetByIdAsync(int id)
        {
            var entity = await _dbSet.FindAsync(id);
            return entity == null ? default(TDto) : _mapper.Map<TDto>(entity);
        }

        // Generic Add
        public async Task<TDto> AddAsync(TDto dto)
        {
            var entity = _mapper.Map<T>(dto);
            _dbSet.Add(entity);
            await _context.SaveChangesAsync();
            return _mapper.Map<TDto>(entity);
        }

        // Generic Update
        public async Task<TDto?> UpdateAsync(int id, TDto dto)
        {
            var existingEntity = await _dbSet.FindAsync(id);
            if (existingEntity == null) return default(TDto);

            _mapper.Map(dto, existingEntity);
            await _context.SaveChangesAsync();

            return _mapper.Map<TDto>(existingEntity);
        }

        // Generic Delete
        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _dbSet.FindAsync(id);
            if (entity == null) return false;

            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        // Apply Filters Dynamically
        private IQueryable<T> ApplyFilters(IQueryable<T> query, FilterParams filterParams)
        {
            foreach (var filter in filterParams.Filters ?? new Dictionary<string, string>())
            {
                var property = typeof(T).GetProperty(filter.Key);
                if (property != null)
                {
                    query = query.Where(ExpressionHelper.BuildPredicate<T>(property, filter.Value));
                }
            }

            foreach (var filter in filterParams.MinFilters ?? new Dictionary<string, string>())
            {
                var property = typeof(T).GetProperty(filter.Key);
                if (property != null)
                {
                    query = query.Where(property.PropertyType == typeof(DateTime)
                        ? ExpressionHelper.BuildDateRangePredicate<T>(property, filter.Value, isMin: true)
                        : ExpressionHelper.BuildRangePredicate<T>(property, filter.Value, isMin: true));
                }
            }

            foreach (var filter in filterParams.MaxFilters ?? new Dictionary<string, string>())
            {
                var property = typeof(T).GetProperty(filter.Key);
                if (property != null)
                {
                    query = query.Where(property.PropertyType == typeof(DateTime)
                        ? ExpressionHelper.BuildDateRangePredicate<T>(property, filter.Value, isMin: false)
                        : ExpressionHelper.BuildRangePredicate<T>(property, filter.Value, isMin: false));
                }
            }

            foreach (var filter in filterParams.MultiValueFilters ?? new Dictionary<string, string>())
            {
                var property = typeof(T).GetProperty(filter.Key);
                if (property != null)
                {
                    string[] values = filter.Value.Split(',');
                    query = query.Where(ExpressionHelper.BuildMultiValuePredicate<T>(property, values));
                }
            }

            if (!string.IsNullOrWhiteSpace(filterParams.SearchTerm))
            {
                query = query.Where(ExpressionHelper.BuildSearchPredicate<T>(filterParams.SearchTerm));
            }

            return query;
        }

        // Apply Pagination Logic
        private async Task<PaginatedResponse<TDto>> ApplyPagination(IQueryable<T> query, FilterParams filterParams)
        {
            var totalRecords = await query.CountAsync();
            var pagedItems = await query
                .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize)
                .ToListAsync();

            var mappedItems = _mapper.Map<List<TDto>>(pagedItems);
            return new PaginatedResponse<TDto>(mappedItems, filterParams.PageNumber, filterParams.PageSize, totalRecords);
        }
    }
}
