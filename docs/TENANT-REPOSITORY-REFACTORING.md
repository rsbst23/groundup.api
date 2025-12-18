# TenantRepository Refactoring - Technical Debt

**Status:** Tracked for future implementation  
**Priority:** Medium  
**Effort:** ~2 hours  
**Risk:** Low (no functionality changes)

---

## Problem

`TenantRepository` currently duplicates all CRUD logic that exists in `BaseRepository<T, TDto>`. This violates DRY principles and makes maintenance harder.

---

## Current State

```csharp
// Current implementation (568 lines)
public class TenantRepository : ITenantRepository
{
    // Manually implements:
    // - GetAllAsync
    // - GetByIdAsync
    // - AddAsync
    // - UpdateAsync
    // - DeleteAsync
    // - ExportAsync
    // - ResolveRealmByUrlAsync (unique to TenantRepository)
}
```

**Why it's like this:**
- Tenant is NOT a tenant-scoped entity (doesn't implement `ITenantEntity`)
- Cannot use `BaseTenantRepository` (which filters by TenantId)
- Should use `BaseRepository` but currently doesn't

---

## Target State

```csharp
// After refactoring (~150 lines)
public class TenantRepository : BaseRepository<Tenant, TenantDto>, ITenantRepository
{
    public TenantRepository(
        ApplicationDbContext context,
        IMapper mapper,
        ILoggingService logger)
        : base(context, mapper, logger)
    {
    }

    // Override only what's different from base:
    
    public override async Task<ApiResponse<TenantDto>> AddAsync(CreateTenantDto dto)
    {
        // Add tenant-specific validation (parent exists, etc.)
        // Call base.AddAsync or use custom logic
    }

    public override async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        // Add tenant-specific validation (no children, no users)
        // Call base.DeleteAsync
    }

    // Unique method not in BaseRepository
    public async Task<ApiResponse<RealmResolutionResponseDto>> ResolveRealmByUrlAsync(string url)
    {
        // This method is unique to TenantRepository - keep as-is
    }
}
```

---

## Benefits

1. ? **Reduced Code** - From ~568 lines to ~150 lines (73% reduction)
2. ? **Maintainability** - Changes to base CRUD logic auto-apply
3. ? **Consistency** - All repositories follow same pattern
4. ? **Less Duplication** - Single source of truth for CRUD operations
5. ? **Easier Testing** - Less code to test

---

## Implementation Steps

### Step 1: Review BaseRepository
- Understand `BaseRepository<T, TDto>` constructor and methods
- Check if it requires `ITenantContext` (it shouldn't for non-tenant entities)
- Verify it uses AutoMapper for entity-DTO mapping

### Step 2: Update TenantRepository Signature
```csharp
public class TenantRepository : BaseRepository<Tenant, TenantDto>, ITenantRepository
```

### Step 3: Update Constructor
```csharp
public TenantRepository(
    ApplicationDbContext context,
    IMapper mapper,
    ILoggingService logger)
    : base(context, mapper, logger)  // Call base constructor
{
}
```

### Step 4: Remove Duplicated Methods
Delete these methods (inherited from base):
- `GetAllAsync` (unless custom query logic needed)
- `GetByIdAsync`
- `ExportAsync`

### Step 5: Override Methods with Custom Logic
Keep and override:
- `AddAsync` - Has parent tenant validation
- `UpdateAsync` - Has RealmUrl update logic
- `DeleteAsync` - Has child/user checks

### Step 6: Keep Unique Methods
Keep as-is:
- `ResolveRealmByUrlAsync` - Unique to TenantRepository

### Step 7: Update AutoMapper
Ensure mapping profile includes:
```csharp
CreateMap<Tenant, TenantDto>();
CreateMap<TenantDto, Tenant>();
CreateMap<CreateTenantDto, Tenant>();
CreateMap<UpdateTenantDto, Tenant>();
```

### Step 8: Test Everything
- Test all CRUD operations
- Test realm resolution
- Test parent tenant validation
- Test delete constraints (children, users)
- Test export functionality

---

## Potential Issues

### Issue 1: BaseRepository may not support custom DTOs
**Current:** Methods use `TenantDto`, `CreateTenantDto`, `UpdateTenantDto`  
**BaseRepository:** Might expect single `TDto` type

**Solution:** Check if `BaseRepository` can handle:
- `AddAsync(CreateTenantDto)` vs `AddAsync(TDto)`
- `UpdateAsync(UpdateTenantDto)` vs `UpdateAsync(TDto)`

If not, we may need to keep custom Add/Update methods.

### Issue 2: Include() for navigation properties
**Current:** Uses `.Include(t => t.ParentTenant)` for eager loading  
**BaseRepository:** May not know about `ParentTenant`

**Solution:** Override `GetAllAsync` and `GetByIdAsync` to add `.Include()` if needed.

### Issue 3: Custom validation logic
**Current:** Validates parent exists, checks for children/users before delete  
**BaseRepository:** Generic, no entity-specific validation

**Solution:** Override methods and add validation before calling base.

---

## Testing Checklist

After refactoring:

- [ ] GET /api/tenants (with pagination) works
- [ ] GET /api/tenants/{id} works
- [ ] POST /api/tenants (standard) works
- [ ] POST /api/tenants (enterprise) works
- [ ] PUT /api/tenants/{id} works
- [ ] DELETE /api/tenants/{id} works
- [ ] DELETE fails when tenant has children
- [ ] DELETE fails when tenant has users
- [ ] POST /api/tenants/resolve-realm works
- [ ] Export to CSV works
- [ ] Export to JSON works
- [ ] ParentTenant navigation property loads correctly
- [ ] TenantType and RealmUrl are saved/loaded correctly

---

## Timeline

**When to do this:**
- After Phase 5 (Services) is complete
- After multi-realm functionality is tested and working
- Before next major feature

**Estimated effort:**
- Implementation: 1-2 hours
- Testing: 1 hour
- Code review: 30 minutes

---

## Alternative: Keep Current Implementation

**Pros:**
- ? Already works
- ? No risk of breaking existing functionality
- ? Explicit code (easier for juniors to understand)

**Cons:**
- ? Code duplication
- ? Harder to maintain
- ? Inconsistent with other repositories

**Recommendation:** Refactor after multi-realm is stable.

---

## Notes

- This refactoring is **low risk** because it's just moving code, not changing logic
- The multi-realm functionality (`ResolveRealmByUrlAsync`) is unaffected
- We can do this incrementally (one method at a time) if preferred
- Keep the TODO comments in the code until refactoring is complete

---

**Created:** 2024-11-27  
**Last Updated:** 2024-11-27  
**Assigned To:** TBD  
**Related:** Phase 4 - Repositories (Multi-Realm Implementation)
