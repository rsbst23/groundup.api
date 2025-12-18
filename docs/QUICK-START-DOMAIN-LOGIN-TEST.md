# Quick Start Testing Guide - Domain-Based Login

## Important: Swagger Doesn't Follow Redirects!

When you call `/api/auth/login` from Swagger, you'll see **"Failed to fetch"** - this is NORMAL!

The endpoint returns a **302 Redirect** to Keycloak, but Swagger UI cannot follow this redirect.

## How to Test (Step-by-Step)

### Option 1: Browser Direct (Easiest)

1. **Open your browser**
2. **Navigate directly to**:
   ```
   http://localhost:5123/api/auth/login
   ```
3. **You should be redirected to Keycloak** login page automatically
4. **Success!** The endpoint is working

### Option 2: Using Swagger (Get Redirect URL)

1. **Open Swagger**: `http://localhost:5123/swagger/index.html`
2. **Find** `/api/auth/login` endpoint
3. **Click "Try it out"**
4. **Leave parameters empty** (for standard login)
5. **Click "Execute"**
6. **Ignore the "Failed to fetch" error**
7. **Look at the Curl command** in the response
8. **Copy the URL** from the curl command
9. **Paste into browser**

### Option 3: Using Curl (See Redirect)

```bash
# Standard login - see redirect to shared realm
curl -i http://localhost:5123/api/auth/login

# Look for "Location:" header in response
# Should show: http://localhost:8080/realms/groundup/protocol/openid-connect/auth?...
```

Expected response:
```http
HTTP/1.1 302 Found
Location: http://localhost:8080/realms/groundup/protocol/openid-connect/auth?client_id=groundup-api&redirect_uri=http%3A%2F%2Flocalhost%3A5123%2Fapi%2Fauth%2Fcallback&response_type=code&scope=openid%20email%20profile&state=...
```

## Testing Enterprise Login (With Domain)

### Prerequisites
First, create a test tenant:
```sql
INSERT INTO Tenant (Name, RealmName, CustomDomain, TenantType, IsActive, CreatedAt)
VALUES ('Test Corp', 'tenant_test_xyz', 'test.yourapp.com', 1, 1, NOW());
```

### Test Domain Lookup

#### Browser:
```
http://localhost:5123/api/auth/login?domain=test.yourapp.com
```

#### Curl:
```bash
curl -i "http://localhost:5123/api/auth/login?domain=test.yourapp.com"
```

Expected:
```http
HTTP/1.1 302 Found
Location: http://localhost:8080/realms/tenant_test_xyz/protocol/openid-connect/auth?...
```

### Test Invalid Domain

```bash
curl -i "http://localhost:5123/api/auth/login?domain=invalid.com"
```

Expected:
```http
HTTP/1.1 400 Bad Request
Content-Type: application/json

{
  "data": null,
  "success": false,
  "message": "Invalid domain",
  "errors": ["No tenant found for domain: invalid.com"],
  "statusCode": 400,
  "errorCode": "INVALID_DOMAIN"
}
```

## What You're Testing

### ? Endpoint accepts request
- No authentication errors
- No configuration errors

### ? Domain-to-realm lookup works
- Standard: no domain ? uses `groundup` realm
- Enterprise: domain provided ? looks up realm in database

### ? Redirect is generated correctly
- Includes proper Keycloak URL
- Has correct realm in path
- Contains encoded state with flow information

## Common Issues

### "Failed to fetch" in Swagger
**This is NORMAL!** Swagger cannot follow redirects. Use browser or curl instead.

### 400 Bad Request: "Invalid domain"
**Fix**: Make sure tenant exists in database with that CustomDomain

### 500 Internal Server Error: "CONFIG_ERROR"
**Fix**: Check environment variables:
- `KEYCLOAK_AUTH_SERVER_URL`
- `KEYCLOAK_RESOURCE`

### Redirect to wrong realm
**Fix**: Check database - domain might map to wrong RealmName

## Success Criteria

You know the endpoint works when:

1. ? Browser redirects to Keycloak login page
2. ? URL contains correct realm (check address bar)
3. ? Keycloak login page loads (may show error if realm doesn't exist yet, but that's expected)

## Next Steps After This Works

1. Complete Keycloak realm setup (if needed)
2. Register a test user in shared realm
3. Test full Scenario 1 from manual test plan
4. Test enterprise realm creation (Scenario 3)

## Need Help?

If you see:
- ? 404 Not Found ? Endpoint doesn't exist, check API is running
- ? 401 Unauthorized ? Should not happen for `/api/auth/login` (it's `[AllowAnonymous]`)
- ? 500 Internal Server Error ? Check logs, likely config issue
- ? 302 Found ? **SUCCESS!** Endpoint is working, just copy the redirect URL

## Quick Validation Script

```bash
#!/bin/bash

echo "Testing standard login..."
curl -i http://localhost:5123/api/auth/login 2>&1 | grep "Location:"

echo -e "\nTesting enterprise login (requires test tenant)..."
curl -i "http://localhost:5123/api/auth/login?domain=test.yourapp.com" 2>&1 | grep "Location:"

echo -e "\nTesting invalid domain..."
curl -i "http://localhost:5123/api/auth/login?domain=invalid.com" 2>&1 | grep "errorCode"

echo -e "\nDone!"
```

Save as `test-login.sh`, run with `bash test-login.sh`
