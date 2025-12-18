# Invitation Controller Consolidation

## Problem

The codebase had **two controllers** handling invitation operations:
1. `TenantInvitationController` - Admin CRUD operations
2. `InvitationController` - User acceptance operations

### Issues:
- **Route collision**: Both used `[Route("api/invitations")]`
- **Confusing API**: API consumers had to know which controller handled which operation
- **No clear separation**: Both handled standard and enterprise invitations
- **Code duplication**: Both injected the same repositories

## Solution

Merged both controllers into a single **`InvitationController`** at `api/invitations`.

## New Unified Structure

### Route: `api/invitations`

All invitation operations are now in one place, organized by purpose:

#### 1. Tenant-Scoped Admin Operations
These require authentication and use the tenant context from the JWT:

- `GET /api/invitations` - List invitations (paginated)
- `GET /api/invitations/{id}` - Get invitation by ID
- `POST /api/invitations` - Create invitation
- `PUT /api/invitations/{id}` - Update invitation
- `DELETE /api/invitations/{id}` - Delete invitation
- `GET /api/invitations/pending` - Get pending invitations
- `POST /api/invitations/{id}/resend` - Resend invitation

#### 2. Cross-Tenant User Operations
These work across all tenants or require no authentication:

- `GET /api/invitations/me` - Get my invitations (all tenants)
- `POST /api/invitations/accept` - Accept an invitation
- `GET /api/invitations/token/{token}` - Preview invitation (public)

#### 3. Public Invitation Flow Endpoints
These handle the OAuth redirect flow:

- `GET /api/invitations/invite/{token}` - Standard invitation redirect (shared realm)
- `GET /api/invitations/enterprise/invite/{token}` - Enterprise invitation redirect (dedicated realm)

## Benefits

1. **Clear API structure**: All invitation operations in one controller
2. **No route collisions**: Single route registration
3. **Better organization**: Operations grouped by region comments
4. **Easier maintenance**: One place to find and modify invitation logic
5. **Better Swagger docs**: Single "Invitations" section in API documentation

## Testing Impact

**Your test URL remains the same:**
```
http://localhost:5123/api/invitations/invite/{invitationToken}
```

All existing endpoints continue to work - this is purely a consolidation of the controller code.

## Code Changes

### Files Modified:
- ? `GroundUp.api/Controllers/InvitationController.cs` - Merged all functionality

### Files Removed:
- ? `GroundUp.api/Controllers/TenantInvitationController.cs` - Deleted (functionality merged)

### Build Status:
? All changes compiled successfully

## Migration Notes

For any external documentation or API clients:
- All endpoint routes remain **unchanged**
- All DTOs remain **unchanged**
- Authorization behavior remains **unchanged**
- Only the internal controller structure changed

No breaking changes to the API surface.
