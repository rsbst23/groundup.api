# ?? Email Verification with OAuth Registration

## What You Need to Know

When you register through Keycloak's **OAuth registration endpoint**, the OAuth context is **preserved through email verification**.

## The Correct Flow

### OAuth Registration Flow (Recommended)
```
User navigates to OAuth registration URL
?
http://localhost:8080/realms/tenant_acme_xxx/protocol/openid-connect/registrations?client_id=groundup-api&redirect_uri=http://localhost:5123/api/auth/callback&response_type=code&scope=openid&state=...
?
User fills out registration form
?
User clicks "Register"
?
Keycloak sends verification email
?
Keycloak shows "You need to verify your email address"
?
User clicks verification link in email
?
Keycloak verifies email ?
?
**Keycloak automatically continues the OAuth flow**
?
Keycloak redirects to: http://localhost:5123/api/auth/callback?code=XXXXX&state=...
?
API exchanges code for tokens ?
?
API creates user record, accepts invitation, assigns to tenant ?
?
Success!
```

## Why This Works

When you use the OAuth registration endpoint:
- Keycloak stores the OAuth session (client_id, redirect_uri, state, etc.)
- After email verification, Keycloak **resumes that session**
- The OAuth flow continues as if email verification never interrupted it
- You end up at the callback URL with an authorization code

## What Was Wrong Before

If you:
1. Manually created a user in Keycloak Admin Console
2. Enabled email verification
3. User got a verification email
4. User clicked the verification link

Then **there was no OAuth session to resume**, so Keycloak just verified the email and stopped. No redirect, no OAuth flow.

## How to Test Correctly

### Step 1: Build Registration URL with State

```javascript
// In browser console
const state = {
  flow: "invitation",
  invitationToken: "YOUR_ACTUAL_TOKEN",
  realm: "YOUR_ACTUAL_REALM"
};

const stateEncoded = btoa(JSON.stringify(state));

const registrationUrl = `http://localhost:8080/realms/${state.realm}/protocol/openid-connect/registrations?client_id=groundup-api&redirect_uri=http://localhost:5123/api/auth/callback&response_type=code&scope=openid&state=${stateEncoded}`;

console.log("Registration URL:", registrationUrl);
```

### Step 2: Register Through OAuth

1. Copy the registration URL from console
2. Paste in browser
3. Fill out registration form
4. Click "Register"
5. Check email for verification link
6. Click verification link
7. **Automatically redirected to callback**
8. API completes setup
9. Done!

## Troubleshooting

### "Token exchange failed" error
This means you clicked a verification link that **wasn't part of an OAuth flow**. 

**Solution**: Don't manually create users in Keycloak Admin and trigger verification. Instead, use the OAuth registration URL which maintains the OAuth context.

### No redirect after email verification
This means the email verification link didn't have an OAuth session associated with it.

**Solution**: Make sure you started the registration process via the OAuth registration URL, not through the Keycloak Admin Console.

### User created but not in database
This means the callback wasn't triggered.

**Solution**: Verify you used the OAuth registration URL with the correct `redirect_uri` and `state` parameters.

## Key Takeaway

? **Always use the OAuth registration URL** for new enterprise tenant users  
? **Don't create users manually** in Keycloak Admin Console (unless testing without OAuth flow)

The OAuth registration URL ensures the entire flow (registration ? email verification ? callback ? user setup) happens automatically.

---

**Status:** Ready for Testing  
**Last Updated:** 2025-12-03  
**Phase:** 5 - Enterprise Tenant Provisioning
