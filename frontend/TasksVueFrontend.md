# Frontend — Tasks & Deployment Phases

> Decisions: nginx pod in k8s · xeokit-sdk with Duplex demo (runtime URL) · .xkt format · model list dashboard

---

## Phase A — Backend gaps (unblocks frontend)

- [x] **A1** Add `GetModelsQuery` + handler → `src/BombasticIFC.Application/UseCases/Models/GetModelsQuery.cs`
  - Returns `List<IfcModelDto>` via `IIfcModelRepository`
  - Add `GetAllAsync()` to `IIfcModelRepository` + `IfcModelRepository` if missing
- [x] **A2** Wire `GET /api/models` in `ModelsController.cs` → `[HttpGet]` calling `GetModelsQuery`
- [x] **A3** Add `GetConversionJobQuery` + handler → `src/BombasticIFC.Application/UseCases/Conversion/GetConversionJobQuery.cs`
  - Fix the 404 stub in `ConversionsController.GetConversionJob()`
- [x] **A4** Add `GET /api/models/{id}/output` → streams `.xkt` file from `OutputFilePath` on disk
  - Returns `application/octet-stream` with Content-Disposition

---

## Phase B — Frontend types & API layer (parallel with A)

- [x] **B1** Create `src/types/models.ts`
  - Interfaces: `IfcModelDto`, `ModelMetadataDto`, `ConversionJobDto`
  - Const objects + type aliases: `ModelStatus`, `ConversionStatus`, `ConversionFormat` (erasableSyntaxOnly)
- [x] **B2** Update `src/types/index.ts` — re-export new types
- [x] **B3** Create `src/api/models.ts`
  - `getModels()` → `GET /api/models`
  - `getModel(id)` → `GET /api/models/:id`
  - `uploadModel(file)` → `POST /api/models/upload` (multipart/form-data)
  - `getModelOutputUrl(id)` → returns `/api/models/${id}/output`
- [x] **B4** Create `src/api/conversions.ts`
  - `createConversionJob(modelId, format)` → `POST /api/conversions`
  - `getConversionJob(id)` → `GET /api/conversions/:id`

---

## Phase C — Frontend views (depends on B)

- [x] **C1** Implement `UploadView.vue`
  - Drag-and-drop + click-to-browse for `.ifc` files
  - Client-side validation (extension, 500 MB limit)
  - `POST /api/models/upload` with axios `onUploadProgress` progress bar
  - On success: `POST /api/conversions` with `{ ModelId, TargetFormat: 0 (XKT) }`
  - Success toast + redirect to Dashboard
- [x] **C2** Implement `DashboardView.vue`
  - Model table (filename, size, status badge, uploaded date)
  - Auto-poll every 5s while any model is Uploaded/Processing
  - "Anzeigen" button → `/viewer/:id` (enabled only when status is Ready)
  - Empty state + error state + refresh button
- [x] **C3** Install `@xeokit/xeokit-sdk` ^2.6.0 → added to `package.json`
- [x] **C4** Implement `ViewerView.vue`
  - Route `/viewer/:id`; full-height canvas with toolbar
  - Dynamic import of xeokit-sdk (avoids SSR issues)
  - `XKTLoaderPlugin` loads from `/api/models/:id/output` when Ready
  - Fallback: Duplex demo `https://xeokit.github.io/xeokit-sdk/assets/models/xkt/v8/Duplex/Duplex.xkt`
  - Error fallback: if real model fails to load, auto-loads demo
  - Destroy `Viewer` on `onUnmounted`

---

## Phase D — Kubernetes deployment (depends on C)

- [x] **D1** Create `frontend/Dockerfile`
  - Stage 1: `node:20-alpine` — `npm ci`, `vite build`
  - Stage 2: `nginx:alpine` — copy `dist/` + `nginx.conf`
- [x] **D2** Create `frontend/nginx.conf`
  - `try_files $uri /index.html` (SPA routing)
  - `location /api/ { proxy_pass http://api-service.bombasticifccluster.svc.cluster.local/api/; }`
  - gzip enabled, static asset caching, serves on port 80
- [x] **D3** Create `kubernetes/frontend-deployment.yaml`
  - Deployment: 1 replica, `bombasticifccluster-frontend:latest`, `imagePullPolicy: Never`
  - ClusterIP Service `frontend-service` → port 80 → containerPort 80
  - Liveness + readiness probes
- [x] **D4** Update `kubernetes/ingress.yaml`
  - `path: /` → backend `frontend-service` (was `api-service`)
  - `path: /api` → backend `api-service` (unchanged)
  - `path: /health` → backend `api-service`
  - Removed `rewrite-target` annotation (frontend handles SPA routing)

---

## Build & Deploy Commands

```bash
# Inside Minikube docker context
eval $(minikube docker-env)

# Build frontend image
docker build -t bombasticifccluster-frontend:latest ./frontend

# Apply manifests
kubectl apply -f kubernetes/frontend-deployment.yaml
kubectl apply -f kubernetes/ingress.yaml
```

---

## Verification

1. `cd frontend && npm install && npm run build` — zero TS errors
2. `docker build -t bombasticifccluster-frontend:latest ./frontend` — succeeds
3. `kubectl get pods -n bombasticifccluster` — frontend pod Running
4. `http://bombasticifccluster.local` → Home page renders
5. Register + Login → Dashboard shows model list
6. Upload `.ifc` → progress bar → model card appears (Pending)
7. Click View → xeokit canvas loads Duplex demo (until conversion worker exists)
8. `http://bombasticifccluster.local/api/health` → `{ status: "healthy" }`

---

## Files to modify

| File | Change |
|---|---|
| `frontend/src/views/UploadView.vue` | Replace stub |
| `frontend/src/views/DashboardView.vue` | Replace stub |
| `frontend/src/views/ViewerView.vue` | Replace stub with xeokit |
| `frontend/package.json` | Add `@xeokit/xeokit-sdk` |
| `frontend/src/types/index.ts` | Re-export new types |
| `src/BombasticIFC.API/Controllers/ModelsController.cs` | Add GET list + output endpoint |
| `src/BombasticIFC.API/Controllers/ConversionsController.cs` | Fix GET stub |
| `kubernetes/ingress.yaml` | Reroute `/` to frontend |

## New files

- `frontend/src/types/models.ts`
- `frontend/src/api/models.ts`
- `frontend/src/api/conversions.ts`
- `frontend/Dockerfile`
- `frontend/nginx.conf`
- `kubernetes/frontend-deployment.yaml`
- `src/BombasticIFC.Application/UseCases/Models/GetModelsQuery.cs`
- `src/BombasticIFC.Application/UseCases/Conversion/GetConversionJobQuery.cs`
