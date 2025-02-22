using GroundUp.core.dtos;

namespace GroundUp.core.interfaces
{
    public interface IInventoryItemRepository
    {
        Task<ApiResponse<PaginatedData<InventoryItemDto>>> GetAllAsync(FilterParams filterParams);
        Task<ApiResponse<InventoryItemDto>> GetByIdAsync(int id);
        Task<ApiResponse<InventoryItemDto>> AddAsync(InventoryItemDto dto);
        Task<ApiResponse<InventoryItemDto>> UpdateAsync(int id, InventoryItemDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
    }
}
