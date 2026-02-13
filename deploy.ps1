# BombasticIFC Cluster - Minikube Deployment Script (PowerShell)
# This script sets up the complete Kubernetes cluster using Minikube

$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "BombasticIFC Cluster - Minikube Setup" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Check if minikube is installed
if (-not (Get-Command minikube -ErrorAction SilentlyContinue)) {
    Write-Host "Error: minikube is not installed" -ForegroundColor Red
    Write-Host "Please install minikube: https://minikube.sigs.k8s.io/docs/start/" -ForegroundColor Yellow
    exit 1
}

# Check if kubectl is installed
if (-not (Get-Command kubectl -ErrorAction SilentlyContinue)) {
    Write-Host "Error: kubectl is not installed" -ForegroundColor Red
    Write-Host "Please install kubectl: https://kubernetes.io/docs/tasks/tools/" -ForegroundColor Yellow
    exit 1
}

# Start Minikube if not running
Write-Host "Checking Minikube status..." -ForegroundColor Green
try {
    minikube status | Out-Null
    Write-Host "Minikube is already running" -ForegroundColor Green
} catch {
    Write-Host "Starting Minikube..." -ForegroundColor Green
    minikube start --cpus=4 --memory=8192 --disk-size=50g
}

# Enable required addons
Write-Host ""
Write-Host "Enabling Minikube addons..." -ForegroundColor Green
minikube addons enable ingress
minikube addons enable storage-provisioner
minikube addons enable default-storageclass
minikube addons enable metrics-server

# Set docker environment to use Minikube's Docker daemon
Write-Host ""
Write-Host "Configuring Docker environment..." -ForegroundColor Green
& minikube -p minikube docker-env --shell powershell | Invoke-Expression

# Build Docker image
Write-Host ""
Write-Host "Building Docker image..." -ForegroundColor Green
docker build -t bombasticifccluster-api:latest .

# Create necessary directories on Minikube node
Write-Host ""
Write-Host "Creating storage directories..." -ForegroundColor Green
minikube ssh "sudo mkdir -p /mnt/data/postgres"
minikube ssh "sudo mkdir -p /mnt/data/storage"
minikube ssh "sudo chmod -R 777 /mnt/data"

# Apply Kubernetes manifests
Write-Host ""
Write-Host "Deploying to Kubernetes..." -ForegroundColor Green

# 1. Create namespace
Write-Host "Creating namespace..." -ForegroundColor Yellow
kubectl apply -f kubernetes/namespace.yaml

# 2. Create secrets and configmaps
Write-Host "Creating secrets and configmaps..." -ForegroundColor Yellow
kubectl apply -f kubernetes/secrets.yaml
kubectl apply -f kubernetes/configmap.yaml

# 3. Create persistent volumes
Write-Host "Creating persistent volumes..." -ForegroundColor Yellow
kubectl apply -f kubernetes/persistent-volumes.yaml

# Wait for PVs to be available
Write-Host "Waiting for persistent volumes..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# 4. Deploy PostgreSQL
Write-Host "Deploying PostgreSQL..." -ForegroundColor Yellow
kubectl apply -f kubernetes/postgres-deployment.yaml

# Wait for PostgreSQL to be ready
Write-Host "Waiting for PostgreSQL to be ready..." -ForegroundColor Yellow
kubectl wait --for=condition=ready pod -l app=postgres -n bombasticifccluster --timeout=300s

# 5. Deploy API
Write-Host "Deploying API..." -ForegroundColor Yellow
kubectl apply -f kubernetes/api-deployment.yaml

# Wait for API to be ready
Write-Host "Waiting for API to be ready..." -ForegroundColor Yellow
kubectl wait --for=condition=ready pod -l app=bombasticifccluster-api -n bombasticifccluster --timeout=300s

# 6. Deploy Ingress
Write-Host "Deploying Ingress..." -ForegroundColor Yellow
kubectl apply -f kubernetes/ingress.yaml

# Get Minikube IP
$MINIKUBE_IP = minikube ip

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Minikube IP: $MINIKUBE_IP" -ForegroundColor Green
Write-Host ""
Write-Host "Services are accessible at:" -ForegroundColor Yellow
Write-Host "  - API (NodePort):  http://${MINIKUBE_IP}:30080" -ForegroundColor White
Write-Host "  - API Health:      http://${MINIKUBE_IP}:30080/health" -ForegroundColor White
Write-Host "  - API Swagger:     http://${MINIKUBE_IP}:30080/swagger" -ForegroundColor White
Write-Host "  - PostgreSQL:      ${MINIKUBE_IP}:30432" -ForegroundColor White
Write-Host ""
Write-Host "Ingress (add to C:\Windows\System32\drivers\etc\hosts):" -ForegroundColor Yellow
Write-Host "  $MINIKUBE_IP bombasticifccluster.local" -ForegroundColor White
Write-Host "  Then access: http://bombasticifccluster.local" -ForegroundColor White
Write-Host ""
Write-Host "Useful commands:" -ForegroundColor Yellow
Write-Host "  - View pods:       kubectl get pods -n bombasticifccluster" -ForegroundColor White
Write-Host "  - View services:   kubectl get svc -n bombasticifccluster" -ForegroundColor White
Write-Host "  - View logs:       kubectl logs -f <pod-name> -n bombasticifccluster" -ForegroundColor White
Write-Host "  - Dashboard:       minikube dashboard" -ForegroundColor White
Write-Host "  - Stop cluster:    minikube stop" -ForegroundColor White
Write-Host "  - Delete cluster:  minikube delete" -ForegroundColor White
Write-Host ""
