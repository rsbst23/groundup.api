# Quick Reference: Login vs Sign Up

## What Changed?

Before: Everything went through `/api/auth/login` and users saw a login page first (confusing!)

Now: Separate endpoints for login and sign-up:

## For Sign Up (New Users)
```
http://localhost:5123/api/auth/register
```
? Goes directly to **registration page** ?

## For Login (Existing Users)
```
http://localhost:5123/api/auth/login
```
? Goes to **login page** ?

## Testing Right Now

### Try Sign Up Flow:
1. Open browser
2. Go to: `http://localhost:5123/api/auth/register`
3. You should see Keycloak **registration form** immediately
4. Fill it out and submit
5. API will create your tenant automatically

### Try Login Flow:
1. Open browser  
2. Go to: `http://localhost:5123/api/auth/login`
3. You should see Keycloak **login form**
4. Enter existing credentials

## For React Frontend

```tsx
// Sign Up button
<button onClick={() => window.location.href = '/api/auth/register'}>
  Sign Up
</button>

// Login button
<button onClick={() => window.location.href = '/api/auth/login'}>
  Login
</button>
```

## Enterprise Tenants (with custom domain)

```tsx
const domain = window.location.host; // e.g., "acme.yourapp.com"

// Sign Up
window.location.href = `/api/auth/register?domain=${domain}`;

// Login
window.location.href = `/api/auth/login?domain=${domain}`;
```

That's it! Much clearer UX. ??
