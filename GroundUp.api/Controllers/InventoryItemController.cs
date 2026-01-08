using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.api.Controllers
{
    [Route("api/inventory-items")]
    [ApiController]
    public class InventoryItemController : ControllerBase
    {
        private readonly IInventoryItemService _inventoryItemService;

        public InventoryItemController(IInventoryItemService inventoryItemService)
        {
            _inventoryItemService = inventoryItemService;
        }

        // GET: api/inventory-items (Paginated)
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<InventoryItemDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _inventoryItemService.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, ToApiResponse(result));
        }

        // GET: api/inventory-items/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<InventoryItemDto>>> GetById(int id)
        {
            var result = await _inventoryItemService.GetByIdAsync(id);
            return StatusCode(result.StatusCode, ToApiResponse(result));
        }

        // POST: api/inventory-items
        [HttpPost]
        public async Task<ActionResult<ApiResponse<InventoryItemDto>>> Create([FromBody] InventoryItemDto inventoryItemDto)
        {
            if (inventoryItemDto == null)
            {
                return BadRequest(new ApiResponse<InventoryItemDto>(default!, false, "Invalid inventory item data."));
            }

            var result = await _inventoryItemService.AddAsync(inventoryItemDto);

            if (!result.Success)
            {
                return StatusCode(result.StatusCode, ToApiResponse(result));
            }

            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, ToApiResponse(result));
        }

        // PUT: api/inventory-items/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] InventoryItemDto inventoryItemDto)
        {
            if (id != inventoryItemDto.Id)
            {
                return BadRequest(new ApiResponse<InventoryItemDto>(default!, false, "ID mismatch."));
            }

            var result = await _inventoryItemService.UpdateAsync(id, inventoryItemDto);

            if (!result.Success)
            {
                return StatusCode(result.StatusCode, ToApiResponse(result));
            }

            return NoContent();
        }

        // DELETE: api/inventory-items/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _inventoryItemService.DeleteAsync(id);

            if (!result.Success)
            {
                return StatusCode(result.StatusCode, ToApiResponse(result));
            }

            return NoContent();
        }

        private static ApiResponse<T> ToApiResponse<T>(OperationResult<T> result)
            => new(
                result.Data!,
                result.Success,
                result.Message,
                result.Errors,
                result.StatusCode,
                result.ErrorCode
            );
    }
}
