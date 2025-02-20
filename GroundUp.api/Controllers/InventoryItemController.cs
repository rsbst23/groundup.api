using GroundUp.core.interfaces;
using GroundUp.core.dtos;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.api.Controllers
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
        public async Task<ActionResult<PaginatedResponse<InventoryItemDto>>> Get([FromQuery] FilterParams filterParams)
        {
            var items = await _inventoryItemRepository.GetAllAsync(filterParams);
            return Ok(items);
        }

        // GET: api/inventory-items/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryItemDto>> GetById(int id)
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
        public async Task<ActionResult<InventoryItemDto>> Create([FromBody] InventoryItemDto inventoryItemDto)
        {
            if (inventoryItemDto == null)
            {
                return BadRequest("Invalid inventory item data.");
            }

            var createdItem = await _inventoryItemRepository.AddAsync(inventoryItemDto);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, createdItem);
        }

        // PUT: api/inventory-items/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] InventoryItemDto inventoryItemDto)
        {
            if (id != inventoryItemDto.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var updatedItem = await _inventoryItemRepository.UpdateAsync(id, inventoryItemDto);
            if (updatedItem == null)
            {
                return NotFound();
            }

            return NoContent();
        }

        // DELETE: api/inventory-items/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existingItem = await _inventoryItemRepository.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            await _inventoryItemRepository.DeleteAsync(id);
            return NoContent();
        }
    }
}
