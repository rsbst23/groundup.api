# GroundUp Refactor + Packaging Plan (Service Layer + Target Project Structure)

> **Single source of truth** for the GroundUp foundational refactor.
> This merges:
> 1) the **service-layer refactor plan** (controllers → services → data) and
> 2) the **target solution structure** needed to run GroundUp as an application *and* later ship it as NuGet packages.
>
> **Guiding principles**
> - API/Controller layer must not directly depend on the data projects.
> - Business logic lives in services.
> - Data layer is EF Core + migrations + repository implementations.
> - Composition root (host) wires DI + connection strings + migrations.
>
> **New hardening decision (important)**
> - We will **not** keep repository interfaces in `GroundUp.Core`.
> - We will introduce `GroundUp.Data.Abstractions` so `GroundUp.Api` cannot even *compile* against `I*Repository` / `IUnitOfWork`.

---

## 0. Architecture rules (hard boundaries)

**Goal**: enforce a hard architecture rule:

- `Controllers` call **Services only**
- `Services` call **Repositories only** (no direct `DbContext`)
- `Repositories` do **database-only** work
- **Business logic** lives in Services
- **Authorization** is enforced at the **Service method boundary**

**Compile-time enforcement**
- `GroundUp.Api` must not reference `GroundUp.Data.Abstractions`.
- Therefore controllers cannot reference `I*Repository` / `IUnitOfWork`.

---

## 1. Target end-state: projects, responsibilities, and references

> This section defines the stable target. We will migrate incrementally while keeping the solution buildable.

### 1.1 GroundUp projects (in this repo)

| Project | Packaged? | Responsibility | References |
|---|---:|---|---|
| `GroundUp.Core` (currently `GroundUp.Core`) | ✅ | **API-safe** shared contracts: DTOs, service interfaces (`I*Service`), attributes (`RequiresPermission`), enums, shared result primitives | None (or minimal) |
| `GroundUp.Data.Abstractions` *(new)* | ✅ | **Data-boundary contracts only**: repository interfaces (`I*Repository`), `IUnitOfWork`, other persistence abstractions | `GroundUp.Core` |
| `GroundUp.Api` (currently `GroundUp.Api`) | ✅ | Controller layer + HTTP concerns (status codes, cookies, headers), middleware, swagger helpers | `GroundUp.Core`, `GroundUp.Services.Core` *(no data abstractions, no data impl)* |
| `GroundUp.Services.Core` | ✅ | Core service layer (auth/tenant/roles/permissions/etc.). Orchestration + business logic. Depends on **repository interfaces** only. | `GroundUp.Core`, `GroundUp.Data.Abstractions` |
| `GroundUp.Data.Core` (currently `GroundUp.Repositories.Core`) | ✅ | EF Core persistence for core domain: `ApplicationDbContext`, migrations, `UnitOfWork` implementation, repository implementations | `GroundUp.Core`, `GroundUp.Data.Abstractions` + EF packages |
| `GroundUp.Sample` | ❌ | Runnable host/composition root for this repo: DI wiring, connection strings, running migrations, hosting swagger/UI | `GroundUp.Api`, `GroundUp.Services.Core`, `GroundUp.Data.Core` |

> Inventory projects are **temporary** and will later be **removed or moved** into a sample app.

#### Notes on naming
- We are standardizing on **`Data`** naming for the EF/persistence implementation project: `GroundUp.Data.Core`.
- `GroundUp.Data.Abstractions` is named explicitly to communicate the boundary and prevent accidental API references.

### 1.2 FutureApp structure (consumer)

| FutureApp project | Uses | References |
|---|---|---|
| `FutureApp.Api` (host) | Hosts GroundUp endpoints + config + optionally runs migrations | NuGets: `GroundUp.Api`, `GroundUp.Services.Core`, `GroundUp.Data.Core` |
| `FutureApp.Services` | App-specific services; can call GroundUp services | `GroundUp.Core` (and optionally `GroundUp.Services.Core`) |
| `FutureApp.Data` | App-specific persistence | Independent (or integrates with GroundUp DB by choice) |

**Key point**:
- `GroundUp.Services.Core` does **not** reference `GroundUp.Data.Core`.
- `GroundUp.Api` does **not** reference `GroundUp.Data.Abstractions` or `GroundUp.Data.Core`.
- The host wires concrete implementations via DI.

---

## 2. Composition root strategy (how things get wired)

### 2.1 Why we need `GroundUp.Sample`
We want:
- `GroundUp.Api` to be repo-free (prevents future controller misuse)
- **but** still be able to run GroundUp locally (Swagger/UI) and apply migrations.

Therefore:
- `GroundUp.Sample` is the startup project for the repo.
- It references `GroundUp.Data.Core` and calls data/service registration methods.
- It loads controllers from `GroundUp.Api` (MVC application parts) and hosts Swagger.

### 2.2 How services talk to repositories without a project reference to the EF project

- `GroundUp.Data.Abstractions` defines `IUserRepository`, `ITenantRepository`, `IUnitOfWork`, etc.
- `GroundUp.Services.Core` constructors take those interfaces.
- `GroundUp.Data.Core` provides concrete implementations (e.g., `UserRepository : IUserRepository`).
- The host (`GroundUp.Sample` / `FutureApp.Api`) registers:
  - `services.AddScoped<IUserRepository, UserRepository>();`
  - `services.AddScoped<IUserService, UserService>();`

At runtime, DI injects the concrete repo implementation into the service via the interface.

### 2.3 Packaging expectation (NuGet consumption)
It is normal for a consuming app to do a small amount of startup wiring. The packages should provide *thin* integration entry points.

- `GroundUp.Data.Core` should expose `IServiceCollection` extensions to register:
  - `DbContext`
  - repository implementations
  - `UnitOfWork`
- `GroundUp.Services.Core` should expose `IServiceCollection` extensions to register services.
- `GroundUp.Data.Core` may expose an **optional** migration helper (e.g., `app.MigrateGroundUpCoreDatabase()`), but hosts may choose CI/CD migrations instead.

---

## 3. Current state (observed problems)

### 3.1 Controllers calling repositories directly
Example: `InventoryCategoryController` historically injected repositories directly.

### 3.2 Service layer exists but is inconsistent
Auth stack uses services (e.g., `IAuthFlowService`), but some services historically used `DbContext` directly.

### 3.3 Permissions enforced at repository layer
Repository interfaces/implementations contain `[RequiresPermission]` and are proxied.

---

## 4. Authorization model (target)

- Move `[RequiresPermission]` from repository boundary to **service boundary**.
- Services are proxied/intercepted for permission enforcement.
- Repository methods become permission-agnostic.

---

## 5. Result shape model (target)

- Prefer a single cross-layer result type: `OperationResult<T>` in `GroundUp.Core`.
- Repositories return `OperationResult<T>`.
- Services return `OperationResult<T>`.
- Controllers map `OperationResult<T>` → `ApiResponse<T>`.

---

## 6. EF + transactions (target)

- No EF usage in services.
- Transaction/retry logic provided via `IUnitOfWork` (**in `GroundUp.Data.Abstractions`**), implemented in `GroundUp.Data.Core`.

---

## 7. Execution roadmap (incremental, keep build runnable)

> We will migrate in small slices and keep the solution compiling/runnable after each step.

### Phase A — Establish the final project *shape* (minimal behavior change)
1) Add `GroundUp.Sample` (startup project for the repo)
   - Owns `Program.cs`, connection strings, migrations, swagger host
   - Loads controllers from `GroundUp.Api`
2) Ensure `GroundUp.Api` has **no reference** to `GroundUp.Data.Abstractions` or `GroundUp.Data.Core`
3) Introduce `GroundUp.Data.Abstractions`
   - Move `I*Repository` and `IUnitOfWork` out of `GroundUp.Core`
4) Ensure DI registrations live on the correct side:
   - `GroundUp.Services.Core` registers services
   - `GroundUp.Data.Core` registers DbContext + repos + UoW implementations
   - `GroundUp.Sample` composes both

**Exit criteria**
- Build succeeds
- Running `GroundUp.Sample` exposes swagger and endpoints

### Phase B — Finish controller-to-service boundary
- Audit controllers: ensure no controller injects any `*Repository`
- Add/adjust service interfaces as needed

### Phase C — Inventory reference implementation (temporary)
- Keep inventory as the pattern reference until we remove it
- Later: move inventory into a sample app or delete

### Phase D — Core context (Auth/Identity/Tenant)
- Refactor services to remove any direct DbContext usage
- Use repositories + `IUnitOfWork` for multi-step flows

### Phase E — Permission enforcement finalization
- Move `[RequiresPermission]` to service methods only
- Stop proxying repositories

### Phase F — Cleanup and prep for packaging
- Remove/disconnect `GroundUp.infrastructure` once fully migrated
- Confirm package boundaries match the table in §1

---

## 8. Mechanical file-by-file inventory (initial focus)

### Inventory (temporary reference)

Controllers:
- `GroundUp.Api/Controllers/InventoryCategoryController.cs`
- `GroundUp.Api/Controllers/InventoryItemController.cs`

Core DTOs / Interfaces:
- `GroundUp.Core/dtos/InventoryCategoryDto.cs`
- `GroundUp.Core/dtos/InventoryItemDto.cs`
- `GroundUp.Data.Abstractions/interfaces/IInventoryCategoryRepository.cs`
- `GroundUp.Data.Abstractions/interfaces/IInventoryItemRepository.cs`

Entities (Option B):
- Move inventory entities out of `GroundUp.Core/entities` into inventory data project (later, likely into a sample app).

---

## 9. Notes / decisions

- Naming decision: **Data layer is `GroundUp.Data.Core`** (not `Repositories` / not `Persistence`).
- New decision: **`GroundUp.Data.Abstractions` owns repository interfaces + `IUnitOfWork`** to enforce controller/service boundaries.
- `GroundUp.Sample` is part of the repo for local validation and is **not** packaged.
- Future consuming apps host GroundUp via their own `FutureApp.Api` startup project.


The following section explains what the project structure should be when we are done and how the layers should reference each other. This is the "target end state" that we will migrate towards incrementally.

GroundUp (this repo)
GroundUp.Core
•	What it is: API-safe shared contracts and primitives: DTOs, enums, ApiResponse<T>, OperationResult<T>, attributes like RequiresPermission.
•	References: ideally none (or minimal framework deps only).
•	Referenced by: everyone (GroundUp.Api, GroundUp.Services.Core, GroundUp.Data.Abstractions, GroundUp.Data.Core, FutureApp.*).
GroundUp.Data.Abstractions (new)
•	What it is: data boundary contracts: I*Repository, IUnitOfWork, repo-related abstractions only.
•	References: GroundUp.Core.
•	Referenced by: GroundUp.Services.Core, GroundUp.Data.Core (and any app-specific data implementations in the future).
•	Not referenced by: GroundUp.Api (this is what enforces “controllers can’t use repos”).
GroundUp.Services.Core
•	What it is: business logic + orchestration. Enforces auth/permissions at service method boundary. Depends on repos via abstractions only.
•	References: GroundUp.Core, GroundUp.Data.Abstractions.
•	Referenced by: GroundUp.Api (controllers call services), GroundUp.Sample, FutureApp.Api, FutureApp.Services (optionally).
GroundUp.Data.Core
•	What it is: EF Core implementation: DbContext, migrations, repository implementations, UnitOfWork implementation, data-layer DI registration.
•	References: GroundUp.Core, GroundUp.Data.Abstractions, EF Core packages/provider.
•	Referenced by: GroundUp.Sample (repo host), FutureApp.Api (consumer host).
•	Not referenced by: GroundUp.Api, GroundUp.Services.Core.
GroundUp.Api
•	What it is: controllers + HTTP-only concerns (middleware, swagger helpers, model binding). No database knowledge.
•	References: GroundUp.Core, GroundUp.Services.Core (no data abstractions, no data core).
•	Referenced by: GroundUp.Sample (loads controllers via application parts), FutureApp.Api (hosts endpoints either by reference or via NuGet).
GroundUp.Sample (not packaged)
•	What it is: runnable composition root for this repo: wiring DI, reading config/connection strings, running migrations, hosting Swagger/UI.
•	References: GroundUp.Api, GroundUp.Services.Core, GroundUp.Data.Core (and temporary inventory projects while they exist).
•	Referenced by: nothing (it’s the executable host used for local/dev validation).
Temporary bounded contexts in this repo right now:
•	GroundUp.Repositories.Inventory / GroundUp.Services.Inventory follow the same pattern but are “temporary”; long-term they move to a sample app or get removed.
---
FutureApp (consumer)
FutureApp.Api (the host)
•	What it is: your real application’s composition root. Hosts GroundUp endpoints + runs GroundUp migrations (or triggers them via CI).
•	References: NuGets/projects: GroundUp.Api, GroundUp.Services.Core, GroundUp.Data.Core.
•	Referenced by: nothing (it’s the executable).
FutureApp.Services
•	What it is: app-specific business logic. May orchestrate its own workflows and optionally call GroundUp.Services.Core.
•	References: GroundUp.Core (and optionally GroundUp.Services.Core).
•	Referenced by: FutureApp.Api.
FutureApp.Data
•	What it is: app-specific persistence (your own DB, or extra tables, etc.). Can be independent or integrate alongside GroundUp DB.
•	References: whatever the app needs (EF Core etc.). Typically does not need GroundUp packages unless you intentionally share types.
•	Referenced by: FutureApp.Api, FutureApp.Services as needed.
Key enforcement rule in this layout:
•	Controllers can’t compile against repositories because FutureApp.Api/GroundUp.Api don’t reference GroundUp.Data.Abstractions. Only GroundUp.Services.Core can see repositories.