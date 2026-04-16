# CHANGELOG

All notable audits, changes, and releases to the BombasticIFC platform are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Audit] Security & API Completeness Audit — 2026-04-16

**Run by:** SoftwareEngineering_Orchestrator (IFCCM agentic pipeline)
**Scope:** Full backend audit — AuthController, ModelsController, ConversionsController, Application layer, Infrastructure services
**Output files:**
- `docs/audit/auth_audit_2026-04-16.md`
- `docs/audit/models_audit_2026-04-16.md`
- `docs/audit/conversions_audit_2026-04-16.md`
- `docs/audit/consolidated_review_2026-04-16.md`
- `docs/openapi/ifccm_api_audit_2026-04-16.yaml`

### Summary of Findings

**3 CRITICAL findings (BLOCKS MERGE):**

1. **CRITICAL-1** — No `[Authorize]` attribute on `ModelsController` or `ConversionsController`. All file management endpoints are publicly accessible without authentication.
2. **CRITICAL-2** — `ConversionJobDto.OutputFilePath` exposes absolute server-side filesystem paths in API responses (information disclosure).
3. **CRITICAL-3** — Refresh token is returned by login/register but is never persisted; the refresh flow is entirely non-functional.

**3 HIGH findings:**
- No token revocation / logout endpoint
- No file upload size limit configured
- `AllowAnyOrigin` CORS policy in production

**6 MEDIUM findings:**
- No input validation on auth DTOs
- No rate limiting on authentication endpoints
- No model ownership tracking (UserId always null)
- Orphaned conversion jobs after worker restart
- No conversion job deduplication
- Controllers bypass application layer for file serving

**7 LOW findings:** HTTP status codes, missing pagination, missing PUT/PATCH, no retry endpoint, binary progress reporting, dev secret in source control, no startup xeokit-convert probe.

### Endpoints Audited

| Endpoint | Status |
|----------|--------|
| POST /api/auth/register | Implemented |
| POST /api/auth/login | Implemented |
| GET /api/auth/me | Implemented |
| POST /api/auth/logout | MISSING |
| POST /api/auth/refresh | MISSING |
| GET /api/models | Implemented |
| POST /api/models/upload | Implemented |
| GET /api/models/{id} | Implemented |
| GET /api/models/{id}/original | Implemented |
| GET /api/models/{id}/output | Implemented |
| DELETE /api/models/{id} | Implemented |
| POST /api/conversions | Implemented |
| GET /api/conversions/{id} | Implemented |
| POST /api/conversions/{id}/retry | MISSING |
| GET /health | Implemented |
