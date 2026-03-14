# Plan: Production Readiness for BombasticIFC Cluster

## Current State
- Ubuntu server running **Minikube** (dev-grade, single-node, VM-in-VM)
- Namespace: `bombasticifccluster`
- Stack: Vue.js frontend, .NET 8 API (2 replicas), PostgreSQL 16
- `imagePullPolicy: Never` → local images only
- `hostPath` volumes at `/mnt/data/postgres` and `/mnt/data/storage`
- Ingress host: `bombasticifccluster.local` (no real domain, no TLS)
- Plaintext secrets in `secrets.yaml`
- PostgreSQL NodePort (30432) exposed externally
- No HTTPS, no backups, no monitoring, no CI/CD

## Production Gaps (priority order)

### CRITICAL
1. Replace Minikube with K3s (minikube is not suitable for production)
2. Real container registry (remove `imagePullPolicy: Never`)
3. Proper secrets management (remove stringData plaintext)
4. TLS/HTTPS with real domain
5. Replace hostPath volumes with durable storage

### IMPORTANT
6. Remove PostgreSQL NodePort exposure
7. Automated backups
8. Monitoring + alerting

### NICE TO HAVE
9. CI/CD pipeline
10. NetworkPolicies
11. Resource quotas / LimitRanges


# Masterplan for setting up Porduction Environment
## Promptready


# Production Readiness Plan — BombasticIFC Cluster

## Context

Ubuntu server currently running **Minikube**. Stack: Vue.js frontend + .NET 8 API (2 replicas) + PostgreSQL 16, all in namespace `bombasticifccluster`.

---

## Current State Summary

| Area | Current | Problem |
|---|---|---|
| K8s runtime | Minikube | Dev-only VM, not suitable for production |
| Image delivery | `imagePullPolicy: Never` | Images must be built locally, no registry |
| TLS | Disabled (`ssl-redirect: "false"`) | No HTTPS |
| Ingress host | `bombasticifccluster.local` | Fake hostname, no real domain |
| Secrets | Plaintext `stringData` in `secrets.yaml` | Must not be committed to git |
| Volumes | `hostPath` at `/mnt/data/` | Node-locked, no replication |
| Database exposure | PostgreSQL NodePort 30432 | DB reachable on host network |
| Backups | Manual only | No automation |
| Monitoring | None | — |

---

## Phase 1 — Replace Minikube with K3s (BLOCKING)

K3s is a CNCF-certified, production-grade, single-binary Kubernetes that runs as a native systemd service. No VM overhead, survives reboots.

```bash
# Remove minikube
minikube stop && minikube delete

# Install K3s
curl -sfL https://get.k3s.io | sh -

# Configure kubeconfig
mkdir -p ~/.kube
sudo cp /etc/rancher/k3s/k3s.yaml ~/.kube/config
sudo chown $USER:$USER ~/.kube/config
export KUBECONFIG=~/.kube/config
```

**K3s uses Traefik as its ingress controller** (not nginx). Either:
- Change `ingressClassName: nginx` → `traefik` in `kubernetes/ingress.yaml`
- Or install the nginx ingress controller on K3s: `kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.10.0/deploy/static/provider/cloud/deploy.yaml`

### Build and import images into K3s containerd

```bash
docker build -t bombasticifccluster-api:latest .
docker build -t bombasticifccluster-frontend:latest ./frontend

docker save bombasticifccluster-api:latest     | sudo k3s ctr images import -
docker save bombasticifccluster-frontend:latest | sudo k3s ctr images import -
```

### Create storage directories

```bash
sudo mkdir -p /mnt/data/postgres /mnt/data/storage
sudo chmod -R 777 /mnt/data
```

---

## Phase 2 — Container Registry (parallel with Phase 1)

Remove dependency on locally-built images.

**Options (pick one):**
- `ghcr.io/<github-user>/<repo>` — free for public repos, lowest friction for GitHub projects
- Docker Hub — free tier with rate limits
- Self-hosted Harbor/Gitea — most control, more ops burden

**Changes required:**
- `kubernetes/api-deployment.yaml`: `image: ghcr.io/<user>/bombasticifccluster-api:latest`
- `kubernetes/api-deployment.yaml`: `imagePullPolicy: IfNotPresent`
- `kubernetes/frontend-deployment.yaml`: same pattern
- Add `imagePullSecrets` if registry is private

---

## Phase 3 — TLS + Real Domain (depends on Phase 1)

1. **Point a domain/subdomain** at the server's public IP (DNS A record).
2. **Install cert-manager** for automatic Let's Encrypt certificates:

```bash
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.14.4/cert-manager.yaml
```

3. **Create a ClusterIssuer** for Let's Encrypt:

```yaml
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: <your-email>
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: traefik   # or nginx
```

4. **Update `kubernetes/ingress.yaml`**:
   - Change `host: bombasticifccluster.local` → your real domain
   - Add annotation `cert-manager.io/cluster-issuer: "letsencrypt-prod"`
   - Add `tls:` block
   - Remove `nginx.ingress.kubernetes.io/ssl-redirect: "false"`

---

## Phase 4 — Secrets Management (can be done any time)

**Minimum fix:**
- Add `kubernetes/secrets.yaml` to `.gitignore`
- Regenerate `jwt-secret` from a cryptographically random value: `openssl rand -base64 64`

**Better approach — Sealed Secrets:**
```bash
# Install controller
kubectl apply -f https://github.com/bitnami-labs/sealed-secrets/releases/download/v0.26.2/controller.yaml

# Install CLI
brew install kubeseal  # or download binary

# Seal the secrets file
kubeseal --format yaml < kubernetes/secrets.yaml > kubernetes/sealed-secrets.yaml
# sealed-secrets.yaml is safe to commit; secrets.yaml is not
```

---

## Phase 5 — Storage Durability (depends on Phase 1)

K3s ships with **local-path-provisioner** which handles dynamic PV provisioning.

Replace the manual `hostPath` PVs in `kubernetes/persistent-volumes.yaml` and use the `local-path` StorageClass instead.

Example PVC replacement:
```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: postgres-pvc
  namespace: bombasticifccluster
spec:
  accessModes: [ReadWriteOnce]
  storageClassName: local-path
  resources:
    requests:
      storage: 10Gi
```

For true HA/multi-node storage, consider **Longhorn** (K3s-native):
```bash
kubectl apply -f https://raw.githubusercontent.com/longhorn/longhorn/v1.6.0/deploy/longhorn.yaml
```

---

## Phase 6 — Security Hardening

### Remove PostgreSQL NodePort
Delete the `postgres-nodeport` Service from `kubernetes/postgres-deployment.yaml`. The database has no business being reachable on the host network.

### Add NetworkPolicy
Restrict pod-to-pod traffic — only the API pods should reach PostgreSQL:

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: postgres-allow-api-only
  namespace: bombasticifccluster
spec:
  podSelector:
    matchLabels:
      app: postgres
  policyTypes: [Ingress]
  ingress:
  - from:
    - podSelector:
        matchLabels:
          app: bombasticifccluster-api
    ports:
    - protocol: TCP
      port: 5432
```

---

## Phase 7 — Automated Backups (after Phase 1 + 5)

Add a Kubernetes CronJob for PostgreSQL backups:

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: postgres-backup
  namespace: bombasticifccluster
spec:
  schedule: "0 2 * * *"   # daily at 02:00
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: pg-dump
            image: postgres:16-alpine
            env:
            - name: PGPASSWORD
              valueFrom:
                secretKeyRef:
                  name: bombasticifccluster-secrets
                  key: postgres-password
            command:
            - /bin/sh
            - -c
            - |
              pg_dump -h postgres-service -U postgres -d bombasticifcdb \
                > /backup/backup_$(date +%Y%m%d_%H%M%S).sql
            volumeMounts:
            - name: backup-storage
              mountPath: /backup
          restartPolicy: OnFailure
          volumes:
          - name: backup-storage
            persistentVolumeClaim:
              claimName: storage-pvc
```

---

## Phase 8 — Monitoring (optional for small production)

Deploy kube-prometheus-stack via Helm:

```bash
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update
helm install monitoring prometheus-community/kube-prometheus-stack \
  --namespace monitoring --create-namespace
```

Provides: Prometheus, Grafana, AlertManager, node exporter, kube-state-metrics out of the box.

---

## Files to Change

| File | Change |
|---|---|
| `kubernetes/ingress.yaml` | Real domain, TLS block, ingressClassName |
| `kubernetes/api-deployment.yaml` | Registry image ref, imagePullPolicy |
| `kubernetes/frontend-deployment.yaml` | Registry image ref, imagePullPolicy |
| `kubernetes/secrets.yaml` | Add to `.gitignore`, use Sealed Secrets |
| `kubernetes/persistent-volumes.yaml` | Migrate to local-path dynamic provisioning |
| `kubernetes/postgres-deployment.yaml` | Remove postgres-nodeport Service |

---

## Verification Checklist

- [ ] `kubectl get pods -n bombasticifccluster` — all `Running`, 0 restarts
- [ ] `curl -I https://<your-domain>` — HTTP 200, valid TLS cert
- [ ] `curl https://<your-domain>/health` — `{"status":"Healthy"}`
- [ ] `kubectl get pv,pvc -n bombasticifccluster` — all `Bound`
- [ ] `kubectl get svc -n bombasticifccluster` — no `postgres-nodeport`
- [ ] `git log -- kubernetes/secrets.yaml` — no plaintext secrets in history

---

## Open Questions

1. **Registry** — Is this project on GitHub? If yes, `ghcr.io` is the easiest option.
2. **Domain** — Do you have a domain name, or is this internal/VPN-only?
3. **Single vs. multi-node** — One server K3s is fine for a personal/small-team project. If you need HA, add a second VM or use a managed K8s offering (Hetzner, DigitalOcean).

