# Domain-Based Login Implementation Summary

## What Changed

### Before (Realm-Based)
```http
GET /api/auth/login/standard
GET /api/auth/login/enterprise?realm=tenant_acme_xyz
```

**Problem**: Frontend doesn't know the realm name!

### After (Domain-Based)
```http
GET /api/auth/login
GET /api/auth/login?domain=acme.yourapp.com
```

**Solution**: Frontend passes the domain it's running on, API looks up realm in database.

---

## How It Works

```
???????????????????????????????????????????????????????????????
? 1. React App Running On: acme.yourapp.com                   ?
???????????????????????????????????????????????????????????????
                  ?
                  ? GET /api/auth/login?domain=acme.yourapp.com
                  ?
???????????????????????????????????????????????????????????????
? 2. API: Domain ? Realm Lookup                               ?
?                                                              ?
?    SELECT RealmName FROM Tenant                             ?
?    WHERE CustomDomain = 'acme.yourapp.com'                  ?
?                                                              ?
?    Result: "tenant_acme_xyz"                                ?
???????????????????????????????????????????????????????????????
                  ?
                  ? 302 Redirect to Keycloak
                  ?
???????????????????????????????????????????????????????????????
? 3. Keycloak: Authenticate in Correct Realm                  ?
?                                                              ?
?    http://keycloak:8080/realms/tenant_acme_xyz/...         ?
???????????????????????????????????????????????????????????????
```

---

## Database Schema Used

### Tenant Table
| Column        | Type    | Description                                    |
|---------------|---------|------------------------------------------------|
| Id            | int     | Primary key                                    |
| Name          | string  | Tenant display name                            |
| **RealmName** | string  | Keycloak realm identifier (e.g., tenant_acme_xyz) |
| **CustomDomain** | string | Frontend domain (e.g., acme.yourapp.com)    |
| TenantType    | enum    | Standard (0) or Enterprise (1)                 |
| IsActive      | bool    | Active flag                                    |

### The Lookup Query
```csharp
var tenant = await _dbContext.Tenants
    .FirstOrDefaultAsync(t => t.CustomDomain == domain && t.IsActive);

string targetRealm = tenant.RealmName;
```

---

## Use Cases

### Standard Tenant (No Custom Domain)
```http
GET /api/auth/login
```
- No domain parameter
- Uses shared realm: `groundup`
- Flow: `new_org` (creates tenant if needed)

### Enterprise Tenant (Subdomain)
```http
GET /api/auth/login?domain=acme.yourapp.com
```
- Domain: `acme.yourapp.com`
- Lookup: `CustomDomain = 'acme.yourapp.com'`
- Realm: `tenant_acme_xyz`
- Flow: `default` (resolves existing tenant)

### Enterprise Tenant (Custom Domain)
```http
GET /api/auth/login?domain=app.acmecorp.com
```
- Domain: `app.acmecorp.com`
- Lookup: `CustomDomain = 'app.acmecorp.com'`
- Realm: `tenant_acme_xyz`
- Flow: `default`

---

## Frontend Code Example

### Automatic Domain Detection
```typescript
function startLogin() {
  // Get current domain the React app is running on
  const currentDomain = window.location.host;
  
  // Standard tenant uses base domain (e.g., yourapp.com)
  // Enterprise tenants use custom domains
  const isStandardTenant = currentDomain === 'yourapp.com' || 
                          currentDomain === 'localhost:5173';
  
  if (isStandardTenant) {
    // Standard login - no domain needed
    window.location.href = '/api/auth/login';
  } else {
    // Enterprise login - pass current domain
    window.location.href = `/api/auth/login?domain=${currentDomain}`;
  }
}
```

### Manual Domain Override (for testing)
```typescript
// Test different tenants without changing domain
function loginAsTenant(customDomain: string) {
  window.location.href = `/api/auth/login?domain=${customDomain}`;
}

// Examples:
loginAsTenant('acme.yourapp.com');
loginAsTenant('app.acmecorp.com');
```

---

## Security Benefits

1. ? **No realm knowledge needed in frontend** - just domain
2. ? **Database validation** - domain must exist in Tenant table
3. ? **No arbitrary realm access** - can't pass random realm names
4. ? **Tenant isolation** - each domain maps to exactly one realm
5. ? **Active check** - only active tenants can authenticate

---

## Testing Checklist

### Standard Login
- [ ] Test without domain parameter
- [ ] Verify redirect to `groundup` realm
- [ ] Confirm `new_org` flow in state

### Enterprise Login
- [ ] Create test tenant with custom domain
- [ ] Test with domain parameter
- [ ] Verify correct realm redirect
- [ ] Confirm `default` flow in state

### Error Handling
- [ ] Test with invalid domain ? 400 Bad Request
- [ ] Test with inactive tenant ? 400 Bad Request
- [ ] Test with missing Keycloak config ? 500 Internal Server Error

---

## Quick Test Commands

### Create Test Enterprise Tenant
```sql
INSERT INTO Tenant (Name, RealmName, CustomDomain, TenantType, IsActive, CreatedAt)
VALUES ('Test Corp', 'tenant_test_xyz', 'test.yourapp.com', 1, 1, NOW());
```

### Test Standard Login
```bash
curl -i http://localhost:5123/api/auth/login
```
Expected: Redirect to `http://localhost:8080/realms/groundup/...`

### Test Enterprise Login
```bash
curl -i "http://localhost:5123/api/auth/login?domain=test.yourapp.com"
```
Expected: Redirect to `http://localhost:8080/realms/tenant_test_xyz/...`

### Test Invalid Domain
```bash
curl -i "http://localhost:5123/api/auth/login?domain=invalid.com"
```
Expected: 400 Bad Request with `"errorCode": "INVALID_DOMAIN"`

---

## Files Modified
- ? `GroundUp.api/Controllers/AuthController.cs` - Domain-based login logic
- ? `docs/LOGIN-ENDPOINT-UNIFICATION.md` - Complete documentation
- ? `docs/groundup-manual-test-plan.md` - Updated test scenarios

## Next Steps
1. Restart API to pick up changes
2. Test standard login (no domain)
3. Create test enterprise tenant
4. Test enterprise login (with domain)
5. Continue with Scenario 1 from manual test plan
