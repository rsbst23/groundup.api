using GroundUp.core.dtos;

namespace GroundUp.core.interfaces
{
    public interface IInventoryCategoryRepository
    {
        Task<IEnumerable<InventoryCategoryDto>> GetAllAsync();
        Task<InventoryCategoryDto?> GetByIdAsync(int id);
        Task<InventoryCategoryDto> AddAsync(InventoryCategoryDto inventoryCategory);
        Task<InventoryCategoryDto?> UpdateAsync(InventoryCategoryDto inventoryCategory);
        Task<bool> DeleteAsync(int id);
    }
}
