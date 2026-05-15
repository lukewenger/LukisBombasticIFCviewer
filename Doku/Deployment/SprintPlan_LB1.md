# Sprint Plan — LB1 Deployment Module
**Project:** BombasticIFC
**Module:** HFI_DEP
**Sprint:** 2026-05-15 (Thu) → 2026-05-17 (Sun) 23:59
**Deadline:** Sunday 2026-05-17, 23:59 (Teams submission)

---

## Sprint Goal

Satisfy criteria C2 (CI/CD with GitHub Actions), C3 (Cloud PaaS — Railway) and C4 (Kubernetes) fully from the BombasticIFC repository, using the existing codebase as the basis.

C1 (Docker Compose) is already fully done — `docker-compose.yml` with 3 services, env-vars, healthchecks, named volumes, two networks, multi-stage Dockerfiles.

---

## Criteria Summary (from LB1 index.html)

### C1 — Docker Compose (10 pts, due 17.05.2026)
Multi-service app with `docker compose up`. Must have:
- Minimum 3 services, at least one with own Dockerfile + multi-stage build
- Configuration via env vars with `.env.example` in repo, `.env` in gitignore
- At least one named volume for persistent data
- At least one healthcheck + `depends_on: condition: service_healthy`
- At least one custom network
- Without downtime restart
- Structured logs on stdout

**Status:** ✅ COMPLETE. `docker-compose.yml` has postgres + api + frontend (3 services). Multi-stage Dockerfiles for api and frontend. `.env.example` created. Named volumes `postgres-data` and `storage-data`. Healthchecks with depends_on. Two networks (`frontend-net`, `backend-net`).

### C2 — CI/CD with GitHub Actions (10 pts, due 17.05.2026)
Pipeline on push to main: Build → Test → Push image to registry. Must have:
- Three stages: Build, Test, Push
- Two image tags: `latest` + `sha-<short-sha>`
- Secrets in GitHub repository secrets, not in code
- Docker layer caching + dependency caching
- Push stage only on main branch merge

**Status:** ✅ CODE DONE. `.github/workflows/ci.yml` created with quality → build → push jobs, GHA Docker layer caching, NuGet + npm caching, sha + latest tags, GITHUB_TOKEN (no manual secret needed).
**Remaining:** Push to GitHub and verify pipeline goes green (student task).

### C3 — Cloud Platform Deployment (10 pts, due 17.05.2026)
Deploy to a public platform (Railway / Render / Fly.io / Coolify). Must have:
- Public HTTPS URL
- Config via env vars (platform dashboard)
- .env.example in repo, no hardcoded secrets
- At least one persistent data store
- Reproducible deployment (git push or CLI command)
- Structured logs visible in platform interface

**Status:** ✅ CODE DONE. `railway.toml` created (config as code). `C3_Cloud_Dokumentation.md` written. Platform: Railway (API + Postgres Plugin).
**Remaining:** Create Railway account, connect GitHub repo, add Postgres plugin, set env vars (student task — ~10 min setup).

### C4 — Kubernetes (10 pts, due 17.05.2026)
Kubernetes deployment with manifests in git. Must have:
- Deployment with at least 2 replicas
- Service exposing the app
- ConfigMap for config, Secret for credentials
- Resource Requests + Limits
- Liveness + Readiness Probes
- Rolling update demonstrable
- Reproducible from git (kubectl apply)

**Status:** ✅ COMPLETE. All manifests exist in `kubernetes/`. 2 replicas (api + frontend). Liveness/Readiness/Startup probes at `/health:8080`. CPU + Memory requests/limits. Rolling update (maxUnavailable=0, maxSurge=1). ConfigMap + Secret reference. `secrets.yaml` gitignored, `secrets.yaml.template` in repo for structure documentation.
**Remaining:** Run Minikube/K3s, apply manifests, demonstrate rolling update (student task).

---

## Task Board

| # | Task | Criterion | Owner | Status | Deadline |
|---|---|---|---|---|---|
| T01 | Fix docker-compose.yml — replace hardcoded password with env vars | C1 | Claude | ✅ DONE | 15.05 |
| T02 | Create .env.example template file | C1 | Claude | ✅ DONE | 15.05 |
| T03 | Update .gitignore — add secrets.yaml, obj/, bin/, .env, .DS_Store, .idea, *.bak | C1/C2/C4 | Claude | ✅ DONE | 15.05 |
| T04 | Create .github/workflows/ci.yml — quality (dotnet test + vue-tsc) → build → push (GHCR) | C2 | Claude | ✅ DONE | 15.05 |
| T05 | Fill PROJECT_CONTEXT.md with concrete project facts | Internal | Claude | ✅ DONE | 15.05 |
| T06 | Verify kubernetes/api-deployment.yaml has liveness + readiness probes at /health | C4 | Claude | ✅ DONE (verified) | 15.05 |
| T07 | Verify /health endpoint exists in Program.cs | C4 | Claude | ✅ DONE (verified, line 134) | 15.05 |
| T08 | Create C1 documentation (4 pages max) | C1 | Claude | ✅ DONE | 15.05 |
| T09 | Create C2 documentation (4 pages max) | C2 | Claude | ✅ DONE | 15.05 |
| T10 | Create C4 documentation (4 pages max) | C4 | Claude | ✅ DONE | 15.05 |
| T11 | Fix .gitignore depth bug — change */obj/* to **/obj/ so obj/ at any depth is ignored | C1/C2/C4 | Claude | ✅ DONE | 15.05 |
| T12 | Create railway.toml — config-as-code for reproducible Railway deployment | C3 | Claude | ✅ DONE | 15.05 |
| T13 | Create C3_Cloud_Dokumentation.md — full C3 documentation (Railway, German) | C3 | Claude | ✅ DONE | 15.05 |
| T14 | Set up GitHub Actions secrets: verify GITHUB_TOKEN has packages:write permission | C2 | Student | TODO | 16.05 |
| T15 | Push to main and verify CI pipeline goes green in GitHub Actions tab | C2 | Student | TODO | 16.05 |
| T16 | Create Railway account → connect GitHub repo → add Postgres Plugin → set env vars → verify /health | C3 | Student | TODO | 16.05 |
| T17 | Verify Railway deployment: public URL returns 200 on /health, logs visible in dashboard | C3 | Student | TODO | 16.05 |
| T18 | Deploy to Minikube / K3s, verify rolling update (kubectl rollout restart) | C4 | Student | TODO | 17.05 |
| T19 | Record C1 screencast (2-5 min: docker compose up, browser, code change cycle) | C1 | Student | TODO | 17.05 |
| T20 | Record C2 screencast (2-5 min: push → GitHub Actions UI → image in GHCR packages) | C2 | Student | TODO | 17.05 |
| T21 | Record C3 screencast (2-5 min: railway up / git push → deployment UI → public URL) | C3 | Student | TODO | 17.05 |
| T22 | Record C4 screencast (2-5 min: kubectl apply, pods running, rolling update) | C4 | Student | TODO | 17.05 |
| T23 | Export/print docs as PDF and submit via Teams with git repo links | ALL | Student | TODO | 17.05 |

---

## What Claude has implemented (files created/modified)

| File | Change | Criterion |
|---|---|---|
| `.claude/PROJECT_CONTEXT.md` | Filled with full project facts | Internal |
| `.gitignore` | Fixed: `*/obj/*` → `**/obj/`, `*/bin/*` → `**/bin/`, added `*.bak` | C1/C2/C4 |
| `docker-compose.yml` | Replaced hardcoded password; env-var pattern; healthcheck; custom networks | C1 |
| `.env.example` | Template with POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB, JWT_SECRET, CONNECTION_STRING | C1 |
| `frontend/nginx.compose.conf` | Docker-Compose-specific nginx config (proxies /api to 'api' service name) | C1 |
| `.github/workflows/ci.yml` | Full 3-job pipeline: quality → build → push (GHCR, main only), NuGet/npm/Docker caching, sha+latest tags | C2 |
| `railway.toml` | Railway config-as-code: Dockerfile builder, /health healthcheck, restart policy | C3 |
| `Doku/Deployment/C1_Docker_Compose_Dokumentation.md` | Full C1 documentation in German | C1 |
| `Doku/Deployment/C2_CICD_Dokumentation.md` | Full C2 documentation in German | C2 |
| `Doku/Deployment/C3_Cloud_Dokumentation.md` | Full C3 documentation in German (Railway) | C3 |
| `Doku/Deployment/C4_Kubernetes_Dokumentation.md` | Full C4 documentation in German | C4 |
| `kubernetes/secrets.yaml.template` | Template showing secret structure (no real values) | C4 |
| `Doku/Deployment/SprintPlan_LB1.md` | This file | All |

---

## What is verified correct in the repo

| Item | Location | Criterion |
|---|---|---|
| Multi-stage API Dockerfile (sdk → aspnet + node20) | `Dockerfile` | C1/C2 |
| Multi-stage Frontend Dockerfile (node:20-alpine → nginx) | `frontend/Dockerfile` | C1/C2 |
| 3 services in docker-compose.yml (postgres, api, frontend) | `docker-compose.yml` | C1 |
| Named volumes (postgres-data, storage-data) | `docker-compose.yml` | C1 |
| Healthcheck + depends_on: service_healthy | `docker-compose.yml` | C1 |
| Two custom networks (frontend-net, backend-net) | `docker-compose.yml` | C1 |
| 3-job CI pipeline (quality → build → push) | `.github/workflows/ci.yml` | C2 |
| Two image tags (sha-<short> + latest) | `.github/workflows/ci.yml` | C2 |
| Docker layer caching (GHA cache) | `.github/workflows/ci.yml` | C2 |
| NuGet + npm caching | `.github/workflows/ci.yml` | C2 |
| Push only on main (not PRs) | `.github/workflows/ci.yml` | C2 |
| railway.toml (config as code) | `railway.toml` | C3 |
| /health endpoint as Railway healthcheck | `railway.toml` | C3 |
| 2 API replicas | `kubernetes/api-deployment.yaml:9` | C4 |
| Liveness probe at `/health:8080` | `kubernetes/api-deployment.yaml` | C4 |
| Readiness probe at `/health:8080` | `kubernetes/api-deployment.yaml` | C4 |
| Startup probe at `/health:8080` | `kubernetes/api-deployment.yaml` | C4 |
| CPU + Memory resource requests/limits | `kubernetes/api-deployment.yaml` | C4 |
| Rolling update strategy (maxUnavailable=0, maxSurge=1) | `kubernetes/api-deployment.yaml` | C4 |
| ConfigMap | `kubernetes/configmap.yaml` | C4 |
| Kubernetes Secret reference (gitignored) | `kubernetes/api-deployment.yaml` | C4 |
| `/health` endpoint in Program.cs | `src/BombasticIFC.API/Program.cs:134` | C4 |

---

## Blockers / Manual Actions Required (Student To-Do)

### BLOCKER 1 — C2: Push to GitHub and verify green pipeline
```bash
git add .github/workflows/ci.yml .gitignore docker-compose.yml .env.example \
        frontend/nginx.compose.conf railway.toml kubernetes/secrets.yaml.template \
        Doku/Deployment/
git commit -m "ci: add GitHub Actions CI/CD pipeline and deployment docs"
git push origin main
# Then: GitHub → Actions tab → verify all 3 jobs pass
```

### BLOCKER 2 — C3: Railway deployment (~10 min)
1. Go to https://railway.app → "New Project" → "Deploy from GitHub repo"
2. Select this repository → Railway detects `railway.toml` automatically
3. Click "+ New" → "Database" → "Add PostgreSQL" (adds managed Postgres)
4. In the api service → "Variables" tab, add:
   - `ConnectionStrings__DefaultConnection` = `${{Postgres.DATABASE_URL}}`
   - `JWT_SECRET` = *(generate with `openssl rand -base64 32`)*
   - `ASPNETCORE_ENVIRONMENT` = `Production`
   - `JwtSettings__Issuer` = `BombasticIFC`
   - `JwtSettings__Audience` = `BombasticIFC-Client`
   - `JwtSettings__ExpirationMinutes` = `120`
   - `StoragePath` = `/data/storage`
5. Wait for deployment to finish → check "Settings" → "Domains" for public URL
6. `curl https://<your-url>.up.railway.app/health` → should return `{"status":"healthy",...}`

### BLOCKER 3 — C4: Minikube demo
```bash
minikube start --cpus=2 --memory=4096 --disk-size=50g --driver=docker
minikube addons enable ingress
eval $(minikube docker-env)
docker build -t bombasticifccluster-api:latest .
docker build -t bombasticifccluster-frontend:latest ./frontend
minikube ssh "sudo mkdir -p /mnt/data/postgres /mnt/data/storage && sudo chmod -R 777 /mnt/data"
kubectl create namespace bombasticifccluster 2>/dev/null || true
kubectl create secret generic bombasticifccluster-secrets \
  --namespace=bombasticifccluster \
  --from-literal=postgres-user=postgres \
  --from-literal=postgres-password=$(openssl rand -base64 16) \
  --from-literal=postgres-db=bombasticifcdb \
  --from-literal=jwt-secret=$(openssl rand -base64 32) \
  --from-literal=connection-string="Host=postgres-service;Port=5432;Database=bombasticifcdb;Username=postgres;Password=secret"
kubectl apply -f kubernetes/
kubectl get pods -n bombasticifccluster -w
# Rolling update demo:
kubectl rollout restart deployment/api-deployment -n bombasticifccluster
kubectl rollout status deployment/api-deployment -n bombasticifccluster
```

### BLOCKER 4 — Screencasts (T19–T22)
All criteria require a 2-5 minute screencast or live demo. Suggested tools: OBS Studio, Loom, or QuickTime.

### BLOCKER 5 — Documentation PDFs (T23)
Export the `.md` files from `Doku/Deployment/` as PDFs (Pandoc, VS Code PDF export, or Typora). Max 4 pages each.

---

## Submission Checklist (Teams, due 17.05.2026 23:59)

### C1 — Docker Compose
- [ ] Git repo link (with docker-compose.yml + .env.example)
- [ ] Documentation PDF: `Doku/Deployment/C1_Docker_Compose_Dokumentation.md`
- [ ] Screencast link

### C2 — CI/CD GitHub Actions
- [ ] Git repo link (with .github/workflows/ci.yml)
- [ ] Documentation PDF: `Doku/Deployment/C2_CICD_Dokumentation.md`
- [ ] Screencast link (push → Actions UI → GHCR packages)

### C3 — Cloud Platform Deployment (Railway)
- [ ] Git repo link (with railway.toml)
- [ ] Documentation PDF: `Doku/Deployment/C3_Cloud_Dokumentation.md`
- [ ] Public URL (https://\<projektname\>.up.railway.app)
- [ ] Screencast link (railway up / git push → deployment UI → public URL)

### C4 — Kubernetes
- [ ] Git repo link (with kubernetes/ manifests)
- [ ] Documentation PDF: `Doku/Deployment/C4_Kubernetes_Dokumentation.md`
- [ ] Screencast link (kubectl apply, pods running, rolling update)