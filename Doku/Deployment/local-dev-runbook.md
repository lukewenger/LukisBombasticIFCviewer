# Local Development Runbook

How to build, deploy, and troubleshoot BombasticIFC on a local Minikube cluster.

---

## Quick start

```bash
# One command to rebuild and redeploy everything
./dev-deploy.sh            # frontend + API

./dev-deploy.sh frontend   # frontend only  (~30 s)
./dev-deploy.sh api        # API only       (~4 min, .NET + node-builder)
```

The script:
1. Points Docker at Minikube's daemon (`eval $(minikube docker-env)`)
2. Builds with `--no-cache` to guarantee source changes are picked up
3. Sets the image tag on the deployment
4. Runs `kubectl rollout restart` to force pod replacement
5. Waits for the rollout to complete

---

## Architecture: request path

```
Browser (192.168.1.101:8080)
  │
  └─► nginx (frontend pod, port 80)
        │
        ├─ /api/*  ──► proxy_pass to api-service:80 → API pod (port 8080)
        │                  [Authorization header forwarded unchanged]
        │
        └─ /*      ──► try_files → dist/ → index.html (SPA fallback)
              │
              └─ /samples/Duplex.xkt  served statically from dist/public/
```

**Important:** `proxy_pass http://api-service.../api/` keeps the `/api/` prefix.
The ASP.NET controllers are registered at `[Route("api/[controller]")]`, so the
backend expects the full path including `/api/`.

---

## Known gotchas — read before touching these areas

### 1. `kubectl set image` with the same tag does NOT restart pods

When you rebuild a Docker image with `--no-cache` but keep the same tag
(`bombasticifc-frontend:local`), Kubernetes sees no change in the deployment spec
and leaves the old pods running. The new image is never used.

**Fix:** always call `kubectl rollout restart` after `kubectl set image`. The
`dev-deploy.sh` script does this automatically. Never use `set image` alone.

```bash
# Wrong — pods keep old image
kubectl set image deployment/api-deployment api=bombasticifc-api:local -n bombasticifccluster

# Correct
kubectl set image deployment/api-deployment api=bombasticifc-api:local -n bombasticifccluster
kubectl rollout restart deployment/api-deployment -n bombasticifccluster
kubectl rollout status  deployment/api-deployment -n bombasticifccluster
```

### 2. Docker layer cache hides C# source changes

Without `--no-cache`, Docker reuses cached layers. If only a `.cs` file changed
(not a `.csproj`), the `COPY src/` step might be cached and the compiled DLL
will contain the old code.

**Fix:** `dev-deploy.sh` always passes `--no-cache` to `docker build`.

### 3. `XktOutputUrl` must NOT include the `/api/` prefix

The backend DTOs (`GetModelsQuery`, `GetModelByIdQuery`) return `XktOutputUrl`
as a path relative to the Axios `baseURL`. The Axios client is configured with
`baseURL: '/api'`, so:

```
XktOutputUrl = "/models/{id}/output"     ← correct
XktOutputUrl = "/api/models/{id}/output" ← WRONG → results in /api/api/... → 404
```

Files to check: `src/BombasticIFC.Application/UseCases/Models/GetModelsQuery.cs`
and `GetModelByIdQuery.cs`.

### 4. Xeokit cannot load authenticated endpoints via `src` URL

`XKTLoaderPlugin.load({ src: url })` uses xeokit's own internal XHR/fetch with
no `Authorization` header. Passing a `/api/models/{id}/output` URL as `src` will
return 401 and the model won't load.

**Correct pattern** — fetch with auth first, pass raw bytes to xeokit:

```typescript
const token = localStorage.getItem('accessToken')
const res = await fetch(`/api${src}`, {
  headers: token ? { Authorization: `Bearer ${token}` } : {},
})
if (!res.ok) throw new Error(`HTTP ${res.status}`)
const buffer = await res.arrayBuffer()

xktLoader.load({ id, xkt: buffer, edges: true })  // ← use 'xkt', not 'src' or 'arraybuffer'
```

The correct parameter name for a pre-loaded ArrayBuffer is **`xkt`**.
(`arraybuffer` is not a valid parameter and xeokit will throw.)

### 5. Blob URLs are unreliable with XKTLoaderPlugin

`URL.createObjectURL(blob)` creates a `blob:http://...` URL. XKTLoaderPlugin
appends a `?_=<timestamp>` cache-buster to the URL before fetching, which
corrupts the blob reference and causes `ERR_FILE_NOT_FOUND`.

**Fix:** use the `xkt` ArrayBuffer parameter (see gotcha 4).

### 6. The demo fallback model path

The fallback XKT (`/samples/Duplex.xkt`) is served by nginx as a **static file**
from the frontend image — it lives in `frontend/public/samples/Duplex.xkt` and
Vite copies it to `dist/` at build time. It is **not** proxied through the API.

---

## Storage: where files live

| What | Path in pod | Host path (Minikube) | PVC |
|---|---|---|---|
| Uploaded IFC + converted XKT | `/data/storage/` | `/mnt/data/storage/` | `storage-pvc` |
| Postgres data | `/var/lib/postgresql/data/` | `/mnt/data/postgres/` | `postgres-pvc` |
| Demo seed XKT (in image) | `/app/seed-data/Duplex.xkt` | n/a | — |

To inspect storage from the host:

```bash
minikube ssh "ls -lh /mnt/data/storage/"
```

To inspect from inside the API pod:

```bash
kubectl exec -n bombasticifccluster deployment/api-deployment -- ls -lh /data/storage/
```

---

## Database access

```bash
kubectl exec -n bombasticifccluster deployment/postgres-deployment -- \
  psql -U postgres -d bombasticifcdb -c 'SELECT "FileName", "Status" FROM "IfcModels";'
```

Useful queries:

```sql
-- Check conversion jobs and their output file paths
SELECT m."FileName", j."Status", j."OutputFilePath", j."CompletedAt"
FROM "IfcModels" m
JOIN "ConversionJobs" j ON j."ModelId" = m."Id"
ORDER BY j."CompletedAt" DESC;
```

---

## Verifying the full request chain

Test the API output endpoint from inside the nginx pod (bypasses your local machine):

```bash
NGINX_POD=$(kubectl get pods -n bombasticifccluster | grep frontend | awk '{print $1}' | head -1)

# 1. Get a token
TOKEN=$(kubectl exec -n bombasticifccluster $NGINX_POD -- wget -qO- \
  --header="Content-Type: application/json" \
  --post-data='{"username":"<user>","password":"<pass>"}' \
  http://api-service.bombasticifccluster.svc.cluster.local/api/auth/login \
  | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)

# 2. Check xktOutputUrl values (must NOT start with /api/)
kubectl exec -n bombasticifccluster $NGINX_POD -- wget -qO- \
  --header="Authorization: Bearer $TOKEN" \
  http://127.0.0.1/api/models \
  | grep -o '"xktOutputUrl":"[^"]*"'

# 3. Download the XKT through nginx (expect HTTP 200, Content-Type: application/octet-stream)
kubectl exec -n bombasticifccluster $NGINX_POD -- wget -S -O /dev/null \
  --header="Authorization: Bearer $TOKEN" \
  "http://127.0.0.1/api/models/<model-id>/output" 2>&1 | grep "HTTP\|Content-Type"
```

---

## Namespace and resource names

| Resource | Name | Namespace |
|---|---|---|
| Namespace | `bombasticifccluster` | — |
| Frontend deployment | `frontend-deployment` | `bombasticifccluster` |
| API deployment | `api-deployment` | `bombasticifccluster` |
| Postgres deployment | `postgres-deployment` | `bombasticifccluster` |
| API service | `api-service` | `bombasticifccluster` |
| Storage PVC | `storage-pvc` | `bombasticifccluster` |
| Postgres PVC | `postgres-pvc` | `bombasticifccluster` |
| Local image tag (frontend) | `bombasticifc-frontend:local` | — |
| Local image tag (API) | `bombasticifc-api:local` | — |
