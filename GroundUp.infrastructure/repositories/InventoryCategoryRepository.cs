using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.Infrastructure.Repositories
{
    public class InventoryCategoryRepository : IInventoryCategoryRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;

        public InventoryCategoryRepository(ApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        // Get All Categories
        public async Task<IEnumerable<InventoryCategoryDto>> GetAllAsync()
        {
            var categories = await _context.InventoryCategories.ToListAsync();
            return _mapper.Map<IEnumerable<InventoryCategoryDto>>(categories);
        }

        // Get Category by ID
        public async Task<InventoryCategoryDto?> GetByIdAsync(int id)
        {
            var category = await _context.InventoryCategories.FindAsync(id);
            return category == null ? null : _mapper.Map<InventoryCategoryDto>(category);
        }

        // Add a New Category
        public async Task<InventoryCategoryDto> AddAsync(InventoryCategoryDto inventoryCategoryDto)
        {
            var entity = _mapper.Map<InventoryCategory>(inventoryCategoryDto);
            _context.InventoryCategories.Add(entity);
            await _context.SaveChangesAsync();
            return _mapper.Map<InventoryCategoryDto>(entity);
        }

        // Update an Existing Category
        public async Task<InventoryCategoryDto?> UpdateAsync(InventoryCategoryDto inventoryCategoryDto)
        {
            var existingEntity = await _context.InventoryCategories.FindAsync(inventoryCategoryDto.Id);
            if (existingEntity == null)
            {
                return null;
            }

            _mapper.Map(inventoryCategoryDto, existingEntity); // Update properties
            await _context.SaveChangesAsync();

            return _mapper.Map<InventoryCategoryDto>(existingEntity);
        }

        // Delete a Category
        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.InventoryCategories.FindAsync(id);
            if (entity == null)
            {
                return false;
            }

            _context.InventoryCategories.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
