# ?? Test Enterprise Tenant Flow RIGHT NOW

## Copy-Paste Testing Instructions

### Step 1: Get Your Realm and Token

From your earlier enterprise tenant creation, you should have received:
- Realm name (e.g., `tenant_acme_a3f2`)
- Invitation token (a long string)

### Step 2: Open Browser Console

1. Open a new browser tab
2. Press F12 to open Developer Tools
3. Go to "Console" tab
4. Paste this code (replace the placeholders):

```javascript
const state = {
  flow: "invitation",
  invitationToken: "YOUR_INVITATION_TOKEN_HERE", // Replace this
  realm: "YOUR_REALM_NAME_HERE" // Replace this
};

const stateEncoded = btoa(JSON.stringify(state));

const registrationUrl = `http://localhost:8080/realms/${state.realm}/protocol/openid-connect/registrations?client_id=groundup-api&redirect_uri=http://localhost:5123/api/auth/callback&response_type=code&scope=openid&state=${stateEncoded}`;

console.log("=".repeat(80));
console.log("COPY THE URL BELOW:");
console.log("=".repeat(80));
console.log(registrationUrl);
console.log("=".repeat(80));
```

5. Press Enter

### Step 3: Copy the Registration URL

You should see output like:
```
================================================================================
COPY THE URL BELOW:
================================================================================
http://localhost:8080/realms/tenant_acme_a3f2/protocol/openid-connect/registrations?client_id=groundup-api&redirect_uri=http://localhost:5123/api/auth/callback&response_type=code&scope=openid&state=eyJmbG93IjoiaW52aXRhdGlvbiIsImludml0YXRpb25Ub2tlbiI6ImFiYzEyMy4uLiIsInJlYWxtIjoidGVuYW50X2FjbWVfYTNmMiJ9
================================================================================
```

Copy the entire URL.

### Step 4: Register

1. Paste the URL in your browser address bar
2. Press Enter
3. You should see a Keycloak registration form
4. Fill it out:
   - **Username:** `jane.doe`
   - **Email:** `rsbst23@yahoo.com` (must match your invitation email)
   - **First Name:** `Jane`
   - **Last Name:** `Doe`
   - **Password:** `SecurePassword123!`
   - **Confirm Password:** `SecurePassword123!`
5. Click **"Register"**

### Step 5: Verify Email

1. You should see a message: "You need to verify your email address"
2. **Check your email** at `rsbst23@yahoo.com`
3. Find the verification email from Keycloak
4. **Click the verification link** in the email

### Step 6: Success!

After clicking the verification link, you should be **automatically redirected** to:

```
http://localhost:5123/api/auth/callback?code=...&state=...
```

And you should see a JSON response like:

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
  },
  "message": "Authentication successful",
  "errors": null,
  "statusCode": 200,
  "errorCode": null
}
```

**?? You're done!** Your user has been:
- ? Created in Keycloak
- ? Email verified
- ? Added to your GroundUp database
- ? Assigned to the enterprise tenant
- ? Given an authentication token

### Step 7: Verify in Database

Run these SQL queries to confirm everything worked:

```sql
-- Check user created
SELECT * FROM Users WHERE Email = 'rsbst23@yahoo.com';

-- Check identity mapping
SELECT * FROM UserKeycloakIdentities 
WHERE RealmName = 'YOUR_REALM_NAME'; -- Replace with your realm

-- Check invitation accepted
SELECT * FROM TenantInvitations 
WHERE ContactEmail = 'rsbst23@yahoo.com';
-- Should show IsAccepted = 1, AcceptedAt = recent timestamp

-- Check tenant assignment
SELECT ut.*, t.Name as TenantName
FROM UserTenants ut
JOIN Tenants t ON ut.TenantId = t.Id
WHERE ut.UserId = (SELECT Id FROM Users WHERE Email = 'rsbst23@yahoo.com');
-- Should show IsAdmin = 1
```

## Troubleshooting

### "You need to verify your email" but no email received
- Check spam folder
- Verify SMTP credentials in `.env` file
- Check AWS SES Console for sender verification
- Look for errors in API logs

### Redirect doesn't happen after verification
- Make sure you used the OAuth registration URL (from Step 2)
- Check browser console for errors
- Verify the `groundup-api` client exists in your realm
- Confirm redirect URI is `http://localhost:5123/api/auth/callback`

### "Invalid redirect_uri" error
- Go to Keycloak Admin Console
- Select your realm
- Go to Clients ? groundup-api
- Check Valid Redirect URIs includes: `http://localhost:5123/api/auth/callback`

### JSON response shows success: false
- Check API logs for error details
- Verify invitation token is valid and not expired
- Confirm email matches the invitation
- Check database for any constraint violations

## Next Steps

After successful registration and callback:

1. **Test subsequent login** (without invitation):
```javascript
const state = { flow: "default", realm: "YOUR_REALM_NAME" };
const stateEncoded = btoa(JSON.stringify(state));
const loginUrl = `http://localhost:8080/realms/YOUR_REALM_NAME/protocol/openid-connect/auth?client_id=groundup-api&redirect_uri=http://localhost:5123/api/auth/callback&response_type=code&scope=openid&state=${stateEncoded}`;
console.log(loginUrl);
```

2. **Test realm resolution**:
```bash
curl -X POST "http://localhost:5123/api/tenants/resolve-realm" \
  -H "Content-Type: application/json" \
  -d '{"url": "acme"}' # Use your subdomain
```

3. **Test tenant management endpoints** with your auth token

---

**Pro Tip:** Save your realm name and registration URL template for future testing!
