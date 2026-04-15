# BombasticIFC — Project Context for CAMPS Agents

Reference this document at the start of every task. It describes the target project that CAMPS agents operate on.

---

## Project Identity

| Field | Value |
|-------|-------|
| Name | BombasticIFCviewer |
| Type | Web application — IFC 3D model viewer & converter |
| Repo root | `/home/luke/LukisBombasticIFCviewer` |
| Solution file | `BombasticIFCcluster.sln` |
| Active branch | `develop` (PRs target `main`) |
| Git user | `lukewenger` |

---

## High-Level Architecture

```
Browser (Vue 3 + xeokit-sdk)
        │ HTTP via Ingress
        ▼
Kubernetes Cluster  (namespace: bombasticifccluster)
├── frontend-deployment   Vue.js SPA served by nginx
├── api-deployment (×2)  .NET 8 REST API
│       └── xeokit-convert  (Node.js CLI, runs inside the API pod)
└── postgres-deployment   PostgreSQL 16-alpine
```

The API pod embeds Node.js + xeokit-convert to convert uploaded `.ifc` files to `.xkt` format, which the browser viewer consumes.

---

## Tech Stack

### Backend (`src/`)
| Layer | Technology |
|-------|-----------|
| Language | C# / .NET 8 |
| Architecture | Clean Architecture + CQRS (MediatR) |
| Database | PostgreSQL 16 via Entity Framework Core |
| Auth | JWT Bearer tokens |
| IFC conversion | `xeokit-convert@1.3.1` (Node.js CLI, called via `Process`) |
| Container | Docker multi-stage build |

### Frontend (`frontend/`)
| Item | Technology |
|------|-----------|
| Framework | Vue 3 (Composition API) |
| State | Pinia |
| 3D viewer | xeokit-sdk |
| Build | Vite |
| Container | nginx:alpine |

### Infrastructure
| Item | Detail |
|------|--------|
| Orchestrator | Minikube (dev) / K3s (prod) |
| Deploy scripts | `deploy.sh`, `deployAPI.sh`, `update-frontend.sh` |
| Storage | hostPath PVCs — `/mnt/data/postgres` (10 Gi), `/mnt/data/storage` (50 Gi) |
| Ingress | nginx; max body 500 MB for large IFC files |

---

## Project Structure

```
/home/luke/LukisBombasticIFCviewer/
├── src/
│   ├── BombasticIFC.Domain/          # Entities, ValueObjects, Enums, Repository interfaces
│   ├── BombasticIFC.Application/     # CQRS commands/queries, DTOs, service interfaces
│   ├── BombasticIFC.Infrastructure/  # EF Core, repositories, ConversionWorker, IfcConversionService
│   ├── BombasticIFC.API/             # Controllers (Auth, Models, Conversions), Program.cs
│   └── BombasticIFC.Shared/          # AppConstants
├── frontend/
│   └── src/
│       ├── api/          # auth.ts, models.ts, conversions.ts, client.ts
│       ├── components/   # AppHeader, LoginForm, RegisterForm, ModelCard, ModelTable, Toast, ToastContainer
│       ├── composables/  # useToasts, useModelPolling, useModelOperations
│       ├── stores/       # auth.ts (Pinia)
│       ├── types/        # models.ts, auth.ts
│       └── views/        # HomeView, LoginView, RegisterView, UploadView, DashboardView, ViewerView
├── kubernetes/           # Kubernetes manifests (apply in order: namespace→secrets→configmap→PVs→postgres→api→frontend→ingress)
├── Doku/                 # Architecture docs (backend, frontend, cluster)
├── BastardAgentFromHell/ # CAMPS multi-agent system (this repo)
├── Dockerfile            # API multi-stage: dotnet build → node-builder → aspnet runtime
├── deploy.sh             # Full cluster redeploy
├── deployAPI.sh          # API-only rebuild + rollout (uses --no-cache)
└── update-frontend.sh    # Frontend-only rebuild + rollout
```

---

## Domain Model

| Entity | Key Fields | Notes |
|--------|-----------|-------|
| `User` | Id, Username, Email, Role, PasswordHash | Roles: Admin, User |
| `IfcModel` | Id, FileName, StoragePath, XktPath, Status, OwnerId | Status: Uploaded→Processing→Converted/Failed |
| `ConversionJob` | Id, ModelId, Status, Format, StartedAt, CompletedAt | Formats: XKT (only supported) |
| `ModelVersion` | Id, ModelId, VersionNumber | Version tracking |

---

## Key Services

### `IfcConversionService` (`src/BombasticIFC.Infrastructure/Services/IfcConversionService.cs`)
Converts `.ifc` → `.xkt` by shelling out to `xeokit-convert` CLI.
- Validates IFC header (ISO-10303-21, FILE_SCHEMA) before invoking the CLI
- 15-minute timeout per conversion
- Output written to `/data/storage/<guid>.xkt`
- Called by `ConversionWorker` (background hosted service)

### `ConversionWorker` (`src/BombasticIFC.Infrastructure/Services/ConversionWorker.cs`)
Background `IHostedService` that dequeues conversion jobs and calls `IfcConversionService`.

### `FileStorageService` (`src/BombasticIFC.Infrastructure/Services/FileStorageService.cs`)
Handles raw file storage to `/data/storage`.

---

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/auth/register` | Register new user |
| POST | `/api/auth/login` | Login → JWT |
| GET | `/api/auth/me` | Current user profile |
| GET | `/api/models` | List models (paginated) |
| GET | `/api/models/{id}` | Get model by ID |
| POST | `/api/models` | Upload IFC file |
| DELETE | `/api/models/{id}` | Delete model |
| POST | `/api/conversions` | Trigger conversion job |
| GET | `/api/conversions/{id}` | Get conversion job status |

---

## Docker Build — API Image (Dockerfile)

Three stages:
1. **build** — `dotnet/sdk:8.0` — restore & build
2. **node-builder** — `node:20-bookworm-slim` + `build-essential python3 make` — installs `@xeokit/xeokit-convert@1.3.1` globally, runs `npm rebuild`, creates `/usr/local/bin/xeokit-convert` wrapper script
3. **final** — `dotnet/aspnet:8.0` — installs Node.js 20 from NodeSource (ABI must match node-builder), copies compiled node_modules + wrapper from node-builder

**Critical constraint:** Node.js ABI in `final` stage **must** match `node-builder` (both Node 20, ABI 115). Mismatched ABI causes `.node` native addon load failures.

---

## Kubernetes Configuration

| Resource | Namespace | Replicas | Image |
|----------|-----------|----------|-------|
| postgres-deployment | bombasticifccluster | 1 | postgres:16-alpine |
| api-deployment | bombasticifccluster | 2 | bombasticifccluster-api:latest |
| frontend-deployment | bombasticifccluster | 1 | bombasticifccluster-frontend:latest |

Image pull policy: `Never` (local Minikube Docker daemon).

### Environment Variables (API pod)
| Variable | Source |
|----------|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Secret |
| `JwtSettings__Secret` | Secret |
| `StoragePath` | `/data/storage` |

---

## Deployment Workflow

```bash
# Full redeploy
./deploy.sh

# API only (most common during backend development)
eval $(minikube docker-env)
./deployAPI.sh          # builds with --no-cache, applies manifest, rolls out

# Frontend only
./update-frontend.sh
```

---

## Known Issues / Active Work

- **xeokit-convert btoa.node**: `@loaders.gl/polyfills` tries to import a native `btoa.node` addon that may not be compiled on all platforms. Node.js 20 provides `btoa` globally so the addon is unnecessary. Fix: patch `dist/index.js` in the Dockerfile `node-builder` stage to replace the missing native import.
- `Dockerfile` is currently modified (`M` in git status) — rebuild required after any Dockerfile change via `./deployAPI.sh`.

---

## Conventions to Follow

- C# follows Clean Architecture layer boundaries — no cross-layer leakage
- CQRS: all business logic through MediatR Commands/Queries
- No direct EF Core access outside `Infrastructure`
- Vue components: Composition API only, `<script setup>`
- Composables for all non-trivial state logic
- Kubernetes manifests applied in dependency order (see deployment workflow)
- All IFC→XKT conversion goes through `IfcConversionService` — never call `xeokit-convert` directly from controllers

---

*Generated 2026-04-15 — update when architecture changes significantly.*
