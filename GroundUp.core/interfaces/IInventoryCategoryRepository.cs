using GroundUp.core.dtos;

namespace GroundUp.core.interfaces
{
    public interface IInventoryCategoryRepository
    {
        Task<PaginatedResponse<InventoryCategoryDto>> GetAllAsync(FilterParams filterParams);
        Task<InventoryCategoryDto?> GetByIdAsync(int id);
        Task<InventoryCategoryDto> AddAsync(InventoryCategoryDto inventoryCategory);
        Task<InventoryCategoryDto?> UpdateAsync(int id, InventoryCategoryDto inventoryCategory);
        Task<bool> DeleteAsync(int id);
    }
}
