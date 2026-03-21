#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

NAMESPACE="bombasticifccluster"
FRONTEND_IMAGE="bombasticifccluster-frontend:latest"
DEPLOYMENT="frontend-deployment"
TIMEOUT="180s"

info()  { echo -e "\n\033[1;34m[INFO]\033[0m  $*"; }
ok()    { echo -e "\033[1;32m[OK]\033[0m    $*"; }
err()   { echo -e "\033[1;31m[ERROR]\033[0m $*" >&2; exit 1; }

echo "=== Frontend Update ==="

# Build Docker image inside Minikube's Docker daemon
# --no-cache ensures Vue/Vite always recompiles all source files so the
# chunk hashes in the served assets always match the current index.html.
info "[1/4] Building frontend Docker image..."
if ! minikube status --format='{{.Host}}' 2>/dev/null | grep -q Running; then
	err "Minikube is not running. Start it first (e.g. minikube start --driver=docker)."
fi
eval "$(minikube docker-env)"
docker build --no-cache -t "${FRONTEND_IMAGE}" ./frontend
ok "Image built: ${FRONTEND_IMAGE}"

# Apply deployment manifest and restart to pick up resource/replica changes plus new image
info "[2/4] Applying frontend manifest..."
kubectl apply -f kubernetes/frontend-deployment.yaml
ok "Frontend manifest applied"

# Restart the deployment to pick up the new image
info "[3/4] Restarting frontend deployment..."
kubectl rollout restart deployment/"${DEPLOYMENT}" -n "${NAMESPACE}"
kubectl rollout status deployment/"${DEPLOYMENT}" -n "${NAMESPACE}" --timeout="${TIMEOUT}"
ok "Rollout complete"

# Verify
info "[4/4] Verifying..."
POD=$(kubectl get pods -n "${NAMESPACE}" -l app=bombasticifccluster-frontend \
	--sort-by=.metadata.creationTimestamp -o jsonpath='{.items[-1:].metadata.name}' 2>/dev/null || true)

if [[ -z "${POD}" ]]; then
	err "No frontend pod found in namespace ${NAMESPACE}."
fi

echo "  Frontend pod: ${POD}"
echo "  Status:       $(kubectl get pod "${POD}" -n "${NAMESPACE}" -o jsonpath='{.status.phase}')"
echo ""
echo "=== Done! Frontend available at http://bombasticifccluster.local/ ==="
