#!/usr/bin/env bash
# Rebuild and redeploy only the API pod.
# Assumes Minikube is already running.
set -euo pipefail

cd "$(dirname "$0")"

NAMESPACE="bombasticifccluster"
API_IMAGE="bombasticifccluster-api:latest"
DEPLOYMENT="api-deployment"
TIMEOUT="180s"

info()  { echo -e "\n\033[1;34m[INFO]\033[0m  $*"; }
ok()    { echo -e "\033[1;32m[OK]\033[0m    $*"; }
err()   { echo -e "\033[1;31m[ERROR]\033[0m $*" >&2; exit 1; }

echo "=== API Redeploy ==="

# Build Docker image inside Minikube's Docker daemon
# --no-cache ensures the xeokit-convert install and dotnet publish always
# reflect the current source, avoiding stale layer problems after updates.
info "[1/3] Building API Docker image..."
eval "$(minikube docker-env)"
docker build --no-cache -t "${API_IMAGE}" .
ok "Image built: ${API_IMAGE}"

# Restart the deployment to pick up the new image
info "[2/3] Restarting API deployment..."
kubectl rollout restart deployment/"${DEPLOYMENT}" -n "${NAMESPACE}"
kubectl rollout status deployment/"${DEPLOYMENT}" -n "${NAMESPACE}" --timeout="${TIMEOUT}"
ok "Rollout complete"

# Verify
info "[3/3] Verifying..."
POD=$(kubectl get pods -n "${NAMESPACE}" -l app=bombasticifccluster-api \
    -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)
echo "  API pod:  ${POD}"
echo "  Status:   $(kubectl get pod "${POD}" -n "${NAMESPACE}" -o jsonpath='{.status.phase}')"
echo ""
echo "=== Done! API is running ==="
