# Login Endpoint Unification - Domain-Based Authentication

## Problem
The original design had two separate login endpoints:
- `/api/auth/login/standard` - for standard tenants (shared realm)
- `/api/auth/login/enterprise?realm=xyz` - for enterprise tenants (dedicated realms)

This created two issues:
1. **User confusion** - users shouldn't need to know if they're "standard" or "enterprise"
2. **Frontend doesn't know realm** - the React app only knows the domain it's running on (e.g., `acme.yourapp.com`)

## Solution
Unified into a single endpoint: `/api/auth/login` that uses **domain-based realm resolution**

### How It Works

1. **Frontend passes domain** (the URL it's running on)
2. **API looks up tenant** by `CustomDomain` in database
3. **API determines realm** from tenant's `RealmName`
4. **API redirects to Keycloak** using the correct realm

### Domain-to-Realm Lookup Flow

```
Domain: acme.yourapp.com
    ?
Database Query: SELECT RealmName FROM Tenant WHERE CustomDomain = 'acme.yourapp.com'
    ?
Result: tenant_acme_xyz
    ?
Redirect: http://keycloak:8080/realms/tenant_acme_xyz/protocol/openid-connect/auth
```

### Behavior

#### Standard User (No Domain)
```
GET /api/auth/login
```
- No domain parameter provided
- Uses shared realm (`groundup`)
- Flow: `new_org` (auto-creates tenant if user has none)

#### Enterprise User (With Domain)
```
GET /api/auth/login?domain=acme.yourapp.com
```
- Looks up tenant by `CustomDomain = 'acme.yourapp.com'`
- Uses tenant's `RealmName` (e.g., `tenant_acme_xyz`)
- Flow: `default` (resolves existing tenant membership)

### Examples

#### Standard Login
```http
GET /api/auth/login HTTP/1.1
Host: localhost:5123
```

**Response**: 302 Redirect to `http://localhost:8080/realms/groundup/protocol/openid-connect/auth?...`

#### Enterprise Login
```http
GET /api/auth/login?domain=acme.yourapp.com HTTP/1.1
Host: localhost:5123
```

**Process**:
1. Query database: `SELECT * FROM Tenant WHERE CustomDomain = 'acme.yourapp.com'`
2. Get tenant's `RealmName`: `tenant_acme_xyz`
3. **Response**: 302 Redirect to `http://localhost:8080/realms/tenant_acme_xyz/protocol/openid-connect/auth?...`

#### Invalid Domain
```http
GET /api/auth/login?domain=invalid.example.com HTTP/1.1
Host: localhost:5123
```

**Response**: 400 Bad Request
```json
{
  "data": null,
  "success": false,
  "message": "Invalid domain",
  "errors": ["No tenant found for domain: invalid.example.com"],
  "statusCode": 400,
  "errorCode": "INVALID_DOMAIN"
}
```

## Implementation Details

### Database Schema Used

```sql
SELECT 
    Id, 
    Name, 
    RealmName,      -- Keycloak realm identifier
    CustomDomain,   -- Frontend domain (for lookup)
    TenantType,     -- Standard or Enterprise
    IsActive
FROM Tenant
WHERE CustomDomain = @domain 
  AND IsActive = 1;
```

### Changes Made

1. **Parameter Changed**: `realm` ? `domain`
2. **Added Domain Lookup**:
   ```csharp
   var tenant = await _dbContext.Tenants
       .FirstOrDefaultAsync(t => t.CustomDomain == domain && t.IsActive);
   
   targetRealm = tenant.RealmName;
   ```

3. **Flow Selection**:
   ```csharp
   Flow = tenant.TenantType == TenantType.Enterprise ? "default" : "new_org"
   ```

4. **Legacy Endpoint Support**:
   - `/api/auth/login/standard` ? redirects to `/api/auth/login` (no domain)
   - `/api/auth/login/enterprise?realm=xyz` ? looks up domain by realm, redirects to `/api/auth/login?domain=xyz`

## Frontend Integration

### React App Usage

```typescript
// Standard tenant (no custom domain)
// App running on: http://yourapp.com
window.location.href = '/api/auth/login';

// Enterprise tenant (custom domain)
// App running on: https://acme.yourapp.com
const domain = window.location.host; // "acme.yourapp.com"
window.location.href = `/api/auth/login?domain=${domain}`;

// OR App running on custom domain: https://app.acmecorp.com
const domain = window.location.host; // "app.acmecorp.com"
window.location.href = `/api/auth/login?domain=${domain}`;
```

### Automatic Domain Detection

```typescript
function startLogin() {
  const currentDomain = window.location.host;
  const baseDomain = 'yourapp.com';
  
  // Check if running on custom domain (not base domain)
  const isCustomDomain = !currentDomain.includes(baseDomain);
  
  if (isCustomDomain) {
    // Enterprise tenant - pass domain
    window.location.href = `/api/auth/login?domain=${currentDomain}`;
  } else {
    // Standard tenant - no domain needed
    window.location.href = '/api/auth/login';
  }
}
```

## Benefits

1. ? **Frontend simplicity** - just passes the domain it's running on
2. ? **No realm knowledge needed** - frontend doesn't need to know Keycloak internals
3. ? **Secure** - domain must exist in database (no arbitrary realm access)
4. ? **Flexible** - supports both subdomain and custom domain patterns
5. ? **Backward compatible** - old endpoints still work

## Testing

### Test Standard Login (No Domain)
```bash
curl -i http://localhost:5123/api/auth/login
```

**Expected**:
- Status: 302 Found
- Location: `http://localhost:8080/realms/groundup/protocol/openid-connect/auth?...`

### Test Enterprise Login (With Domain)

**Setup**: First create an enterprise tenant
```sql
INSERT INTO Tenant (Name, RealmName, CustomDomain, TenantType, IsActive)
VALUES ('Acme Corp', 'tenant_acme_xyz', 'acme.yourapp.com', 1, 1);
```

**Test**:
```bash
curl -i "http://localhost:5123/api/auth/login?domain=acme.yourapp.com"
```

**Expected**:
- Status: 302 Found
- Location: `http://localhost:8080/realms/tenant_acme_xyz/protocol/openid-connect/auth?...`

### Test Invalid Domain
```bash
curl -i "http://localhost:5123/api/auth/login?domain=invalid.com"
```

**Expected**:
- Status: 400 Bad Request
- Body contains: `"errorCode": "INVALID_DOMAIN"`

## Security Considerations

### Domain Validation
- ? Domain must exist in `Tenant` table
- ? Tenant must be active (`IsActive = 1`)
- ? No SQL injection risk (parameterized queries)
- ? No arbitrary realm access

### Attack Scenarios Prevented

**Scenario**: Attacker tries to access admin realm
```bash
curl "http://localhost:5123/api/auth/login?domain=admin.internal.com"
```
**Result**: 400 Bad Request (domain not in database)

**Scenario**: Attacker tries to access another company's realm
```bash
curl "http://localhost:5123/api/auth/login?domain=competitor.yourapp.com"
```
**Result**: 400 Bad Request (unless they have access to that domain's DNS/routing)

## Database Requirements

### Required Columns
- `Tenant.CustomDomain` - The domain to match against (can be NULL for standard tenants)
- `Tenant.RealmName` - The Keycloak realm identifier
- `Tenant.TenantType` - Standard (0) or Enterprise (1)
- `Tenant.IsActive` - Boolean flag

### Index Recommendation
```sql
CREATE INDEX IX_Tenant_CustomDomain 
ON Tenant(CustomDomain) 
WHERE CustomDomain IS NOT NULL AND IsActive = 1;
```

## Troubleshooting

### Error: "Invalid domain"
- **Cause**: Domain not found in database or tenant is inactive
- **Fix**: Verify tenant exists: `SELECT * FROM Tenant WHERE CustomDomain = 'your.domain.com'`

### Error: "Keycloak configuration is missing"
- **Cause**: Environment variables `KEYCLOAK_AUTH_SERVER_URL` or `KEYCLOAK_RESOURCE` not set
- **Fix**: Check `.env` file or environment configuration

### Redirect to wrong realm
- **Cause**: `CustomDomain` mismatch or wrong `RealmName` in database
- **Fix**: Update tenant: `UPDATE Tenant SET RealmName = 'correct_realm' WHERE CustomDomain = 'your.domain.com'`

## Related Files
- `GroundUp.api/Controllers/AuthController.cs` - Main implementation
- `GroundUp.core/entities/Tenant.cs` - Tenant model with CustomDomain and RealmName
- `docs/groundup-manual-test-plan.md` - Updated test procedures
- `GroundUp.api/.env` - Environment variables (KEYCLOAK_AUTH_SERVER_URL, etc.)

## Next Steps

1. ? Domain-based login implemented
2. ? Configuration bug fixed
3. ? Test plan updated
4. ? Update frontend to pass domain parameter
5. ? Test Scenario 1 (Standard Tenant Bootstrap)
6. ? Test Scenario 3 (Enterprise Tenant Bootstrap with domain lookup)
