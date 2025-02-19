using AutoMapper;
using GroundUp.core.interfaces;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.infrastructure.data;
using Microsoft.EntityFrameworkCore;
using GroundUp.infrastructure.utilities;

namespace GroundUp.infrastructure.repositories
{
    public class InventoryItemRepository : IInventoryItemRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;

        public InventoryItemRepository(ApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        // Get All with Pagination
        public async Task<PaginatedResponse<InventoryItemDto>> GetAllAsync(FilterParams filterParams)
        {
            var query = _context.InventoryItems
                .Include(i => i.InventoryCategory)
                .AsQueryable();

            // Apply Dynamic Filters
            if (filterParams.Filters != null)
            {
                foreach (var filter in filterParams.Filters)
                {
                    string propertyName = filter.Key;
                    string filterValue = filter.Value;

                    var property = typeof(InventoryItem).GetProperty(propertyName);
                    if (property != null)
                    {
                        query = query.Where(ExpressionHelper.BuildPredicate<InventoryItem>(property, filterValue));
                    }
                }
            }

            // Apply Pagination
            var totalRecords = await query.CountAsync();
            var pagedItems = await query
                .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize)
                .ToListAsync();

            var mappedItems = _mapper.Map<List<InventoryItemDto>>(pagedItems);
            return new PaginatedResponse<InventoryItemDto>(mappedItems, filterParams.PageNumber, filterParams.PageSize, totalRecords);
        }

        // Get Item by ID
        public async Task<InventoryItemDto?> GetByIdAsync(int id)
        {
            var inventoryItem = await _context.InventoryItems
                .Include(i => i.InventoryCategory)
                .FirstOrDefaultAsync(i => i.Id == id);

            return inventoryItem == null ? null : _mapper.Map<InventoryItemDto>(inventoryItem);
        }

        // Add a New Item
        public async Task<InventoryItemDto> AddAsync(InventoryItemDto inventoryItemDto)
        {
            var entity = _mapper.Map<InventoryItem>(inventoryItemDto);
            _context.InventoryItems.Add(entity);
            await _context.SaveChangesAsync();
            return _mapper.Map<InventoryItemDto>(entity);
        }

        // Update an Existing Item
        public async Task<InventoryItemDto?> UpdateAsync(int id, InventoryItemDto inventoryItemDto)
        {
            var existingEntity = await _context.InventoryItems.FindAsync(id);
            if (existingEntity == null)
            {
                return null;
            }

            _mapper.Map(inventoryItemDto, existingEntity);
            await _context.SaveChangesAsync();
            return _mapper.Map<InventoryItemDto>(existingEntity);
        }

        // Delete an Item
        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.InventoryItems.FindAsync(id);
            if (entity == null)
            {
                return false;
            }

            _context.InventoryItems.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
