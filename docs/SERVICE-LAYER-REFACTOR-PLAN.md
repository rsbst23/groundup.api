# GroundUp Service Layer Refactor Plan (Option B)

> **Goal**: Enforce a hard architecture rule:
>
> - `Controllers` call **Services only**
> - `Services` call **Repositories only** (no direct `DbContext`)
> - `Repositories` do **database-only** work
> - **Business logic** lives in Services
> - **Authorization** is enforced at **Service method** boundary
>
> **Option B**: Move persistence entities into bounded-context repository projects (not in `GroundUp.core`).

---

## 1. Current state (observed)

### Controllers call repositories directly
Example: `GroundUp.api/Controllers/InventoryCategoryController.cs` injects `IInventoryCategoryRepository` and calls repository methods directly.

### Service layer exists but is inconsistent
Auth stack uses services (e.g., `IAuthFlowService`), but some service implementations (e.g., `AuthFlowService`) call `ApplicationDbContext` directly and are not consistently designed.

### Permissions are repository-interface annotations
Repository interfaces include `[RequiresPermission("...")]`. Enforcement happens via Castle DynamicProxy interceptors (`PermissionInterceptor`).

---

## 2. Target architecture (end-state)

### Layer boundaries
- API layer (`GroundUp.api`): controllers, HTTP concerns only (status codes, cookies, headers).
- Service layer (`GroundUp.Services.*`): orchestration + business logic; depends on repositories + other services; no EF access.
- Repository layer (`GroundUp.Repositories.*`): EF Core + database interaction only; implements repository interfaces.
- Shared contracts (`GroundUp.core`): DTOs, result primitives, interfaces, security attributes, validation, enums.

### Authorization
- `[RequiresPermission]` moves from **repository interfaces** to **service interfaces**.
- Services are proxied/intercepted for permission enforcement.
- Repository methods become permission-agnostic.

### Consistent results
- Adopt a single cross-layer result type (recommended): `OperationResult<T>` in `GroundUp.core`.
- Repositories return `OperationResult<T>`; Services return `OperationResult<T>`; Controllers map to `ApiResponse<T>`.
  - (You *can* also migrate controllers to return `OperationResult<T>` directly, but keeping `ApiResponse<T>` at HTTP boundary is typical.)

### No `DbContext` in Services
Transaction and retry logic is provided via `IUnitOfWork` in core, implemented in repository layer.

---

## 3. Proposed bounded contexts (initial)

You indicated:
- **Core service context**: Auth / Identity / Tenant (foundational to the app)
- **Inventory context**: sample reference implementation

Start with these two to establish the pattern:

1) `Core` context
- Auth flows (callback handling, tenant selection, enterprise signup)
- Identity provider integration (Keycloak)
- Tenant setup / SSO settings

2) `Inventory` context
- Inventory categories
- Inventory items

Later contexts can be split out when needed (roles/policies/permissions could be part of Core initially).

---

## 4. New solution/project structure

### Keep
- `GroundUp.api` (API)
- `GroundUp.core` (shared contracts)

### Add (initial)
- `GroundUp.Services.Core` (services: auth/tenant/identity)
- `GroundUp.Services.Inventory`
- `GroundUp.Repositories.Core`
- `GroundUp.Repositories.Inventory`

### Database decision (CONFIRMED)
? **Single database + single EF Core `DbContext`**.

- `ApplicationDbContext` and EF migrations will live in **one** repository project: **`GroundUp.Repositories.Core`**.
- Other repository projects (e.g., `GroundUp.Repositories.Inventory`) will:
  - reference `GroundUp.Repositories.Core` (to access `ApplicationDbContext`)
  - add their entity types and configure mappings via `IEntityTypeConfiguration<T>` and/or `DbContext` model-building hooks.

> This gives you modular repository projects while keeping migrations and DB configuration centralized.

---

## 5. Entity ownership (Option B)

### Entity placement
- Inventory entities (`InventoryCategory`, `InventoryItem`, etc.) move out of `GroundUp.core/entities` into `GroundUp.Repositories.Inventory/Entities`.
- Core entities (Tenant, User, Role, Policy, Permission, etc.) move into `GroundUp.Repositories.Core/Entities`.

### Who references entities?
- Repositories: yes.
- Services: ideally no (prefer DTOs). If needed, only via repository return types (but the goal is DTO boundary).
- Controllers: no.

### EF Core ownership rule
- `ApplicationDbContext` (in `GroundUp.Repositories.Core`) must reference *all* entity CLR types.
- Entities living in other projects (like Inventory) is fine as long as:
  - `GroundUp.Repositories.Core` references those projects, or
  - `ApplicationDbContext` discovers configurations via assembly scanning.

---

## 6. Key technical decisions to implement

### 6.1 Introduce `OperationResult<T>`
Create in `GroundUp.core`:
- `GroundUp.core/dtos/OperationResult.cs` (or a `core/results` folder)
- Mimic the fields you already use in `ApiResponse<T>`: `Success`, `Message`, `Errors`, `StatusCode`, `ErrorCode`, and `Data`.

### 6.2 Add `IUnitOfWork`
Create in `GroundUp.core/interfaces`:
- `IUnitOfWork` exposing `ExecuteInTransactionAsync`.

Implement in repository project using EF:
- Use `DbContext.Database.CreateExecutionStrategy()` and `BeginTransactionAsync()`.

### 6.3 Service interfaces + implementations
- Service interfaces in `GroundUp.core/interfaces` (keeps API layer depending only on core) **or** in each service project.
- Implementations in `GroundUp.Services.*`.

### 6.4 Permission enforcement at service boundary
- Move `[RequiresPermission]` attributes to service interfaces.
- Update DI to proxy services (Castle) with `PermissionInterceptor`.
- Remove/stop proxying repositories for permission enforcement.

---

## 7. Phased migration plan (execution)

### Phase 1 — Create project skeletons and wire DI
**Deliverables**
- New projects: `GroundUp.Services.Core`, `GroundUp.Services.Inventory`, `GroundUp.Repositories.Core`, `GroundUp.Repositories.Inventory`.
- Project references updated:
  - `GroundUp.api` references `GroundUp.core` and service projects.
  - Service projects reference `GroundUp.core` and corresponding repository projects.
  - Repository projects reference `GroundUp.core` and EF dependencies.

**DI changes**
- Move service registrations out of `GroundUp.infrastructure/extensions/ServiceCollectionExtensions.cs` into a new registration surface in service/repository projects (or create new extension methods) so API can call something like:
  - `AddCoreServices()`
  - `AddInventoryServices()`
  - `AddCoreRepositories()`
  - `AddInventoryRepositories()`

### Phase 2 — Inventory as the reference implementation (establish the pattern)
**Goal**: migrate inventory end-to-end first.

**Work items**
1) Create `IInventoryCategoryService`, `IInventoryItemService` in core.
   - Mirror current controller surface.
   - Add `[RequiresPermission]` attributes on service methods.
2) Implement services in `GroundUp.Services.Inventory`.
   - Pure orchestration, mostly pass-through for CRUD.
3) Move inventory repositories into `GroundUp.Repositories.Inventory`.
4) Move inventory entities into `GroundUp.Repositories.Inventory/Entities`.
5) Update `ApplicationDbContext` (now in `GroundUp.Repositories.Core`) to include inventory entities/configurations.
   - Prefer `ApplyConfigurationsFromAssembly(...)` for the inventory repo assembly.
6) Update controllers:
   - `InventoryCategoryController` injects `IInventoryCategoryService` only.
   - `InventoryItemController` injects `IInventoryItemService` only.
7) Update integration tests to use the API unchanged (ideally no test changes beyond DI wiring).

**Exit criteria**
- Build succeeds.
- Inventory integration tests pass.
- No controller references any `*Repository` interface.

### Phase 3 — Core context (Auth/Identity/Tenant)
**Goal**: normalize services and remove direct `DbContext` usage.

**Work items**
1) Create coherent service API:
   - `IAuthFlowService` (already exists—may need redesign)
   - `IAuthSessionService` (new: `SetTenant`, token issuance)
   - `ITenantService`/`ITenantSsoService` (normalize)
   - `IEnterpriseSignupService` (exists)
2) Update implementations in `GroundUp.Services.Core`.
3) Refactor `AuthFlowService`:
   - Replace direct `_dbContext` usage with repository calls + `IUnitOfWork` transactions.
   - Push any EF-specific queries into repositories.
4) Move relevant entities and repositories into `GroundUp.Repositories.Core`.
5) Move permissions annotations to service methods.

### Phase 4 — Permissions/roles/policies
Depending on desired boundaries:
- Keep in Core bounded context initially.
- Move repository code to `GroundUp.Repositories.Core` and service code to `GroundUp.Services.Core`.

### Phase 5 — Remove `GroundUp.infrastructure` project
Once all code is migrated:
- Delete or repurpose `GroundUp.infrastructure`.
- Ensure all DI extension methods live in service/repository projects.

---

## 8. Mechanical file-by-file inventory (initial focus)

### Inventory (current files)

Controllers:
- `GroundUp.api/Controllers/InventoryCategoryController.cs`
- `GroundUp.api/Controllers/InventoryItemController.cs`

Core DTOs / Interfaces:
- `GroundUp.core/dtos/InventoryCategoryDto.cs`
- `GroundUp.core/dtos/InventoryItemDto.cs`
- `GroundUp.core/interfaces/IInventoryCategoryRepository.cs`
- `GroundUp.core/interfaces/IInventoryItemRepository.cs`

Entities (move to inventory repo project under Option B):
- `GroundUp.core/entities/InventoryCategory.cs`
- `GroundUp.core/entities/InventoryItem.cs`

Repository implementations:
- `GroundUp.infrastructure/repositories/InventoryCategoryRepository.cs`
- `GroundUp.infrastructure/repositories/InventoryItemRepository.cs`
- `GroundUp.infrastructure/repositories/BaseRepository.cs`
- `GroundUp.infrastructure/repositories/BaseTenantRepository.cs`

Data:
- `GroundUp.infrastructure/data/ApplicationDbContext.cs`

Integration tests:
- `GroundUp.api.Tests.Integration/InventoryCategoryIntegrationTest.cs`
- `GroundUp.api.Tests.Integration/InventoryItemIntegrationTests.cs`

---

## 9. Open decisions (updated)

1) ? **Single DB context / migrations**
   - Confirmed: One `ApplicationDbContext` and one migrations assembly in `GroundUp.Repositories.Core`.

2) **Where do repository interfaces live?**
   - Keep in `GroundUp.core/interfaces` (API depends only on core; services depend on core; repositories implement core interfaces).

3) **Result type naming**
   - Use `OperationResult<T>` (recommended) and map to `ApiResponse<T>` in controllers.

4) **Permission model**
   - Attribute-driven interception at service interface boundary (recommended).

---

## 10. Immediate next step after this plan is approved

Implement **Phase 1 + Phase 2 (Inventory)** first:
- Create new projects
- Add `OperationResult<T>` + `IUnitOfWork`
- Create inventory services
- Move inventory entities and repos
- Move `ApplicationDbContext` (+ migrations) into `GroundUp.Repositories.Core`
- Update DI + controllers
- Run build + integration tests

---

## Appendix: Why policies/handlers are "bigger change" (informational)

ASP.NET authorization policies/handlers typically mean:
- Define permissions as policies: `options.AddPolicy("inventory.view", policy => policy.Requirements.Add(new PermissionRequirement("inventory.view")))`
- Implement `IAuthorizationHandler` to check DB-backed permissions.
- Enforce via `[Authorize(Policy = "inventory.view")]` on controllers/actions or by calling `IAuthorizationService.AuthorizeAsync()` from services.

Pros:
- Standard ASP.NET Core pipeline integration.

Cons (relative to your desired architecture):
- Ties permissions to HTTP layer unless you manually invoke authorization from services.
- Your current attribute+interceptor approach cleanly enforces at the service boundary without MVC coupling.
