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

# Enable ingress addon (idempotent — safe to call when already enabled)
info "Enabling ingress addon..."
minikube addons enable ingress
# Wait for the ingress-nginx-controller deployment to be available before
# looking up its NodePort — it may not exist yet on first run.
kubectl wait deployment/ingress-nginx-controller \
    -n ingress-nginx \
    --for=condition=available \
    --timeout=120s
ok "Ingress addon ready"

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
    # api-deployment.yaml and frontend-deployment.yaml are intentionally omitted here.
    # deployAPI.sh and update-frontend.sh build the images first, then apply those manifests,
    # so applying them here (before images exist) causes ErrImageNeverPull.
    #
    # ingress.yaml is intentionally omitted here too.
    # api-service and frontend-service are created by deployAPI.sh and update-frontend.sh
    # respectively. Applying the Ingress before those Services exist causes the
    # nginx-ingress controller to log "service not found" errors and leave all
    # ingress rules in a broken state (503 for every route). The Ingress is
    # applied after both sub-scripts have completed (see below).
)

for manifest in "${MANIFESTS[@]}"; do
    kubectl apply -f "${manifest}"
done
ok "All manifests applied"

# ──────────────────────────────────────────────
# Step 3 — Wait for Postgres
# ──────────────────────────────────────────────
info "[3/6] Waiting for Postgres to be ready..."
# Use `kubectl wait` instead of `rollout status` so that an already-running
# deployment (with a stale ProgressDeadlineExceeded condition from a prior
# rollout) doesn't cause a false failure.
kubectl wait deployment/postgres-deployment \
    -n "${NAMESPACE}" \
    --for=condition=available \
    --timeout=120s
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

# Apply the Ingress only after both api-service and frontend-service exist.
# The nginx-ingress controller validates backend Service references at admission
# time; applying ingress.yaml before the Services are created causes a
# "service not found" error and leaves all ingress routes broken (503s).
info "Applying Ingress manifest (after services are ready)..."
kubectl apply -f kubernetes/ingress.yaml
ok "Ingress applied"

# ──────────────────────────────────────────────
# Step 6 — Summary
# ──────────────────────────────────────────────
info "[6/6] Cluster status:"
kubectl get pods -n "${NAMESPACE}" -o wide
echo ""

MINIKUBE_IP=$(minikube ip)

# ──────────────────────────────────────────────
# socat port-forward: 0.0.0.0:8080 → Minikube ingress NodePort
# Binding to 0.0.0.0 makes port 8080 reachable over LAN, not just localhost.
# We target the ingress-nginx NodePort rather than port 80 directly so the
# forward stays valid across ingress controller restarts.
# ──────────────────────────────────────────────
INGRESS_NODEPORT=$(kubectl get svc ingress-nginx-controller \
    -n ingress-nginx \
    -o jsonpath='{.spec.ports[?(@.name=="http")].nodePort}')
info "Starting socat port-forward 0.0.0.0:8080 → ${MINIKUBE_IP}:${INGRESS_NODEPORT} ..."
pkill -9 -x socat 2>/dev/null || true
sleep 0.5
nohup socat TCP-LISTEN:8080,fork,reuseaddr TCP:"${MINIKUBE_IP}":"${INGRESS_NODEPORT}" \
    >/tmp/socat-8080.log 2>&1 &
SOCAT_PID=$!
LAN_IP=$(ip addr show eth0 | awk '/inet /{print $2}' | head -1 | cut -d/ -f1)
ok "socat running (PID ${SOCAT_PID})"

echo "╔══════════════════════════════════════════╗"
echo "║              Redeploy Complete           ║"
echo "╠══════════════════════════════════════════╣"
echo "║  Minikube IP:  ${MINIKUBE_IP}"
echo "║  Ingress port: ${INGRESS_NODEPORT}"
echo "║  Local:        http://localhost:8080"
echo "║  LAN:          http://${LAN_IP}:8080"
echo "║  Swagger:      http://${LAN_IP}:8080/api/swagger"
echo "╚══════════════════════════════════════════╝"
echo ""
