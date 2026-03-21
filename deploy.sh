#!/usr/bin/env bash
# Full clean redeploy of the BombasticIFC cluster.
#
# What this script does:
#   1. Ensures Minikube is running
#   2. Creates host-path storage dirs on the Minikube node
#   3. Applies all Kubernetes manifests (idempotent kubectl apply)
#   4. Waits for Postgres to be ready
#   5. Calls deployAPI.sh  — rebuilds API image + rolls out api-deployment
#   6. Calls update-frontend.sh — rebuilds frontend image + rolls out frontend-deployment
#   7. Prints a status summary
#
# Prerequisites: setupKubernetes.sh must have been run at least once
# (Docker, kubectl, Minikube must be installed and the EF migrations must exist).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "${SCRIPT_DIR}"

NAMESPACE="bombasticifccluster"

info()  { echo -e "\n\033[1;34m[INFO]\033[0m  $*"; }
ok()    { echo -e "\033[1;32m[OK]\033[0m    $*"; }
warn()  { echo -e "\033[1;33m[WARN]\033[0m  $*"; }
err()   { echo -e "\033[1;31m[ERROR]\033[0m $*" >&2; exit 1; }

echo ""
echo "╔══════════════════════════════════════════╗"
echo "║   BombasticIFC — Full Cluster Redeploy   ║"
echo "╚══════════════════════════════════════════╝"

# ──────────────────────────────────────────────
# Step 1 — Ensure Minikube is running
# ──────────────────────────────────────────────
info "[1/6] Checking Minikube..."
if minikube status --format='{{.Host}}' 2>/dev/null | grep -q Running; then
    ok "Minikube is already running"
else
    warn "Minikube is not running — starting it now..."
    minikube start --driver=docker
    ok "Minikube started"
fi

# Ensure host-path dirs exist on the node
info "Ensuring storage directories on Minikube node..."
minikube ssh "sudo mkdir -p /mnt/data/postgres /mnt/data/storage && sudo chmod -R 777 /mnt/data"
ok "Directories ready: /mnt/data/postgres, /mnt/data/storage"

# ──────────────────────────────────────────────
# Step 2 — Apply Kubernetes manifests
# ──────────────────────────────────────────────
info "[2/6] Applying Kubernetes manifests..."

MANIFESTS=(
    kubernetes/namespace.yaml
    kubernetes/secrets.yaml
    kubernetes/configmap.yaml
    kubernetes/persistent-volumes.yaml
    kubernetes/postgres-deployment.yaml
    kubernetes/api-deployment.yaml
    kubernetes/frontend-deployment.yaml
    kubernetes/ingress.yaml
)

for manifest in "${MANIFESTS[@]}"; do
    kubectl apply -f "${manifest}"
done
ok "All manifests applied"

# ──────────────────────────────────────────────
# Step 3 — Wait for Postgres
# ──────────────────────────────────────────────
info "[3/6] Waiting for Postgres to be ready..."
kubectl rollout status deployment/postgres-deployment -n "${NAMESPACE}" --timeout=120s
ok "Postgres is ready"

# ──────────────────────────────────────────────
# Step 4 — Redeploy API
# ──────────────────────────────────────────────
info "[4/6] Redeploying API..."
bash "${SCRIPT_DIR}/deployAPI.sh"

# ──────────────────────────────────────────────
# Step 5 — Redeploy Frontend
# ──────────────────────────────────────────────
info "[5/6] Redeploying Frontend..."
bash "${SCRIPT_DIR}/update-frontend.sh"

# ──────────────────────────────────────────────
# Step 6 — Summary
# ──────────────────────────────────────────────
info "[6/6] Cluster status:"
kubectl get pods -n "${NAMESPACE}" -o wide
echo ""

MINIKUBE_IP=$(minikube ip)
echo "╔══════════════════════════════════════════╗"
echo "║              Redeploy Complete           ║"
echo "╠══════════════════════════════════════════╣"
echo "║  Minikube IP:  ${MINIKUBE_IP}"
echo "║  App URL:      http://bombasticifccluster.local"
echo "║  Swagger:      http://bombasticifccluster.local/api/swagger"
echo "╚══════════════════════════════════════════╝"
echo ""
echo "  If bombasticifccluster.local is not in /etc/hosts, add:"
echo "    echo '${MINIKUBE_IP} bombasticifccluster.local' | sudo tee -a /etc/hosts"
echo ""
