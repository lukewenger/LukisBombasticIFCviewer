# Auth API Audit — 2026-04-16

Auditor: Auth_Api_Agent
Scope: `src/BombasticIFC.API/Controllers/AuthController.cs`, Application use-case handlers, Infrastructure services

---

## 1. Endpoint Inventory

| Method | Route | Auth Required | Status |
|--------|-------|--------------|--------|
| POST | /api/auth/register | No | Implemented |
| POST | /api/auth/login | No | Implemented |
| GET | /api/auth/me | Yes (Bearer JWT) | Implemented |
| POST | /api/auth/logout | — | MISSING |
| POST | /api/auth/refresh | — | MISSING |

---

## 2. Implemented Endpoint Analysis

### POST /api/auth/register
- Accepts `{ username, email, password }` in request body.
- Delegates to `RegisterCommand` via MediatR.
- Handler checks username uniqueness via `IUserRepository.ExistsAsync` and email uniqueness via `GetByEmailAsync`.
- Hashes password with BCrypt (work factor 12) before persisting.
- Returns `AuthResponseDto` containing `userId`, `username`, `email`, `role`, `accessToken`, and `refreshToken` on HTTP 200.
- Returns HTTP 409 Conflict if username or email already exists.
- **Issue**: Returns HTTP 200 instead of HTTP 201 Created on successful registration.

### POST /api/auth/login
- Accepts `{ username, password }`.
- Delegates to `LoginCommand` via MediatR.
- Handler fetches user by username, checks `IsActive` flag, then verifies password hash.
- Returns same `AuthResponseDto` shape on HTTP 200.
- Returns HTTP 401 Unauthorized on failure.
- Error messages use a consistent "Invalid username or password" string (avoids username enumeration). GOOD.
- **Issue**: The `refreshToken` returned by both register and login is a 64-byte cryptographically random value generated at login time but is NEVER stored anywhere. It cannot be validated on the refresh endpoint (which does not exist). This makes the refresh token field in the response entirely non-functional.

### GET /api/auth/me
- Protected with `[Authorize]` attribute.
- Extracts `userId` from the `sub` or `ClaimTypes.NameIdentifier` claim.
- Delegates to `GetCurrentUserQuery`.
- Returns `UserProfileDto` (id, username, email, role, isActive, createdAt).
- Returns HTTP 401 if claim is absent or not a valid GUID.
- Well-implemented. No issues.

---

## 3. Missing Endpoints

### POST /api/auth/logout
- No endpoint exists. There is no token blacklist, no server-side session store, and no mechanism to invalidate a JWT before its natural expiry.
- **Impact**: Issued access tokens remain valid for their full `ExpirationInMinutes` duration (60 min production, 120 min dev) after a user intends to log out. A stolen token cannot be revoked.

### POST /api/auth/refresh
- No endpoint exists. The `refreshToken` field in `AuthResponseDto` is generated but never persisted, so server-side validation of a refresh request is impossible.
- **Impact**: Frontend receives a refresh token it can never use. Any token-refresh logic in the frontend will fail silently or produce misleading errors.

---

## 4. JWT Implementation Review

**TokenService** (`src/BombasticIFC.Infrastructure/Services/TokenService.cs`):
- Uses HMAC-SHA256 symmetric signing. Acceptable for a single-service deployment.
- Claims included: `sub` (userId), `unique_name` (username), `email`, `role`, `jti` (unique JWT ID).
- Expiry set from configuration via `ExpirationInMinutes`. Defaults to 60 minutes.
- Validation in `Program.cs` enables: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey`. GOOD.
- **Issue**: The `jti` claim is generated but there is no token blacklist backed by it. Even with a blacklist, logout cannot be enforced without one.
- **Issue**: `secretKey` is read directly from `IConfiguration`. In `appsettings.json` (committed to the repository) the value is the placeholder string `"REPLACE-WITH-ENV-VAR-OR-SECRET-IN-PRODUCTION"`. In `appsettings.Development.json` (also committed) a 64-character development secret is hardcoded. The development secret being in source control is an accepted practice only if that environment is never internet-facing; the placeholder in production config makes the API fail to start if not overridden, which is intentional-fail-safe behavior.

---

## 5. Password Hashing Review

**PasswordHasherService** (`src/BombasticIFC.Infrastructure/Services/PasswordHasherService.cs`):
- Uses `BCrypt.Net.BCrypt` with work factor 12. GOOD — industry standard, computationally expensive.
- `VerifyPassword` delegates to `BCrypt.Net.BCrypt.Verify`. No custom logic. GOOD.
- No password complexity/length validation exists at any layer (controller, command, or domain). Empty passwords would be accepted and hashed.

---

## 6. Authorization & CORS

- CORS policy `AllowAll` (`AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`) is configured and applied globally in `Program.cs`.
- **Security Concern**: `AllowAnyOrigin` combined with `AllowAnyMethod`/`AllowAnyHeader` is an overly permissive CORS policy acceptable only in development. In production this should be restricted to known frontend origins.
- No role-based authorization (`[Authorize(Roles = "Admin")]`) is applied on any auth endpoint. All authenticated users can call `/me`. Acceptable for current scope.

---

## 7. Input Validation

- `RegisterRequest` and `LoginRequest` are plain records with no `[Required]`, `[MinLength]`, `[EmailAddress]`, or `[MaxLength]` data annotations.
- ASP.NET model validation is not explicitly invoked; invalid requests (null username, empty password) would reach the use-case handlers and either throw or behave unexpectedly.
- No rate limiting is applied on login or register. Brute-force attacks are not mitigated.

---

## 8. Security Findings Summary

| Severity | Finding |
|----------|---------|
| HIGH | Refresh token is generated but never persisted — the refresh flow is non-functional |
| HIGH | No logout/token-invalidation endpoint — tokens cannot be revoked before expiry |
| MEDIUM | No input validation (length, format) on register/login request DTOs |
| MEDIUM | No rate limiting on authentication endpoints (brute-force exposure) |
| MEDIUM | CORS policy `AllowAll` is too permissive for production |
| LOW | POST /api/auth/register returns HTTP 200 instead of HTTP 201 |
| LOW | Development JWT secret committed to source control in `appsettings.Development.json` |
| INFO | No password complexity policy enforced |

---

## 9. What is Well-Implemented

- BCrypt with work factor 12 is correct.
- Login error messages avoid username enumeration.
- JWT validation parameters are all enabled (issuer, audience, lifetime, signing key).
- `IsActive` check on login prevents disabled accounts from authenticating.
- Duplicate username and duplicate email checks on registration are both present.
