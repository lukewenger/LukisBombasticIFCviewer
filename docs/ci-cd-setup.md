# CI/CD Setup Checklist — BombasticIFC

## 1. GitHub Secrets

Go to **Settings → Secrets and variables → Actions → New repository secret** and add the following:

| Secret name | Value |
|---|---|
| `GHCR_PAT` | GitHub Personal Access Token with `read:packages` scope (used by the deploy job to pull images from GHCR) |
| `DB_PASSWORD` | PostgreSQL password — choose a strong password (e.g. `openssl rand -base64 16`) |
| `JWT_SECRET` | JWT signing key — minimum 32 characters (e.g. `openssl rand -base64 32`) |

---

## 2. Self-Hosted Runner Setup

The deploy job runs on the VM where Minikube lives (`runs-on: self-hosted`).

1. Go to **Settings → Actions → Runners → New self-hosted runner**
2. Select **Linux / x64**
3. Follow the download, configure, and start instructions shown on that page
4. The runner user must:
   - Be in the `docker` group (`sudo usermod -aG docker <runner-user>`)
   - Have `kubectl` and `minikube` on `$PATH`
5. Install as a systemd service so the runner survives reboots:
   ```bash
   sudo ./svc.sh install
   sudo ./svc.sh start
   ```
6. Confirm the runner shows as **Idle** in the GitHub UI before triggering a deploy

---

## 3. First-Time Cluster Setup

Run once on the VM before the first deploy:

```bash
./setupKubernetes.sh
```

This initialises the Minikube cluster and any base infrastructure the deployments depend on.

---

## 4. Pipeline Flow

```
quality (lint + test)
    └── build (Docker images, no push)
            └── push (GHCR) — main branch only
                    └── deploy (self-hosted runner) — main branch only
```

The deploy job:
1. Pulls the published images from GHCR
2. Loads them into Minikube via `minikube image load`
3. Creates / updates the `bombasticifccluster-secrets` and `ghcr-login-secret` Kubernetes secrets
4. Applies all manifests under `kubernetes/`
5. Triggers a rolling restart of the API and frontend deployments
6. Waits for rollout to complete and shows final pod status
