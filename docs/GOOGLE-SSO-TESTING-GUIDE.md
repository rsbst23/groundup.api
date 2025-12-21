# Google SSO Testing Guide

## Prerequisites
- Enterprise tenant created (e.g., realm: `acme-corp`)
- Google OAuth credentials configured in Keycloak
- First admin already set up with local account

## Testing Scenarios

### Scenario 1: Invite User ? Google SSO Login

**Step 1: Create invitation (as tenant admin)**
```bash
# Login as enterprise admin first
POST http://localhost:5123/api/auth/login
Content-Type: application/json

{
  "email": "admin@acme-corp.com",
  "password": "your-password",
  "tenantId": 1
}

# Create invitation for new user
POST http://localhost:5123/api/invitations
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "email": "newuser@gmail.com",
  "roleId": 2,
  "message": "Welcome to Acme Corp!"
}
```

**Step 2: Accept invitation (as new user)**
```bash
# Get invitation token from email or database
GET http://localhost:5123/api/invitations/invite/{invitation-token}

# This should:
# 1. Redirect to Keycloak login page for acme-corp realm
# 2. Show "Sign in with Google" button
# 3. After Google auth, redirect back to your app
# 4. Automatically assign user to tenant with invited role
```

**Step 3: Verify user can login with Google**
```bash
# Try logging in via domain-based login
POST http://localhost:5123/api/auth/login
Content-Type: application/json

{
  "email": "newuser@gmail.com"
}

# Expected: Redirects to Google SSO, then back with JWT token
```

---

### Scenario 2: Manual SSO Test (Without Invitation)

**Step 1: Navigate directly to enterprise realm login**
```
http://localhost:8080/realms/acme-corp/account
```

**Step 2: Click "Sign in with Google"**

**Step 3: Complete Google OAuth flow**

**Step 4: Verify user created in Keycloak**
- Check Keycloak Admin ? Users
- Should see new user with Google as identity provider link

**Step 5: Sync user to your database**
```bash
# Login via your API (this triggers UserSyncMiddleware)
POST http://localhost:5123/api/auth/login
Content-Type: application/json

{
  "email": "googlessouser@gmail.com"
}
```

---

## Verification Checklist

### In Keycloak
- [ ] User exists in enterprise realm
- [ ] User has "Federated Identity" link to Google
- [ ] User's email is verified (from Google)

### In Your Database
- [ ] User record created in `users` table
- [ ] If invited: `user_tenants` record exists with correct role
- [ ] If not invited: No tenant assignment yet

### In Your Application
- [ ] User can login via `/api/auth/login` with Google email
- [ ] JWT token contains correct realm claim
- [ ] User can access tenant resources (if assigned)

---

## Debugging Common Issues

### Issue: "Sign in with Google" button not showing
**Solution**: 
- Verify Google IDP is enabled in realm
- Check redirect URI matches exactly in Google Cloud Console
- Clear browser cache and try incognito mode

### Issue: "Invalid redirect_uri" error from Google
**Solution**:
```
Add to Google Cloud Console ? Authorized Redirect URIs:
http://localhost:8080/realms/{your-realm}/broker/google/endpoint
```

### Issue: User authenticates but doesn't appear in database
**Solution**: Login via your API endpoint to trigger `UserSyncMiddleware`:
```bash
POST /api/auth/login with user's Google email
```

### Issue: User has no tenant assignment after SSO
**Expected Behavior**: 
- SSO alone doesn't assign tenants
- User must either:
  1. Be invited first (invitation ? SSO ? auto-assignment)
  2. Be manually assigned by admin after SSO

---

## Testing Matrix

| Scenario | Expected Outcome |
|----------|------------------|
| **New user with invitation ? Google SSO** | User created, tenant assigned with invited role |
| **Existing Keycloak user ? Google SSO** | Synced to DB, no tenant assignment |
| **Invited user (existing in Keycloak) ? Accept** | Existing user assigned to tenant |
| **Admin invites non-Google email** | User gets execute-actions email (password setup) |
| **Admin invites Google email** | User can login via Google immediately |

---

## Next Steps After Testing

Once Google SSO works:

1. **Document SSO configuration** for production
2. **Add Azure AD** for enterprise customers using Microsoft
3. **Add SAML providers** for larger enterprises
4. **Implement SSO auto-provisioning** (optional: auto-create users on first SSO login)

---

## Production Considerations

### Redirect URIs for Production
```
https://auth.yourapp.com/realms/{realm}/broker/google/endpoint
https://auth.yourapp.com/realms/*/broker/google/endpoint
```

### Google OAuth Consent Screen
- Configure consent screen in Google Cloud Console
- Add your production domain
- Verify domain ownership
- Submit for verification if needed (for more than 100 users)

### Security
- Don't share client secrets
- Use environment variables for secrets
- Rotate credentials periodically
- Monitor OAuth usage in Google Cloud Console
