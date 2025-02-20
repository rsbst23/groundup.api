using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.Api.Controllers
{
    [Route("api/inventory-categories")]
    [ApiController]
    public class InventoryCategoryController : ControllerBase
    {
        private readonly IInventoryCategoryRepository _inventoryCategoryRepository;

        public InventoryCategoryController(IInventoryCategoryRepository inventoryCategoryRepository)
        {
            _inventoryCategoryRepository = inventoryCategoryRepository;
        }

        // GET: api/inventory-categories
        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryCategoryDto>>> GetInventoryCategories()
        {
            var categories = await _inventoryCategoryRepository.GetAllAsync();
            return Ok(categories);
        }

        // GET: api/inventory-categories/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryCategoryDto>> GetInventoryCategory(int id)
        {
            var category = await _inventoryCategoryRepository.GetByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            return Ok(category);
        }

        // POST: api/inventory-categories
        [HttpPost]
        public async Task<ActionResult<InventoryCategoryDto>> CreateInventoryCategory([FromBody] InventoryCategoryDto inventoryCategoryDto)
        {
            if (inventoryCategoryDto == null)
            {
                return BadRequest("Invalid inventory category data.");
            }

            var createdCategory = await _inventoryCategoryRepository.AddAsync(inventoryCategoryDto);
            return CreatedAtAction(nameof(GetInventoryCategory), new { id = createdCategory.Id }, createdCategory);
        }

        // PUT: api/inventory-categories/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateInventoryCategory(int id, [FromBody] InventoryCategoryDto inventoryCategoryDto)
        {
            if (id != inventoryCategoryDto.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var updatedCategory = await _inventoryCategoryRepository.UpdateAsync(inventoryCategoryDto);
            if (updatedCategory == null)
            {
                return NotFound();
            }

            return NoContent();
        }

        // DELETE: api/inventory-categories/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInventoryCategory(int id)
        {
            var existingCategory = await _inventoryCategoryRepository.GetByIdAsync(id);
            if (existingCategory == null)
            {
                return NotFound();
            }

            await _inventoryCategoryRepository.DeleteAsync(id);
            return NoContent();
        }
    }
}
