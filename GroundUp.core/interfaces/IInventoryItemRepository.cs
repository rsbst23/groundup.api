using GroundUp.core.dtos;

namespace GroundUp.core.interfaces
{
    public interface IInventoryItemRepository
    {
        Task<PaginatedResponse<InventoryItemDto>> GetAllAsync(FilterParams filterParams);
        Task<InventoryItemDto?> GetByIdAsync(int id);
        Task<InventoryItemDto> AddAsync(InventoryItemDto inventoryItem);
        Task<InventoryItemDto?> UpdateAsync(int id, InventoryItemDto inventoryItem);
        Task<bool> DeleteAsync(int id);
    }
}
