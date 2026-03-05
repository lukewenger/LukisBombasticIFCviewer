# BombasticIFC Cluster — Deployment Tasks

## Phase 0 — Prerequisites (Linux)

- [ ] Install Minikube
- [ ] Install kubectl
- [ ] Install Docker and ensure user is in the `docker` group

## Phase 1 — Create Missing `kubernetes/secrets.yaml`

- [x] Create `kubernetes/secrets.yaml` with:
  - `connection-string` (PostgreSQL connection string)
  - `postgres-user`
  - `postgres-password`
  - `postgres-db`
  - `jwt-secret` (strong, random 64+ char key)
- [x] **Do NOT commit** `secrets.yaml` to version control (already in `.gitignore`)
- [x] Rotate hardcoded JWT secret in `src/BombasticIFC.API/appsettings.json`
- [x] Rotate hardcoded DB password (`postgres`) in appsettings and docker-compose
- [x] Verify `JwtSettings__Secret` env var is injected in `kubernetes/api-deployment.yaml` (added)

## Phase 2 — Start Minikube & Build Image

- [x] Start Minikube: `minikube start --cpus=2 --memory=4096 --disk-size=50g --driver=docker` (adjusted for 6.8GB system)
- [x] Enable addons: `ingress`, `storage-provisioner`, `default-storageclass`, `metrics-server`
- [x] Point Docker CLI at Minikube's daemon: `eval $(minikube docker-env)`
- [x] Build API image inside Minikube context: `docker build -t bombasticifccluster-api:latest .` (251MB)
- [x] Create host-path directories: `/mnt/data/postgres` and `/mnt/data/storage`

## Phase 3 — Apply Kubernetes Manifests (in order)

- [x] `kubectl apply -f kubernetes/namespace.yaml`
- [x] `kubectl apply -f kubernetes/secrets.yaml`
- [x] `kubectl apply -f kubernetes/configmap.yaml`
- [x] `kubectl apply -f kubernetes/persistent-volumes.yaml`
- [x] `kubectl apply -f kubernetes/postgres-deployment.yaml`
- [x] Wait for PostgreSQL: ready ✅
- [x] `kubectl apply -f kubernetes/api-deployment.yaml` (port fixed: 80→8080)
- [x] Wait for API: both pods ready ✅
- [x] `kubectl apply -f kubernetes/ingress.yaml`

## Phase 4 — Database Migrations

- [x] Confirmed `Program.cs` does NOT auto-apply migrations → added `db.Database.Migrate()` on startup
- [x] Fixed EF warning: added matching query filters for `ModelVersion` and `ConversionJob` in `ApplicationDbContext`
- [x] Created `InitialCreate` migration via `dotnet ef migrations add`
- [x] Rebuilt image (`--no-cache`) and redeployed — migration applied successfully on first pod startup

## Phase 5 — Access & Verification

- [ ] Get Minikube IP: `minikube ip`
- [ ] Add `/etc/hosts` entry: `<MINIKUBE_IP> bombasticifccluster.local`
- [ ] Verify health: `curl http://bombasticifccluster.local/api/health`
- [ ] Verify Swagger: `curl http://<MINIKUBE_IP>:30080/swagger`
- [ ] Verify PostgreSQL access: `psql -h <MINIKUBE_IP> -p 30432 -U postgres bombasticifcdb`

## Phase 6 — Frontend Deployment (not yet in k8s)

- [ ] Decide approach: local dev server / nginx pod in k8s / static files served by .NET API
- [ ] If local dev: update `vite.config.ts` proxy target to `http://<MINIKUBE_IP>:30080`
- [ ] If k8s: create `frontend-deployment.yaml` with an nginx image serving the Vite build output
- [ ] If .NET-hosted: add `app.UseStaticFiles()` and copy Vite output into the Docker image

## Known Gaps & Future Work

- [ ] No conversion worker service exists — conversion jobs are stored but never processed
- [ ] No RBAC / ServiceAccounts defined
- [ ] No NetworkPolicies for pod-to-pod segmentation
- [ ] No resource requests/limits or LimitRanges on the namespace
- [ ] hostPath storage is single-node only — not production-ready
