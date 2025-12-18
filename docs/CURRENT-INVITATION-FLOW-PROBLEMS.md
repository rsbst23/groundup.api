# Current Enterprise Invitation Flow - Problems & Analysis

## What We're Trying to Do (Simple Goal)

**Enterprise tenant provisioning with invitation-based onboarding:**
1. Create enterprise tenant
2. Send invitation email to admin
3. Admin clicks link, sets up account
4. Admin is logged in and assigned to tenant

**This is standard functionality in every SaaS app.**

---

## Current Implementation

### Enterprise Tenant Creation
```
POST /api/tenants/enterprise/signup
{
  "companyName": "Acme Corp",
  "contactEmail": "admin@acme.com",
  "contactName": "Jane Doe",
  "customDomain": "https://www.acmecorp.com",
  "requestedSubdomain": "acme",
  "plan": "enterprise-trial"
}

API Creates:
1. Tenant record in database (TenantType = "enterprise")
2. Dedicated Keycloak realm (e.g., "tenant_acme_a3f2")
3. Client in realm ("groundup-api")
4. Invitation record in database (TenantInvitations table)
5. Sends invitation email
```

### Current Invitation Email
```
Subject: You're invited to join Acme Corp

Click here to accept: http://localhost:5123/accept-invitation?token=abc123...

(Currently points to registration URL)
```

### Current User Registration Flow (The Broken Part)

**What we're doing now:**
1. Invitation email contains Keycloak registration URL
2. User clicks ? goes to Keycloak registration page
3. User fills out form (username, email, password, name)
4. User clicks "Register"

**In Production (email verification enabled):**
5. Keycloak creates user (NOT YET VERIFIED)
6. Keycloak sends verification email
7. User clicks verification link
8. **OAuth SESSION IS LOST** ?
9. Email is verified but user is NOT logged in
10. User must manually navigate to login page
11. User must remember their credentials
12. User logs in
13. THEN the callback code runs
14. User is finally set up

**Problem:** Steps 8-12 are a terrible user experience.

---

## The Core Problem

### Keycloak's Email Verification Doesn't Preserve OAuth Context

**What happens:**
```
1. User starts OAuth registration flow
   URL: /realms/tenant_acme/protocol/openid-connect/registrations?
        client_id=groundup-api&
        redirect_uri=http://localhost:5123/api/auth/callback&
        state=<invitation_token>

2. Keycloak creates a session with all OAuth parameters

3. User submits registration form

4. Keycloak sends verification email with action token

5. User clicks verification link:
   URL: /realms/tenant_acme/login-actions/action-token?key=...

6. Keycloak verifies email ?

7. BUT: The action token flow is SEPARATE from the OAuth flow

8. Keycloak tries to redirect but has lost the OAuth context

9. Redirect happens with invalid/expired authorization code
   
10. Token exchange fails ?

11. **This is a known limitation of Keycloak's architecture.**
    
---

## Why This is Hard

### The Email Mismatch Problem

**Invitation sent to:** `admin@acme.com`

**User could register with:** `jane@example.com`

**Question:** Should we allow this?

**If YES:**
- User has freedom to use preferred email
- But how do we verify they received the invitation?
- They could share the invitation link with anyone

**If NO:**
- We lock them to the invitation email
- But then why have a registration form at all?
- We could pre-create the user

---

## Our Design Principle (Currently)

**"Keycloak owns all user creation and authentication"**

**What this means:**
- We don't create users via API
- We don't store passwords
- We don't handle authentication
- Keycloak is the source of truth for user identity

**Why this is good:**
- ? Security is Keycloak's problem
- ? We don't handle sensitive data
- ? Standard OAuth/OIDC flows
- ? Can add SSO, MFA, etc. via Keycloak

**Where this breaks down:**
- ? Keycloak's self-service registration doesn't fit invitation model
- ? Email verification breaks OAuth flow
- ? No way to link invitation to registration without pre-creating user

---

## Attempted Solutions (All Have Issues)

### Solution 1: Disable Email Verification
**What:** Turn off email verification in development
**Problem:** Doesn't solve production flow; just hides the problem

### Solution 2: Pre-create User with UPDATE_PASSWORD
**What:** Create user in Keycloak when invitation sent
**Problems:**
- Security issue: Anyone with login URL can set password
- Violates "Keycloak owns user creation" principle
- User account exists before user accepts invitation

### Solution 3: Registration URL with State Preservation
**What:** Use OAuth registration endpoint with state parameter
**Problem:** Email verification still breaks OAuth session (doesn't actually work in Keycloak)

### Solution 4: API-Based User Creation
**What:** API endpoint accepts invitation + password, creates Keycloak user
**Problems:**
- API now handles user creation (violates design principle)
- API receives plaintext password (security concern)
- Duplicates Keycloak functionality

---

## What Other Systems Do

### Auth0 Approach
1. Create user via Management API when invitation sent
2. User account exists with "change password" required action
3. Send email with magic link (single-use token)
4. User clicks link ? sets password ? logged in
5. No separate email verification (invitation proves email ownership)

### Okta Approach
1. Create user via API when invitation sent
2. Set activation status to "PENDING"
3. Send activation email
4. User clicks link ? activates account ? sets password
5. User is redirected to login ? completes OAuth flow

### AWS Cognito Approach
1. Create user via AdminCreateUser API
2. User status: FORCE_CHANGE_PASSWORD
3. Send temporary password via email
4. User logs in with temp password ? forced to change
5. OAuth flow completes

**Common pattern:**
- ? All pre-create the user
- ? All use their management APIs
- ? All rely on email link as proof of ownership
- ? No separate email verification step

---

## The Fundamental Tension

### We Want:
1. Keycloak to own user creation
2. Clean OAuth flow
3. Email verification for security
4. Invitation-based onboarding

### Keycloak Provides:
1. Self-service registration ?
2. OAuth/OIDC flows ?
3. Email verification ?
4. Admin API for user creation ?

### What Doesn't Work Together:
- Self-service registration + invitation model
- Email verification + OAuth flow preservation
- No pre-creation + secure invitation acceptance

---

## Current State

### What's Broken (Production Mode)
- ? Email verification breaks OAuth flow
- ? User must manually login after verification
- ? Poor user experience
- ? Invitation context can be lost

### What's Insecure (Any Mode)
- ?? Invitation link can be shared (no single-use enforcement)
- ?? No verification that registrant owns the invited email
- ?? Pre-created users (if we go that route) are vulnerable

---

## Questions for ChatGPT

1. **Is there a way to make Keycloak's email verification preserve OAuth session?**
   - Custom authenticator?
   - Custom required action?
   - Different OAuth flow?

2. **How do enterprise systems handle invitation-based onboarding with Keycloak?**
   - Do they all pre-create users?
   - Do they use a different identity provider?
   - Do they build custom flows?

3. **Is it acceptable to have API create users in Keycloak via Admin API?**
   - Does this violate separation of concerns?
   - What are the security implications?
   - How do we handle password securely?

4. **What's the "right" architecture here?**
   - Should we rethink using Keycloak for this use case?
   - Should we accept the manual login step after verification?
   - Should we build a custom Keycloak extension?

---

## Our Constraints

1. **Multi-tenant:** Each enterprise gets their own realm
2. **Invitation-based:** Users don't self-sign-up; they're invited
3. **Security:** Must verify email ownership
4. **User experience:** Should be smooth, not require manual steps
5. **Maintainability:** Don't want complex custom code
6. **Standards:** Want to use OAuth/OIDC properly

---

## Files to Reference

- `GroundUp.api/Controllers/AuthController.cs` - OAuth callback handling
- `GroundUp.api/Controllers/TenantController.cs` - Enterprise signup endpoint
- `GroundUp.infrastructure/services/IdentityProviderAdminService.cs` - Keycloak admin API
- `GroundUp.infrastructure/repositories/TenantInvitationRepository.cs` - Invitation logic
- `docs/PHASE5-IMPLEMENTATION-COMPLETE.md` - Current implementation details

---

## The Question

**How do we implement secure, invitation-based enterprise tenant onboarding with Keycloak while maintaining:**
- Clean OAuth flows
- Email verification
- Good user experience
- Separation of concerns (Keycloak owns auth)
- No custom Keycloak extensions if possible

**Is this even possible with Keycloak's architecture?**
