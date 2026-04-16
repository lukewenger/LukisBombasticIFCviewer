# Consolidated Security & API Completeness Review — 2026-04-16

Reviewer: IFCCM_Code_Reviewer_Agent
Sources: auth_audit_2026-04-16.md, models_audit_2026-04-16.md, conversions_audit_2026-04-16.md + direct backend scan
Repository: C:/Github/LukisConstructionManager

---

## Executive Summary

The BombasticIFC backend is a well-structured .NET 8 Clean Architecture / CQRS application with MediatR, Entity Framework Core (PostgreSQL), JWT authentication, and a background conversion worker. The core domain model is sound and the IFC conversion pipeline is robustly implemented.

However, the audit found **three CRITICAL issues** that block safe production merge, plus multiple high and medium severity findings. The most serious structural problem is that **authentication is not enforced on any Models or Conversions endpoint**, making the entire file management surface unauthenticated. Additionally, **the refresh token flow is non-functional** and **internal filesystem paths are leaked in the API response**.

---

## CRITICAL Findings (BLOCKS MERGE)

---

### CRITICAL-1 — No Authorization on Models or Conversions Controllers [BLOCKS MERGE]

**Affected files:**
- `src/BombasticIFC.API/Controllers/ModelsController.cs`
- `src/BombasticIFC.API/Controllers/ConversionsController.cs`

**Description:**
Neither `ModelsController` nor `ConversionsController` has an `[Authorize]` attribute at the controller level, nor on any individual action method. The `[Authorize]` attribute exists only on `AuthController.GetCurrentUser`.

Any unauthenticated HTTP client can:
- Enumerate all models (`GET /api/models`)
- Upload arbitrary IFC files (`POST /api/models/upload`)
- Download all original IFC files (`GET /api/models/{id}/original`)
- Download all converted XKT outputs (`GET /api/models/{id}/output`)
- Delete any model and all its associated files (`DELETE /api/models/{id}`)
- Create conversion jobs (`POST /api/conversions`)
- Poll any conversion job status (`GET /api/conversions/{id}`)

JWT authentication infrastructure is correctly wired in `Program.cs` (`UseAuthentication()`, `UseAuthorization()`). The omission is entirely at the controller attribute level.

**Remediation:** Add `[Authorize]` to both controllers at the class level. Evaluate whether any read endpoints (GET /api/models, GET /api/models/{id}) should be public.

---

### CRITICAL-2 — `outputFilePath` Leaked in ConversionJobDto Response [BLOCKS MERGE]

**Affected files:**
- `src/BombasticIFC.Application/DTOs/ConversionJobDto.cs`
- `src/BombasticIFC.Application/UseCases/Conversion/GetConversionJobQuery.cs`
- `src/BombasticIFC.Application/UseCases/Conversion/CreateConversionJobCommand.cs`

**Description:**
`ConversionJobDto.OutputFilePath` contains the absolute server-side filesystem path of the XKT output file (e.g., `/data/storage/abc123.xkt`). This field is serialized and returned by both `POST /api/conversions` (HTTP 201) and `GET /api/conversions/{id}`.

Exposing internal filesystem paths is an information disclosure vulnerability. An attacker learns the storage directory structure and file naming scheme, which aids exploitation of path traversal vulnerabilities (present or future), and confirms deployment topology.

**Remediation:** Remove `OutputFilePath` from `ConversionJobDto` entirely. Replace with a boolean `HasOutput` or a relative URL such as `/api/models/{modelId}/output`.

---

### CRITICAL-3 — Refresh Token is Non-Functional [BLOCKS MERGE]

**Affected files:**
- `src/BombasticIFC.Infrastructure/Services/TokenService.cs`
- `src/BombasticIFC.Application/UseCases/Auth/LoginCommand.cs`
- `src/BombasticIFC.Application/UseCases/Auth/RegisterCommand.cs`
- `src/BombasticIFC.API/Controllers/AuthController.cs`

**Description:**
Both `LoginCommand` and `RegisterCommand` call `_tokenService.GenerateRefreshToken()` and return the value in `AuthResponseDto.RefreshToken`. The token is a cryptographically random 64-byte value, but **it is never persisted** to the database or any store. There is no `POST /api/auth/refresh` endpoint. Any frontend that attempts to use this token to obtain a new access token will receive an error or silently fail.

This creates a broken contract: the API advertises a refresh token capability that does not exist, leading to silent session expiry with no recovery path for frontend users.

**Remediation (minimum):** Remove `RefreshToken` from `AuthResponseDto` until the feature is fully implemented. **Remediation (full):** Persist refresh tokens (hashed) in the database with an expiry, implement `POST /api/auth/refresh` that validates the token, rotates it, and issues a new access token; implement `POST /api/auth/logout` that deletes the stored refresh token.

---

## HIGH Severity Findings

---

### HIGH-1 — No Token Revocation / Logout Endpoint

**Affected files:** `src/BombasticIFC.API/Controllers/AuthController.cs`

No `POST /api/auth/logout` endpoint exists. Issued access tokens remain valid until expiry (60 minutes production). Compromised tokens cannot be revoked.

**Remediation:** Implement logout by deleting the stored refresh token (once CRITICAL-3 is resolved). For access token blacklisting, consider a short-lived in-memory or Redis blacklist keyed on `jti`.

---

### HIGH-2 — No File Upload Size Limit

**Affected files:** `src/BombasticIFC.API/Controllers/ModelsController.cs`, `src/BombasticIFC.API/Program.cs`

ASP.NET Core's default request size limit (30 MB) is not overridden. IFC files for large buildings commonly exceed 100–500 MB. The upload endpoint will reject valid large files silently, or — if the limit is raised globally — could be exploited for denial-of-service.

**Remediation:** Explicitly configure `[RequestSizeLimit]` and `[RequestFormLimits]` on the upload action. Set a documented maximum (e.g., 500 MB) appropriate for IFC files.

---

### HIGH-3 — No CORS Origin Restriction in Production

**Affected files:** `src/BombasticIFC.API/Program.cs`

`AllowAnyOrigin` is configured globally. In production, this allows any web page on the internet to make credentialed cross-origin requests to the API.

**Remediation:** Replace `AllowAnyOrigin()` with an explicit list of permitted frontend origins, configurable via `appsettings.json`.

---

## MEDIUM Severity Findings

---

### MEDIUM-1 — No Input Validation on Auth Request DTOs

`RegisterRequest` and `LoginRequest` are plain C# records with no data annotations. No minimum password length, no email format validation, no maximum field lengths. An empty-string username and empty password would be accepted.

**Remediation:** Add `[Required]`, `[MinLength(8)]`, `[MaxLength(256)]`, `[EmailAddress]` attributes and ensure model validation filter is active.

---

### MEDIUM-2 — No Rate Limiting on Authentication Endpoints

`POST /api/auth/login` and `POST /api/auth/register` have no rate limiting. Brute-force and credential-stuffing attacks are not mitigated.

**Remediation:** Apply ASP.NET 8's built-in rate limiting middleware (`AddRateLimiter`) with a fixed or sliding-window policy on auth endpoints.

---

### MEDIUM-3 — No Model Ownership Tracking

`UploadModelCommand` accepts a nullable `UserId` which is always passed as `null` from the controller. Models are not associated with the uploading user.

**Remediation:** Extract `UserId` from the authenticated JWT claims in `ModelsController.UploadModel` and pass it to `UploadModelCommand`.

---

### MEDIUM-4 — Orphaned Jobs After Worker Restart

Jobs that transition to `Processing` before an API process restart are permanently stuck. The `ConversionWorker` only polls for `Queued` jobs on startup.

**Remediation:** On `ConversionWorker.ExecuteAsync` startup, re-queue any jobs with `Status == Processing` by resetting them to `Queued`.

---

### MEDIUM-5 — Conversion Job Deduplication Missing

Multiple `POST /api/conversions` calls for the same model ID and format will create multiple queued jobs. All will run sequentially, wasting resources.

**Remediation:** In `CreateConversionJobCommandHandler`, check for an existing `Queued` or `Processing` job for the same `ModelId` and `TargetFormat` before creating a new one.

---

### MEDIUM-6 — Controllers Bypass Application Layer for File Access

`ModelsController.GetModelOriginal` and `GetModelOutput` inject and directly call `IIfcModelRepository` and `IConversionJobRepository`, bypassing the MediatR/CQRS layer used consistently elsewhere.

**Remediation:** Extract `GetModelOriginalQuery` and `GetModelOutputQuery` use cases in the Application layer; inject only `IMediator` into the controller.

---

## LOW Severity Findings

---

### LOW-1 — POST /api/auth/register Returns HTTP 200 Instead of HTTP 201

Cosmetic REST convention violation. Should return `CreatedAtAction` with HTTP 201.

### LOW-2 — No Pagination on GET /api/models

Unbounded list response. Should implement cursor or offset pagination.

### LOW-3 — No PUT/PATCH on /api/models/{id}

Model metadata cannot be updated post-upload without delete + re-upload.

### LOW-4 — No Retry API Endpoint for Conversions

Failed jobs require creating a new job. A `POST /api/conversions/{id}/retry` endpoint would improve UX.

### LOW-5 — Progress is Binary (0% → 100%)

`ProgressPercentage` is always 0 until conversion completes, then immediately 100%. No incremental reporting.

### LOW-6 — development appsettings.Development.json JWT Secret in Source Control

`"BombasticIFC-Dev-SuperSecret-Key-That-Is-Long-Enough-256-Bit!!"` is committed. Acceptable for dev-only use; ensure this key is never used in any environment accessible from the internet.

### LOW-7 — `xeokit-convert` Presence Not Validated at Startup

The converter binary must be on PATH. A missing binary is only discovered at first conversion job execution. A startup probe or health check should verify the binary is present.

---

## Informational / Positive Findings

- BCrypt work factor 12: correct and industry-standard.
- JWT validation parameters fully enabled (issuer, audience, lifetime, key).
- Login error message avoids username enumeration.
- `IsActive` flag checked on login.
- Cascading delete in `DeleteModelCommand` handles files and job records cleanly.
- Bulk job status resolution in `GetModelsQuery` avoids N+1 queries.
- `ConversionWorker` correctly uses `IServiceScopeFactory` for scoped services.
- IFC header validation before conversion saves time on invalid files.
- External process argument list (not string concat) prevents argument injection.
- Process tree kill on timeout is correct.
- Partial output file cleanup on conversion failure is correct.
- Migration applied automatically on startup with fallback to `EnsureCreated`.

---

## Severity Summary

| Severity | Count |
|----------|-------|
| CRITICAL (BLOCKS MERGE) | 3 |
| HIGH | 3 |
| MEDIUM | 6 |
| LOW | 7 |
| INFO | 12 |

---

## Recommended Merge Gate

The following must be resolved before this codebase merges to a production branch:

1. **CRITICAL-1**: Add `[Authorize]` to `ModelsController` and `ConversionsController`.
2. **CRITICAL-2**: Remove `OutputFilePath` from `ConversionJobDto`; replace with a URL or boolean.
3. **CRITICAL-3**: Remove `RefreshToken` from `AuthResponseDto` until the refresh flow is fully implemented, OR implement the full refresh flow.
