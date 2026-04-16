# Models API Audit — 2026-04-16

Auditor: Models_Api_Agent
Scope: `src/BombasticIFC.API/Controllers/ModelsController.cs`, Application use-case handlers for models, Infrastructure repositories and file storage

---

## 1. Endpoint Inventory

| Method | Route | Auth Required | Status |
|--------|-------|--------------|--------|
| GET | /api/models | No | Implemented |
| POST | /api/models/upload | No | Implemented |
| GET | /api/models/{id} | No | Implemented |
| GET | /api/models/{id}/original | No | Implemented |
| GET | /api/models/{id}/output | No | Implemented |
| DELETE | /api/models/{id} | No | Implemented |
| PUT/PATCH | /api/models/{id} | — | MISSING |

---

## 2. Implemented Endpoint Analysis

### GET /api/models
- No query parameters (no pagination, filtering, or sorting).
- Delegates to `GetModelsQuery` via MediatR.
- Handler fetches all models, then resolves `XktOutputUrl` and `OriginalFileUrl` from job status in two bulk queries (avoids N+1). GOOD.
- Returns `List<IfcModelDto>` with fields: `id`, `fileName`, `fileSizeBytes`, `status`, `createdAt`, `updatedAt`, `xktOutputUrl`, `originalFileUrl`, `conversionError`.
- **Issue**: No pagination. For large deployments with many models this will produce unbounded query results and large response payloads.
- **Issue**: No authentication/authorization guard. Any unauthenticated caller can enumerate all models.

### POST /api/models/upload
- Accepts `multipart/form-data` with a single `IFormFile` parameter named `file`.
- Validates: file not null/empty, file extension must be `.ifc`.
- Delegates to `UploadModelCommand`.
- Returns `IfcModelDto` with HTTP 201 Created and `Location` header pointing to `/api/models/{id}`.
- **Issue**: No file size limit enforced at the controller level (ASP.NET's default request size limit applies, typically 30 MB, but this is not explicitly configured for large IFC files which can be hundreds of MB).
- **Issue**: Only extension validation (`.ifc`) — no MIME type check, no magic-byte/header validation at upload time. A file named `malicious.ifc` containing arbitrary content would be accepted and stored (though `IfcConversionService` does validate the IFC header before conversion).
- **Issue**: `UploadModelCommand` accepts a nullable `UserId` (`Guid?`), and the controller always passes `null`. Model ownership/authorship is not tracked.
- **Issue**: No `[Authorize]` attribute. Any unauthenticated user can upload models.
- Conversion job is NOT automatically triggered on upload. The client must make a separate POST to `/api/conversions` after upload.

### GET /api/models/{id}
- Returns single `IfcModelDto` or HTTP 404.
- Delegates to `GetModelByIdQuery`.
- **Note**: `GetModelByIdQuery` implementation was not located in the scanned source. Based on handler registration it should exist in `UseCases/Models/`.
- No authentication guard.

### GET /api/models/{id}/original
- Directly accesses `IIfcModelRepository` and `IFileStorageService` — bypasses MediatR/CQRS pattern used elsewhere.
- Streams the raw IFC source file as `application/octet-stream`.
- Returns HTTP 404 if model or file not found.
- **Issue**: No authentication guard — anyone can download raw IFC files.
- **Issue**: Inconsistent architecture: bypasses the application layer pattern.

### GET /api/models/{id}/output
- Resolves the most recent completed `ConversionJob` with a non-null `OutputFilePath`.
- Streams the XKT file as `application/octet-stream`.
- Returns HTTP 404 if no completed job or if file is missing from disk.
- **Issue**: No authentication guard.
- **Issue**: Inconsistent architecture: bypasses the application layer.
- **Issue**: When the file is missing on disk but a completed job record exists, returns HTTP 404 with `application/json` content type set via `Response.ContentType`, but the return type is still `NotFound(new { ... })` which should set the content type automatically. The manual `Response.ContentType` assignment before a `NotFound()` result is redundant and potentially misleading.

### DELETE /api/models/{id}
- Delegates to `DeleteModelCommand`.
- Handler deletes original file, all related conversion job output files, all conversion job records, then the model record — in that order.
- Returns HTTP 204 No Content on success, HTTP 404 if not found.
- Cascading file deletion is well-implemented.
- **Issue**: No authentication guard. Any unauthenticated caller can delete any model.
- **Issue**: No role check — no admin-only restriction on deletes.

---

## 3. Missing Endpoints

### PUT or PATCH /api/models/{id}
- No update endpoint exists. Model metadata (filename, description) cannot be changed after upload without deleting and re-uploading.

### GET /api/models/{id}/jobs (or similar)
- No endpoint to list all conversion jobs for a specific model. Clients must track job IDs from the creation response or use `/api/conversions/{id}`.

---

## 4. Request/Response Shape

### IfcModelDto
```
{
  "id": "guid",
  "fileName": "string",
  "fileSizeBytes": "int64",
  "status": "enum (Pending|Processing|Ready|Failed)",
  "createdAt": "datetime (UTC)",
  "updatedAt": "datetime (UTC) | null",
  "xktOutputUrl": "string | null",
  "originalFileUrl": "string | null",
  "conversionError": "string | null"
}
```

### Upload Request
```
Content-Type: multipart/form-data
Field: file (IFormFile, required, must end in .ifc)
```

---

## 5. File Storage

- `FileStorageService` stores files under the configured `StoragePath` (`/data/storage` in production, `./data/storage` in dev).
- Files are stored with GUID-based names generated at save time (collision risk negligible).
- No virus/malware scanning at upload.
- No file-size quota per user.

---

## 6. Security Findings Summary

| Severity | Finding |
|----------|---------|
| HIGH | All model endpoints lack authentication — any unauthenticated caller can upload, download, enumerate, and delete models |
| HIGH | No file size limit configured — potential DoS via large upload |
| MEDIUM | No model ownership enforcement — `UserId` is always null |
| MEDIUM | No MIME type or magic-byte validation at upload (only extension check) |
| MEDIUM | GET /original and GET /output bypass the application layer (direct repository access in controller) |
| LOW | No pagination on GET /api/models |
| LOW | No PUT/PATCH endpoint for model metadata updates |
| INFO | Conversion is not auto-triggered on upload — requires separate API call |

---

## 7. What is Well-Implemented

- HTTP 201 + Location header on successful upload.
- Cascading delete (files + job records + model record) in `DeleteModelCommand`.
- Bulk job status resolution in `GetModelsQuery` avoids N+1 database queries.
- `XktOutputUrl` and `OriginalFileUrl` are returned as relative URLs, keeping the API self-describing.
- `ConversionError` from the latest failed job is surfaced in the model list response.
