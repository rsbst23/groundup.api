# Join-Link Management API - Quick Reference

## Overview

Tenant administrators can now create, list, and revoke join links for their tenants. Users can join tenants by visiting these links.

---

## Admin Endpoints (Authenticated)

### 1. Create Join Link

**Endpoint:** `POST /api/tenant-join-links`

**Authentication:** Required (tenant-scoped token)

**Request:**
```json
{
  "expirationDays": 30,
  "defaultRoleId": null  // optional: role to assign on join
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "tenantId": 1,
    "joinToken": "abc123def456789...",
    "joinUrl": "https://api.example.com/api/join/abc123def456789...",
    "expiresAt": "2024-02-15T12:00:00Z",
    "isRevoked": false,
    "createdAt": "2024-01-15T12:00:00Z",
    "defaultRoleId": null
  },
  "message": "Join link created successfully",
  "statusCode": 201
}
```

---

### 2. List Join Links

**Endpoint:** `GET /api/tenant-join-links`

**Authentication:** Required (tenant-scoped token)

**Query Parameters:**
- `pageNumber` (default: 1)
- `pageSize` (default: 10)
- `includeRevoked` (default: false)

**Example:**
```
GET /api/tenant-join-links?pageNumber=1&pageSize=10&includeRevoked=false
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 1,
        "tenantId": 1,
        "joinToken": "abc123...",
        "joinUrl": "https://api.example.com/api/join/abc123...",
        "expiresAt": "2024-02-15T12:00:00Z",
        "isRevoked": false,
        "createdAt": "2024-01-15T12:00:00Z",
        "defaultRoleId": null
      }
    ],
    "pageNumber": 1,
    "pageSize": 10,
    "totalRecords": 1,
    "totalPages": 1
  },
  "statusCode": 200
}
```

---

### 3. Get Join Link by ID

**Endpoint:** `GET /api/tenant-join-links/{id}`

**Authentication:** Required (tenant-scoped token)

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "tenantId": 1,
    "joinToken": "abc123...",
    "joinUrl": "https://api.example.com/api/join/abc123...",
    "expiresAt": "2024-02-15T12:00:00Z",
    "isRevoked": false,
    "createdAt": "2024-01-15T12:00:00Z",
    "defaultRoleId": null
  },
  "statusCode": 200
}
```

**Error Response (404 Not Found):**
```json
{
  "success": false,
  "message": "Join link not found",
  "statusCode": 404,
  "errorCode": "NotFound"
}
```

---

### 4. Revoke Join Link

**Endpoint:** `DELETE /api/tenant-join-links/{id}`

**Authentication:** Required (tenant-scoped token)

**Response (200 OK):**
```json
{
  "success": true,
  "data": true,
  "message": "Join link revoked successfully",
  "statusCode": 200
}
```

**Error Response (404 Not Found):**
```json
{
  "success": false,
  "data": false,
  "message": "Join link not found",
  "statusCode": 404,
  "errorCode": "NotFound"
}
```

---

## Public Endpoint (No Authentication)

### Visit Join Link

**Endpoint:** `GET /api/join/{joinToken}`

**Authentication:** None (public)

**Behavior:**
1. Validates join link (not revoked, not expired)
2. Builds OIDC state with join link metadata
3. Redirects to Keycloak for authentication
4. After auth, AuthController creates UserTenant with ExternalUserId

**Example:**
```
GET https://api.example.com/api/join/abc123def456789...
```

**Redirects to:**
```
https://keycloak.example.com/realms/groundup/protocol/openid-connect/auth
  ?client_id=groundup-api
  &redirect_uri=https://api.example.com/api/auth/callback
  &response_type=code
  &scope=openid%20email%20profile
  &state=<encrypted_join_link_metadata>
```

---

## cURL Examples

### Create Join Link
```bash
curl -X POST http://localhost:5000/api/tenant-join-links \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "expirationDays": 30
  }'
```

### List Join Links
```bash
curl http://localhost:5000/api/tenant-join-links?pageNumber=1&pageSize=10 \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Get Join Link by ID
```bash
curl http://localhost:5000/api/tenant-join-links/1 \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Revoke Join Link
```bash
curl -X DELETE http://localhost:5000/api/tenant-join-links/1 \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Visit Join Link (Browser)
```
http://localhost:5000/api/join/abc123def456789...
```

---

## Validation Rules

### CreateTenantJoinLinkDto
- `expirationDays`: Must be between 1 and 365
- `defaultRoleId`: Optional, must reference valid Role.Id if provided

### Join Link Validation (Public Endpoint)
- Token must exist
- `IsRevoked` must be false
- `ExpiresAt` must be in the future
- Tenant must exist

---

## Authorization

### Admin Endpoints (TODO)
- Currently requires authentication
- **TODO:** Add admin permission check
  - Check `UserTenant.IsAdmin` OR
  - Check permission: `"tenant.join_links.manage"`

### Public Endpoint
- No authentication required
- Anyone with the link can join (if valid)

---

## Database Schema

### TenantJoinLinks Table
```sql
CREATE TABLE TenantJoinLinks (
  Id INT PRIMARY KEY AUTO_INCREMENT,
  TenantId INT NOT NULL,
  JoinToken VARCHAR(200) NOT NULL UNIQUE,
  IsRevoked BOOLEAN NOT NULL DEFAULT FALSE,
  ExpiresAt DATETIME(6) NOT NULL,
  CreatedAt DATETIME(6) NOT NULL,
  DefaultRoleId INT NULL,
  FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE
);

CREATE INDEX IX_TenantJoinLinks_TenantId ON TenantJoinLinks(TenantId);
CREATE UNIQUE INDEX IX_TenantJoinLinks_JoinToken ON TenantJoinLinks(JoinToken);
```

---

## Frontend Integration Example

### Admin UI: Create Join Link

```typescript
async function createJoinLink(expirationDays: number = 30) {
  const response = await fetch('/api/tenant-join-links', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({ expirationDays })
  });
  
  const result = await response.json();
  
  if (result.success) {
    // Show join URL to admin
    alert(`Share this link: ${result.data.joinUrl}`);
  }
}
```

### Admin UI: List Join Links

```typescript
async function listJoinLinks() {
  const response = await fetch('/api/tenant-join-links?pageNumber=1&pageSize=10', {
    headers: {
      'Authorization': `Bearer ${token}`
    }
  });
  
  const result = await response.json();
  
  if (result.success) {
    // Display join links in table
    result.data.items.forEach(link => {
      console.log(`Link: ${link.joinUrl}, Expires: ${link.expiresAt}`);
    });
  }
}
```

### Admin UI: Revoke Join Link

```typescript
async function revokeJoinLink(linkId: number) {
  const response = await fetch(`/api/tenant-join-links/${linkId}`, {
    method: 'DELETE',
    headers: {
      'Authorization': `Bearer ${token}`
    }
  });
  
  const result = await response.json();
  
  if (result.success) {
    alert('Join link revoked successfully');
  }
}
```

---

## Error Codes

| Code | Status | Description |
|------|--------|-------------|
| `NotFound` | 404 | Join link not found or not accessible |
| `ValidationFailed` | 400 | Invalid request data |
| `InternalServerError` | 500 | Unexpected error |
| `Unauthorized` | 401 | Missing or invalid authentication token |
| `JOIN_LINK_REVOKED` | 400 | Join link has been revoked |
| `JOIN_LINK_EXPIRED` | 400 | Join link has expired |
| `INVALID_JOIN_LINK` | 400 | Join link token is invalid |

---

## Testing Checklist

- [ ] Create join link as admin
- [ ] List join links with pagination
- [ ] Get specific join link by ID
- [ ] Revoke join link
- [ ] Visit join link as unauthenticated user
- [ ] Complete Keycloak authentication
- [ ] Verify UserTenant created with ExternalUserId
- [ ] Verify user has access to tenant
- [ ] Try to visit revoked join link (should fail)
- [ ] Try to visit expired join link (should fail)
- [ ] Try to create join link without authentication (should fail)

---

**All endpoints are now available and ready for frontend integration!** ??
