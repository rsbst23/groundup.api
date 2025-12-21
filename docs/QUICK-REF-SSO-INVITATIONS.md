# Quick Reference: SSO vs Local Account Invitations

## TL;DR

**Problem**: Pre-creating Keycloak users caused "user already exists" errors with Google SSO

**Solution**: Added `isLocalAccount` flag to invitations

---

## Create Invitation

### Local Account (Password)
```json
{
  "email": "user@example.com",
  "isLocalAccount": true  ? Password-based
}
```
? User created in Keycloak
? Email sent with password setup link

### SSO (Google/Azure AD)
```json
{
  "email": "user@gmail.com",
  "isLocalAccount": false  ? SSO-based
}
```
? NO user created in Keycloak
? User authenticates via SSO
? Keycloak creates user from SSO provider

---

## Default

`isLocalAccount` defaults to `true` (backward compatible)

---

## When to Use Each

| Local Account | SSO |
|--------------|-----|
| No SSO account | Has Gmail/Google Workspace |
| Password preferred | Has Azure AD/Microsoft account |
| Internal testing | Enterprise SSO configured |
| Custom user management | Higher security (no passwords) |

---

## Testing

```powershell
# Test both flows
.\scripts\test-sso-local-invitations.ps1
```

**Verify**:
- Local: User exists in Keycloak immediately
- SSO: User does NOT exist until after OAuth

---

## Troubleshooting

**"User already exists"** ? Use `isLocalAccount: false`

**No email sent** ? Check SMTP config OR use SSO

**User not assigned** ? Check invitation expiration

---

## Files Changed

1. `GroundUp.core/dtos/TenantInvitationDtos.cs`
2. `GroundUp.infrastructure/repositories/TenantInvitationRepository.cs`

---

## Docs

- [Complete Fix Summary](./SSO-INVITATION-FIX-COMPLETE.md)
- [Detailed Guide](./SSO-VS-LOCAL-ACCOUNT-INVITATIONS-FIX.md)
- [Google SSO Testing](./GOOGLE-SSO-TESTING-GUIDE.md)
