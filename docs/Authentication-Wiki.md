# GroundUp API - Multi-Tenant Authentication & Authorization Wiki

## Table of Contents
1. [Architecture Overview](#architecture-overview)
2. [Authentication Flow](#authentication-flow)
3. [Authorization & Permissions](#authorization--permissions)
4. [Multi-Tenancy](#multi-tenancy)
5. [Key Components](#key-components)
6. [Configuration](#configuration)
7. [Development Guide](#development-guide)
8. [Troubleshooting](#troubleshooting)

---

## Architecture Overview

GroundUp uses a **dual-token authentication system** combined with **role-based and permission-based authorization** in a **multi-tenant environment**.

### Technology Stack
- **ASP.NET Core 8** - Web API framework
- **Keycloak** - Identity and Access Management (OpenID Connect/OAuth2)
- **MySQL** - Database
- **Castle DynamicProxy** - Method interception for automatic permission checks
- **FluentValidation** - Request validation
- **AutoMapper** - DTO mapping
- **Serilog** - Logging

### Architecture Layers

```
???????????????????????????????????????????????????????????
?                      UI Layer                           ?
?  (React/Vue - Future Implementation)                    ?
???????????????????????????????????????????????????????????
                          ?
???????????????????????????????????????????????????????????
?                   API Layer (GroundUp.api)              ?
?  • Controllers                                          ?
?  • Middleware (Exception Handling)                      ?
?  • Program.cs (Service Configuration)                   ?
???????????????????????????????????????????????????????????
                          ?
???????????????????????????????????????????????????????????
?            Infrastructure Layer (GroundUp.infrastructure)?
?  • Repositories (with Dynamic Proxies)                  ?
?  • Interceptors (Permission/Lazy)                       ?
?  • Services (Token, Permission, Logging)                ?
?  • Extensions (Service Registration)                    ?
?  • Data (DbContext, Migrations)                         ?
???????????????????????????????????????????????????????????
                          ?
???????????????????????????????????????????????????????????
?              Core Layer (GroundUp.core)                 ?
?  • Entities (Domain Models)                             ?
?  • DTOs (Data Transfer Objects)                         ?
?  • Interfaces (Contracts)                               ?
?  • Validators                                           ?
?  • Security Attributes                                  ?
???????????????????????????????????????????????????????????
```

---

## Authentication Flow

### Overview
The system uses **two JWT token schemes**:

1. **Keycloak Token** - Issued by Keycloak after initial login
2. **Custom Token** - Issued by our API after tenant selection (includes `tenant_id`)

### Complete Authentication Flow

```
????????????????
?    User      ?
????????????????
       ?
       ? 1. Click "Login"
       ?
????????????????????????????????????????????
?  UI redirects to Keycloak                ?
?  http://localhost:8080/realms/groundup   ?
?  /protocol/openid-connect/auth           ?
????????????????????????????????????????????
       ?
       ? 2. User enters credentials
       ?    (or uses social login)
       ?
????????????????????????????????????????????
?  Keycloak validates and redirects        ?
?  back to UI with authorization code      ?
????????????????????????????????????????????
       ?
       ? 3. UI exchanges code for token
       ?
????????????????????????????????????????????
?  UI receives Keycloak JWT Token          ?
?  Contains: user info, roles, email, etc. ?
????????????????????????????????????????????
       ?
       ? 4. UI calls /api/auth/set-tenant
       ?    Authorization: Bearer <keycloak-token>
       ?
????????????????????????????????????????????
?  API validates Keycloak token            ?
?  • "Keycloak" JWT Bearer scheme          ?
?  • Extracts user ID from claims          ?
?  • Queries user's tenants                ?
????????????????????????????????????????????
       ?
       ? 5. API response
       ?
????????????????????????????????????????????
?  Option A: Single Tenant                 ?
?  {                                        ?
?    "selectionRequired": false,           ?
?    "token": "<custom-jwt-with-tenant>"   ?
?  }                                        ?
?                                           ?
?  Option B: Multiple Tenants              ?
?  {                                        ?
?    "selectionRequired": true,            ?
?    "availableTenants": [...]             ?
?  }                                        ?
????????????????????????????????????????????
       ?
       ? 6. UI stores custom token
       ?    (localStorage or cookie)
       ?
????????????????????????????????????????????
?  All subsequent API calls                ?
?  Authorization: Bearer <custom-token>    ?
?                                           ?
?  Custom token contains:                  ?
?  • User ID, name, email, roles           ?
?  • tenant_id (for multi-tenancy)         ?
?  • Issuer: "GroundUp"                    ?
?  • Audience: "GroundUpUsers"             ?
????????????????????????????????????????????
```

### Token Generation Process

**File:** `GroundUp.infrastructure/services/TokenService.cs`

```csharp
public Task<string> GenerateTokenAsync(Guid userId, int tenantId, 
    IEnumerable<Claim> existingClaims)
{
    // 1. Filter out Keycloak infrastructure claims (iss, aud, exp, etc.)
    // 2. Keep user claims (name, email, roles, etc.)
    // 3. Add tenant_id claim
    // 4. Sign with our JWT_SECRET
    // 5. Return custom JWT token
}
```

**Key Points:**
- ? Custom token has **our issuer** ("GroundUp"), not Keycloak's
- ? Custom token includes **tenant_id** for multi-tenancy
- ? Custom token expires in **1 hour** (configurable)
- ? Custom token is **also set as HTTP-only cookie** (for browser clients)

---

## Authorization & Permissions

### Two-Layer Security Model

#### Layer 1: Authentication (Global)
**File:** `GroundUp.api/Program.cs`

```csharp
services.AddAuthorization(options =>
{
    // FallbackPolicy requires authentication on ALL endpoints
    options.FallbackPolicy = new AuthorizationPolicyBuilder("Keycloak", "Custom")
        .RequireAuthenticatedUser()
        .Build();
});
```

**Effect:**
- ? Every endpoint requires authentication automatically
- ? No need for `[Authorize]` attributes on controllers
- ? Use `[AllowAnonymous]` only for truly public endpoints

#### Layer 2: Permission Checking (Fine-Grained)
**File:** `GroundUp.infrastructure/interceptors/PermissionInterceptor.cs`

```csharp
[RequiresPermission(Permissions.READ_ROLE, RequiredRoles = new[] { "ADMIN" })]
Task<ApiResponse<PaginatedData<RoleDto>>> GetAllAsync(FilterParams filterParams);
```

**How It Works:**

1. **Repository interface** has `[RequiresPermission]` attribute
2. **Castle DynamicProxy** wraps repository with interceptor
3. **Before method execution**, `PermissionInterceptor` checks:
   - Is user authenticated?
   - Does user have required role?
   - Does user have required permission?
4. **If authorized** ? method executes
5. **If not authorized** ? throws `ForbiddenAccessException`

### Permission Flow

```
HTTP Request ? Authentication Middleware ? Controller ? Repository Method
                     ?
            Validates JWT Token
            Populates User.Identity
                     ?
                 Controller
            (no attributes needed)
                     ?
              Repository Call
                     ?
        PermissionInterceptor.Intercept()
                     ?
    ?????????????????????????????????????
    ? Check [RequiresPermission]        ?
    ? attribute on interface method     ?
    ?????????????????????????????????????
                ?
                ?
    ?????????????????????????????????????
    ? Is User.Identity.IsAuthenticated? ?
    ?????????????????????????????????????
                ? YES
                ?
    ?????????????????????????????????????
    ? Check RequiredRoles               ?
    ? User has ADMIN role?              ?
    ?????????????????????????????????????
                ? NO
                ?
    ?????????????????????????????????????
    ? Check Permissions                 ?
    ? User has READ_ROLE permission?    ?
    ?????????????????????????????????????
                ? YES
                ?
    ?????????????????????????????????????
    ? AUTHORIZED                        ?
    ? Method executes                   ?
    ?????????????????????????????????????
```

### Permission Database Schema

```
????????????????       ????????????????       ????????????????
?  Permission  ?       ?    Policy    ?       ?     Role     ?
????????????????       ????????????????       ????????????????
? Id           ?       ? Id           ?       ? Id           ?
? Name         ?       ? Name         ?       ? Name         ?
? Description  ?       ? Description  ?       ? RoleType     ?
? TenantId     ?       ? TenantId     ?       ? TenantId     ?
????????????????       ????????????????       ????????????????
       ?                      ?                       ?
       ?   ????????????????????????????              ?
       ?   ?                          ?              ?
       ?   ?                          ?              ?
       ? ???????????????????   ???????????????      ?
       ??? PolicyPermission?   ? RolePolicy  ????????
         ???????????????????   ???????????????
         ? PolicyId        ?   ? RoleName    ?
         ? PermissionId    ?   ? PolicyId    ?
         ? TenantId        ?   ? TenantId    ?
         ???????????????????   ???????????????
```

**Permission Hierarchy:**
1. **Permission** - Atomic action (e.g., "READ_ROLE", "WRITE_INVENTORY")
2. **Policy** - Collection of permissions (e.g., "InventoryManager")
3. **Role** - Assigned to users, contains policies (e.g., "ADMIN", "USER")

---

## Multi-Tenancy

### Tenant Isolation Strategy

Every entity that needs tenant isolation implements `ITenantEntity`:

```csharp
public interface ITenantEntity
{
    int TenantId { get; set; }
}
```

### Automatic Tenant Filtering

**File:** `GroundUp.infrastructure/repositories/BaseTenantRepository.cs`

```csharp
public virtual async Task<ApiResponse<PaginatedData<TDto>>> GetAllAsync(
    FilterParams filterParams)
{
    var query = _dbSet.AsQueryable();

    // Automatically filter by current user's tenant
    if (typeof(ITenantEntity).IsAssignableFrom(typeof(T)))
    {
        var tenantId = _tenantContext.TenantId; // From JWT token
        query = query.Where(e => ((ITenantEntity)e).TenantId == tenantId);
    }
    
    // ... rest of query
}
```

### Tenant Context

**File:** `GroundUp.infrastructure/services/TenantContext.cs`

```csharp
public class TenantContext : ITenantContext
{
    public int TenantId
    {
        get
        {
            // Extract tenant_id from JWT token
            var tenantIdClaim = _httpContext.User.FindFirst("tenant_id")?.Value;
            if (int.TryParse(tenantIdClaim, out var tenantId))
            {
                return tenantId;
            }
            throw new InvalidOperationException("TenantId claim is missing or invalid.");
        }
    }
}
```

**How It Works:**
1. ? User's JWT token contains `tenant_id` claim
2. ? `TenantContext` reads it from HTTP context
3. ? `BaseTenantRepository` automatically filters all queries
4. ? Users can **only see/modify data from their tenant**

### Tenant Assignment

**Database Table:** `UserTenants`

```sql
CREATE TABLE UserTenants (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    UserId CHAR(36) NOT NULL,     -- From Keycloak
    TenantId INT NOT NULL,        -- Tenant assignment
    FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
```

**Tenant Selection Flow:**
1. User logs in via Keycloak
2. API queries `UserTenants` for that user
3. If **1 tenant** ? auto-select
4. If **multiple tenants** ? show selector UI
5. Generate custom token with selected `tenant_id`

---

## Key Components

### 1. AuthController
**File:** `GroundUp.api/Controllers/AuthController.cs`

**Endpoints:**
- `POST /api/auth/set-tenant` - Select tenant and get custom token
- `GET /api/auth/me` - Get current user profile
- `POST /api/auth/logout` - Clear authentication cookie
- `GET /api/auth/debug-token` - Debug: view all claims

### 2. TokenService
**File:** `GroundUp.infrastructure/services/TokenService.cs`

**Purpose:** Generate custom JWT tokens with tenant context

**Key Method:**
```csharp
Task<string> GenerateTokenAsync(Guid userId, int tenantId, 
    IEnumerable<Claim> existingClaims)
```

### 3. PermissionInterceptor
**File:** `GroundUp.infrastructure/interceptors/PermissionInterceptor.cs`

**Purpose:** Intercept repository method calls to check permissions

**Flow:**
1. Check if method has `[RequiresPermission]` attribute
2. If no attribute ? proceed
3. If has attribute ? check user authentication
4. Check user roles match `RequiredRoles`
5. Check user has any of specified `Permissions`
6. If authorized ? proceed, else throw exception

### 4. ServiceCollectionExtensions
**File:** `GroundUp.infrastructure/extensions/ServiceCollectionExtensions.cs`

**Methods:**
- `AddInfrastructureServices()` - Register repositories with interceptors
- `AddApplicationServices()` - Register FluentValidation
- `AddKeycloakServices()` - Configure dual JWT authentication

### 5. BaseTenantRepository
**File:** `GroundUp.infrastructure/repositories/BaseTenantRepository.cs`

**Purpose:** Base class for all tenant-aware repositories

**Features:**
- ? Automatic tenant filtering on queries
- ? Automatic tenant ID assignment on creates
- ? Tenant validation on updates/deletes
- ? CRUD operations with pagination
- ? Export functionality (CSV, JSON)

---

## Configuration

### Environment Variables

**Required for Production:**

```sh
# Keycloak Configuration
KEYCLOAK_AUTH_SERVER_URL=http://localhost:8080
KEYCLOAK_REALM=groundup
KEYCLOAK_RESOURCE=groundup-api
KEYCLOAK_CLIENT_SECRET=<your-secret>
KEYCLOAK_ADMIN_CLIENT_ID=admin-cli
KEYCLOAK_ADMIN_CLIENT_SECRET=<admin-secret>

# Custom JWT Configuration
JWT_SECRET=<at-least-32-character-secret>
JWT_ISSUER=GroundUp
JWT_AUDIENCE=GroundUpUsers

# Database Configuration
MYSQL_SERVER=localhost
MYSQL_PORT=3306
MYSQL_DATABASE=groundup
MYSQL_USER=root
MYSQL_PASSWORD=<your-password>

# AWS Configuration (for production)
AWS_EXECUTION_ENV=<set-if-running-on-aws>
CLOUDWATCH_LOG_GROUP=GroundUpApiLogs
```

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "JWT_SECRET": "supersecretkeythatshouldbe32charsmin!",
  "JWT_ISSUER": "GroundUp",
  "JWT_AUDIENCE": "GroundUpUsers"
}
```

### Keycloak Setup

1. **Create Realm:** `groundup`
2. **Create Client:** `groundup-api`
   - Client Protocol: `openid-connect`
   - Access Type: `confidential`
   - Enable: `Direct Access Grants` (for password flow)
   - Valid Redirect URIs: `http://localhost:5173/*`
3. **Create Roles:** `ADMIN`, `USER`, `SYSTEMADMIN`
4. **Create Users** and assign roles

---

## Development Guide

### Adding a New Protected Endpoint

**Step 1: Create Repository Interface**
```csharp
public interface IProductRepository
{
    [RequiresPermission(Permissions.READ_PRODUCT)]
    Task<ApiResponse<PaginatedData<ProductDto>>> GetAllAsync(FilterParams filterParams);
}
```

**Step 2: Implement Repository**
```csharp
public class ProductRepository : BaseTenantRepository<Product, ProductDto>, 
    IProductRepository
{
    // Implementation inherits from base
}
```

**Step 3: Create Controller**
```csharp
[Route("api/products")]
[ApiController]
public class ProductController : ControllerBase
{
    private readonly IProductRepository _productRepository;
    
    [HttpGet]
    public async Task<ActionResult> Get([FromQuery] FilterParams filters)
    {
        var result = await _productRepository.GetAllAsync(filters);
        return StatusCode(result.StatusCode, result);
    }
}
```

**That's it!** No `[Authorize]` needed - global policy handles authentication, interceptor handles permissions.

### Adding a New Permission

**Step 1: Add to Permissions class**
```csharp
public static class Permissions
{
    public const string READ_PRODUCT = "READ_PRODUCT";
    public const string WRITE_PRODUCT = "WRITE_PRODUCT";
}
```

**Step 2: Seed in database**
```sql
INSERT INTO Permissions (Name, Description, TenantId) 
VALUES ('READ_PRODUCT', 'Can view products', 1);
```

**Step 3: Add to Policy**
```sql
INSERT INTO PolicyPermissions (PolicyId, PermissionId, TenantId)
VALUES (1, <permission-id>, 1);
```

### Running Locally

```sh
# 1. Start Keycloak
docker-compose -f keycloak-compose.yml up -d

# 2. Start MySQL
docker-compose up -d mysql

# 3. Run migrations
cd GroundUp.api
dotnet ef database update --project ../GroundUp.infrastructure

# 4. Start API
dotnet run
```

**API will be available at:** `https://localhost:7001`

---

## Troubleshooting

### Issue: "User is not authenticated"

**Symptoms:** Getting 401 Unauthorized even with valid token

**Causes:**
1. ? Token has wrong issuer (Keycloak issuer instead of "GroundUp")
2. ? Token expired
3. ? `AuthenticationType` not set on token validation

**Solution:**
- Check token claims in `/api/auth/debug-token`
- Verify issuer is "GroundUp" for custom tokens
- Check token expiration (`exp` claim)

### Issue: "User lacks permission"

**Symptoms:** Getting 403 Forbidden

**Causes:**
1. ? User doesn't have required role
2. ? User doesn't have required permission
3. ? Permission not assigned to user's role

**Solution:**
```sql
-- Check user's roles
SELECT r.Name 
FROM UserRoles ur 
JOIN Roles r ON ur.RoleId = r.Id 
WHERE ur.UserId = '<user-id>';

-- Check role's permissions
SELECT p.Name 
FROM Permissions p
JOIN PolicyPermissions pp ON p.Id = pp.PermissionId
JOIN RolePolicies rp ON pp.PolicyId = rp.PolicyId
WHERE rp.RoleName = '<role-name>';
```

### Issue: "TenantId claim is missing"

**Symptoms:** Exception when accessing tenant-scoped data

**Causes:**
1. ? Using Keycloak token instead of custom token
2. ? Token doesn't have `tenant_id` claim

**Solution:**
- Call `/api/auth/set-tenant` first
- Use the **returned custom token** for subsequent requests
- Check token claims in `/api/auth/debug-token`

### Issue: Data from wrong tenant showing up

**Symptoms:** Seeing data from other tenants

**Causes:**
1. ? Entity doesn't implement `ITenantEntity`
2. ? Using `BaseRepository` instead of `BaseTenantRepository`

**Solution:**
```csharp
// ? Correct
public class Product : ITenantEntity
{
    public int TenantId { get; set; }
}

public class ProductRepository : BaseTenantRepository<Product, ProductDto>
{
    // Automatically filters by tenant
}
```

### Debug Logging

Enable detailed logging in `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Debug",
      "GroundUp": "Debug"
    }
  }
}
```

Logs go to:
- Console (Serilog)
- CloudWatch (if on AWS)

---

## Security Best Practices

### ? DO

- Use HTTPS in production
- Rotate JWT_SECRET regularly
- Set `Secure = true` for cookies in production
- Validate all user inputs with FluentValidation
- Use parameterized queries (EF Core does this)
- Keep Keycloak updated

### ? DON'T

- Expose JWT_SECRET in source control
- Use `AllowAnonymous` without careful consideration
- Skip tenant validation checks
- Log sensitive information (passwords, tokens)
- Disable HTTPS in production

---

## Future Enhancements

### Planned Features

- [ ] Social login (Google, Facebook, Microsoft)
- [ ] Multi-factor authentication (MFA)
- [ ] Refresh tokens for long-lived sessions
- [ ] Audit logging for permission changes
- [ ] Rate limiting per tenant
- [ ] Custom Keycloak theme matching app branding
- [ ] Role hierarchy (inherit permissions)
- [ ] Dynamic permission updates without redeployment

---

## Additional Resources

- [Keycloak Documentation](https://www.keycloak.org/documentation)
- [JWT.io](https://jwt.io) - Decode and inspect JWT tokens
- [Castle DynamicProxy](http://www.castleproject.org/projects/dynamicproxy/)
- [FluentValidation](https://docs.fluentvalidation.net/)

---

**Last Updated:** November 2024  
**Version:** 1.0  
**Maintainer:** GroundUp Development Team
