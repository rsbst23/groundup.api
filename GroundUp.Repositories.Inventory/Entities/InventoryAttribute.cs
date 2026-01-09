using System.ComponentModel.DataAnnotations;
using GroundUp.core.entities;

namespace GroundUp.Repositories.Inventory.Entities;

public class InventoryAttribute : ITenantEntity
{
    public int Id { get; set; }

    public int InventoryItemId { get; set; }

    [Required]
    [MaxLength(255)]
    public required string FieldName { get; set; }

    public string? FieldValue { get; set; }

    public int TenantId { get; set; }

    public InventoryItem InventoryItem { get; set; } = default!;
}
