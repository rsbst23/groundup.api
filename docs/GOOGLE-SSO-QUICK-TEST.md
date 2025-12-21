# Google SSO - 5 Minute Test

## Prerequisites ?
- [x] Google OAuth consent screen configured
- [x] OAuth Client ID created
- [x] Keycloak Google IDP configured in enterprise realm

---

## Quick Manual Test (No Scripts)

### Test 1: Direct Keycloak Login with Google

**Step 1**: Navigate to your enterprise realm login
```
http://localhost:8080/realms/{your-realm}/account
```

**Step 2**: You should see:
- Username/password fields
- **"Sign in with Google"** button ? This proves IDP is working!

**Step 3**: Click "Sign in with Google"
- Google OAuth consent screen appears
- Approve access
- Redirected back to Keycloak account page
- ? User logged in with Google!

**Verify**: 
- Go to Keycloak Admin ? Users
- See new user with Google federated identity

---

### Test 2: SSO via Invitation Flow

**Step 1**: Create invitation via Swagger or Postman
```http
POST http://localhost:5123/api/invitations
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "email": "testuser@gmail.com",
  "roleId": 2,
  "message": "Welcome!"
}
```

**Response**:
```json
{
  "success": true,
  "data": {
    "token": "abc123...",
    "expiresAt": "..."
  }
}
```

**Step 2**: Open invitation URL in browser
```
http://localhost:5123/api/invitations/invite/{token}
```

**Expected Flow**:
```
1. Your API receives invitation acceptance
   ?
2. Redirects to Keycloak login for enterprise realm
   ?
3. User sees "Sign in with Google" button
   ?
4. User clicks ? Google OAuth
   ?
5. Google approves ? Redirects back to Keycloak
   ?
6. Keycloak creates/links user
   ?
7. Redirects back to your API with auth code
   ?
8. Your API exchanges code for token
   ?
9. User assigned to tenant with invited role
   ?
10. ? Success! User can login via API
```

**Step 3**: Verify user can login via API
```http
POST http://localhost:5123/api/auth/login
Content-Type: application/json

{
  "email": "testuser@gmail.com"
}
```

**Expected**: User authenticates via Google SSO and receives JWT token

---

## Troubleshooting

### Issue: No "Sign in with Google" button

**Check 1**: Google IDP enabled in Keycloak?
- Admin Console ? Identity Providers ? google ? Toggle ON

**Check 2**: Redirect URI matches?
- Google Cloud Console: `http://localhost:8080/realms/*/broker/google/endpoint`
- Or specific realm: `http://localhost:8080/realms/acme-corp/broker/google/endpoint`

**Check 3**: Browser cache?
- Open incognito window
- Hard refresh (Ctrl+Shift+R)

---

### Issue: "redirect_uri_mismatch" error

**Fix**: Add EXACT redirect URI to Google Cloud Console:
```
Go to: APIs & Services ? Credentials ? Edit OAuth client
Add: http://localhost:8080/realms/{your-realm}/broker/google/endpoint
```

---

### Issue: User authenticates but not in database

**Expected Behavior**: 
- First SSO login creates user in Keycloak only
- User must login via your API endpoint to sync to database

**Fix**: Login via API after SSO:
```http
POST http://localhost:5123/api/auth/login
Content-Type: application/json

{
  "email": "testuser@gmail.com"
}
```

This triggers `UserSyncMiddleware` which creates user in your database.

---

## Success Indicators

### ? Google IDP Working
- "Sign in with Google" button appears on Keycloak login
- Can complete Google OAuth flow
- User created in Keycloak with federated identity link

### ? Invitation + SSO Working
- Invited user can accept invitation
- Redirected to Keycloak with Google SSO option
- After SSO, user assigned to tenant with invited role
- User can login via API

### ? Domain-Based Login Working
```http
POST /api/auth/login
{
  "email": "user@gmail.com"  # No tenant ID needed!
}
```
- API detects user's realm
- Redirects to Keycloak with Google SSO
- Returns JWT token with tenant assignment

---

## What This Proves

? **Multi-realm SSO**: Each enterprise tenant has own realm with own SSO config  
? **Google Identity Brokering**: Keycloak federates to Google  
? **Real OAuth/OIDC Flow**: Standard enterprise SSO pattern  
? **Production-Ready Pattern**: Same approach works for Azure AD, Okta, etc.  

---

## Next Steps After Success

1. **Add Azure AD** for Microsoft SSO testing
2. **Document SSO setup** for customers
3. **Add SSO provider detection** (auto-redirect Gmail users to Google)
4. **Configure production redirect URIs**

---

## Production Checklist

- [ ] Google OAuth consent screen verified (for 100+ users)
- [ ] Production redirect URIs added
- [ ] Client secrets stored in environment variables
- [ ] HTTPS enabled for production Keycloak
- [ ] Custom domain for Keycloak (e.g., auth.yourapp.com)
