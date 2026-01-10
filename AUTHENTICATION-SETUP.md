# Authentication & Authorization Implementation

## Overview

This document describes the JWT-based authentication and authorization system implemented for the Hubletix application. The system supports multi-tenant architecture with both platform-level and tenant-level roles.

## Architecture

### Core Components

1. **Microsoft Identity** - Handles user management, password hashing, and authentication
2. **JWT Tokens** - Stateless authentication with short-lived access tokens
3. **Refresh Tokens** - Long-lived tokens stored server-side with rotation support
4. **Multi-Tenant Roles** - Users can have different roles in different tenants

### Key Entities

- **User** (extends `IdentityUser`)
  - `Id`, `Email`, `UserName` (from Identity)
  - `FirstName`, `LastName`, `TenantId`
  - `IsActive` - Account status flag

- **RefreshToken**
  - `TokenHash` - SHA256 hash (never store raw tokens)
  - `ExpiresAt`, `CreatedAt`, `CreatedByIp`
  - `RevokedAt`, `RevokedByIp`, `ReplacedByTokenHash`
  - Token rotation support for security

- **TenantUserRole**
  - Maps users to roles within specific tenants
  - `TenantId`, `UserId`, `Role`
  - Allows users to have different roles per tenant

## Role System

### Platform Roles (Identity Roles)
- **PlatformAdmin** - Full platform administration
- **PlatformUser** - Can create tenants (future)

### Tenant Roles (TenantUserRole)
- **Admin** - Full tenant administration
- **Coach** - Manage events and classes
- **Member** - Regular member access

## JWT Token Structure

### Access Token Claims
```json
{
  "sub": "user-id",
  "email": "user@example.com",
  "name": "John Doe",
  "first_name": "John",
  "last_name": "Doe",
  "jti": "unique-token-id",
  "platform_role": "PlatformAdmin",  // Optional
  "tenant_id": "tenant-123",          // Optional
  "tenant_role": "Admin",             // Optional
  "exp": "expiration-timestamp",
  "iss": "Hubletix",
  "aud": "HubletixApp"
}
```

### Token Lifecycle
1. **Access Token**: 15 minutes (configurable)
2. **Refresh Token**: 30 days (configurable)
3. **Rotation**: Refresh tokens are rotated on each use
4. **Revocation**: Tokens can be revoked (logout, security breach)

## Services

### IAccountService
Handles user registration and authentication:

```csharp
Task<(bool success, string? error, User? user)> RegisterAsync(
    string email, string password, string firstName, string lastName,
    string? tenantId, string? tenantRole = null);

Task<(bool success, string? error, User? user)> LoginAsync(
    string email, string password, string? tenantId = null);
```

### ITokenService
Manages JWT and refresh token lifecycle:

```csharp
Task<(string accessToken, string refreshToken)> CreateTokensAsync(
    User user, string? tenantId = null);

Task<(string accessToken, string refreshToken)> RefreshAsync(
    string refreshToken, string ipAddress);

Task RevokeRefreshTokenAsync(
    string refreshToken, string ipAddress, string? reason = null);
```

## Usage

### Registration Flow
1. User submits registration form
2. `AccountService.RegisterAsync()` creates user account
3. Creates `TenantUserRole` if tenant context provided
4. `TokenService.CreateTokensAsync()` generates tokens
5. Tokens stored in HTTP-only cookies
6. User redirected to dashboard

### Login Flow
1. User submits credentials
2. `AccountService.LoginAsync()` validates credentials
3. Checks user has access to tenant (if tenant context)
4. `TokenService.CreateTokensAsync()` generates tokens
5. Tokens stored in HTTP-only cookies
6. User redirected to appropriate page

### Token Refresh Flow
1. Client detects expired access token
2. Sends refresh token to `/api/account/refresh`
3. `TokenService.RefreshAsync()` validates and rotates token
4. Returns new access token and refresh token
5. Old refresh token marked as revoked

## Security Features

### Password Requirements
- Minimum 8 characters
- Requires digit
- Requires lowercase letter
- Optional: uppercase, special characters

### Account Lockout
- 5 failed attempts trigger lockout
- 15-minute lockout duration
- Prevents brute force attacks

### Token Security
- Refresh tokens stored as SHA256 hashes
- HTTP-only cookies prevent XSS attacks
- Token rotation prevents replay attacks
- IP address tracking for audit trail
- Revocation support for compromised tokens

### Multi-Tenant Isolation
- Users verified against tenant access
- Tenant ID embedded in JWT claims
- TenantUserRole checks for cross-tenant access

## Authorization Policies

Defined in `Program.cs`:

```csharp
"PlatformAdmin" - Requires platform_role claim = "PlatformAdmin"
"TenantAdmin"   - Requires tenant_role claim = "Admin" + tenant_id
"TenantMember"  - Requires tenant_id claim
```

### Usage in Controllers/Pages
```csharp
[Authorize(Policy = "TenantAdmin")]
public class AdminController : Controller { }

[Authorize(Policy = "TenantMember")]
public IActionResult MemberDashboard() { }
```

## Configuration

### appsettings.json
```json
{
  "Jwt": {
    "Secret": "CHANGE_THIS_IN_PRODUCTION",
    "Issuer": "Hubletix",
    "Audience": "HubletixApp",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 30
  }
}
```

### Production Recommendations
- Store JWT secret in Azure Key Vault or similar
- Use at least 32-character random secret
- Enable HTTPS only
- Implement email confirmation
- Add 2FA support
- Monitor failed login attempts

## Database Tables

### AspNetUsers (Identity)
- Standard Identity tables for user management
- Extended with custom fields (FirstName, LastName, TenantId)

### RefreshTokens
- Stores refresh token hashes
- Tracks creation, expiration, revocation
- Supports audit trail with IP addresses

### TenantUserRoles
- Maps users to tenant-specific roles
- Unique constraint on (TenantId, UserId)
- Cascade deletes with tenant/user

## Migration Path

### Create Migration
```bash
dotnet ef migrations add AddIdentityAuth \
  --project src/Hubletix.Infrastructure \
  --startup-project src/Hubletix.Api
```

### Apply Migration
```bash
dotnet ef database update \
  --project src/Hubletix.Infrastructure \
  --startup-project src/Hubletix.Api
```

## Testing Checklist

- [ ] User registration with tenant context
- [ ] User login with tenant validation
- [ ] Password validation (length, complexity)
- [ ] Account lockout after failed attempts
- [ ] Token generation and validation
- [ ] Refresh token rotation
- [ ] Token revocation (logout)
- [ ] Multi-tenant access control
- [ ] Platform admin vs tenant admin roles
- [ ] Cookie storage (HTTP-only, secure)

## Future Enhancements

1. **Email Confirmation** - Verify email before activation
2. **Password Reset** - Email-based password recovery
3. **2FA/MFA** - Two-factor authentication
4. **OAuth Providers** - Google, Facebook, Microsoft login
5. **Session Management** - List/revoke active sessions
6. **Audit Logging** - Track all authentication events
7. **Rate Limiting** - Prevent brute force at API level
8. **Permission System** - Granular permissions beyond roles
9. **API Keys** - For programmatic access
10. **SSO Support** - SAML/OpenID Connect for enterprises

## API Endpoints (To Be Created)

```
POST /api/account/register  - Create new user account
POST /api/account/login     - Authenticate user
POST /api/account/refresh   - Refresh access token
POST /api/account/logout    - Revoke refresh token
GET  /api/account/me        - Get current user info
PUT  /api/account/profile   - Update user profile
POST /api/account/change-password - Change password
```

## Support

For questions or issues with the authentication system:
1. Check logs in Azure Application Insights / CloudWatch
2. Review `RefreshTokens` table for token issues
3. Verify JWT secret is correctly configured
4. Ensure HTTPS is enabled in production
