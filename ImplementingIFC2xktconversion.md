# IFC→XKT Conversion Pipeline — Implementation Tasks

## Overview

When a user uploads an IFC file, a conversion job must be created automatically, the file converted to XKT (stored in `/mnt/data/storage` on the cluster), and the model's status updated to `Ready` so the Vue frontend can load and display it in the xeokit viewer.

**Current state:**
- Upload works → saves `.ifc` to `/data/storage`, creates `IfcModel` (status = `Uploaded`), and the frontend already calls `conversionsApi.createConversionJob` right after upload
- `CreateConversionJobCommand` only queues a `ConversionJob` record (status = `Queued`) — **nothing ever processes it**
- `IIfcConversionService` interface exists but has **no implementation**
- No background worker exists
- `IfcModel.Status` is never updated to `Processing` or `Ready`
- The dashboard polls every 5s while status is `Uploaded/Processing`, and the viewer loads `/api/models/{id}/output` when `Ready` — **both already wired up and waiting for the backend to deliver**

---

## Conversion Tool Choice

IFC→XKT uses the standard xeokit two-step pipeline:
1. **`IfcConvert`** (IfcOpenShell, statically-linked Linux binary) — `.ifc` → `.glb`
2. **`convert2xkt`** (xeokit Node.js CLI, `@xeokit/xeokit-convert`) — `.glb` → `.xkt`

Both must be installed inside the API Docker image.

---

## Phase 1 — `IfcConversionService`

**Create:** `src/BombasticIFC.Infrastructure/Services/IfcConversionService.cs`

- Implements `IIfcConversionService`
- `ConvertAsync(sourceFilePath, XKT, progress, ct)`:
  1. Derive temp `.glb` path and final `.xkt` path in storage dir (use `Guid.NewGuid()` prefix)
  2. Shell out: `IfcConvert "{src}" "{glbPath}" --use-element-guids` — report ~40% progress
  3. Shell out: `convert2xkt -s "{glbPath}" -o "{xktPath}"` — report 100% progress
  4. Delete intermediate `.glb`
  5. Return `xktPath`
- On non-zero exit code: throw `InvalidOperationException` with stderr
- `IsConversionSupportedAsync` → `true` for `XKT` only
- Constructor takes `string storagePath`

---

## Phase 2 — `ConversionWorker` (Background Service)

**Create:** `src/BombasticIFC.Infrastructure/Services/ConversionWorker.cs`

- Implements `BackgroundService`
- Every **5 seconds**: fetch all `Queued` jobs via `IConversionJobRepository.GetByStatusAsync(Queued)`
- For each job (sequential, one at a time):
  1. Fetch `IfcModel` → `job.StartProcessing()` + `model.UpdateStatus(Processing)` → persist both
  2. Call `IIfcConversionService.ConvertAsync(...)` with progress reporting → `job.UpdateProgress(pct)` + `UpdateAsync(job)`
  3. On success: `job.Complete(xktPath)` + `model.UpdateStatus(Ready)` → persist
  4. On failure: `job.Fail(ex.Message)` + `model.UpdateStatus(Failed)` → persist
- Use `IServiceScopeFactory` to resolve scoped services (repositories) inside the singleton hosted service

---

## Phase 3 — DI Registration

**Modify:** `src/BombasticIFC.API/Program.cs`

Add after existing registrations:
```csharp
builder.Services.AddScoped<IIfcConversionService>(_ =>
    new IfcConversionService(
        builder.Configuration.GetValue<string>("StoragePath") ?? "/data/storage"));

builder.Services.AddHostedService<ConversionWorker>();
```

---

## Phase 4 — Dockerfile

**Modify:** `Dockerfile` (runtime `final` stage, before `ENTRYPOINT`)

```dockerfile
RUN apt-get update && apt-get install -y --no-install-recommends \
    nodejs npm wget unzip ca-certificates && \
    rm -rf /var/lib/apt/lists/*

RUN wget -q https://github.com/IfcOpenShell/IfcOpenShell/releases/download/ifcopenshell-python-0.7.0/ifcconvert-0.7.0-linux64.zip \
    -O /tmp/ifcconvert.zip && \
    unzip /tmp/ifcconvert.zip -d /usr/local/bin && \
    chmod +x /usr/local/bin/IfcConvert && \
    rm /tmp/ifcconvert.zip

RUN npm install -g @xeokit/xeokit-convert
```

> **Important:** Match `@xeokit/xeokit-convert` version to the `@xeokit/xeokit-sdk` version in `frontend/package.json` to ensure XKT format compatibility.

---

## Phase 5 — Frontend: UploadView UX

**Modify:** `frontend/src/views/UploadView.vue`

- After `createConversionJob` succeeds, show: *"Hochgeladen — Konvertierung läuft im Hintergrund"*
- Redirect to `/dashboard` immediately (don't wait for conversion — the dashboard polls)
- No polling needed in UploadView; the dashboard's existing 5-second polling handles the rest

---

## Phase 6 — Race Condition Fix (2 API replicas)

**Modify:** `kubernetes/api-deployment.yaml`

Set `replicas: 1` **or** add an atomic job-claim mechanism in `ConversionWorker` (update job status to `Processing` in a single `UPDATE ... WHERE status = Queued` before loading it, to prevent two pods processing the same job).

The simplest correct fix for this single-node Minikube setup: keep `replicas: 2` for API serving but use a separate single-replica **worker** deployment. Alternatively, just reduce to `replicas: 1` for now.

---

## Key Files

| File | Action |
|---|---|
| `src/BombasticIFC.Infrastructure/Services/IfcConversionService.cs` | **Create** |
| `src/BombasticIFC.Infrastructure/Services/ConversionWorker.cs` | **Create** |
| `src/BombasticIFC.API/Program.cs` | **Modify** — register service + worker |
| `Dockerfile` | **Modify** — install tools |
| `frontend/src/views/UploadView.vue` | **Modify** — UX message |

---

## Verification

1. `docker build -t bombasticifccluster-api:latest .` — verify build succeeds
2. `docker run --rm bombasticifccluster-api:latest IfcConvert --version` and `convert2xkt --version`
3. `minikube image load bombasticifccluster-api:latest`
4. `kubectl rollout restart deployment/api-deployment -n bombasticifccluster`
5. Upload `test/Sample/ELEMENT_C_1.OG_C1_STFT.ifc` via frontend
6. `kubectl logs -n bombasticifccluster -l app=bombasticifccluster-api -f` — watch conversion logs
7. `kubectl exec -it <pod> -n bombasticifccluster -- ls /data/storage/` — verify `.xkt` file appears
8. Dashboard model status: `Hochgeladen → Verarbeitung → Bereit`
9. Click **Anzeigen** → xeokit viewer loads the XKT

---

## Risks

1. **IfcConvert URL** — pin to a specific GitHub release; the URL in Phase 4 may need adjustment for the latest stable build
2. **XKT version compatibility** — `convert2xkt` output format must match frontend SDK version; check `package.json`
3. **Memory limit** — large IFC files can spike RAM; may need to raise `limits.memory: 512Mi` in `api-deployment.yaml`
4. **Dual-pod race** — two replicas will both poll for `Queued` jobs; see Phase 6
