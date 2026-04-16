# Conversions API Audit — 2026-04-16

Auditor: Conversions_Api_Agent
Scope: `src/BombasticIFC.API/Controllers/ConversionsController.cs`, Application use-case handlers, `ConversionWorker`, `IfcConversionService`

---

## 1. Endpoint Inventory

| Method | Route | Auth Required | Status |
|--------|-------|--------------|--------|
| POST | /api/conversions | No | Implemented |
| GET | /api/conversions/{id} | No | Implemented |
| POST | /api/conversions/{id}/retry | — | MISSING |
| GET | /api/conversions (list all) | — | MISSING |

---

## 2. Implemented Endpoint Analysis

### POST /api/conversions
- Accepts `{ modelId: guid, targetFormat: enum }`.
- `ConversionFormat` enum currently has only `XKT` as a valid value.
- Delegates to `CreateConversionJobCommand` via MediatR.
- Handler verifies the model exists, calls `model.CreateConversionJob(targetFormat)`, persists the job record, returns `ConversionJobDto`.
- Returns HTTP 201 Created with `Location` header pointing to `/api/conversions/{id}`.
- The job is created with `ConversionStatus.Queued`. The `ConversionWorker` background service picks it up within its polling interval (5 seconds).
- **Issue**: No authentication guard.
- **Issue**: No deduplication — multiple queued jobs can be created for the same model without restriction. A client could flood the queue.
- **Issue**: No validation that the model is in a state ready for conversion (e.g., a model with `Status == Processing` could receive another job).

### GET /api/conversions/{id}
- Returns `ConversionJobDto` or HTTP 404.
- Delegates to `GetConversionJobQuery`.
- `ConversionJobDto` includes: `id`, `modelId`, `targetFormat`, `status`, `progressPercentage`, `createdAt`, `startedAt`, `completedAt`, `outputFilePath`, `errorMessage`.
- **Issue**: `outputFilePath` is an absolute server-side filesystem path. This is an internal implementation detail that should not be exposed to API consumers. It could reveal server directory structure.
- **Issue**: No authentication guard.

---

## 3. Missing Endpoints

### POST /api/conversions/{id}/retry
- No retry endpoint exists via the API. Retry logic exists only within the `ConversionWorker` (automatic retry on transient errors, max 2 attempts). A failed job cannot be requeued by a client without creating a new job via POST /api/conversions.

### GET /api/conversions (list)
- No endpoint to list all conversion jobs. Clients can only poll individual jobs by known ID.

### GET /api/conversions?modelId={modelId} (filter by model)
- No filtered query endpoint exists.

---

## 4. Worker and Queue Mechanism

### ConversionWorker
- Implemented as an ASP.NET `IHostedService` (`BackgroundService`).
- Polls `ConversionJobRepository.GetByStatusAsync(Queued)` every 5 seconds.
- Processes jobs sequentially within each poll cycle.
- On pickup: sets job to `Processing`, sets model to `Processing`, persists both.
- On success: sets job to `Completed` with output path, sets model to `Ready`, persists both.
- On failure: sets job to `Failed` with error message, sets model to `Failed`, persists both.
- **Issue**: In-process background worker means the conversion queue shares the API process. A long-running conversion blocks no other requests but any API process restart (rolling deploy, crash) will abandon in-flight jobs. Jobs that were set to `Processing` before a restart will remain stuck in `Processing` state permanently — there is no recovery/requeue mechanism for orphaned jobs.
- **Issue**: Sequential processing only. Concurrent conversions are not supported. A large IFC file could block the queue for minutes.

### ConversionWorker — Retry Logic
- `ConvertWithRetryAsync` retries up to `MaxAttempts = 2` times.
- Retries only on `TimeoutException` or `IOException` (transient errors).
- Retry delay is 2 seconds.
- If all attempts fail, a descriptive `InvalidOperationException` is thrown and the job is failed.
- **Issue**: Maximum of only 2 total attempts (1 initial + 1 retry) may be insufficient for workloads with intermittent transient failures.

### IfcConversionService
- Executes `xeokit-convert -s <input> -o <output>` as an external process.
- Validates IFC header (ISO-10303-21, FILE_SCHEMA, IFC schema name) before invoking the converter.
- Conversion timeout: 15 minutes per job.
- On process failure: cleans up partial output file.
- On timeout: kills the process tree, throws `TimeoutException`.
- Uses `Process.StartInfo.ArgumentList` (not string concatenation) for argument passing — protected against argument injection. GOOD.
- **Issue**: `xeokit-convert` must be on the system PATH. No startup check validates its presence — failures manifest only when the first job is processed.
- **Issue**: Progress reporting calls `progress.Report(100)` only at the very end of conversion (after process exit). There is no incremental progress — clients polling `/api/conversions/{id}` will see 0% until the job completes, then immediately 100%.

---

## 5. Error Handling Gaps

| Gap | Impact |
|-----|--------|
| No orphaned job recovery (stuck in Processing after restart) | Jobs can be permanently stuck |
| `outputFilePath` exposed in API response | Information disclosure |
| No deduplication of conversion jobs per model | Queue flooding possible |
| No model-state guard before creating a job | Duplicate/concurrent conversion attempts |
| No retry API endpoint | Failed jobs require new job creation via POST |
| Progress always 0% until completion | Poor UX for polling clients |

---

## 6. ConversionJobDto — Response Shape

```
{
  "id": "guid",
  "modelId": "guid",
  "targetFormat": "enum (XKT)",
  "status": "enum (Queued|Processing|Completed|Failed)",
  "progressPercentage": "int (0-100)",
  "createdAt": "datetime (UTC)",
  "startedAt": "datetime (UTC) | null",
  "completedAt": "datetime (UTC) | null",
  "outputFilePath": "string | null",   <- INTERNAL PATH, SHOULD BE REMOVED
  "errorMessage": "string | null"
}
```

---

## 7. Security Findings Summary

| Severity | Finding |
|----------|---------|
| HIGH | `outputFilePath` (absolute server filesystem path) is exposed in `ConversionJobDto` response |
| HIGH | No authentication on any conversion endpoint |
| MEDIUM | No job deduplication — any caller can flood the conversion queue |
| MEDIUM | In-process worker: jobs stuck in Processing state after API restart cannot self-recover |
| LOW | No retry API endpoint — failed jobs require creating a new job |
| LOW | No list/filter endpoint for conversion jobs |
| LOW | Progress reporting is binary (0% → 100%) not incremental |
| INFO | Only XKT format is supported — `ConversionFormat` enum is a single value |

---

## 8. What is Well-Implemented

- `ConversionWorker` correctly uses `IServiceScopeFactory` for scoped dependencies in a singleton background service. GOOD.
- Transient error retry with exponential-delay concept (though fixed delay is used).
- Process timeout with `CancellationTokenSource` linking and process tree kill. GOOD.
- IFC header validation before invoking the converter saves time on invalid files.
- Partial output file cleanup on conversion failure is correct.
- Argument list construction for external process avoids shell injection. GOOD.
- `ConversionJobDto` includes both `startedAt` and `completedAt` timestamps for duration calculation.
