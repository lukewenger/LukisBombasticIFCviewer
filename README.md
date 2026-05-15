# BombasticIFC

A single-page application for managing and visualising IFC (Industry Foundation Classes) building models. Users authenticate, upload `.ifc` files, track conversion progress, and inspect 3D models interactively in the browser using the xeokit SDK.

---

## Tech stack

| Layer | Technology |
|---|---|
| Frontend | Vue 3 + TypeScript, Vite, Pinia, Vue Router, Tailwind CSS v4, vee-validate + Zod |
| 3D Viewer | xeokit-sdk v2.6, xeokit-convert v1.3.1 (IFC to XKT conversion) |
| Backend API | .NET 8 ASP.NET Core, Clean Architecture (5 layers) |
| ORM / CQRS | Entity Framework Core 8 (Npgsql), MediatR |
| Database | PostgreSQL 16 Alpine |
| Auth | JWT Bearer tokens, HS256, BCrypt (work factor 12) |
| Containerisation | Docker multi-stage builds — `Dockerfile` (API), `frontend/Dockerfile` (nginx) |
| Orchestration | Kubernetes manifests in `kubernetes/` — Minikube (dev), K3s (production path) |
| CI/CD | GitHub Actions — GHCR as container registry, self-hosted runner for deploy |

---

## Repository layout

```plaintext
BombasticIFCcluster.sln          .NET solution file
Dockerfile                        Multi-stage API image (sdk → publish → aspnet + node20 + xeokit-convert)
docker-compose.yml                Local dev stack: postgres + api + frontend
frontend/
  Dockerfile                      Multi-stage frontend image (node:20-alpine → nginx:alpine)
  nginx.conf                      nginx SPA config (try_files, /api proxy) — used in K8s
  nginx.compose.conf              nginx config for Docker Compose (proxies /api to api service)
  src/
    api/                          Axios client + per-feature API modules
    components/                   AppHeader, ModelCard, XeokitPointViewer, PointPickerPanel, EntityAttributesPanel
    composables/                  useModelPolling.ts
    router/index.ts               Vue Router with auth guards
    stores/auth.ts                Pinia auth store (JWT token management)
    types/models.ts               TypeScript DTOs
    views/                        DashboardView, UploadView, ViewerView, LoginView, RegisterView
kubernetes/
  namespace.yaml                  Namespace: bombasticifccluster
  secrets.yaml                    K8s Secrets — NOT committed (gitignored)
  configmap.yaml                  Non-sensitive environment variables
  persistent-volumes.yaml         hostPath PVs for postgres (10 Gi) and storage (50 Gi)
  postgres-deployment.yaml        PostgreSQL 16 + ClusterIP service + NodePort 30432
  api-deployment.yaml             .NET API, replicas=2, liveness/readiness probes at /health
  frontend-deployment.yaml        Vue/nginx, replicas=2, rolling update
  ingress.yaml                    nginx ingress: / → frontend, /api → api, /health → api
src/
  BombasticIFC.API/               Controllers (Auth, Models, Conversions), Program.cs, appsettings
  BombasticIFC.Application/       CQRS commands/queries (MediatR), DTOs, interfaces
  BombasticIFC.Domain/            Entities (User, IfcModel, ConversionJob, ModelVersion), value objects, enums
  BombasticIFC.Infrastructure/    EF Core DbContext, repositories, TokenService, FileStorageService
  BombasticIFC.Shared/            AppConstants (MaxFileSize = 500 MB, allowed extensions)
test/
  BombasticIFC.Tests/             xUnit test project
docker/
  patch-btoa.js                   Patches @loaders.gl/polyfills btoa.node native addon post-install
Doku/                             Extended documentation (architecture, cluster, exam export)
```

---

## Getting started (local dev)

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) with the Compose plugin
- Node.js 20 (required for xeokit-convert native addon ABI compatibility)
- .NET 8 SDK

### Docker Compose quickstart

All three services (PostgreSQL, API, frontend) start together. The frontend is served at `http://localhost` and the API is reachable at both `http://localhost/api` (via nginx proxy) and `http://localhost:5000` (direct).

```bash
cp .env.example .env        # edit POSTGRES_PASSWORD and JWT_SECRET before first use
docker compose up -d
```

To stop and remove volumes (clears the database):

```bash
docker compose down -v
```

### Running the backend only

```bash
cd src/BombasticIFC.API
dotnet run
# API available at http://localhost:5000
# Swagger UI at http://localhost:5000/swagger
```

### Running the frontend only

```bash
cd frontend
npm ci --legacy-peer-deps   # --legacy-peer-deps required by xeokit peer dependency constraints
npm run dev
# Dev server at http://localhost:5173
```

---

## Running tests

```bash
dotnet test
```

---

## Building Docker images

**API image** (from repository root):

```bash
docker build -t bombasticifccluster-api:latest .
```

**Frontend image**:

```bash
docker build -t bombasticifccluster-frontend:latest ./frontend
```

Both images use multi-stage builds. The API image includes a dedicated Node.js 20 stage to compile xeokit-convert native addons at the correct V8 ABI (ABI 115). The frontend image produces a static build served by nginx.

---

## Kubernetes deployment (Minikube)

### Prerequisites

- [Minikube](https://minikube.sigs.k8s.io/docs/start/)
- `kubectl` configured to target the Minikube cluster
- A self-hosted GitHub Actions runner installed on the VM (required for the automated deploy job)

### One-time VM setup

1. In the repository, go to **Settings → Actions → Runners → New self-hosted runner** and follow the Linux install instructions.
2. Authenticate the runner's Docker daemon with GHCR so it can pull images:

```bash
docker login ghcr.io -u <github-username> -p <PAT with read:packages scope>
```

### Manual deploy sequence

Apply manifests in dependency order. The `secrets.yaml` file must be created manually on the VM and is never committed — see [Environment variables / secrets](#environment-variables--secrets) below.

```bash
kubectl apply -f kubernetes/namespace.yaml
kubectl apply -f kubernetes/secrets.yaml          # created manually — never commit this file
kubectl apply -f kubernetes/configmap.yaml
kubectl apply -f kubernetes/persistent-volumes.yaml
kubectl apply -f kubernetes/postgres-deployment.yaml
kubectl apply -f kubernetes/api-deployment.yaml
kubectl apply -f kubernetes/frontend-deployment.yaml
kubectl apply -f kubernetes/ingress.yaml
```

### CI/CD automated deploy

The pipeline in `.github/workflows/ci.yml` runs four jobs on every push to `main`:

| Job | Runner | Trigger | What it does |
|---|---|---|---|
| `quality` | `ubuntu-latest` | push or PR to `main` | Restores, builds, and tests .NET; TypeScript type-check |
| `build` | `ubuntu-latest` | after `quality` passes | Builds both Docker images (no push) with GHA layer cache |
| `push` | `ubuntu-latest` | push to `main` only | Logs in to GHCR, rebuilds, pushes `latest` + `sha-<short>` tags |
| `deploy` | `self-hosted` | after `push` completes | Pulls images from GHCR, loads into Minikube, applies manifests, triggers rolling restart, waits for rollout |

The deploy job runs directly on the self-hosted runner where Minikube and `kubectl` already live — no inbound SSH connection is required.

---

## Environment variables / secrets

The API reads configuration from environment variables at runtime. In Docker Compose these come from `.env`; in Kubernetes they come from `kubernetes/secrets.yaml` (for sensitive values) and `kubernetes/configmap.yaml` (for non-sensitive values).

| Variable | Required | Description |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Yes | PostgreSQL connection string (Host, Port, Database, Username, Password) |
| `JwtSettings__Secret` | Yes | HS256 signing key for JWT tokens — use a long random value |
| `ASPNETCORE_ENVIRONMENT` | Yes | `Development` (Docker Compose) or `Production` (Kubernetes) |
| `StoragePath` | Yes | Filesystem path for uploaded and converted files (e.g. `/data/storage`) |

> **`kubernetes/secrets.yaml` is gitignored and must never be committed.** Create it manually on the VM containing the base64-encoded values for `connection-string` and `jwt-secret`. The `.env` file used by Docker Compose is also gitignored — use `.env.example` as the template.

---

## API reference

Base path: `/api`

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | None | Register a new user |
| POST | `/api/auth/login` | None | Login — returns a JWT |
| GET | `/api/auth/me` | JWT | Current user profile |
| GET | `/api/models` | JWT | List models (filtered to the authenticated user) |
| POST | `/api/models/upload` | JWT | Upload a `.ifc` file (max 500 MB) |
| GET | `/api/models/{id}` | JWT | Model details and associated conversion jobs |
| GET | `/api/models/{id}/output?format=XKT` | JWT | Download the converted output file |
| POST | `/api/conversions` | JWT | Create a conversion job |
| GET | `/api/conversions/{id}` | JWT | Get conversion job status and progress |
| GET | `/health` | None | Health check — used by K8s liveness and readiness probes |
| GET | `/swagger` | None | Swagger UI |

Tokens are JWT Bearer (HS256). Auth endpoints are rate-limited to 10 requests per minute. Access tokens expire after 60 minutes (development) or 120 minutes (production). Available roles: `User`, `Administrator`, `Viewer`.

---

## Architecture notes

- **Clean Architecture** — strict inward dependency rule: Domain has zero external dependencies. Layer order: Domain → Application → Infrastructure → API.
- **CQRS via MediatR** — commands (writes) and queries (reads) live in the Application layer; controllers dispatch via `IMediator` and do not contain business logic.
- **Repository pattern** — interfaces declared in Domain, implementations in Infrastructure. Soft delete is applied globally via EF Core query filters.
- **Result pattern** — all command/query handlers return `Result<T>`; controllers inspect success or failure before mapping to HTTP responses.
- **Error conventions** — 400 for validation failures, 401 for missing/invalid authentication, 403 for authorisation failures, 404 for missing resources, 409 for conflicts.
- **Health endpoint** — `GET /health` returns `{ "status": "healthy", "timestamp": "..." }` and is used by all three Kubernetes probes (startup, liveness, readiness) on port 8080.
- **Port conventions** — API container port 8080 (Kubernetes) / mapped to 5000 on the host in Docker Compose; frontend container port 80.
