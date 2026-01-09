# Entity move notes (Inventory)

We attempted to move `InventoryCategory` / `InventoryItem` / `InventoryAttribute` out of `GroundUp.core` into `GroundUp.Repositories.Inventory` (Option B).

## Why it failed (current solution state)
Today the EF `ApplicationDbContext` still lives in `GroundUp.infrastructure`.

- `GroundUp.Repositories.Inventory` currently references `GroundUp.infrastructure` (to reuse `ApplicationDbContext` + `BaseTenantRepository`).
- If `GroundUp.infrastructure` references `GroundUp.Repositories.Inventory` (so DbContext can see the new entity CLR types), it creates a **project reference cycle**:
  - `GroundUp.Repositories.Inventory -> GroundUp.infrastructure -> GroundUp.Repositories.Inventory`

## Correct sequence
To complete Option B cleanly:
1. Move `ApplicationDbContext` + migrations from `GroundUp.infrastructure` to `GroundUp.Repositories.Core`.
2. Make bounded-context repository projects (e.g. `GroundUp.Repositories.Inventory`) reference `GroundUp.Repositories.Core` (for DbContext).
3. Then move Inventory entities into `GroundUp.Repositories.Inventory/Entities` and update DbContext model discovery.

Until step (1) is done, inventory entities must remain in `GroundUp.core/entities`.
