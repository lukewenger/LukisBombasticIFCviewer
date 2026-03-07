#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

echo "=== Frontend Update ==="

# Build Docker image inside Minikube's Docker daemon
# --no-cache ensures Vue/Vite always recompiles all source files so the
# chunk hashes in the served assets always match the current index.html.
echo "[1/3] Building frontend Docker image..."
eval $(minikube docker-env)
docker build --no-cache -t bombasticifccluster-frontend:latest ./frontend

# Restart the deployment to pick up the new image
echo "[2/3] Restarting frontend deployment..."
kubectl rollout restart deployment/frontend-deployment -n bombasticifccluster
kubectl rollout status deployment/frontend-deployment -n bombasticifccluster --timeout=120s

# Verify
echo "[3/3] Verifying..."
POD=$(kubectl get pods -n bombasticifccluster -l app=frontend -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)
echo "Frontend pod: $POD"
echo "Status: $(kubectl get pod "$POD" -n bombasticifccluster -o jsonpath='{.status.phase}')"
echo ""
echo "=== Done! Frontend available at http://localhost:8080/ ==="
