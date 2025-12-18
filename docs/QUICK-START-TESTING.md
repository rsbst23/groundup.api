# ?? Quick Start: Testing Enterprise Tenant with OAuth Registration

## What You Need

From your enterprise tenant creation (Step 1), you should have:
- ? Realm name (e.g., `tenant_acme_a3f2`)
- ? Invitation token (e.g., `abc123...`)
- ? Contact email (e.g., `rsbst23@yahoo.com`)

## Recommended: OAuth Registration Flow

This is the **easiest and most reliable** method. It handles everything automatically.

### 1?? Build Registration URL

Open browser console and run:

```javascript
const state = {
  flow: "invitation",
  invitationToken: "PASTE_YOUR_TOKEN_HERE",
  realm: "PASTE_YOUR_REALM_HERE"
};

const stateEncoded = btoa(JSON.stringify(state));

const registrationUrl = `http://localhost:8080/realms/${state.realm}/protocol/openid-connect/registrations?client_id=groundup-api&redirect_uri=http://localhost:5123/api/auth/callback&response_type=code&scope=openid&state=${stateEncoded}`;

console.log("Open this URL to register:");
console.log(registrationUrl);
```

### 2?? Register

1. Copy the URL from console
2. Paste in browser
3. Fill out registration form:
   - Username: `jane.doe`
   - Email: `rsbst23@yahoo.com` (must match invitation email)
   - First Name: `Jane`
   - Last Name: `Doe`
   - Password: `SecurePassword123!`
4. Click "Register"

### 3?? Verify Email

1. Check your email for verification link
2. Click the link
3. **Automatically redirected to callback!**

### 4?? Verify Success

You should see a JSON response like:

```json
{
  "success": true,
  "data": {
    "flow": "invitation",
    "success": true,
    "token": "eyJhbGci...",
    "tenantId": 1,
    "tenantName": "Acme Corporation",
    "requiresTenantSelection": false,
    "message": "Invitation accepted successfully"
  }
}
```

### 5?? Check Database

```sql
-- Check user created
SELECT * FROM Users WHERE Email = 'rsbst23@yahoo.com';

-- Check identity mapping
SELECT * FROM UserKeycloakIdentities WHERE RealmName = 'YOUR_REALM';

-- Check invitation accepted
SELECT * FROM TenantInvitations WHERE ContactEmail = 'rsbst23@yahoo.com';

-- Check tenant assignment
SELECT * FROM UserTenants WHERE UserId = 'YOUR_USER_ID';
```

## Alternative: Manual Login (If You Used Admin Console)

Only use this if you created the user via Keycloak Admin Console instead of OAuth registration.

### 1?? Build Login URL

```javascript
const state = {
  flow: "invitation",
  invitationToken: "PASTE_YOUR_TOKEN_HERE",
  realm: "PASTE_YOUR_REALM_HERE"
};

const stateEncoded = btoa(JSON.stringify(state));

console.log("Your login URL:");
console.log(`http://localhost:8080/realms/${state.realm}/protocol/openid-connect/auth?client_id=groundup-api&redirect_uri=http://localhost:5123/api/auth/callback&response_type=code&scope=openid&state=${stateEncoded}`);
```

### 2?? Login

1. Copy the complete URL from console
2. Paste in browser
3. Login with your credentials
4. Redirected to callback
5. Check for success response

## Common Issues

### "You need to verify your email"
? **Normal!** Check your email and click the verification link. After verification, Keycloak will automatically redirect to the callback.

### "Token exchange failed"
?? This means you clicked a verification link that wasn't part of an OAuth flow. Use the OAuth registration URL instead.

### "Invalid redirect_uri"
Check Keycloak Admin ? Realms ? YOUR_REALM ? Clients ? groundup-api ? Valid Redirect URIs
Should include: `http://localhost:5123/api/auth/callback`

### Email verification link doesn't redirect
Make sure you started registration via the OAuth registration URL, not through the admin console.

## Why OAuth Registration?

| OAuth Registration URL | Admin Console |
|------------------------|---------------|
| ? Automatic callback after email verification | ? Manual login required |
| ? Single-step process | ? Multi-step process |
| ? Preserves invitation context | ?? Need to remember token |
| ? Better UX | ? More complex |

## Need More Help?

- `docs/EMAIL-VERIFICATION-TESTING.md` - Detailed OAuth flow explanation
- `docs/MANUAL-TESTING-GUIDE.md` - Complete testing guide
- `docs/PHASE5-IMPLEMENTATION-COMPLETE.md` - Implementation details

---

**Quick Tip:** Always use the OAuth registration URL for the smoothest experience!
