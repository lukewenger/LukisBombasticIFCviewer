# Coolify Setup Guide — BombasticIFC

**Branch:** `coolify`  
**Last updated:** 2026-05-23  
**Coolify version:** latest (ghcr.io/coollabsio/coolify)

---

## Overview

Coolify is a self-hosted deployment platform that replaces the Kubernetes/GitHub Actions pipeline for environments where Minikube is not needed. It deploys the full BombasticIFC stack (PostgreSQL + API + Frontend) from the `coolify` branch via Docker Compose, with Traefik as the built-in reverse proxy.

```
GitHub (coolify branch)
        │
        ▼
   Coolify (port 8000)
        │  builds images from Dockerfiles
        ▼
┌────────────────────────────────────────┐
│  Docker Compose stack                  │
│                                        │
│  postgres:16-alpine  (internal)        │
│       ↓ healthy                        │
│  migrate (one-shot)  (internal)        │
│       ↓ exit 0                         │
│  api (.NET 8)        (internal :8080)  │
│       ↓ healthy                        │
│  frontend (nginx)    (host :8090)      │
└────────────────────────────────────────┘
        │
        ▼
  http://<VM-IP>:8090
```

---

## Prerequisites

| Requirement | Notes |
|---|---|
| Ubuntu/Debian VM | Tested on Ubuntu 24.04 |
| Docker ≥ 24 | `docker --version` |
| Docker Compose v2 | `docker compose version` |
| Port 8000 open | Coolify UI |
| Port 8090 open | Frontend (configurable) |
| GitHub repository | With `coolify` branch |

---

## Step 1 — Install Coolify

If Coolify is not yet installed, run the official one-liner:

```bash
curl -fsSL https://cdn.coollabs.io/coolify/install.sh | bash
```

This pulls and starts the following containers:
- `coolify` — main app (PHP/Laravel), accessible on port **8000**
- `coolify-proxy` — Traefik reverse proxy (ports 80, 443)
- `coolify-db` — internal PostgreSQL for Coolify's own data
- `coolify-redis` — internal Redis
- `coolify-sentinel` — resource monitoring
- `coolify-realtime` — WebSocket server

Verify all containers are healthy:

```bash
docker ps --filter "name=coolify"
```

Open the UI at `http://<VM-IP>:8000` and complete the initial account setup (admin email + password).

---

## Step 2 — Connect the GitHub Repository

1. In Coolify UI → **Sources** → **Add** → **GitHub App** (or GitHub Personal Access Token)
2. Follow the OAuth flow to authorise Coolify to access your repository
3. After authorisation, Coolify can read branches and trigger deployments on push

> **Note:** If using a Personal Access Token (PAT), give it `repo` and `read:packages` scopes.

---

## Step 3 — Create a New Project

1. Coolify UI → **Projects** → **New Project**
2. Name it (e.g. `BombasticIFC`)
3. Inside the project → **New Resource** → **Docker Compose**

---

## Step 4 — Configure the Docker Compose Resource

| Field | Value |
|---|---|
| **Source** | Your GitHub repository |
| **Branch** | `coolify` |
| **Docker Compose file** | `docker-compose.coolify.yml` |
| **Base directory** | `/` (repository root) |

Leave all other fields at their defaults. Coolify will automatically parse the compose file and detect the three services (postgres, api, frontend).

---

## Step 5 — Set Environment Variables

Go to the resource → **Environment Variables** tab. Add the following two variables:

| Variable | Example value | Notes |
|---|---|---|
| `DB_PASSWORD` | `xywJlJORVKNTuM57RS9WwQfqsEguzF` | Password for PostgreSQL `postgres` user |
| `JWT_SECRET` | `rzKYDGFR4CXax+Pd...` | Signing key for JWT access tokens (≥ 32 chars) |

Generate strong values:

```bash
# DB_PASSWORD (alphanumeric, no special chars to avoid .env escaping issues)
openssl rand -base64 24 | tr -d '/+='

# JWT_SECRET
openssl rand -base64 48
```

> **Important:** Coolify injects ALL environment variables into every container at runtime.
> The variable substitution in the compose file (`${DB_PASSWORD}`) works for PostgreSQL
> because Docker Compose processes it via `--env-file`.
> `JWT_SECRET` is read directly by the API at runtime (see §8 for details).

---

## Step 6 — Configure the Frontend Domain / Port

In the Coolify resource, the **frontend** service is exposed on host port **8090**.  
This is set in `docker-compose.coolify.yml`:

```yaml
frontend:
  ports:
    - "8090:80"
```

If you want a public domain with HTTPS instead, Coolify can manage TLS via Traefik:

1. In the resource → **frontend** service → **Domains**
2. Add your domain (e.g. `ifc.yourdomain.com`)
3. Enable **HTTPS** — Coolify provisions a Let's Encrypt certificate automatically
4. Remove the `ports: - "8090:80"` line from the compose and let Traefik route traffic

---

## Step 7 — Deploy

Click **Deploy** in the Coolify UI.

Coolify will:
1. Pull the `coolify` branch from GitHub
2. Build Docker images for `api` and `frontend` from their Dockerfiles
3. Start the services in order:

```
postgres  →  (healthcheck: pg_isready)  →  healthy ✅
migrate   →  dotnet --migrate            →  exit 0  ✅
api       →  ASP.NET Core starts         →  healthy ✅
frontend  →  nginx starts                →  running ✅
```

The full deployment takes **3–5 minutes** on first run (Docker build + migration).

---

## Step 8 — Verify

```bash
# Check all containers are running
docker ps --filter "name=fwhpn26"   # replace UUID with your Coolify resource UUID

# Check API health endpoint
curl http://localhost:8090/api/health

# Check frontend is served
curl -I http://localhost:8090/
```

Access the application at `http://<VM-IP>:8090`.

---

## Automatic Deployments (optional)

To trigger a redeploy on every push to the `coolify` branch:

1. Coolify UI → resource → **Webhooks**
2. Copy the webhook URL
3. GitHub → repository → **Settings** → **Webhooks** → **Add webhook**
4. Paste the URL, set content type to `application/json`, select "Push" events

---

## File Changes Made to the Repository

The following files were added or modified to make Coolify work alongside the existing Kubernetes setup:

### New files

| File | Purpose |
|---|---|
| `docker-compose.coolify.yml` | Full stack definition for Coolify (postgres + migrate + api + frontend) |
| `frontend/docker-entrypoint.sh` | Runs `envsubst` to inject `API_UPSTREAM` into nginx config at container start |

### Modified files

| File | Change | Reason |
|---|---|---|
| `frontend/nginx.conf` | Replaced hardcoded K8s DNS with `${API_UPSTREAM}` | Same image now works in both K8s and Docker Compose |
| `frontend/Dockerfile` | Copies nginx.conf as a template; adds custom entrypoint; sets default `ENV API_UPSTREAM=http://api:8080` | Enables runtime-configurable API upstream |
| `kubernetes/frontend-deployment.yaml` | Added `API_UPSTREAM=http://api-service.bombasticifccluster.svc.cluster.local` env var | Required now that nginx.conf is a template |
| `src/BombasticIFC.API/Program.cs` | JWT secret reads `JwtSettings__Secret` first, falls back to `JWT_SECRET` | Coolify injects `JWT_SECRET` directly; compose `${JWT_SECRET}` substitution is unreliable |

---

## Troubleshooting

### PostgreSQL container is unhealthy — `superuser password is not specified`

**Cause:** `DB_PASSWORD` is empty in Coolify's environment variables.  
**Fix:** Go to Coolify → resource → Environment Variables → set a non-empty value for `DB_PASSWORD`, save, and redeploy.

---

### API crashes with `relation "IfcModels" does not exist`

**Cause:** EF Core migrations have not run — the database schema is empty.  
**Fix:** The `migrate` service in the compose file handles this automatically. If the migrate service fails, check:

```bash
docker logs migrate-<uuid>
```

The connection string must be correct (check `DB_PASSWORD` matches what PostgreSQL was initialised with).

---

### API container is unhealthy — `/bin/sh: wget: not found`

**Cause:** `wget` is not installed in the `mcr.microsoft.com/dotnet/aspnet:8.0` image (curl is also purged during the Node.js install step in the Dockerfile).  
**Fix:** The healthcheck uses bash's built-in TCP socket — no external tools needed:

```yaml
healthcheck:
  test: ["CMD-SHELL", "bash -c '</dev/tcp/localhost/8080'"]
```

---

### JWT secret not picked up — `JWT Secret is not configured`

**Cause:** `${JWT_SECRET}` compose substitution produced an empty string, and `JwtSettings__Secret` was set to empty.  
**Fix:** `Program.cs` now falls back to the `JWT_SECRET` environment variable directly (which Coolify injects at runtime). Ensure `JWT_SECRET` is set in Coolify's environment variables panel.

---

### Frontend shows "page not found" / can't connect on port 8090

**Cause:** Either nothing is listening on 8090, or the frontend container started before the API was healthy.  
**Fix:**
1. Check containers are running: `docker ps | grep frontend`
2. Check that the compose has `ports: - "8090:80"` for the frontend service
3. If you need to expose it immediately without redeploying:

```bash
# Find the current random host port
docker ps --filter "name=frontend" --format "{{.Ports}}"

# Forward 8090 → that port (temporary, until next redeploy)
nohup socat TCP-LISTEN:8090,fork,reuseaddr TCP:localhost:<random-port> \
  > /tmp/socat-frontend.log 2>&1 &
```

---

## Architecture Notes

### Why a separate `migrate` service?

The K8s setup uses an `initContainer` to run `dotnet BombasticIFC.API.dll --migrate` before the API pod starts. Docker Compose does not have initContainers, so the equivalent is a `restart: "no"` service with `service_completed_successfully` as a dependency condition. The API will only start after migrations exit 0.

### Why is `JwtSettings__Secret` not in the compose environment?

Coolify uses `--env-file` for Docker Compose variable substitution. Depending on timing and Coolify's internal behaviour, `${JWT_SECRET}` can resolve to an empty string in the substituted compose even when the variable is set in the UI. To avoid the API starting with an empty JWT secret, the compose no longer sets `JwtSettings__Secret`. Instead, `Program.cs` falls back to `builder.Configuration["JWT_SECRET"]`, which Coolify always injects as a runtime environment variable.

### Why does the frontend use `envsubst`?

The same Docker image is deployed in both Kubernetes and Coolify:
- In **Kubernetes**, the API upstream is `http://api-service.bombasticifccluster.svc.cluster.local`
- In **Coolify/Docker Compose**, the API upstream is `http://api:8080` (Docker DNS)

Rather than hardcoding one value, `nginx.conf` uses `${API_UPSTREAM}` which `docker-entrypoint.sh` replaces at container startup via `envsubst`. Only `${API_UPSTREAM}` is substituted — all other nginx variables (`$host`, `$uri`, etc.) are left untouched.
