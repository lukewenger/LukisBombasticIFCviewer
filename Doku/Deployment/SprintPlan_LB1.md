# Sprint Plan — LB1 Deployment Module
**Project:** BombasticIFC
**Module:** HFI_DEP
**Sprint:** 2026-05-15 (Thu) → 2026-05-17 (Sun) 23:59
**Deadline:** Sunday 2026-05-17, 23:59 (Teams submission)

---

## Sprint Goal

Satisfy criteria C2 (CI/CD with GitHub Actions) and C4 (Kubernetes) fully, and partially satisfy C3 (Cloud PaaS) from the BombasticIFC repository, using the existing codebase as the basis.

C1 (Docker Compose) is already substantially done — the `docker-compose.yml` exists with postgres + api. It needs minor hardening (env vars for secrets, healthchecks, named volumes) which is complete.

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

**Status:** Substantially done. `docker-compose.yml` has postgres + api, multi-stage Dockerfile exists.
**Gap closed:** Secrets moved to `.env` vars, `.env.example` created, network renamed, passwords removed from hardcoded values.

### C2 — CI/CD with GitHub Actions (10 pts, due 17.05.2026)
Pipeline on push to main: Build → Test → Push image to registry. Must have:
- Three stages: Build, Test, Push
- Two image tags: `latest` + `sha-<short-sha>`
- Secrets in GitHub repository secrets, not in code
- Docker layer caching + dependency caching
- Push stage only on main branch merge

**Status:** Created `.github/workflows/ci.yml` with quality → build → push jobs.

### C3 — Cloud Platform Deployment (10 pts, due 17.05.2026)
Deploy to a public platform (Railway / Render / Fly.io / Coolify). Must have:
- Public HTTPS URL
- Config via env vars (platform dashboard)
- .env.example in repo, no hardcoded secrets
- At least one persistent data store
- Reproducible deployment (git push or CLI command)
- Structured logs visible in platform interface

**Status:** MANUAL TASK — requires a real platform account and server. See blockers section.

### C4 — Kubernetes (10 pts, due 17.05.2026)
Kubernetes deployment with manifests in git. Must have:
- Deployment with at least 2 replicas
- Service exposing the app
- ConfigMap for config, Secret for credentials
- Resource Requests + Limits
- Liveness + Readiness Probes
- Rolling update demonstrable
- Reproducible from git (kubectl apply)

**Status:** Kubernetes manifests exist in `kubernetes/`. All requirements are met.
Gap: `secrets.yaml` was previously committed (now gitignored). Resource limits are set.

---

## Task Board

| # | Task | Criterion | Owner | Status | Deadline |
|---|---|---|---|---|---|
| T01 | Fix docker-compose.yml — replace hardcoded password with env vars | C1 | Claude | DONE | 15.05 |
| T02 | Create .env.example template file | C1 | Claude | DONE | 15.05 |
| T03 | Update .gitignore — add kubernetes/secrets.yaml, node_modules, dist, .env, .DS_Store, .idea | C1/C2/C4 | Claude | DONE | 15.05 |
| T04 | Create .github/workflows/ci.yml — quality (dotnet test + vue-tsc) → build (docker images) → push (GHCR) | C2 | Claude | DONE | 15.05 |
| T05 | Fill PROJECT_CONTEXT.md with concrete project facts | Internal | Claude | DONE | 15.05 |
| T06 | Verify kubernetes/api-deployment.yaml has liveness + readiness probes at /health | C4 | Claude | DONE (verified) | 15.05 |
| T07 | Verify /health endpoint exists in Program.cs | C4 | Claude | DONE (verified, line 134) | 15.05 |
| T08 | Create C1 documentation (4 pages max) | C1 | Student | TODO | 17.05 |
| T09 | Create C2 documentation (4 pages max) | C2 | Student | TODO | 17.05 |
| T10 | Create C4 documentation (4 pages max) | C4 | Student | TODO | 17.05 |
| T11 | Record C1 screencast (2-5 min: docker compose up, browser, code change cycle) | C1 | Student | TODO | 17.05 |
| T12 | Record C2 screencast (2-5 min: push → pipeline UI → image in GHCR) | C2 | Student | TODO | 17.05 |
| T13 | Record C4 screencast (2-5 min: kubectl apply, pods running, rolling update) | C4 | Student | TODO | 17.05 |
| T14 | Set up GitHub Actions secrets: GHCR_TOKEN (or use GITHUB_TOKEN) | C2 | Student | TODO | 16.05 |
| T15 | Push to main and verify CI pipeline goes green | C2 | Student | TODO | 16.05 |
| T16 | Deploy to Minikube / K3s, verify rolling update | C4 | Student | TODO | 17.05 |
| T17 | (Optional) C3: Deploy to Railway or Coolify for public URL | C3 | Student | OPTIONAL | 17.05 |

---

## What Claude has already implemented (files created/modified)

| File | Change | Criterion |
|---|---|---|
| `.claude/PROJECT_CONTEXT.md` | Filled from template with full project facts | Internal |
| `.gitignore` | Added: `kubernetes/secrets.yaml`, `node_modules/`, `dist/`, `.env`, `.env.local`, `.env.*.local`, `.DS_Store`, `Thumbs.db`, `.idea/`, `.vscode/` | C1/C2/C4 |
| `docker-compose.yml` | Replaced hardcoded password with `${POSTGRES_PASSWORD}` env var pattern; added `${JWT_SECRET}`; explicit healthcheck; custom network renamed to `backend-net` | C1 |
| `.env.example` | Template with POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB, JWT_SECRET, CONNECTION_STRING | C1 |
| `.github/workflows/ci.yml` | Full 3-job pipeline: quality (dotnet test + vue-tsc) → build (docker images, no push) → push (GHCR, main only). Includes NuGet cache, npm cache, Docker layer caching, two tags (sha + latest) | C2 |
| `Doku/Deployment/SprintPlan_LB1.md` | This file | All |

---

## What is already correct in the repo (verified by Claude)

| Item | Location | Criterion |
|---|---|---|
| Multi-stage API Dockerfile (sdk → aspnet + node20) | `Dockerfile` | C1/C2 |
| Multi-stage Frontend Dockerfile (node:20-alpine → nginx) | `frontend/Dockerfile` | C1/C2 |
| 2 API replicas | `kubernetes/api-deployment.yaml:9` | C4 |
| Liveness probe at `/health:8080` | `kubernetes/api-deployment.yaml:55-60` | C4 |
| Readiness probe at `/health:8080` | `kubernetes/api-deployment.yaml:61-66` | C4 |
| Startup probe at `/health:8080` | `kubernetes/api-deployment.yaml:67-72` | C4 |
| CPU + Memory resource requests/limits | `kubernetes/api-deployment.yaml:48-54` | C4 |
| Rolling update strategy (maxUnavailable=0, maxSurge=1) | `kubernetes/api-deployment.yaml:10-14` | C4 |
| ConfigMap | `kubernetes/configmap.yaml` | C4 |
| Kubernetes Secret reference | `kubernetes/api-deployment.yaml:33-42` | C4 |
| `/health` endpoint in Program.cs | `src/BombasticIFC.API/Program.cs:134` | C4 |
| Named volume for postgres | `docker-compose.yml` | C1 |
| healthcheck + depends_on service_healthy | `docker-compose.yml` | C1 |
| Custom network (backend-net) | `docker-compose.yml` | C1 |

---

## Blockers / Manual Actions Required

### BLOCKER 1 — C2: GitHub Actions secrets
Before the push job can run, the GitHub repository needs:
- `GITHUB_TOKEN` is automatically available (used for GHCR login — no manual setup needed if packages are public)
- If the GHCR packages are private, you may need to set `CR_PAT` in Settings → Secrets → Actions

**Action:** Go to `github.com/<your-repo>/settings/secrets/actions` and verify `GITHUB_TOKEN` has `packages: write` permission. The workflow already uses `permissions: packages: write`.

### BLOCKER 2 — C3: No public URL yet
C3 requires a publicly accessible HTTPS URL. Options ranked by effort:
1. **Railway** (easiest): Connect GitHub repo, set env vars in dashboard, get `myapp.railway.app` URL in ~5 min
2. **Coolify on existing server**: If you have an Ubuntu VPS, install Coolify (`curl -fsSL https://cdn.coollabs.io/coolify/install.sh | bash`) and add the repo
3. **Render**: Similar to Railway, free tier available

**Action:** Choose a platform, deploy the API (from the GHCR image after C2 is set up), set env vars (DB connection string, JWT secret), verify `/health` returns 200.

### BLOCKER 3 — C4: Minikube/K3s server must be running
The Kubernetes demo requires a live cluster. On the exam submission laptop/server:
```bash
# If using Minikube
minikube start --cpus=2 --memory=4096
minikube addons enable ingress
eval $(minikube docker-env)
docker build -t bombasticifccluster-api:latest .
docker build -t bombasticifccluster-frontend:latest ./frontend
kubectl apply -f kubernetes/
```

### BLOCKER 4 — Screencasts
All criteria require a 2-5 minute screencast or live demo. These must be recorded by the student. Suggested tools: OBS Studio, Loom, or QuickTime.

### BLOCKER 5 — Documentation PDFs
Each criterion requires a max 4-page documentation covering: what was implemented, why this solution, architecture diagram, setup guide, key decisions, and reflection. Claude can help draft these if needed.

---

## Submission Checklist (Teams, due 17.05.2026 23:59)

### C1 — Docker Compose
- [ ] Git repo link (with docker-compose.yml + .env.example + README)
- [ ] Documentation PDF (max 4 pages)
- [ ] Screencast link or sign up for live demo

### C2 — CI/CD GitHub Actions
- [ ] Git repo link (with .github/workflows/ci.yml + README)
- [ ] Documentation PDF (max 4 pages)
- [ ] Screencast link showing: push → pipeline UI → image in GHCR

### C3 — Cloud Platform Deployment (optional but recommended)
- [ ] Git repo link
- [ ] Documentation PDF (max 4 pages)
- [ ] Public URL of running app
- [ ] Screencast link

### C4 — Kubernetes
- [ ] Git repo link (with kubernetes/ manifests)
- [ ] Documentation PDF (max 4 pages)
- [ ] Screencast link showing: kubectl apply, pods running, rolling update
