# Sprint Tasks - IFC Converter Reliability

## Goal
Improve IFC conversion reliability and correctness for broader IFC files while keeping xeokit-convert as the converter.

## In Scope
- Fix stale mocked-service comments in tests.
- Harden converter process execution (validation, timeout, cancellation, logging).
- Improve command/path safety for source and output files.
- Improve worker resilience with bounded retry and safer progress persistence.
- Add failed artifact cleanup on conversion failures.
- Add unit tests for the new behavior.

## Tasks

1. Update stale test comments
- File: test/BombasticIFC.Tests/Conversion/IfcToXktConversionTests.cs
- Replace outdated wording that claims no production converter is wired.
- Keep clear rationale for mocking in unit tests.

2. Harden converter input validation
- File: src/BombasticIFC.Infrastructure/Services/IfcConversionService.cs
- Validate file exists and is non-empty.
- Validate IFC/STEP header and schema markers before launching converter.

3. Improve converter process robustness
- File: src/BombasticIFC.Infrastructure/Services/IfcConversionService.cs
- Use safe process argument handling.
- Add timeout and cancellation-aware process termination.
- Capture both stdout and stderr.
- Emit structured logs for command, duration, exit code, and diagnostics.

4. Improve conversion worker reliability
- File: src/BombasticIFC.Infrastructure/Services/ConversionWorker.cs
- Replace fire-and-forget progress persistence with safe in-memory progress updates.
- Add bounded retry for transient conversion failures.
- Ensure model and job statuses remain consistent on all paths.

5. Cleanup failed output artifacts
- File: src/BombasticIFC.Infrastructure/Services/IfcConversionService.cs
- Delete partially generated output when conversion fails or times out.

6. Improve upload filename safety
- File: src/BombasticIFC.Infrastructure/Services/FileStorageService.cs
- Sanitize uploaded filenames used on disk while preserving original display name in domain entity.

7. Wire DI for updated converter constructor
- File: src/BombasticIFC.API/Program.cs
- Register converter with logger-capable constructor.

8. Add/update unit tests
- Files:
  - test/BombasticIFC.Tests/Conversion/IfcToXktConversionTests.cs
  - test/BombasticIFC.Tests/Conversion/ConversionWorkerTests.cs (new)
- Cover invalid IFC header rejection, timeout/cancellation behavior (at worker level), retry behavior, and failure state handling.

## Acceptance Criteria
- Conversion service rejects invalid IFC files early with clear messages.
- Converter process respects cancellation and timeout and does not leave zombies.
- Failed conversions do not leave partial output files.
- Worker retries transient failures once and fails deterministically afterward.
- Existing and new unit tests pass.
- Test comments accurately reflect current architecture.

## Out of Scope
- Replacing xeokit-convert with a different converter stack.
- Integration/e2e conversion tests in this sprint.
- Kubernetes resource tuning changes.

---

# Sprint 2 — File Storage DB Integration & Viewer Fix

## Goal
Fix the `404 / getXKT error : [object ArrayBuffer]` viewer error by surfacing both the original IFC URL and the converted XKT URL as first-class fields in the model DTO returned by the API. The Vue app must resolve file locations from the database, not by constructing hard-coded URL patterns.

## Root Cause Analysis

| # | Problem | Affected file |
|---|---------|---------------|
| 1 | `IfcModelDto` exposes no file paths or download URLs | `DTOs/IfcModelDto.cs` |
| 2 | `GetModelByIdQueryHandler` / `GetModelsQueryHandler` do not join conversion jobs, so the viewer never learns whether a valid XKT exists | `UseCases/Models/Get*.cs` |
| 3 | Viewer constructs XKT URL as a hard-coded pattern; 404s when no completed conversion job exists yet | `ViewerView.vue`, `DashboardView.vue` |
| 4 | Upload flow redirects to dashboard immediately after `createConversionJob`, before the worker finishes, so the viewer opens with `status !== Ready` | `UploadView.vue` |
| 5 | No API endpoint to serve the original unchanged IFC file | `ModelsController.cs` |

## Summary of Changes

### Backend

**Task B1 — Extend `IfcModelDto` with file URL fields**
- File: `src/BombasticIFC.Application/DTOs/IfcModelDto.cs`
- Add `string? XktOutputUrl` — set to `/api/models/{id}/output` when a completed conversion job exists.
- Add `string? OriginalFileUrl` — set to `/api/models/{id}/original` always when a source file exists.

**Task B2 — Inject conversion repo into `GetModelByIdQueryHandler`**
- File: `src/BombasticIFC.Application/UseCases/Models/GetModelByIdQuery.cs`
- Inject `IConversionJobRepository`.
- After loading the model, call `GetByModelIdAsync` and pick the latest `Completed` job.
- Populate `XktOutputUrl` and `OriginalFileUrl` on the returned DTO.

**Task B3 — Inject conversion repo into `GetModelsQueryHandler`**
- File: `src/BombasticIFC.Application/UseCases/Models/GetModelsQuery.cs`
- Load all conversion jobs in a single batch query (avoid N+1).
- Group by `ModelId`, pick latest `Completed` job per model.
- Populate `XktOutputUrl` and `OriginalFileUrl` for each DTO in the result list.

**Task B4 — Add `GET /api/models/{id}/original` endpoint**
- File: `src/BombasticIFC.API/Controllers/ModelsController.cs`
- Fetch `IfcModel` from repository, return original IFC file from `OriginalFilePath` as `application/octet-stream`.
- Return 404 with a structured JSON message if model not found or file missing on disk.

**Task B5 — Harden `GET /api/models/{id}/output` endpoint**
- File: `src/BombasticIFC.API/Controllers/ModelsController.cs`
- Explicitly set `Content-Type: application/octet-stream`.
- Never return a JSON body on error paths that xeokit will receive as an ArrayBuffer.
- Use `Content-Disposition: inline; filename="{modelId}.xkt"` so browser/xeokit gets a clear filename.

### Frontend

**Task F1 — Add `xktOutputUrl` and `originalFileUrl` to the TypeScript DTO**
- File: `frontend/src/types/models.ts`
- Add `xktOutputUrl: string | null` and `originalFileUrl: string | null` to `IfcModelDto`.

**Task F2 — Fix `ViewerView.vue` — use URL from DTO**
- File: `frontend/src/views/ViewerView.vue`
- Load `model.xktOutputUrl` from DTO rather than calling `modelsApi.getModelOutputUrl(modelId)`.
- Show a clear "Konvertierung ausstehend" / conversion-pending state when `xktOutputUrl` is null.
- Only attempt to load xeokit when `xktOutputUrl` is non-null and model status is `Ready`.

**Task F3 — Fix `DashboardView.vue` — use URL from DTO**
- File: `frontend/src/views/DashboardView.vue`
- Pass `model.xktOutputUrl` to xeokit `xktLoader.load({src})` instead of computing the URL manually via `modelsApi.getModelOutputUrl`.

**Task F4 — Fix `UploadView.vue` — poll until conversion finishes**
- File: `frontend/src/views/UploadView.vue`
- After `createConversionJob`, poll `modelsApi.getModel(id)` every 3 s (max 10 min).
- Show live conversion status badge to the user while polling.
- Redirect to dashboard only once status is `Ready` or `Failed`.
- Stop polling on component unmount.

## Acceptance Criteria
- `GET /api/models` and `GET /api/models/{id}` return populated `xktOutputUrl` for models with a completed conversion job.
- `GET /api/models/{id}/original` serves the raw IFC file.
- Viewer does not attempt to load XKT when `xktOutputUrl` is null.
- Viewer loads xeokit successfully when `xktOutputUrl` is populated.
- Upload flow shows live conversion state and redirects only after job completes.
- No `404 / getXKT error` appears in the browser console for valid converted models.

## Affected Files

| File | Change |
|------|--------|
| `src/BombasticIFC.Application/DTOs/IfcModelDto.cs` | Add `XktOutputUrl`, `OriginalFileUrl` |
| `src/BombasticIFC.Application/UseCases/Models/GetModelByIdQuery.cs` | Join latest completed job |
| `src/BombasticIFC.Application/UseCases/Models/GetModelsQuery.cs` | Batch-join completed jobs |
| `src/BombasticIFC.API/Controllers/ModelsController.cs` | New `/original` endpoint; harden `/output` |
| `frontend/src/types/models.ts` | Add TS URL fields |
| `frontend/src/views/ViewerView.vue` | Load XKT from DTO URL |
| `frontend/src/views/DashboardView.vue` | Load XKT from DTO URL |
| `frontend/src/views/UploadView.vue` | Poll until job complete |

## Out of Scope
- Authentication on file download endpoints (can be added in a later sprint).
- Serving XKT files directly from a CDN or pre-signed storage URL.
- Streaming large IFC files with range requests.
