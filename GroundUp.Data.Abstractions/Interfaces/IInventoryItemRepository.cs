using GroundUp.Core.dtos;

namespace GroundUp.Data.Abstractions.Interfaces;

public interface IInventoryItemRepository
{
    Task<OperationResult<PaginatedData<InventoryItemDto>>> GetAllAsync(FilterParams filterParams);
    Task<OperationResult<InventoryItemDto>> GetByIdAsync(int id);
    Task<OperationResult<InventoryItemDto>> AddAsync(InventoryItemDto dto);
    Task<OperationResult<InventoryItemDto>> UpdateAsync(int id, InventoryItemDto dto);
    Task<OperationResult<bool>> DeleteAsync(int id);
}
