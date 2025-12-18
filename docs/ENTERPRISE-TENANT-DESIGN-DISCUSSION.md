# Enterprise Tenant Design Discussion

**Purpose:** Design the enterprise tenant creation and user management flow.

**Status:** ?? In Progress - Awaiting Design Decisions

---

## ?? **Context**

We've successfully documented the [Standard Tenant New User Flow](./STANDARD-TENANT-NEW-USER-FLOW.md). Now we need to design the **Enterprise Tenant** creation and user management flow.

### **Key Differences: Standard vs Enterprise**

| Aspect | Standard Tenant | Enterprise Tenant |
|--------|----------------|-------------------|
| **Realm** | "groundup" (shared) | Dedicated realm per tenant |
| **Custom URL** | None (RealmUrl = NULL) | Required (e.g., company.acme.com) |
| **Auth Providers** | Fixed (Google, Facebook, etc.) | Configurable per tenant |
| **User Creation** | Self-service via Keycloak | Admin-managed OR invitation-based |
| **Isolation** | Database-level (tenant_id) | Realm-level + Database-level |
| **Branding** | GroundUp default | Custom (enterprise login page) |
| **Cost** | Free | Paid ($99/mo, etc.) |

---

## ? **Open Questions**

### **1. Enterprise Tenant Creation: Who and How?**

**Options:**

**A. Self-Service (Complex)**
- User visits landing page
- Clicks "Enterprise Plan"
- Must authenticate first (in groundup realm)
- Then creates enterprise tenant via authenticated API call
- System creates dedicated Keycloak realm
- ?? Issue: User now exists in groundup realm, not enterprise realm

**B. Admin-Only (Simpler)**
- You (SYSTEMADMIN) create enterprise tenants manually
- Configure realm, auth providers, branding
- Send invitation to first admin user
- ? Pros: Full control, better setup
- ? Cons: Slower onboarding, requires sales process

**C. Hybrid (Recommended?)**
- User fills enterprise signup form (no auth required)
- Creates "pending" enterprise tenant in database
- You review, approve, configure realm
- You send invitation to first admin
- ? Pros: User intent captured, you maintain control

**Which approach do you want?**

---

### **2. Cross-Realm User Identity**

**Scenario:**
```
Service Provider User: john@consultant.com

Needs access to:
- Client 1: Acme Corp (realm: "acme")
- Client 2: Beta Industries (realm: "beta")
- Client 3: Gamma LLC (realm: "gamma")
- Own Standard Tenant (realm: "groundup")
```

**Problem:** How does ONE user access MULTIPLE realms?

**Proposed Solution:**
```sql
-- Users table (ONE record per user globally)
Users:
  Id: "user-global-guid"  ? Our internal ID
  Email: "john@consultant.com"  ? Unique identifier

-- UserKeycloakIdentities (Multiple Keycloak IDs per user)
UserKeycloakIdentities:
  UserId: "user-global-guid"
  RealmName: "groundup"
  KeycloakUserId: "kc-groundup-123"

UserKeycloakIdentities:
  UserId: "user-global-guid"  ? Same user
  RealmName: "acme"
  KeycloakUserId: "kc-acme-456"  ? Different Keycloak ID

UserKeycloakIdentities:
  UserId: "user-global-guid"  ? Same user
  RealmName: "beta"
  KeycloakUserId: "kc-beta-789"  ? Different Keycloak ID
```

**How it works:**
- User registers in "groundup" realm ? Creates Users record + first UserKeycloakIdentity
- User invited to "acme" realm ? Registers separately in "acme" realm
- API links via email ? Creates new UserKeycloakIdentity for same Users record
- User can now access both realms with different Keycloak accounts

**Does this approach make sense?**

---

### **3. Initial Enterprise Admin User**

**Question:** How does the first admin user get created for an enterprise tenant?

**Options:**

**A. Pre-Registration in Groundup Realm**
```
1. User john@acme.com registers in "groundup" realm
2. You create "acme" realm for Acme Corp
3. You send invitation to john@acme.com
4. John clicks invitation ? redirects to "acme" realm
5. John registers AGAIN in "acme" realm
6. API links both identities via email
7. John is now admin of Acme Corp enterprise tenant
```

**B. Direct Enterprise Realm Registration**
```
1. You create "acme" realm for Acme Corp
2. You send invitation to john@acme.com
3. John clicks invitation ? redirects to "acme" realm
4. John registers for first time in "acme" realm
5. API creates Users record + UserKeycloakIdentity
6. John is admin of Acme Corp enterprise tenant
7. John has NO presence in "groundup" realm
```

**Which flow is cleaner?**

---

### **4. IsAdmin Flag Location**

**Current Design:**
```sql
UserTenants:
  UserId: "user-guid"
  TenantId: 1
  IsAdmin: true  ? Tenant-specific admin flag
```

**This is correct, right?** User can be:
- Admin of Tenant 1
- Regular user of Tenant 2
- Admin of Tenant 3

IsAdmin is **per-tenant**, not global user property.

**Confirmed?**

---

### **5. Enterprise Tenant Characteristics**

**What we know:**
- TenantType = "enterprise"
- RealmUrl = "company.acme.com" (custom URL)
- RealmName = "acme" (dedicated Keycloak realm)
- IsActive = true

**What we DON'T know:**
- How to handle auth provider configuration (copy from groundup? Manual setup?)
- How to handle realm branding (custom logo, colors?)
- How to handle SAML/LDAP integration (enterprise-specific)
- How to charge customers (billing integration?)

**What's the priority?**

---

## ?? **Proposed Enterprise Tenant Flow (Draft)**

### **Option: Admin-Managed Enterprise Creation**

```
PHASE 1: Enterprise Tenant Setup (By You - SYSTEMADMIN)
?????????????????????????????????????????????????????

1. Customer contacts you for enterprise plan
   ?
2. You create enterprise tenant in database:
   POST /api/tenants (authenticated as SYSTEMADMIN)
   {
     "name": "Acme Corporation",
     "tenantType": "enterprise",
     "realmUrl": "company.acme.com"
   }
   ?
3. API creates Keycloak realm "acme"
   - Copies auth providers from groundup (optional)
   - Sets up custom branding (optional)
   - Configures SAML/LDAP if requested
   ?
4. Database state:
   Tenants:
     Id: 1
     Name: "Acme Corporation"
     TenantType: "enterprise"
     RealmUrl: "company.acme.com"
     RealmName: "acme"
     IsActive: true
   ?
5. You send invitation to first admin:
   POST /api/invitations
   {
     "email": "john@acme.com",
     "tenantId": 1,
     "isAdmin": true
   }


PHASE 2: First Admin Accepts Invitation
?????????????????????????????????????????

6. John receives email with invitation link:
   http://localhost:5173/accept-invitation?token=abc123...
   ?
7. Frontend decodes invitation ? gets tenant info:
   {
     "tenantId": 1,
     "tenantName": "Acme Corporation",
     "realmName": "acme",  ? Enterprise realm!
     "realmUrl": "company.acme.com"
   }
   ?
8. Frontend redirects to Keycloak "acme" realm:
   http://localhost:8080/realms/acme/protocol/openid-connect/auth
                                ^^^^ ? Enterprise realm
   ?redirect_uri=http://localhost:5173/auth/callback
   &state=eyJmbG93IjoiaW52aXRhdGlvbiIsInRva2VuIjoiYWJjMTIzIn0=
   
   State: {"flow":"invitation","token":"abc123...","realm":"acme"}
   ?
9. Keycloak "acme" realm login page:
   - Custom Acme Corp branding
   - Only auth providers configured for Acme
   - John registers (first time in this realm)
   ?
10. Keycloak creates user in "acme" realm:
    {
      "id": "kc-acme-456",  ? Different from groundup IDs
      "email": "john@acme.com",
      "firstName": "John",
      "lastName": "Doe"
    }
    ?
11. Keycloak redirects to React callback
    ?
12. API callback processing:
    a. Exchange code with "acme" realm
    b. Get user details from "acme" realm
    c. Check if Users record exists (by email)
    d. IF NOT EXISTS:
       - Create Users record with new global ID
       - Create UserKeycloakIdentity for "acme" realm
    e. IF EXISTS (user has groundup account):
       - Use existing Users record
       - Create NEW UserKeycloakIdentity for "acme" realm
    f. Accept invitation
    g. Create UserTenants record (IsAdmin = true)
    h. Generate tenant-scoped token
    ?
13. John can now access company.acme.com


PHASE 3: Subsequent Enterprise Users
??????????????????????????????????????

14. John (admin) invites jane@acme.com
    ?
15. Jane receives invitation
    ?
16. Jane clicks link ? "acme" realm
    ?
17. Jane registers in "acme" realm
    ?
18. Same process as John (steps 10-13)
    ?
19. Jane can access company.acme.com
```

---

## ??? **Database Schema Changes Needed**

### **Option A: UserKeycloakIdentities Table (NEW)**

```sql
CREATE TABLE UserKeycloakIdentities (
  Id INT IDENTITY(1,1) PRIMARY KEY,
  UserId UNIQUEIDENTIFIER NOT NULL,  -- Links to Users.Id (our global ID)
  RealmName NVARCHAR(255) NOT NULL,  -- "groundup", "acme", "beta", etc.
  KeycloakUserId NVARCHAR(255) NOT NULL,  -- Keycloak's user ID in that realm
  CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
  
  FOREIGN KEY (UserId) REFERENCES Users(Id),
  UNIQUE (RealmName, KeycloakUserId)  -- Prevent duplicates per realm
);

-- Example data:
-- User has accounts in 3 realms:
UserId: "global-guid-abc"  RealmName: "groundup"  KeycloakUserId: "kc-groundup-123"
UserId: "global-guid-abc"  RealmName: "acme"      KeycloakUserId: "kc-acme-456"
UserId: "global-guid-abc"  RealmName: "beta"      KeycloakUserId: "kc-beta-789"
```

### **Option B: Keep Users.Id = Keycloak ID (Current)**

```sql
-- Keep current schema
Users:
  Id: UNIQUEIDENTIFIER  -- Still Keycloak ID from DEFAULT realm only

-- Add realm tracking:
Users:
  Id: UNIQUEIDENTIFIER
  Email: NVARCHAR(255)
  PrimaryRealmName: NVARCHAR(255)  -- "groundup" for most users
  -- For enterprise-only users, could be "acme"
```

**Problem with Option B:**
- User can only exist in ONE realm
- Service provider can't access multiple enterprise tenants
- Doesn't support cross-realm scenarios

**Recommendation: Option A (UserKeycloakIdentities table)**

**Do you agree?**

---

## ?? **Key Design Decisions Needed**

### **Decision 1: Enterprise Tenant Creation Flow**
- [ ] A. Self-Service (user creates, system auto-configures)
- [ ] B. Admin-Only (you create, full control)
- [ ] C. Hybrid (user requests, you approve and configure)

### **Decision 2: Cross-Realm User Identity**
- [ ] A. UserKeycloakIdentities table (supports multiple realms)
- [ ] B. Keep current schema (one realm per user)
- [ ] C. Different approach (specify)

### **Decision 3: First Admin Registration**
- [ ] A. Must register in groundup first, then enterprise
- [ ] B. Can register directly in enterprise realm
- [ ] C. Different approach (specify)

### **Decision 4: IsAdmin Flag**
- [ ] A. On UserTenants table (current design - RECOMMENDED)
- [ ] B. On Users table (global admin flag)
- [ ] C. Separate AdminUsers table

### **Decision 5: MVP Scope**
Which features are essential for first enterprise customer?
- [ ] Custom realm creation
- [ ] Custom URL (company.acme.com)
- [ ] Invitation-based user management
- [ ] Custom branding (can defer?)
- [ ] SAML/LDAP integration (can defer?)
- [ ] Billing integration (can defer?)

---

## ?? **Next Steps**

Once design decisions are made:

1. **Update database schema** (add UserKeycloakIdentities if needed)
2. **Create migration** for new tables
3. **Update UserRepository** to handle multiple Keycloak identities
4. **Update AuthController** callback logic
5. **Create enterprise tenant creation endpoint**
6. **Update invitation flow** for enterprise realms
7. **Test cross-realm scenarios**
8. **Document enterprise flow** (like STANDARD-TENANT-NEW-USER-FLOW.md)

---

## ?? **Questions for New Thread**

**Start the new conversation with:**

> I've reviewed the [Standard Tenant New User Flow](./STANDARD-TENANT-NEW-USER-FLOW.md) and the design questions in [ENTERPRISE-TENANT-DESIGN-DISCUSSION.md](./ENTERPRISE-TENANT-DESIGN-DISCUSSION.md).
>
> **My question:** Can you walk me through how the enterprise tenant creation and first user registration flow would work?
>
> **Key scenarios to cover:**
> 1. How does a brand new enterprise tenant get created?
> 2. How does the first admin user register for that enterprise tenant?
> 3. How does a service provider user access multiple enterprise tenants (cross-realm scenario)?
> 4. What database schema changes are needed to support this?
>
> **Design context:** We want to support:
> - Dedicated Keycloak realm per enterprise tenant
> - Custom URLs (company.acme.com)
> - Service providers accessing multiple enterprise clients
> - IsAdmin flag at tenant level (not user level)

---

## ?? **Reference Documents**

- [Standard Tenant New User Flow](./STANDARD-TENANT-NEW-USER-FLOW.md) - ? Complete
- [Multi-Realm Refactoring Complete](./MULTI-REALM-REFACTORING-COMPLETE.md) - Background
- [Tenant Management Summary](./TENANT-MANAGEMENT-SUMMARY.md) - Current state
- [User Flows Guide](./USER-FLOWS-GUIDE.md) - General patterns

---

**Document Status:** ?? Draft - Awaiting Design Discussion  
**Last Updated:** 2025-01-21  
**Purpose:** Capture open questions and design options for enterprise tenant flow  
**Next Action:** Discuss in new thread to finalize design
