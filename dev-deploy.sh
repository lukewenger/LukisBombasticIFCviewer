#!/usr/bin/env bash
# dev-deploy.sh — build and hot-deploy one or both services into the local Minikube cluster.
#
# Usage:
#   ./dev-deploy.sh              # build + deploy both frontend and api
#   ./dev-deploy.sh frontend     # frontend only
#   ./dev-deploy.sh api          # api only
#
# Requirements: minikube, kubectl, docker available on PATH.

set -euo pipefail

NAMESPACE="bombasticifccluster"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET="${1:-all}"

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'

log()  { echo -e "${CYAN}[dev-deploy]${NC} $*"; }
ok()   { echo -e "${GREEN}[ok]${NC} $*"; }
warn() { echo -e "${YELLOW}[warn]${NC} $*"; }
die()  { echo -e "${RED}[error]${NC} $*" >&2; exit 1; }

# ── pre-flight ────────────────────────────────────────────────────────────────
command -v minikube &>/dev/null || die "minikube not found"
command -v kubectl  &>/dev/null || die "kubectl not found"
minikube status --format='{{.Host}}' 2>/dev/null | grep -q "Running" || die "Minikube is not running. Start it with: minikube start"

log "Pointing Docker client at Minikube daemon..."
eval "$(minikube docker-env)"

# ── build + deploy frontend ───────────────────────────────────────────────────
deploy_frontend() {
  log "Building frontend image..."
  docker build -t bombasticifc-frontend:local "$REPO_ROOT/frontend"
  ok "Frontend image built → bombasticifc-frontend:local"

  log "Deploying frontend to cluster (namespace: $NAMESPACE)..."
  kubectl set image deployment/frontend-deployment \
    frontend=bombasticifc-frontend:local \
    -n "$NAMESPACE"
  kubectl rollout status deployment/frontend-deployment -n "$NAMESPACE"
  ok "Frontend rolled out successfully."
}

# ── build + deploy api ────────────────────────────────────────────────────────
deploy_api() {
  log "Building API image (this takes ~2 min on first run)..."
  docker build -t bombasticifc-api:local "$REPO_ROOT"
  ok "API image built → bombasticifc-api:local"

  log "Deploying API to cluster (namespace: $NAMESPACE)..."
  kubectl set image deployment/api-deployment \
    api=bombasticifc-api:local \
    -n "$NAMESPACE"
  kubectl rollout status deployment/api-deployment -n "$NAMESPACE"
  ok "API rolled out successfully."
}

# ── dispatch ──────────────────────────────────────────────────────────────────
case "$TARGET" in
  frontend) deploy_frontend ;;
  api)      deploy_api ;;
  all)      deploy_frontend; deploy_api ;;
  *)        die "Unknown target '$TARGET'. Valid values: frontend | api | all" ;;
esac

log "Done. Access the app at: $(minikube service frontend-service -n $NAMESPACE --url 2>/dev/null || echo 'http://192.168.1.101:8080')"
