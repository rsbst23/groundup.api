# ?? Multi-Realm Identity Architecture - Design Complete!

## Executive Summary

We have **completed the design phase** for GroundUp's multi-realm identity architecture. The design has been thoroughly reviewed, refined, and is now **approved for implementation**.

---

## ?? Design Status

| Aspect | Status | Confidence |
|--------|--------|------------|
| **Core Architecture** | ? Approved | HIGH |
| **Security Design** | ? Approved | HIGH |
| **Operational Procedures** | ? Documented | HIGH |
| **Risk Assessment** | ? Complete | LOW RISK |
| **Implementation Plan** | ? Ready | HIGH |

---

## ?? What We Accomplished

### **1. Core Architecture Design** ?

**Identity Anchor Pattern:**
```
(RealmName, KeycloakUserId) ? Users.Id
```

**Key Decisions:**
- ? Keycloak is single source of truth
- ? Email is metadata, not identity
- ? Manual account linking (not automatic)
- ? Separate realms for enterprise tenants
- ? Invitations as primary tenant association mechanism

**Database Schema:**
- ? `UserKeycloakIdentities` - identity anchor
- ? `AccountLinkTokens` - cross-realm linking
- ? `Users` - email/username nullable
- ? `Tenants` - Plan, UpdatedAt added
- ? `TenantInvitations` - ContactEmail (required)

---

### **2. Design Issues Found & Fixed** ?

#### **Issue #1: Orphaned Keycloak Identities** ??
**Solution:** Background cleanup job (Hangfire)
- Daily scan for orphaned identities
- Soft delete ? grace period ? hard delete
- See: `docs/BACKGROUND-CLEANUP-JOB-DESIGN.md`

#### **Issue #2: Unused Enterprise Tenants** ??
**Solution:** Automated cleanup after 30 days
- Email warning at 15 days
- Delete Keycloak realm
- Soft delete tenant

#### **Issue #3: Realm Immutability** ??
**Solution:** Explicit immutability policy
- Realm names cannot be changed
- Migration = new realm + paid service
- Documented in Section 7

#### **Issue #4: Invitation Race Condition** ?? **CRITICAL**
**Solution:** Atomic transactions
- Wrap user creation + invitation acceptance in single transaction
- Prevents duplicate users
- Prevents double-acceptance
- Added to Sections 4.1, 5.2, 9

#### **Issue #5: Bootstrap Admin Lockout** ?? **CRITICAL**
**Solution:** Multi-layer security
1. Email verification required
2. Break-glass admin account
3. Minimum 2 admins before SSO-only
4. Secrets manager integration
5. Full audit trail
- See: `docs/ENTERPRISE-BOOTSTRAP-SECURITY.md`

---

## ?? Updated Design Documents

### **Main Design Document**
**File:** `C:\Users\Rob\Downloads\groundup-auth-architecture.md`

**New Sections Added:**
1. **Section 7: Realm Immutability**
   - Naming strategy
   - Migration process
   - Cost considerations

2. **Section 9: Enterprise Realm Bootstrap Security**
   - Email verification requirements
   - Break-glass admin accounts
   - Minimum admin enforcement
   - Secrets manager integration
   - Audit trail

3. **Section 9: Atomic Operations and Race Conditions**
   - Transaction patterns for invitation acceptance
   - Transaction patterns for new org creation
   - Transaction patterns for account linking
   - Best practices

**Modified Sections:**
- Section 4.1: New Standard Tenant (added transaction)
- Section 5.2: Enterprise First Admin (added transaction)

---

### **Supporting Documentation**

| Document | Purpose | Status |
|----------|---------|--------|
| `docs/BACKGROUND-CLEANUP-JOB-DESIGN.md` | Orphaned identity & unused tenant cleanup | ? Complete |
| `docs/ENTERPRISE-BOOTSTRAP-SECURITY.md` | Break-glass accounts & email verification | ? Complete |
| `docs/DESIGN-REFINEMENTS-FINAL-REVIEW.md` | Design review summary | ? Complete |
| `docs/MULTI-REALM-IDENTITY-PHASE1-COMPLETE.md` | Phase 1 implementation summary | ? Complete |
| `docs/PHASE2-AUTHCONTROLLER-UPDATE-GUIDE.md` | Next steps guide | ? Complete |

---

## ??? Implementation Phases

### **Phase 1: Database Schema** ? **COMPLETE**
- [x] Created `UserKeycloakIdentities` table
- [x] Created `AccountLinkTokens` table
- [x] Modified `Users` table (nullable email/username)
- [x] Modified `Tenants` table (Plan, UpdatedAt)
- [x] Modified `TenantInvitations` (ContactEmail, ContactName)
- [x] Generated migration
- [x] Migration ready to apply

**Status:** ? **Ready for database deployment**

---

### **Phase 2: AuthController Updates** ?? **NEXT**

**Tasks:**
- [ ] Add `IUserKeycloakIdentityRepository` dependency
- [ ] Update `AuthCallback` to use identity resolution
- [ ] Wrap invitation flow in transaction
- [ ] Wrap new org flow in transaction
- [ ] Handle email verification check
- [ ] Update error handling

**Timeline:** 1-2 weeks  
**Status:** ?? **Ready to start**

**See:** `docs/PHASE2-AUTHCONTROLLER-UPDATE-GUIDE.md`

---

### **Phase 3: Background Cleanup Jobs** ?? **FUTURE**

**Tasks:**
- [ ] Install Hangfire NuGet package
- [ ] Create `ICleanupService` interface
- [ ] Implement `CleanupService`
- [ ] Register Hangfire in `Program.cs`
- [ ] Schedule recurring jobs
- [ ] Add `IsActive`, `DeletedAt` to `UserKeycloakIdentities`
- [ ] Create Hangfire authorization filter
- [ ] Add email service for deletion warnings

**Timeline:** 2-3 weeks  
**Status:** ?? **Design complete, ready when needed**

**See:** `docs/BACKGROUND-CLEANUP-JOB-DESIGN.md`

---

### **Phase 4: Enterprise Bootstrap Security** ?? **FUTURE**

**Tasks:**
- [ ] Add `RequiresEmailVerification` to `TenantInvitation`
- [ ] Create `AuditLog` entity
- [ ] Implement `ISecretsManager` interface
- [ ] Implement AWS Secrets Manager provider
- [ ] Implement break-glass account creation
- [ ] Create emergency access endpoint
- [ ] Add email verification enforcement
- [ ] Add minimum admin enforcement

**Timeline:** 3-4 weeks  
**Status:** ?? **Design complete, ready when needed**

**See:** `docs/ENTERPRISE-BOOTSTRAP-SECURITY.md`

---

### **Phase 5: Enterprise Tenant Provisioning** ?? **FUTURE**

**Tasks:**
- [ ] Create `POST /api/tenants/enterprise/signup` endpoint
- [ ] Implement Keycloak realm creation
- [ ] Create first admin invitation
- [ ] Implement email notification service
- [ ] Add realm deletion support

**Timeline:** 2-3 weeks  
**Status:** ?? **Awaiting Phases 2-4**

---

### **Phase 6: Account Linking** ?? **FUTURE**

**Tasks:**
- [ ] Create `IAccountLinkTokenRepository`
- [ ] Create `POST /api/account-link/start` endpoint
- [ ] Create `POST /api/account-link/complete` endpoint
- [ ] Implement identity merge logic
- [ ] Frontend UI for linking

**Timeline:** 2-3 weeks  
**Status:** ?? **Lower priority**

---

## ?? Security Analysis

### **Strengths**

| Area | Security Measure | Risk Mitigation |
|------|------------------|-----------------|
| **Identity** | Multi-realm resolution | ? Prevents cross-realm impersonation |
| **Data Integrity** | Atomic transactions | ? Prevents partial writes, race conditions |
| **Account Recovery** | Email verification | ? Ensures password reset capability |
| **Break-Glass** | Hidden admin account | ? Prevents permanent lockout |
| **Audit Trail** | All actions logged | ? Compliance, forensics |
| **Secrets** | Encrypted storage | ? Credentials not in database |

### **Remaining Risks**

| Risk | Severity | Mitigation |
|------|----------|------------|
| **Secrets manager outage** | MEDIUM | High-availability SLA, fallback procedures |
| **Break-glass password leak** | LOW | Rotation policy, access logging |
| **Email verification bypass** | LOW | Keycloak-enforced, no backend bypass |
| **Transaction deadlock** | LOW | Short transactions, retry logic |

---

## ?? Key Design Principles

### **1. Email is Metadata, Not Identity**
```
? WRONG: User.Id = email hash
? RIGHT: (RealmName, KeycloakUserId) ? Users.Id
```

**Why?**
- Social logins may not provide email
- Enterprise SSO may not share email
- Email can change over time
- Privacy-conscious users may not want to share email

---

### **2. Manual Account Linking**
```
? WRONG: Auto-link based on verified email
? RIGHT: User explicitly links accounts via token
```

**Why?**
- Email verification in one realm ? trust in another
- Email could be compromised between realms
- User should consent to linking identities

---

### **3. Atomic Operations**
```
? WRONG:
await CreateUser();
await CreateIdentity();
await AcceptInvitation(); // If this fails, user exists without membership

? RIGHT:
using var transaction = await BeginTransactionAsync();
try {
    await CreateUser();
    await CreateIdentity();
    await AcceptInvitation();
    await CommitAsync();
} catch { await RollbackAsync(); }
```

**Why?**
- Prevents partial writes
- Prevents race conditions
- Ensures data consistency

---

### **4. Defense in Depth**
```
Layer 1: Email verification (prevent typos)
Layer 2: Break-glass account (prevent lockout)
Layer 3: Minimum admins (prevent single point of failure)
Layer 4: Secrets manager (protect credentials)
Layer 5: Audit trail (detect misuse)
```

**Why?**
- Single layer can fail
- Multiple layers provide redundancy
- Compliance requirements

---

## ?? Architecture Diagram

```
???????????????????????????????????????????????????????????????
?                        Keycloak                              ?
?  ???????????????  ???????????????  ???????????????        ?
?  ?   groundup  ?  ?tenant_acme  ?  ?tenant_newco ?        ?
?  ?   (shared)  ?  ?(enterprise) ?  ?(enterprise) ?        ?
?  ???????????????  ???????????????  ???????????????        ?
?         ?                 ?                 ?               ?
???????????????????????????????????????????????????????????????
          ?                 ?                 ?
          ?  OIDC/OAuth2    ?                 ?
          ?                 ?                 ?
???????????????????????????????????????????????????????????????
?                    GroundUp API                              ?
?  ?????????????????????????????????????????????????????????? ?
?  ?              AuthController                             ? ?
?  ?  • Identity resolution: (realm, sub) ? Users.Id        ? ?
?  ?  • Invitation acceptance (transaction)                 ? ?
?  ?  • New org creation (transaction)                      ? ?
?  ?????????????????????????????????????????????????????????? ?
???????????????????????????????????????????????????????????????
          ?
          ?
???????????????????????????????????????????????????????????????
?                      Database                                ?
?  ????????????????  ????????????????????????????            ?
?  ?    Users     ?  ? UserKeycloakIdentities   ?            ?
?  ? (global)     ?  ? (identity anchor)        ?            ?
?  ?              ?  ?                          ?            ?
?  ? Id (PK)      ?  ? RealmName + KeycloakUserId ? Users.Id? ?
?  ? DisplayName  ?  ?                          ?            ?
?  ? Email (NULL) ?  ????????????????????????????            ?
?  ? IsActive     ?  ????????????????????????????            ?
?  ????????????????  ?     UserTenants          ?            ?
?                    ?  (memberships)            ?            ?
?  ????????????????  ?                          ?            ?
?  ?   Tenants    ?  ? UserId ? TenantId        ?            ?
?  ?              ?  ? IsAdmin                  ?            ?
?  ? TenantType   ?  ????????????????????????????            ?
?  ? KeycloakRealm?                                           ?
?  ? Plan         ?  ????????????????????????????            ?
?  ????????????????  ?  TenantInvitations       ?            ?
?                    ?                          ?            ?
?                    ? InvitationToken          ?            ?
?                    ? ContactEmail (required)  ?            ?
?                    ????????????????????????????            ?
???????????????????????????????????????????????????????????????
```

---

## ? Approval Checklist

- [x] Core architecture reviewed
- [x] Security concerns addressed
- [x] Race conditions identified and fixed
- [x] Operational procedures documented
- [x] Implementation phases defined
- [x] Database schema designed
- [x] Migration generated
- [x] Supporting documentation complete
- [x] Bootstrap security designed
- [x] Cleanup jobs designed

**Status:** ? **APPROVED FOR IMPLEMENTATION**

---

## ?? Next Steps

### **Immediate (This Week)**
1. ? Review this summary
2. ? Confirm approval to proceed
3. ?? Apply database migration
4. ?? Start Phase 2: Update AuthController

### **Short-term (Next 2 Weeks)**
1. Implement Phase 2 (AuthController updates)
2. Add transaction wrappers
3. Test invitation flow
4. Test new org flow

### **Medium-term (Next Month)**
1. Implement Phase 3 (background cleanup)
2. Implement Phase 4 (bootstrap security)
3. Test enterprise tenant provisioning

### **Long-term (Next Quarter)**
1. Implement Phase 5 (enterprise provisioning)
2. Implement Phase 6 (account linking)
3. User discovery for standard tenants

---

## ?? Support & Questions

**Design Questions:**
- Refer to: `C:\Users\Rob\Downloads\groundup-auth-architecture.md`
- Review: `docs/DESIGN-REFINEMENTS-FINAL-REVIEW.md`

**Implementation Guidance:**
- Phase 1: `docs/MULTI-REALM-IDENTITY-PHASE1-COMPLETE.md`
- Phase 2: `docs/PHASE2-AUTHCONTROLLER-UPDATE-GUIDE.md`
- Phase 3: `docs/BACKGROUND-CLEANUP-JOB-DESIGN.md`
- Phase 4: `docs/ENTERPRISE-BOOTSTRAP-SECURITY.md`

**Questions or Concerns?**
- Open a GitHub issue
- Tag as `architecture` or `security`
- Reference this document

---

## ?? Success Criteria

**Phase 2 (AuthController) is successful when:**
- [ ] User logs in via Keycloak
- [ ] Identity resolved via `UserKeycloakIdentityRepository`
- [ ] No duplicate users created on rapid logins
- [ ] Invitation acceptance is atomic
- [ ] New org creation is atomic
- [ ] All existing flows still work

**Overall Architecture is successful when:**
- [ ] Standard tenants use shared realm
- [ ] Enterprise tenants use dedicated realms
- [ ] Users can have identities in multiple realms
- [ ] Account linking works across realms
- [ ] Email is truly optional (except for invitations)
- [ ] No user lockouts (break-glass works)
- [ ] Orphaned identities cleaned up automatically

---

## ?? Document Index

| Document | Purpose | Phase |
|----------|---------|-------|
| `groundup-auth-architecture.md` | Main design document | All |
| `MULTI-REALM-IDENTITY-PHASE1-COMPLETE.md` | Phase 1 summary | 1 |
| `PHASE2-AUTHCONTROLLER-UPDATE-GUIDE.md` | Phase 2 guide | 2 |
| `BACKGROUND-CLEANUP-JOB-DESIGN.md` | Cleanup jobs | 3 |
| `ENTERPRISE-BOOTSTRAP-SECURITY.md` | Bootstrap security | 4 |
| `DESIGN-REFINEMENTS-FINAL-REVIEW.md` | Design review | All |
| **This document** | **Complete summary** | **All** |

---

**?? Congratulations! The design phase is complete. Ready to implement!**

---

**Created:** 2025-11-30  
**Status:** ? **APPROVED**  
**Ready for:** Phase 2 Implementation  
**Confidence:** HIGH  
**Risk:** LOW
