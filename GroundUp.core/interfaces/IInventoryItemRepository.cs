using GroundUp.core.dtos;

namespace GroundUp.core.interfaces
{
    public interface IInventoryItemRepository
    {
        Task<OperationResult<PaginatedData<InventoryItemDto>>> GetAllAsync(FilterParams filterParams);
        Task<OperationResult<InventoryItemDto>> GetByIdAsync(int id);
        Task<OperationResult<InventoryItemDto>> AddAsync(InventoryItemDto dto);
        Task<OperationResult<InventoryItemDto>> UpdateAsync(int id, InventoryItemDto dto);
        Task<OperationResult<bool>> DeleteAsync(int id);
    }
}
