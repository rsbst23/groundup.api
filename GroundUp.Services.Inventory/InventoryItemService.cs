using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core.dtos;
using GroundUp.Services.Inventory.Interfaces;

namespace GroundUp.Services.Inventory;

public sealed class InventoryItemService : IInventoryItemService
{
    private readonly IInventoryItemRepository _repo;

    public InventoryItemService(IInventoryItemRepository repo)
    {
        _repo = repo;
    }

    public Task<OperationResult<PaginatedData<InventoryItemDto>>> GetAllAsync(FilterParams filterParams)
        => _repo.GetAllAsync(filterParams);

    public Task<OperationResult<InventoryItemDto>> GetByIdAsync(int id)
        => _repo.GetByIdAsync(id);

    public Task<OperationResult<InventoryItemDto>> AddAsync(InventoryItemDto dto)
        => _repo.AddAsync(dto);

    public Task<OperationResult<InventoryItemDto>> UpdateAsync(int id, InventoryItemDto dto)
        => _repo.UpdateAsync(id, dto);

    public Task<OperationResult<bool>> DeleteAsync(int id)
        => _repo.DeleteAsync(id);
}
