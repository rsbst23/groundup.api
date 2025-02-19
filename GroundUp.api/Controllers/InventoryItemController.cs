using GroundUp.core.interfaces;
using GroundUp.core.dtos;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.Api.Controllers
{
    [Route("api/inventory-items")]
    [ApiController]
    public class InventoryItemController : ControllerBase
    {
        private readonly IInventoryItemRepository _inventoryItemRepository;

        public InventoryItemController(IInventoryItemRepository inventoryItemRepository)
        {
            _inventoryItemRepository = inventoryItemRepository;
        }

        // GET: api/inventory-items (Paginated)
        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<InventoryItemDto>>> GetInventoryItems([FromQuery] FilterParams filterParams)
        {
            var items = await _inventoryItemRepository.GetAllAsync(filterParams);
            return Ok(items);
        }

        // GET: api/inventory-items/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryItemDto>> GetInventoryItem(int id)
        {
            var item = await _inventoryItemRepository.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            return Ok(item);
        }

        // POST: api/inventory-items
        [HttpPost]
        public async Task<ActionResult<InventoryItemDto>> CreateInventoryItem([FromBody] InventoryItemDto inventoryItemDto)
        {
            if (inventoryItemDto == null)
            {
                return BadRequest("Invalid inventory item data.");
            }

            var createdItem = await _inventoryItemRepository.AddAsync(inventoryItemDto);
            return CreatedAtAction(nameof(GetInventoryItem), new { id = createdItem.Id }, createdItem);
        }
    }
}
