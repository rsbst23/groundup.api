using GroundUp.core.interfaces;
using GroundUp.core.dtos;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

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
        public async Task<ActionResult<ApiResponse<PaginatedData<InventoryItemDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _inventoryItemRepository.GetAllAsync(filterParams);
            return Ok(result);
        }

        // GET: api/inventory-items/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<InventoryItemDto>>> GetById(int id)
        {
            var result = await _inventoryItemRepository.GetByIdAsync(id);
            if (!result.Success)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        // POST: api/inventory-items
        [HttpPost]
        public async Task<ActionResult<ApiResponse<InventoryItemDto>>> Create([FromBody] InventoryItemDto inventoryItemDto)
        {
            if (inventoryItemDto == null)
            {
                return BadRequest(new ApiResponse<InventoryItemDto>(default, false, "Invalid inventory category data."));
            }

            var result = await _inventoryItemRepository.AddAsync(inventoryItemDto);
            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }

        // PUT: api/inventory-items/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] InventoryItemDto inventoryItemDto)
        {
            if (id != inventoryItemDto.Id)
            {
                return BadRequest(new ApiResponse<InventoryItemDto>(default, false, "ID mismatch."));
            }

            var result = await _inventoryItemRepository.UpdateAsync(id, inventoryItemDto);
            if (!result.Success)
            {
                return NotFound(result);
            }

            return NoContent();
        }

        // DELETE: api/inventory-items/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _inventoryItemRepository.DeleteAsync(id);
            if (!result.Success)
            {
                return NotFound(result);
            }

            return NoContent();
        }
    }
}
