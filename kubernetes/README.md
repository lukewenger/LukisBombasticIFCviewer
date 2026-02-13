# Kubernetes Configuration Files

This directory contains all Kubernetes manifests for deploying the BombasticIFC Cluster.

## Files Overview

- **namespace.yaml**: Creates the `bombasticifccluster` namespace
- **secrets.yaml**: Contains database credentials and connection strings (DO NOT commit to version control)
- **configmap.yaml**: Configuration data for PostgreSQL
- **persistent-volumes.yaml**: PersistentVolumes and PersistentVolumeClaims for database and file storage
- **postgres-deployment.yaml**: PostgreSQL database deployment and services
- **api-deployment.yaml**: Main API deployment and services
- **ingress.yaml**: Ingress configuration for routing external traffic

## Deployment Order

Apply the manifests in the following order:

1. Namespace
2. Secrets and ConfigMaps
3. Persistent Volumes
4. PostgreSQL Deployment
5. API Deployment
6. Ingress

See the main deployment script for automated deployment.

## Services

### PostgreSQL
- **ClusterIP Service**: postgres-service (internal communication)
- **NodePort Service**: postgres-nodeport (external access on port 30432)

### API
- **ClusterIP Service**: api-service (internal communication)
- **NodePort Service**: api-nodeport (external access on port 30080)

## Storage

- **postgres-pv/pvc**: 10Gi for PostgreSQL data (hostPath: /mnt/data/postgres)
- **storage-pv/pvc**: 50Gi for IFC file storage (hostPath: /mnt/data/storage)

## Accessing Services

- API: http://localhost:30080 (via NodePort)
- PostgreSQL: localhost:30432 (via NodePort)
- Via Ingress: http://bombasticifccluster.local (requires Ingress controller)
