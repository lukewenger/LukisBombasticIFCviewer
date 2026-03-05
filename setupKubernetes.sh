#!/usr/bin/env bash
# BombasticIFC Cluster — Ubuntu Server Setup (Phase 0–2)
# Run as a regular user with sudo privileges on a vanilla Ubuntu 22.04/24.04 server.
# Usage: chmod +x setup.sh && ./setup.sh

set -euo pipefail

MINIKUBE_CPUS=2
MINIKUBE_MEMORY=4096
MINIKUBE_DISK="50g"
API_IMAGE="bombasticifccluster-api:latest"

info()  { echo -e "\n\033[1;34m[INFO]\033[0m  $*"; }
ok()    { echo -e "\033[1;32m[OK]\033[0m    $*"; }
err()   { echo -e "\033[1;31m[ERROR]\033[0m $*" >&2; exit 1; }

# ──────────────────────────────────────────────
# Phase 0 — Prerequisites
# ──────────────────────────────────────────────

info "Updating package index..."
sudo apt-get update -qq

# --- Docker ---
if command -v docker &>/dev/null; then
    ok "Docker is already installed ($(docker --version))"
else
    info "Installing Docker..."
    sudo apt-get install -y -qq ca-certificates curl gnupg
    sudo install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
    sudo chmod a+r /etc/apt/keyrings/docker.gpg
    echo \
      "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
      $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
      sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
    sudo apt-get update -qq
    sudo apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-buildx-plugin
    ok "Docker installed"
fi

# Ensure current user is in the docker group
if groups "$USER" | grep -qw docker; then
    ok "User '$USER' is already in the docker group"
else
    info "Adding '$USER' to the docker group..."
    sudo usermod -aG docker "$USER"
    echo ""
    echo "=============================================="
    echo "  You were added to the 'docker' group."
    echo "  Please log out and back in (or run 'newgrp docker'),"
    echo "  then re-run this script to continue."
    echo "=============================================="
    exit 0
fi

# Verify Docker is accessible without sudo
if ! docker info &>/dev/null; then
    err "Docker daemon is not accessible. Try 'newgrp docker' or log out and back in, then re-run."
fi

# --- kubectl ---
if command -v kubectl &>/dev/null; then
    ok "kubectl is already installed ($(kubectl version --client --short 2>/dev/null || kubectl version --client))"
else
    info "Installing kubectl..."
    KUBECTL_VERSION=$(curl -fsSL https://dl.k8s.io/release/stable.txt)
    curl -fsSLO "https://dl.k8s.io/release/${KUBECTL_VERSION}/bin/linux/amd64/kubectl"
    sudo install kubectl /usr/local/bin/kubectl
    rm -f kubectl
    ok "kubectl ${KUBECTL_VERSION} installed"
fi

# --- Minikube ---
if command -v minikube &>/dev/null; then
    ok "Minikube is already installed ($(minikube version --short))"
else
    info "Installing Minikube..."
    curl -fsSLO https://storage.googleapis.com/minikube/releases/latest/minikube-linux-amd64
    sudo install minikube-linux-amd64 /usr/local/bin/minikube
    rm -f minikube-linux-amd64
    ok "Minikube installed ($(minikube version --short))"
fi

# ──────────────────────────────────────────────
# Phase 2 — Start Minikube & Build Image
# ──────────────────────────────────────────────

# Start or reuse existing Minikube cluster
if minikube status --format='{{.Host}}' 2>/dev/null | grep -q Running; then
    ok "Minikube is already running"
else
    info "Starting Minikube (cpus=${MINIKUBE_CPUS}, memory=${MINIKUBE_MEMORY}MB, disk=${MINIKUBE_DISK})..."
    minikube start \
        --cpus="${MINIKUBE_CPUS}" \
        --memory="${MINIKUBE_MEMORY}" \
        --disk-size="${MINIKUBE_DISK}" \
        --driver=docker
    ok "Minikube started"
fi

# Enable addons (idempotent)
info "Enabling Minikube addons..."
minikube addons enable ingress
minikube addons enable storage-provisioner
minikube addons enable default-storageclass
minikube addons enable metrics-server
ok "Addons enabled"

# Point Docker CLI at Minikube's daemon for image build
info "Configuring Docker CLI to use Minikube's daemon..."
eval "$(minikube docker-env)"

# Build the API image
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
info "Building ${API_IMAGE} from ${SCRIPT_DIR}..."
docker build -t "${API_IMAGE}" "${SCRIPT_DIR}"
ok "Image built: ${API_IMAGE} ($(docker images --format '{{.Size}}' "${API_IMAGE}" | head -1))"

# Create host-path directories on the Minikube node
info "Creating host-path storage directories on Minikube node..."
minikube ssh "sudo mkdir -p /mnt/data/postgres /mnt/data/storage && sudo chmod -R 777 /mnt/data"
ok "Directories created: /mnt/data/postgres, /mnt/data/storage"

# ──────────────────────────────────────────────
# Done
# ──────────────────────────────────────────────

MINIKUBE_IP=$(minikube ip)
echo ""
echo "========================================="
echo "  Setup Complete (Phase 0–2)"
echo "========================================="
echo ""
echo "  Minikube IP:    ${MINIKUBE_IP}"
echo "  Docker env:     eval \$(minikube docker-env)"
echo "  API image:      ${API_IMAGE}"
echo ""
echo "  Next step: Phase 3 — Apply Kubernetes manifests"
echo "    kubectl apply -f kubernetes/namespace.yaml"
echo "    kubectl apply -f kubernetes/secrets.yaml"
echo "    kubectl apply -f kubernetes/configmap.yaml"
echo "    ..."
echo ""
