using GroundUp.Core;
using GroundUp.Core.dtos;
using GroundUp.Services.Inventory.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GroundUp.Api.Controllers
{
    [Route("api/inventory-categories")]
    [ApiController]
    public class InventoryCategoryController : ControllerBase
    {
        private readonly IInventoryCategoryService _inventoryCategoryService;

        public InventoryCategoryController(IInventoryCategoryService inventoryCategoryService)
        {
            _inventoryCategoryService = inventoryCategoryService;
        }

        // GET: api/inventory-categories (Paginated)
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedData<InventoryCategoryDto>>>> Get([FromQuery] FilterParams filterParams)
        {
            var result = await _inventoryCategoryService.GetAllAsync(filterParams);
            return StatusCode(result.StatusCode, ToApiResponse(result));
        }

        // GET: api/inventory-categories/export
        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] string format = "csv", [FromQuery] string? sortBy = null, [FromQuery] bool exportAll = true, [FromQuery] FilterParams? filterParams = null)
        {
            filterParams ??= new FilterParams();

            if (exportAll)
            {
                filterParams.PageSize = 10000;
                filterParams.PageNumber = 1;
            }

            if (!string.IsNullOrEmpty(sortBy))
            {
                filterParams.SortBy = sortBy;
            }

            var result = await _inventoryCategoryService.ExportAsync(filterParams, format);

            if (!result.Success)
            {
                return StatusCode(result.StatusCode, ToApiResponse(result));
            }

            string contentType;
            string filename;

            switch (format.ToLowerInvariant())
            {
                case "xlsx":
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    filename = $"inventory-categories-{DateTime.Now:yyyy-MM-dd}.xlsx";
                    break;
                case "json":
                    contentType = "application/json";
                    filename = $"inventory-categories-{DateTime.Now:yyyy-MM-dd}.json";
                    break;
                case "csv":
                default:
                    contentType = "text/csv";
                    filename = $"inventory-categories-{DateTime.Now:yyyy-MM-dd}.csv";
                    break;
            }

            Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{filename}\"");
            return File(result.Data!, contentType, filename);
        }

        // GET: api/inventory-categories/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<InventoryCategoryDto>>> GetById(int id)
        {
            var result = await _inventoryCategoryService.GetByIdAsync(id);
            return StatusCode(result.StatusCode, ToApiResponse(result));
        }

        // POST: api/inventory-categories
        [HttpPost]
        public async Task<ActionResult<ApiResponse<InventoryCategoryDto>>> Create([FromBody] InventoryCategoryDto inventoryCategoryDto)
        {
            if (inventoryCategoryDto == null)
            {
                return BadRequest(new ApiResponse<InventoryCategoryDto>(
                    default!,
                    false,
                    "Invalid inventory category data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            var result = await _inventoryCategoryService.AddAsync(inventoryCategoryDto);
            return StatusCode(result.StatusCode, ToApiResponse(result));
        }

        // PUT: api/inventory-categories/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<InventoryCategoryDto>>> Update(int id, [FromBody] InventoryCategoryDto inventoryCategoryDto)
        {
            if (inventoryCategoryDto == null)
            {
                return BadRequest(new ApiResponse<InventoryCategoryDto>(
                    default!,
                    false,
                    "Invalid inventory category data.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed
                ));
            }

            if (id != inventoryCategoryDto.Id)
            {
                return BadRequest(new ApiResponse<InventoryCategoryDto>(
                    default!,
                    false,
                    "ID mismatch.",
                    null,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.IdMismatch
                ));
            }

            var result = await _inventoryCategoryService.UpdateAsync(id, inventoryCategoryDto);
            return StatusCode(result.StatusCode, ToApiResponse(result));
        }

        // DELETE: api/inventory-categories/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _inventoryCategoryService.DeleteAsync(id);
            return StatusCode(result.StatusCode, ToApiResponse(result));
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