# Tenant Invitation Creation - Debug Guide

## Problem
Getting foreign key constraint error when creating tenant invitations:
```
MySqlException: Cannot add or update a child row: a foreign key constraint fails 
(`groundup_db`.`TenantInvitations`, CONSTRAINT `FK_TenantInvitations_Users_CreatedByUserId` 
FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`))
```

## Root Cause Analysis

The error occurs because the `CreatedByUserId` being passed doesn't exist in the `Users` table.

## Debugging Steps

### 1. Check Your Database - What Users Exist?

Run this SQL query to see what users are in your database:

```sql
SELECT 
    Id,
    Email,
    Username,
    DisplayName,
    FirstName,
    LastName,
    CreatedAt
FROM Users
ORDER BY CreatedAt DESC;
```

### 2. Check Your Token - What User ID is in the JWT?

Add this temporary endpoint to your `AuthController.cs`:

```csharp
[HttpGet("debug/user-info")]
[Authorize]
public ActionResult<object> DebugUserInfo()
{
    var claims = User.Claims.ToDictionary(c => c.Type, c => c.Value);
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
    return Ok(new
    {
        UserId = userId,
        AllClaims = claims,
        Message = "Check if this UserId matches a record in your Users table"
    });
}
```

Call this endpoint while logged in and compare the `UserId` to the database query results.

### 3. Check Your UserTenant Mapping

The user should also have a `UserTenant` record linking them to the tenant:

```sql
SELECT 
    ut.Id,
    ut.UserId,
    ut.TenantId,
    ut.ExternalUserId,
    ut.IsAdmin,
    ut.JoinedAt,
    u.Email,
    u.Username,
    t.Name as TenantName
FROM UserTenants ut
LEFT JOIN Users u ON ut.UserId = u.Id
LEFT JOIN Tenants t ON ut.TenantId = t.Id
ORDER BY ut.JoinedAt DESC;
```

## Common Scenarios

### Scenario A: User Exists But Token Has Wrong ID
**Symptom**: Database has user records, but token contains a different Guid

**Cause**: Token was generated with wrong user ID or multiple user creation attempts

**Fix**: Log out and log back in to get a fresh token

### Scenario B: User Doesn't Exist At All
**Symptom**: No records in Users table, or missing the user you logged in as

**Cause**: User creation failed during login, but token was still generated

**Fix**: Check `AuthController` logs during login to see if user creation succeeded

### Scenario C: Database Schema Mismatch
**Symptom**: CreatedByUserId should be nullable but constraint rejects NULL

**Cause**: Database migration didn't apply correctly

**Fix**: Check if `CreatedByUserId` column allows NULL in database:

```sql
SHOW COLUMNS FROM TenantInvitations WHERE Field = 'CreatedByUserId';
```

Should show `Null: YES`

## Solution Steps

### Step 1: Verify User Creation on Login

1. Clear your browser cookies
2. Log out completely
3. Log in fresh
4. Check the logs for: `"Created new user {userId} for realm {realm}"`
5. Verify that user ID is in the database

### Step 2: Verify Token Contains Correct User ID

1. Call `/api/auth/debug/user-info` (add the endpoint above)
2. Compare the `UserId` in the response to your database
3. They should match

### Step 3: Try Creating Invitation

1. Make sure you're using the correct tenant context
2. Try creating an invitation
3. Check the logs for: `"Creating invitation for {email} to tenant {tenantId} by user {createdByUserId}"`
4. Verify that `createdByUserId` matches what's in the database

## Quick Test

Run this complete test flow:

```bash
# 1. Login and get token
curl -X GET "https://localhost:7128/api/auth/login" -k

# 2. Follow redirect, authenticate, get cookie

# 3. Check your user ID
curl -X GET "https://localhost:7128/api/auth/debug/user-info" \
  -H "Cookie: AuthToken=YOUR_TOKEN" -k

# 4. Check database
mysql -u root -p groundup_db -e "SELECT Id, Email FROM Users;"

# 5. Try creating invitation
curl -X POST "https://localhost:7128/api/invitations" \
  -H "Content-Type: application/json" \
  -H "Cookie: AuthToken=YOUR_TOKEN" \
  -d '{
    "email": "test@example.com",
    "isAdmin": false,
    "expirationDays": 7
  }' -k
```

## Expected Log Output

When creating an invitation successfully, you should see:

```
[INFO] Creating invitation for test@example.com to tenant 1 by user a1b2c3d4-e5f6-...
[INFO] Created invitation ID 1 with token abc123...
```

When it fails, you should see:

```
[INFO] Creating invitation for test@example.com to tenant 1 by user a1b2c3d4-e5f6-...
[ERROR] User a1b2c3d4-e5f6-... not found in Users table
```

This tells you exactly which user ID is missing from the database.

## Still Having Issues?

If the user ID in the token doesn't match the database:

1. **Option A - Simple**: Make `CreatedByUserId` nullable in the database and allow system-generated invitations
2. **Option B - Complete**: Fix the user creation flow to ensure users are always created before tokens are issued

For Option A, run this migration:

```sql
ALTER TABLE TenantInvitations 
MODIFY COLUMN CreatedByUserId CHAR(36) NULL;
```

Then you can create invitations without a valid user (for system-generated invites).
