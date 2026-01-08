using GroundUp.core.dtos;
using GroundUp.core.security;

namespace GroundUp.core.interfaces;

public interface IInventoryItemService
{
    [RequiresPermission("inventory.view")]
    Task<OperationResult<PaginatedData<InventoryItemDto>>> GetAllAsync(FilterParams filterParams);

    [RequiresPermission("inventory.view")]
    Task<OperationResult<InventoryItemDto>> GetByIdAsync(int id);

    [RequiresPermission("inventory.create")]
    Task<OperationResult<InventoryItemDto>> AddAsync(InventoryItemDto dto);

    [RequiresPermission("inventory.update")]
    Task<OperationResult<InventoryItemDto>> UpdateAsync(int id, InventoryItemDto dto);

    [RequiresPermission("inventory.delete")]
    Task<OperationResult<bool>> DeleteAsync(int id);
}
