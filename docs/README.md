# Authentication System Documentation

Complete documentation for the Keycloak-based authentication system with multi-tenant support.

---

## ?? **Documentation Index**

| Document | Purpose | When to Use |
|----------|---------|-------------|
| **[AUTHENTICATION-SETUP-GUIDE.md](AUTHENTICATION-SETUP-GUIDE.md)** | Complete setup instructions | Setting up Keycloak and configuring the system |
| **[USER-FLOWS-GUIDE.md](USER-FLOWS-GUIDE.md)** | How user flows work | Understanding registration, login, invitations |
| **[TESTING-GUIDE.md](TESTING-GUIDE.md)** | Testing procedures | Testing the authentication system |
| **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** | Common problems & solutions | When something doesn't work |

---

## ?? **Quick Start**

### **New to the Project?**

1. **Start here:** [AUTHENTICATION-SETUP-GUIDE.md](AUTHENTICATION-SETUP-GUIDE.md)
   - Follow steps 1-8 for Keycloak setup
   - Configure your `.env` file
   - Implement React auth callback page

2. **Understand the flows:** [USER-FLOWS-GUIDE.md](USER-FLOWS-GUIDE.md)
   - See how registration works
   - Understand login process
   - Learn about invitations

3. **Test it:** [TESTING-GUIDE.md](TESTING-GUIDE.md)
   - Manual testing procedures
   - API testing examples
   - Verify everything works

4. **Having issues?** [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
   - Common error solutions
   - Diagnostic commands
   - Configuration checklist

---

## ?? **Architecture Overview**

```
???????????????????
?  React (5173)   ?  User Interface
?   Frontend      ?  - Login/Signup buttons
?                 ?  - Auth callback page
?                 ?  - Navigation logic
???????????????????
         ? API Calls (JSON)
         ? credentials: 'include'
         ?
???????????????????
?  .NET API       ?  Business Logic
?    (5123)       ?  - Auth callback endpoint
?                 ?  - User sync service
?                 ?  - Tenant assignment
?                 ?  - Token generation
???????????????????
         ? OAuth/Token Exchange
         ? User Management API
         ?
???????????????????
?  Keycloak       ?  Authentication
?    (8080)       ?  - User registration
?                 ?  - Login/Logout
?                 ?  - Social auth
?                 ?  - Token issuance
???????????????????
```

---

## ?? **Key Concepts**

### **Authentication Flow**

```
User ? Keycloak ? React ? API ? Database ? React
```

1. User clicks "Login" in React
2. React redirects to Keycloak
3. User authenticates
4. Keycloak redirects back to React with OAuth code
5. React calls API with code
6. API exchanges code for tokens
7. API syncs user to database
8. API assigns user to tenant(s)
9. API returns JSON response
10. React navigates based on response

### **Three Main Flows**

1. **New User Registration**
   - Creates user in Keycloak
   - Syncs to database
   - Auto-creates organization
   - User becomes admin

2. **Existing User Login**
   - Authenticates with Keycloak
   - Checks tenant assignments
   - Auto-selects single tenant or shows selection

3. **Invitation Flow**
   - User clicks invitation link
   - Registers/logs in
   - Auto-accepts invitation
   - Joins existing organization

### **Multi-Tenant Support**

- Users can belong to multiple tenants
- Each tenant is isolated (data, permissions)
- Tenant-scoped JWT tokens
- Users can switch between tenants

---

## ?? **Prerequisites**

### **Required Services**

- ? Docker Desktop (for Keycloak)
- ? .NET 8 SDK
- ? Node.js 18+ (for React)
- ? MySQL/PostgreSQL

### **Your URLs**

| Service | URL | Port |
|---------|-----|------|
| Keycloak | `http://localhost:8080` | 8080 |
| .NET API | `http://localhost:5123` | 5123 |
| React App | `http://localhost:5173` | 5173 |

---

## ? **Setup Checklist**

### **Keycloak Configuration:**
- [ ] Keycloak running via Docker
- [ ] Realm `groundup` created
- [ ] Client `groundup-api` configured
- [ ] Client authentication enabled
- [ ] Standard flow enabled
- [ ] Redirect URI: `http://localhost:5173/auth/callback*`
- [ ] Web origins configured
- [ ] User registration enabled
- [ ] Roles created (SYSTEMADMIN, Admin, Member)
- [ ] Test user created with SYSTEMADMIN role

### **API Configuration:**
- [ ] `.env` file created with Keycloak settings
- [ ] Client secret configured
- [ ] Database connection string set
- [ ] API running on port 5123
- [ ] Swagger accessible

### **React Configuration:**
- [ ] `/auth/callback` page implemented
- [ ] Login button component created
- [ ] Routes configured
- [ ] API calls use `credentials: 'include'`
- [ ] Running on port 5173

---

## ?? **Testing**

### **Quick Test**

1. **Start all services:**
   ```bash
   docker-compose -f keycloak-compose.yml up -d
   cd GroundUp.api && dotnet run
   cd your-react-app && npm run dev
   ```

2. **Navigate to React:** `http://localhost:5173`

3. **Click "Sign Up"**

4. **Should redirect to Keycloak** registration page

5. **Complete registration**

6. **Should redirect back** to React and navigate to onboarding

**Expected:**
- ? User created in Keycloak
- ? User synced to database
- ? New tenant created
- ? User assigned as admin
- ? Auth cookie set
- ? Onboarding page displayed

---

## ?? **Common Issues**

| Problem | Solution | Document |
|---------|----------|----------|
| Redirect URI mismatch | Add `http://localhost:5173/auth/callback*` | [TROUBLESHOOTING.md](TROUBLESHOOTING.md#redirect-uri-mismatch) |
| Invalid client credentials | Check client secret in `.env` | [TROUBLESHOOTING.md](TROUBLESHOOTING.md#invalid-client-credentials) |
| CORS errors | Configure web origins in Keycloak | [TROUBLESHOOTING.md](TROUBLESHOOTING.md#cors-errors) |
| No register button | Enable user registration | [TROUBLESHOOTING.md](TROUBLESHOOTING.md#user-registration-not-available) |

---

## ?? **Detailed Documentation**

### **[AUTHENTICATION-SETUP-GUIDE.md](AUTHENTICATION-SETUP-GUIDE.md)**

Complete step-by-step setup instructions:
- Keycloak installation and configuration
- Realm and client setup
- Role creation
- Test user creation
- API configuration
- React implementation
- Testing procedures

### **[USER-FLOWS-GUIDE.md](USER-FLOWS-GUIDE.md)**

Detailed explanation of user flows:
- New user registration flow
- Existing user login flow
- Invitation flow (email invites)
- New organization flow
- Multi-tenant user handling
- Technical implementation details
- Database state changes

### **[TESTING-GUIDE.md](TESTING-GUIDE.md)**

Testing procedures and scenarios:
- Manual testing steps
- API endpoint testing
- End-to-end testing
- Common test scenarios
- Test data setup
- Debugging procedures

### **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)**

Solutions to common problems:
- Keycloak configuration issues
- API authentication errors
- React integration problems
- Database sync issues
- Common error messages
- Diagnostic commands

---

## ?? **Security Features**

- ? **OAuth 2.0 / OpenID Connect** - Industry standard authentication
- ? **Keycloak** - Production-ready identity management
- ? **Tenant isolation** - Data separated by tenant
- ? **HttpOnly cookies** - XSS protection
- ? **CSRF protection** - SameSite cookies
- ? **JWT tokens** - Stateless authentication
- ? **Role-based access** - Permission system ready
- ? **Invitation tokens** - Secure, one-time use
- ? **Email verification** - Optional but recommended

---

## ?? **React Pages Required**

Your React app needs these pages:

| Path | Purpose | When Displayed |
|------|---------|----------------|
| `/auth/callback` | OAuth callback handler | After Keycloak redirect |
| `/dashboard` | Main app dashboard | After successful login |
| `/select-tenant` | Tenant selection | User has multiple tenants |
| `/pending-invitations` | Show pending invites | User has invitations but no tenants |
| `/no-access` | No access message | User has no tenants/invitations |
| `/onboarding` | New organization setup | After creating new org |

---

## ?? **Next Steps**

### **After Setup:**

1. **Test the complete flow** end-to-end
2. **Create React pages** for all user flows
3. **Set up email integration** for invitations (optional for dev)
4. **Configure social login** (Google, Microsoft, etc.)
5. **Customize Keycloak theme** (optional)
6. **Set up production environment** with HTTPS

### **Future Enhancements:**

- Multi-factor authentication (MFA)
- Password complexity requirements
- Account lockout policies
- Social identity providers
- Custom email templates
- Brute force protection
- Session management
- Audit logging

---

## ?? **Support**

### **Documentation:**
- [Keycloak Official Docs](https://www.keycloak.org/docs/latest/)
- [ASP.NET Core Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/)
- [OAuth 2.0 Specification](https://oauth.net/2/)

### **Common Questions:**

**Q: Can users have multiple organizations?**
A: Yes! Users can be members of multiple tenants and switch between them.

**Q: How do invitations work?**
A: Admin creates invitation ? Email sent ? User clicks link ? Registers/logs in ? Auto-assigned to tenant.

**Q: Do I need email configured?**
A: Not for development. For production, email is required for invitations and password resets.

**Q: Can I use social login?**
A: Yes! Keycloak supports Google, Microsoft, Facebook, and many others.

---

## ? **System Status**

- ? **Keycloak integration:** Complete
- ? **User sync service:** Implemented
- ? **Multi-tenant support:** Working
- ? **Invitation system:** Functional
- ? **API endpoints:** Returning JSON
- ? **Documentation:** Complete
- ? **React implementation:** Pending
- ? **Email integration:** Pending (optional)

---

**Updated:** 2025-01-21  
**Architecture:** React-first, API returns JSON  
**Status:** ? Ready for React implementation
