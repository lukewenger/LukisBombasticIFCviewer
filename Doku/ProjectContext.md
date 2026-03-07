## Complete File Contents

---

### 1. kubernetes/CLUSTER-DOKUMENTATION.md

```markdown
# BombasticIFC Cluster — Kubernetes-Dokumentation

Vollständige technische Dokumentation des Kubernetes-Clusters für die BombasticIFC-Plattform.

---

## Inhaltsverzeichnis

1. [Architekturübersicht](#architekturübersicht)
2. [Voraussetzungen](#voraussetzungen)
3. [Cluster-Komponenten](#cluster-komponenten)
   - [Namespace](#namespace)
   - [Secrets & ConfigMaps](#secrets--configmaps)
   - [Persistent Volumes](#persistent-volumes)
   - [PostgreSQL-Deployment](#postgresql-deployment)
   - [API-Deployment](#api-deployment)
   - [Frontend-Deployment](#frontend-deployment)
   - [Ingress](#ingress)
4. [Docker-Images](#docker-images)
5. [Deployment-Ablauf](#deployment-ablauf)
   - [Windows (Minikube)](#option-a-windows--minikube)
   - [Ubuntu Server (K3s)](#option-b-ubuntu-server--k3s)
6. [Netzwerk & Zugriff](#netzwerk--zugriff)
7. [Monitoring & Debugging](#monitoring--debugging)
8. [Skalierung](#skalierung)
9. [Backup & Wiederherstellung](#backup--wiederherstellung)
10. [Cluster zurücksetzen](#cluster-zurücksetzen)
11. [Visualisierung](#visualisierung)

---

## Architekturübersicht

**PlantUML Architecture Diagram:** Siehe [architecture.puml](architecture.puml)

```
                         ┌──────────────────────────────────────────────────────────┐
                         │           Kubernetes-Cluster                             │
                         │           Namespace: bombasticifccluster                 │
                         │                                                          │
  Client (Browser)       │  ┌───────────────────────────────────────────┐           │
        │                │  │         Ingress Controller                 │           │
        │  HTTP          │  │     bombasticifccluster.local              │           │
        ▼                │  │     nginx / traefik                        │           │
  ┌───────────┐          │  └──────┬──────────┬───────────────────────┘           │
  │  :30080   │──────────│─────────│──────────│                                    │
  │  NodePort │          │         │          ▼                                    │
  └───────────┘          │         │  ┌──────────────────┐                         │
                         │         │  │ frontend-service  │                         │
                         │         │  │ ClusterIP :80     │                         │
                         │         │  └────────┬─────────┘                         │
                         │         │           ▼                                    │
                         │         │  ┌──────────────────┐                         │
                         │         │  │  Frontend Pod     │                         │
                         │         │  │  Vue.js + nginx   │                         │
                         │         │  │  Port 80          │                         │
                         │         │  └──────────────────┘                         │
                         │         │                                                │
                         │         ▼                                                │
                         │  ┌──────────────────┐                                    │
                         │  │  api-service      │                                    │
                         │  │  ClusterIP :80    │                                    │
                         │  └────────┬─────────┘                                    │
                         │           │                                              │
                         │           ▼                                              │
                         │  ┌──────────────────┐    ┌──────────────────┐           │
                         │  │  API Pod 1        │    │  API Pod 2        │           │
                         │  │  .NET 8 Runtime   │    │  .NET 8 Runtime   │           │
                         │  │  Port 8080        │    │  Port 8080        │           │
                         │  └────────┬─────────┘    └────────┬─────────┘           │
                         │           │                        │                     │
                         │           ▼                        ▼                     │
                         │  ┌────────────────────────────────────────┐              │
                         │  │  postgres-service (ClusterIP :5432)     │              │
                         │  └──────────┬─────────────────────────────┘              │
                         │             │                                            │
                         │             ▼                                            │
                         │  ┌──────────────────┐                                    │
                         │  │  PostgreSQL 16    │                                    │
                         │  │  Alpine           │                                    │
                         │  │  Port 5432        │                                    │
                         │  └────────┬─────────┘                                    │
                         │           │                                              │
                         │       ┌───┴────┐                                         │
                         │       │  PVCs  │                                         │
                         │       └───┬────┘                                         │
                         └───────────┼──────────────────────────────────────────────┘
                                     ▼
                             ┌───────────────┐
                             │  Host-Storage  │
                             │  /mnt/data/    │
                             └───────────────┘
```

### Ressourcenübersicht

| Ressource | Typ | Replicas | Image |
|-----------|-----|----------|-------|
| PostgreSQL | Deployment | 1 | `postgres:16-alpine` |
| API | Deployment | 2 | `bombasticifccluster-api:latest` |
| Frontend | Deployment | 1 | `bombasticifccluster-frontend:latest` |

---

## Voraussetzungen

### Für Windows (Lokale Entwicklung)

| Tool | Version | Download |
|------|---------|----------|
| Docker Desktop | 4.x+ | [docker.com](https://www.docker.com/get-started) |
| Minikube | 1.32+ | [minikube.sigs.k8s.io](https://minikube.sigs.k8s.io/docs/start/) |
| kubectl | 1.28+ | [kubernetes.io](https://kubernetes.io/docs/tasks/tools/) |
| .NET SDK | 8.0 | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) |

### Für Ubuntu Server (Produktion)

| Tool | Version | Installationsbefehl |
|------|---------|---------------------|
| Docker | 24.x+ | `sudo apt install docker-ce` |
| K3s | 1.28+ | `curl -sfL https://get.k3s.io \| sh -` |

---

## Cluster-Komponenten

### Namespace

**Datei:** `namespace.yaml`

Alle Ressourcen des Projekts laufen im dedizierten Namespace `bombasticifccluster`. Dies bietet:

- **Isolation** — Trennung von anderen Workloads im selben Cluster
- **Ressourcenkontrolle** — Möglichkeit für Resource Quotas
- **Übersichtlichkeit** — Alle Komponenten unter einem Namen gebündelt

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: bombasticifccluster
  labels:
    name: bombasticifccluster
```

### Secrets & ConfigMaps

#### Secrets (`secrets.yaml`)

Enthält sensitive Konfigurationsdata als Kubernetes-Secrets:

| Key | Beschreibung | Verwendet von |
|-----|-------------|---------------|
| `postgres-user` | PostgreSQL-Benutzername | PostgreSQL Pod |
| `postgres-password` | PostgreSQL-Passwort | PostgreSQL Pod |
| `postgres-db` | Datenbankname | PostgreSQL Pod |
| `connection-string` | Vollständiger Connection String | API Pods |
| `jwt-secret` | JWT Token Secret für Authentifizierung | API Pods |

> **Sicherheitshinweis:** Die `secrets.yaml` enthält Klartext-Werte (`stringData`) und sollte in einer Produktionsumgebung durch einen Secret Manager (z. B. HashiCorp Vault, Sealed Secrets) ersetzt werden. Die Datei sollte **nicht** in ein öffentliches Git-Repository committed werden.

#### ConfigMap (`configmap.yaml`)

Enthält nicht-sensitive Konfigurationsparameter:

| Key | Wert | Beschreibung |
|-----|------|-------------|
| `POSTGRES_DB` | `bombasticifcdb` | Datenbankname |
| `POSTGRES_USER` | `postgres` | Benutzername |

### Persistent Volumes

**Datei:** `persistent-volumes.yaml`

Das Cluster verwendet `hostPath`-basierte PersistentVolumes mit der StorageClass `manual`:

| PV / PVC | Kapazität | Access Mode | Host-Pfad | Zweck |
|----------|-----------|-------------|-----------|-------|
| `postgres-pv` / `postgres-pvc` | 10 Gi | ReadWriteOnce | `/mnt/data/postgres` | PostgreSQL-Datenbankdateien |
| `storage-pv` / `storage-pvc` | 50 Gi | ReadWriteMany | `/mnt/data/storage` | IFC-Datei-Uploads & konvertierte Modelle |

**Reclaim Policy:** `Retain` — Daten bleiben auch nach Löschen des PVC erhalten.

```
/mnt/data/
├── postgres/       # PostgreSQL-Daten (10 Gi, ReadWriteOnce)
│   └── pgdata/
└── storage/        # IFC-Dateien (50 Gi, ReadWriteMany)
    └── *.ifc, *.xkt, ...
```

> **Hinweis:** `hostPath`-Volumes sind an einen einzelnen Node gebunden. Für Multi-Node-Cluster in der Produktion sollten NFS, Longhorn oder ein Cloud-StorageProvider verwendet werden.

### PostgreSQL-Deployment

**Datei:** `postgres-deployment.yaml`

PostgreSQL 16 (Alpine) als einzelnes Replica mit Health Checks:

| Eigenschaft | Wert |
|-------------|------|
| Image | `postgres:16-alpine` |
| Replicas | 1 |
| Port | 5432 |
| CPU Request/Limit | 250m / 500m |
| Memory Request/Limit | 256Mi / 512Mi |
| Volume Mount | `/var/lib/postgresql/data` → `postgres-pvc` |

**Health Checks:**

| Probe | Befehl | Intervall | Initial Delay |
|-------|--------|-----------|---------------|
| Liveness | `pg_isready -U postgres` | 10s | 30s |
| Readiness | `pg_isready -U postgres` | 10s | 5s |

**Services:**

| Service | Typ | Port | Beschreibung |
|---------|-----|------|-------------|
| `postgres-service` | ClusterIP | 5432 | Interne Kommunikation (API → DB) |
| `postgres-nodeport` | NodePort | 30432 | Externer Zugriff (Entwicklung, DB-Tools) |

### API-Deployment

**Datei:** `api-deployment.yaml`

Die .NET 8 REST-API wird mit 2 Replicas deployed für grundlegende Hochverfügbarkeit:

| Eigenschaft | Wert |
|-------------|------|
| Image | `bombasticifccluster-api:latest` |
| Image Pull Policy | `Never` (lokales Image) |
| Replicas | 2 |
| Container Port | 8080 |
| CPU Request/Limit | 250m / 500m |
| Memory Request/Limit | 256Mi / 512Mi |
| Volume Mount | `/data/storage` → `storage-pvc` |

**Umgebungsvariablen:**

| Variable | Quelle | Wert |
|----------|--------|------|
| `ASPNETCORE_ENVIRONMENT` | Direkt | `Production` |
| `ConnectionStrings__DefaultConnection` | Secret | Connection String |
| `JwtSettings__Secret` | Secret | JWT Token Secret |
| `StoragePath` | Direkt | `/data/storage` |

**Health Checks:**

| Probe | Endpunkt | Intervall | Initial Delay |
|-------|----------|-----------|---------------|
| Liveness | `GET /health` (Port 8080) | 10s | 30s |
| Readiness | `GET /health` (Port 8080) | 5s | 10s |

**Services:**

| Service | Typ | Port | Target Port | Beschreibung |
|---------|-----|------|-------------|-------------|
| `api-service` | ClusterIP | 80 | 8080 | Interne Kommunikation (Ingress → API) |
| `api-nodeport` | NodePort | 80 | 8080 (NodePort: 30080) | Direkter externer Zugriff |

### Frontend-Deployment

**Datei:** `frontend-deployment.yaml`

Die Vue.js Frontend-Anwendung wird über nginx bereitgestellt:

| Eigenschaft | Wert |
|-------------|------|
| Image | `bombasticifccluster-frontend:latest` |
| Image Pull Policy | `Never` (lokales Image) |
| Replicas | 1 |
| Port | 80 |
| CPU Request/Limit | 50m / 100m |
| Memory Request/Limit | 64Mi / 128Mi |

**Health Checks:**

| Probe | Endpunkt | Intervall | Initial Delay |
|-------|----------|-----------|---------------|
| Liveness | `GET /` | 10s | 10s |
| Readiness | `GET /` | 5s | 5s |

**Services:**

| Service | Typ | Port | Beschreibung |
|---------|-----|------|-------------|
| `frontend-service` | ClusterIP | 80 | Interne Kommunikation (Ingress → Frontend) |

### Ingress

**Datei:** `ingress.yaml`

NGINX-Ingress zur Weiterleitung von externem HTTP-Traffic:

| Eigenschaft | Wert |
|-------------|------|
| Ingress Class | `nginx` |
| Host | `bombasticifccluster.local` |
| Max Body Size | 500 MB (für grosse IFC-Dateien) |
| SSL Redirect | Deaktiviert |

**Routing-Regeln:**

| Pfad | Service | Port | Beschreibung |
|------|---------|------|-------------|
| `/` | `frontend-service` | 80 | Vue.js Frontend (Root) |
| `/api` | `api-service` | 80 | REST API Endpoints |
| `/health` | `api-service` | 80 | Health Check Endpoint |

---

## Docker-Images

### API - Multi-Stage Build (`Dockerfile`)

Das API-Image wird in drei Stufen gebaut:

```
┌─────────────────────────────────────────────────────────┐
│  Stage 1: build (mcr.microsoft.com/dotnet/sdk:8.0)      │
│  ├── COPY *.csproj → dotnet restore                     │
│  └── COPY src/ → dotnet build -c Release                │
├─────────────────────────────────────────────────────────┤
│  Stage 2: publish                                        │
│  └── dotnet publish -c Release                           │
├─────────────────────────────────────────────────────────┤
│  Stage 3: final (mcr.microsoft.com/dotnet/aspnet:8.0)   │
│  ├── mkdir /data/storage                                 │
│  ├── COPY --from=publish /app/publish .                  │
│  └── ENTRYPOINT ["dotnet", "BombasticIFC.API.dll"]       │
└─────────────────────────────────────────────────────────┘
```

**Ergebnis:** Schlankes Runtime-Image (~200 MB) ohne SDK-Overhead (~900 MB).

### Frontend - Build (`frontend/Dockerfile`)

Das Frontend-Image wird mit nginx bereitgestellt:

```
┌─────────────────────────────────────────────────────────┐
│  Stage 1: build (node:lts-alpine)                       │
│  ├── npm install                                         │
│  └── npm run build → dist/                               │
├─────────────────────────────────────────────────────────┤
│  Stage 2: production (nginx:alpine)                     │
│  ├── COPY --from=build dist/ → /usr/share/nginx/html/   │
│  └── COPY nginx.conf → /etc/nginx/conf.d/default.conf   │
└─────────────────────────────────────────────────────────┘
```

**Ergebnis:** Sehr schlankes nginx-Image (~20-30 MB) mit statischen Dateien.

### Images bauen

```bash
# API-Image (aus dem Projekt-Root)
docker build -t bombasticifccluster-api:latest .

# Frontend-Image (aus dem frontend-Verzeichnis)
docker build -t bombasticifccluster-frontend:latest ./frontend
```

---

## Deployment-Ablauf

### Option A: Windows + Minikube

Automatisiert über `deploy.ps1`:

```powershell
.\deploy.ps1
```

**Ablauf des Scripts:**

```
1. Prüfe Minikube & kubectl Installation
2. Starte Minikube (--cpus=4 --memory=8192 --disk-size=50g)
3. Aktiviere Addons:
   ├── ingress
   ├── storage-provisioner
   ├── default-storageclass
   └── metrics-server
4. Konfiguriere Docker-Umgebung → Minikube Docker Daemon
5. Baue Docker-Image (bombasticifccluster-api:latest)
6. Erstelle Storage-Verzeichnisse auf Minikube-Node:
   ├── /mnt/data/postgres
   └── /mnt/data/storage
7. Kubernetes-Manifeste anwenden:
   ├── namespace.yaml
   ├── secrets.yaml
   ├── configmap.yaml
   ├── persistent-volumes.yaml
   ├── postgres-deployment.yaml  → Warten auf Ready
   ├── api-deployment.yaml       → Warten auf Ready
   ├── frontend-deployment.yaml  → Warten auf Ready
   └── ingress.yaml
8. Ausgabe der Zugangs-URLs
```

### Option B: Ubuntu Server + K3s

#### 1. K3s installieren

```bash
curl -sfL https://get.k3s.io | sh -
mkdir -p ~/.kube
sudo cp /etc/rancher/k3s/k3s.yaml ~/.kube/config
sudo chown $USER:$USER ~/.kube/config
export KUBECONFIG=~/.kube/config
```

#### 2. Docker-Images bauen und in K3s importieren

```bash
cd ~/BombasticIFCcluster

# API-Image bauen
docker build -t bombasticifccluster-api:latest .

# Frontend-Image bauen
docker build -t bombasticifccluster-frontend:latest ./frontend

# In K3s containerd importieren
docker save bombasticifccluster-api:latest | sudo k3s ctr images import -
docker save bombasticifccluster-frontend:latest | sudo k3s ctr images import -
```

#### 3. Storage-Verzeichnisse erstellen

```bash
sudo mkdir -p /mnt/data/postgres
sudo mkdir -p /mnt/data/storage
sudo chmod -R 777 /mnt/data
```

#### 4. Manifeste anwenden

```bash
kubectl apply -f kubernetes/namespace.yaml
kubectl apply -f kubernetes/secrets.yaml
kubectl apply -f kubernetes/configmap.yaml
kubectl apply -f kubernetes/persistent-volumes.yaml
kubectl apply -f kubernetes/postgres-deployment.yaml
kubectl wait --for=condition=ready pod -l app=postgres -n bombasticifccluster --timeout=300s
kubectl apply -f kubernetes/api-deployment.yaml
kubectl wait --for=condition=ready pod -l app=bombasticifccluster-api -n bombasticifccluster --timeout=300s
kubectl apply -f kubernetes/frontend-deployment.yaml
kubectl wait --for=condition=ready pod -l app=bombasticifccluster-frontend -n bombasticifccluster --timeout=300s
kubectl apply -f kubernetes/ingress.yaml
```

> **Hinweis für K3s:** Der Ingress-Controller ist Traefik (nicht nginx). Die `ingressClassName` in `ingress.yaml` muss ggf. auf `traefik` geändert werden.

---

## Netzwerk & Zugriff

### Zugangspunkte

| Zugriff | URL / Adresse | Voraussetzung |
|---------|---------------|---------------|
| Frontend via Ingress | `http://bombasticifccluster.local` | hosts-Eintrag nötig |
| API via Ingress | `http://bombasticifccluster.local/api` | hosts-Eintrag nötig |
| Health Check via Ingress | `http://bombasticifccluster.local/health` | hosts-Eintrag nötig |
| API via NodePort | `http://<NODE-IP>:30080` | Direkt erreichbar |
| Swagger UI | `http://<NODE-IP>:30080/swagger` | Direkt erreichbar |
| Health Check (NodePort) | `http://<NODE-IP>:30080/health` | Direkt erreichbar |
| PostgreSQL extern | `<NODE-IP>:30432` | Direkt erreichbar |

### hosts-Datei konfigurieren

**Windows:** `C:\Windows\System32\drivers\etc\hosts`
**Linux/macOS:** `/etc/hosts`

```
<NODE-IP>  bombasticifccluster.local
```

Node-IP ermitteln:

```powershell
# Minikube
minikube ip

# K3s / Ubuntu
hostname -I | awk '{print $1}'
```

### Internes Netzwerk (Pod-zu-Pod)

| Quell-Pod | Ziel-Service | DNS-Name | Port |
|-----------|-------------|----------|------|
| API | PostgreSQL | `postgres-service` | 5432 |
| Ingress | API | `api-service` | 80 (Target: 8080) |
| Ingress | Frontend | `frontend-service` | 80 |

Kubernetes DNS löst Service-Namen automatisch innerhalb des Namespaces auf:
```
postgres-service.bombasticifccluster.svc.cluster.local
api-service.bombasticifccluster.svc.cluster.local
```

---

## Monitoring & Debugging

### Cluster-Status

```bash
# Alle Ressourcen im Namespace
kubectl get all -n bombasticifccluster

# Detaillierter Pod-Status
kubectl get pods -n bombasticifccluster -o wide

# Events (nützlich bei Fehlern)
kubectl get events -n bombasticifccluster --sort-by=.metadata.creationTimestamp
```

### Logs

```bash
# API-Logs (live)
kubectl logs -f deployment/api-deployment -n bombasticifccluster

# Frontend-Logs (live)
kubectl logs -f deployment/frontend-deployment -n bombasticifccluster

# PostgreSQL-Logs (live)
kubectl logs -f deployment/postgres-deployment -n bombasticifccluster

# Logs eines spezifischen Pods
kubectl logs <pod-name> -n bombasticifccluster

# Letzte 100 Zeilen
kubectl logs deployment/api-deployment -n bombasticifccluster --tail=100
```

### Debugging

```bash
# In einen Pod einsteigen (Frontend)
kubectl exec -it deployment/frontend-deployment -n bombasticifccluster -- /bin/sh

# In einen Pod einsteigen (API)
kubectl exec -it deployment/api-deployment -n bombasticifccluster -- /bin/bash

# In einen Pod einsteigen (PostgreSQL)
kubectl exec -it deployment/postgres-deployment -n bombasticifccluster -- psql -U postgres -d bombasticifcdb

# Pod-Details & Events
kubectl describe pod <pod-name> -n bombasticifccluster

# Service-Endpoints prüfen
kubectl get endpoints -n bombasticifccluster
```

### Dashboard

```bash
# Minikube
minikube dashboard

# K3s (Kubernetes Dashboard separat installieren)
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.7.0/aio/deploy/recommended.yaml
```

### Häufige Fehler

| Symptom | Mögliche Ursache | Lösung |
|---------|------------------|--------|
| Pod `CrashLoopBackOff` | Datenbankverbindung fehlgeschlagen | `kubectl logs <pod>`, Secret prüfen |
| Pod `Pending` | PVC nicht gebunden | `kubectl get pv,pvc -n bombasticifccluster` |
| Pod `ImagePullBackOff` | Image nicht lokal vorhanden | `imagePullPolicy: Never` prüfen, Image neu bauen |
| Ingress kein Traffic | Ingress Controller fehlt | `minikube addons enable ingress` |
| `pg_isready` fehlschlägt | PostgreSQL nicht gestartet | Logs prüfen, Volume-Berechtigungen kontrollieren |

---

## Skalierung

### API horizontal skalieren

```bash
# Auf 4 Replicas erhöhen
kubectl scale deployment api-deployment --replicas=4 -n bombasticifccluster

# Zurück auf 2
kubectl scale deployment api-deployment --replicas=2 -n bombasticifccluster
```

### Autoscaling (HPA)

```bash
# Autoscaler aktivieren (2–6 Pods, Ziel: 70% CPU)
kubectl autoscale deployment api-deployment \
  --min=2 --max=6 --cpu-percent=70 \
  -n bombasticifccluster

# HPA-Status
kubectl get hpa -n bombasticifccluster
```

> **Voraussetzung:** Metrics Server muss aktiv sein (`minikube addons enable metrics-server`).

### Ressourcen-Übersicht

| Komponente | CPU Request | CPU Limit | Memory Request | Memory Limit |
|------------|-------------|-----------|----------------|--------------|
| PostgreSQL | 250m | 500m | 256Mi | 512Mi |
| API (pro Pod) | 250m | 500m | 256Mi | 512Mi |
| Frontend | 50m | 100m | 64Mi | 128Mi |
| **Total (Default)** | **800m** | **1600m** | **832Mi** | **1664Mi** |
| **Total (2 API Replicas)** | **1050m** | **2100m** | **1088Mi** | **2176Mi** |

---

## Backup & Wiederherstellung

### PostgreSQL-Backup

```bash
# Backup erstellen
kubectl exec deployment/postgres-deployment -n bombasticifccluster -- \
  pg_dump -U postgres -d bombasticifcdb > backup_$(date +%Y%m%d_%H%M%S).sql

# Backup wiederherstellen
kubectl exec -i deployment/postgres-deployment -n bombasticifccluster -- \
  psql -U postgres -d bombasticifcdb < backup_20260228_120000.sql
```

### Storage-Backup

```bash
# IFC-Dateien sichern (auf dem Host / Minikube-Node)
# Minikube:
minikube ssh "tar czf /tmp/storage-backup.tar.gz /mnt/data/storage"
minikube cp minikube:/tmp/storage-backup.tar.gz ./storage-backup.tar.gz

# Ubuntu Server:
tar czf ~/backups/storage-backup-$(date +%Y%m%d).tar.gz /mnt/data/storage
```

---

## Cluster zurücksetzen

### Nur Deployments löschen (Daten behalten)

```bash
kubectl delete -f kubernetes/ingress.yaml
kubectl delete -f kubernetes/frontend-deployment.yaml
kubectl delete -f kubernetes/api-deployment.yaml
kubectl delete -f kubernetes/postgres-deployment.yaml
```

### Vollständig zurücksetzen (inkl. Daten)

```bash
# Alle Ressourcen im Namespace löschen
kubectl delete namespace bombasticifccluster

# Persistent Volumes manuell löschen
kubectl delete pv postgres-pv storage-pv
```

### Minikube komplett entfernen

```powershell
minikube stop
minikube delete
```

### K3s deinstallieren (Ubuntu)

```bash
/usr/local/bin/k3s-uninstall.sh
```

---

## Manifest-Übersicht

Anwendungsreihenfolge der Manifeste:

```
kubernetes/
├── 1. namespace.yaml            # Namespace erstellen
├── 2. secrets.yaml              # Datenbankzugangsdaten + JWT Secret
├── 3. configmap.yaml            # PostgreSQL-Konfiguration
├── 4. persistent-volumes.yaml   # PV + PVC für DB und Storage
├── 5. postgres-deployment.yaml  # PostgreSQL + Services
├── 6. api-deployment.yaml       # .NET API + Services
├── 7. frontend-deployment.yaml  # Vue.js Frontend + Service
└── 8. ingress.yaml              # HTTP-Routing (Frontend + API)
```

---

## Visualisierung

Eine detaillierte PlantUML-Architekturdiagramm ist verfügbar unter:
- **Datei:** [architecture.puml](architecture.puml)
- **Beschreibung:** Vollständige Darstellung der Kubernetes-Cluster-Architektur mit allen Komponenten, Services, Ingress-Routing, Persistent Storage und Ressourcenverteilung

Um das Diagramm zu rendern, verwenden Sie einen PlantUML-Viewer oder Online-Tools wie [PlantUML Online Editor](http://www.plantuml.com/plantuml).

---

*Erstellt am 28.02.2026 — Aktualisiert am 07.03.2026 — BombasticIFC Cluster v1.1*
```

---

### 2. kubernetes/README.md

```markdown
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
```

---

### 3. kubernetes/architecture.puml

```
@startuml BombasticIFC Kubernetes Architecture
!define KUBERNETES_COLOR #326CE5
!define DATABASE_COLOR #336791
!define API_COLOR #512BD4
!define FRONTEND_COLOR #42B883

skinparam backgroundColor #FFFFFF
skinparam defaultFontName Arial
skinparam shadowing false
skinparam ArrowColor #666666

title BombasticIFC Cluster - Kubernetes Architecture

actor "Client\n(Browser)" as client #lightblue

package "External Access" {
  component "NodePort\n:30080" as nodeport_api #lightgray
  component "NodePort\n:30432" as nodeport_db #lightgray
}

package "Kubernetes Cluster" <<Namespace: bombasticifccluster>> {
  
  package "Ingress Layer" {
    component "Ingress Controller\n(nginx / traefik)" as ingress KUBERNETES_COLOR
    note right of ingress
      Host: bombasticifccluster.local
      Max Body Size: 500MB
      
      Routes:
      • / → frontend-service
      • /api → api-service
      • /health → api-service
    end note
  }

  package "Frontend Layer" {
    component "frontend-service\n(ClusterIP :80)" as frontend_svc KUBERNETES_COLOR
    
    frame "Frontend Pods" {
      component "Frontend Pod 1\n(Vue.js + nginx)\nPort: 80" as frontend_pod1 FRONTEND_COLOR
    }
    
    note right of frontend_pod1
      Image: bombasticifccluster-frontend:latest
      Replicas: 1
      Resources:
      • CPU: 50m / 100m
      • Memory: 64Mi / 128Mi
    end note
  }

  package "API Layer" {
    component "api-service\n(ClusterIP :80)" as api_svc KUBERNETES_COLOR
    
    frame "API Pods" {
      component "API Pod 1\n(.NET 8)\nPort: 8080" as api_pod1 API_COLOR
      component "API Pod 2\n(.NET 8)\nPort: 8080" as api_pod2 API_COLOR
    }
    
    note right of api_pod1
      Image: bombasticifccluster-api:latest
      Replicas: 2
      Resources:
      • CPU: 250m / 500m
      • Memory: 256Mi / 512Mi
      
      Environment:
      • ASPNETCORE_ENVIRONMENT: Production
      • ConnectionStrings (from Secret)
      • JwtSettings__Secret (from Secret)
      • StoragePath: /data/storage
    end note
  }

  package "Database Layer" {
    component "postgres-service\n(ClusterIP :5432)" as db_svc KUBERNETES_COLOR
    
    component "PostgreSQL Pod\n(PostgreSQL 16-alpine)\nPort: 5432" as db_pod DATABASE_COLOR
    
    note right of db_pod
      Image: postgres:16-alpine
      Replicas: 1
      Resources:
      • CPU: 250m / 500m
      • Memory: 256Mi / 512Mi
      
      Health Checks:
      • Liveness: pg_isready -U postgres
      • Readiness: pg_isready -U postgres
    end note
  }

  package "Configuration" {
    storage "Secrets" as secrets #yellow
    storage "ConfigMap" as configmap #lightyellow
    
    note right of secrets
      • postgres-user
      • postgres-password
      • postgres-db
      • connection-string
      • jwt-secret
    end note
  }

  package "Persistent Storage" {
    database "postgres-pvc\n(10Gi, RWO)" as pvc_db
    database "storage-pvc\n(50Gi, RWX)" as pvc_storage
    
    storage "postgres-pv\n/mnt/data/postgres" as pv_db #lightgray
    storage "storage-pv\n/mnt/data/storage" as pv_storage #lightgray
  }
}

package "Host Storage" {
  folder "/mnt/data/postgres\n(PostgreSQL Data)" as host_db #lightgray
  folder "/mnt/data/storage\n(IFC Files)" as host_storage #lightgray
}

' Client connections
client --> nodeport_api : "HTTP :30080\n(Direct Access)"
client --> nodeport_db : "TCP :30432\n(DB Tools)"
client --> ingress : "HTTP\nbombasticifccluster.local"

' Ingress routing
ingress --> frontend_svc : "/ (root)"
ingress --> api_svc : "/api, /health"

' NodePort connections
nodeport_api --> api_svc
nodeport_db --> db_svc

' Service to Pod connections
frontend_svc --> frontend_pod1
api_svc --> api_pod1
api_svc --> api_pod2
db_svc --> db_pod

' API to Database connection
api_pod1 ..> db_svc : "PostgreSQL\nConnection"
api_pod2 ..> db_svc : "PostgreSQL\nConnection"

' Configuration connections
api_pod1 ..> secrets : "read"
api_pod2 ..> secrets : "read"
db_pod ..> secrets : "read"
db_pod ..> configmap : "read"

' Storage connections
db_pod --> pvc_db : "mount\n/var/lib/postgresql/data"
api_pod1 --> pvc_storage : "mount\n/data/storage"
api_pod2 --> pvc_storage : "mount\n/data/storage"

pvc_db --> pv_db : "bound"
pvc_storage --> pv_storage : "bound"

pv_db --> host_db : "hostPath"
pv_storage --> host_storage : "hostPath"

legend right
  |= Component |= Type |= Description |
  | <back:FRONTEND_COLOR>   </back> Frontend | Vue.js + nginx | Web UI |
  | <back:API_COLOR>   </back> API | .NET 8 | REST API |
  | <back:DATABASE_COLOR>   </back> Database | PostgreSQL 16 | Data persistence |
  | <back:KUBERNETES_COLOR>   </back> Service | Kubernetes | Load balancing |
  
  **Resource Totals (Default):**
  • CPU Request: 550m (2x250m + 50m)
  • CPU Limit: 1100m (2x500m + 100m)
  • Memory Request: 576Mi (2x256Mi + 64Mi)
  • Memory Limit: 1152Mi (2x512Mi + 128Mi)
  • + PostgreSQL: 250m/500m CPU, 256Mi/512Mi RAM
  
  **Storage:**
  • postgres-pv: 10Gi (ReadWriteOnce)
  • storage-pv: 50Gi (ReadWriteMany)
endlegend

@enduml
```

---

### 4. kubernetes/api-deployment.yaml

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: api-deployment
  namespace: bombasticifccluster
  labels:
    app: bombasticifccluster-api
spec:
  replicas: 2
  selector:
    matchLabels:
      app: bombasticifccluster-api
  template:
    metadata:
      labels:
        app: bombasticifccluster-api
    spec:
      containers:
      - name: api
        image: bombasticifccluster-api:latest
        imagePullPolicy: Never
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: bombasticifccluster-secrets
              key: connection-string
        - name: JwtSettings__Secret
          valueFrom:
            secretKeyRef:
              name: bombasticifccluster-secrets
              key: jwt-secret
        - name: StoragePath
          value: "/data/storage"
        volumeMounts:
        - name: storage-volume
          mountPath: /data/storage
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5
      volumes:
      - name: storage-volume
        persistentVolumeClaim:
          claimName: storage-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: api-service
  namespace: bombasticifccluster
  labels:
    app: bombasticifccluster-api
spec:
  type: ClusterIP
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP
  selector:
    app: bombasticifccluster-api
---
apiVersion: v1
kind: Service
metadata:
  name: api-nodeport
  namespace: bombasticifccluster
  labels:
    app: bombasticifccluster-api
spec:
  type: NodePort
  ports:
  - port: 80
    targetPort: 8080
    nodePort: 30080
    protocol: TCP
  selector:
    app: bombasticifccluster-api
```

---

### 5. kubernetes/frontend-deployment.yaml

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: frontend-deployment
  namespace: bombasticifccluster
  labels:
    app: bombasticifccluster-frontend
spec:
  replicas: 1
  selector:
    matchLabels:
      app: bombasticifccluster-frontend
  template:
    metadata:
      labels:
        app: bombasticifccluster-frontend
    spec:
      containers:
      - name: frontend
        image: bombasticifccluster-frontend:latest
        imagePullPolicy: Never
        ports:
        - containerPort: 80
        resources:
          requests:
            memory: "64Mi"
            cpu: "50m"
          limits:
            memory: "128Mi"
            cpu: "100m"
        livenessProbe:
          httpGet:
            path: /
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: frontend-service
  namespace: bombasticifccluster
  labels:
    app: bombasticifccluster-frontend
spec:
  type: ClusterIP
  ports:
  - port: 80
    targetPort: 80
    protocol: TCP
  selector:
    app: bombasticifccluster-frontend
```

---

### 6. kubernetes/postgres-deployment.yaml

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: postgres-deployment
  namespace: bombasticifccluster
  labels:
    app: postgres
spec:
  replicas: 1
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
      - name: postgres
        image: postgres:16-alpine
        ports:
        - containerPort: 5432
        env:
        - name: POSTGRES_USER
          valueFrom:
            secretKeyRef:
              name: bombasticifccluster-secrets
              key: postgres-user
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: bombasticifccluster-secrets
              key: postgres-password
        - name: POSTGRES_DB
          valueFrom:
            secretKeyRef:
              name: bombasticifccluster-secrets
              key: postgres-db
        volumeMounts:
        - name: postgres-storage
          mountPath: /var/lib/postgresql/data
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          exec:
            command:
            - pg_isready
            - -U
            - postgres
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          exec:
            command:
            - pg_isready
            - -U
            - postgres
          initialDelaySeconds: 5
          periodSeconds: 10
      volumes:
      - name: postgres-storage
        persistentVolumeClaim:
          claimName: postgres-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: postgres-service
  namespace: bombasticifccluster
  labels:
    app: postgres
spec:
  type: ClusterIP
  ports:
  - port: 5432
    targetPort: 5432
    protocol: TCP
  selector:
    app: postgres
---
apiVersion: v1
kind: Service
metadata:
  name: postgres-nodeport
  namespace: bombasticifccluster
  labels:
    app: postgres
spec:
  type: NodePort
  ports:
  - port: 5432
    targetPort: 5432
    nodePort: 30432
    protocol: TCP
  selector:
    app: postgres
```

---

### 7. kubernetes/ingress.yaml

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: bombasticifccluster-ingress
  namespace: bombasticifccluster
  annotations:
    nginx.ingress.kubernetes.io/ssl-redirect: "false"
    nginx.ingress.kubernetes.io/proxy-body-size: "500m"
spec:
  ingressClassName: nginx
  rules:
  - http:
      paths:
      - path: /api
        pathType: Prefix
        backend:
          service:
            name: api-service
            port:
              number: 80
      - path: /health
        pathType: Exact
        backend:
          service:
            name: api-service
            port:
              number: 80
      - path: /
        pathType: Prefix
        backend:
          service:
            name: frontend-service
            port:
              number: 80
```

---

### 8. kubernetes/configmap.yaml

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: postgres-config
  namespace: bombasticifccluster
data:
  POSTGRES_DB: bombasticifcdb
  POSTGRES_USER: postgres
```

---

### 9. kubernetes/secrets.yaml

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: bombasticifccluster-secrets
  namespace: bombasticifccluster
type: Opaque
stringData:
  postgres-user: "postgres"
  postgres-password: "05W4V1KEUHpeQQrnfme9iizbyR+rO3II"
  postgres-db: "bombasticifcdb"
  connection-string: "Host=postgres-service;Port=5432;Database=bombasticifcdb;Username=postgres;Password=05W4V1KEUHpeQQrnfme9iizbyR+rO3II"
  jwt-secret: "sWTvwXxdR1RQEkausrAVXKkn5sqW0YnUH5pp2PGZhPlrs9nJBCZS+HZUtLvZUUOc"
```

---

### 10. kubernetes/namespace.yaml

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: bombasticifccluster
  labels:
    name: bombasticifccluster
```

---

### 11. kubernetes/persistent-volumes.yaml

```yaml
apiVersion: v1
kind: PersistentVolume
metadata:
  name: postgres-pv
  namespace: bombasticifccluster
spec:
  capacity:
    storage: 10Gi
  accessModes:
    - ReadWriteOnce
  persistentVolumeReclaimPolicy: Retain
  storageClassName: manual
  hostPath:
    path: "/mnt/data/postgres"
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: postgres-pvc
  namespace: bombasticifccluster
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 10Gi
  storageClassName: manual
---
apiVersion: v1
kind: PersistentVolume
metadata:
  name: storage-pv
  namespace: bombasticifccluster
spec:
  capacity:
    storage: 50Gi
  accessModes:
    - ReadWriteMany
  persistentVolumeReclaimPolicy: Retain
  storageClassName: manual
  hostPath:
    path: "/mnt/data/storage"
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: storage-pvc
  namespace: bombasticifccluster
spec:
  accessModes:
    - ReadWriteMany
  resources:
    requests:
      storage: 50Gi
  storageClassName: manual
```

---

### 12. docker-compose.yml

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    container_name: bombasticifcluster-db
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: 05W4V1KEUHpeQQrnfme9iizbyR+rO3II
      POSTGRES_DB: bombasticifcdb
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - bombasticifccluster-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

  api:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: bombasticifcluster-api
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=bombasticifcdb;Username=postgres;Password=05W4V1KEUHpeQQrnfme9iizbyR+rO3II
      - StoragePath=/data/storage
    ports:
      - "5000:80"
    volumes:
      - storage-data:/data/storage
    depends_on:
      postgres:
        condition: service_healthy
    networks:
      - bombasticifccluster-network
    restart: unless-stopped

volumes:
  postgres-data:
  storage-data:

networks:
  bombasticifccluster-network:
    driver: bridge
```

---

### 13. Dockerfile

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY BombasticIFCcluster.sln ./
COPY src/BombasticIFC.Domain/BombasticIFC.Domain.csproj ./src/BombasticIFC.Domain/
COPY src/BombasticIFC.Application/BombasticIFC.Application.csproj ./src/BombasticIFC.Application/
COPY src/BombasticIFC.Infrastructure/BombasticIFC.Infrastructure.csproj ./src/BombasticIFC.Infrastructure/
COPY src/BombasticIFC.API/BombasticIFC.API.csproj ./src/BombasticIFC.API/
COPY src/BombasticIFC.Shared/BombasticIFC.Shared.csproj ./src/BombasticIFC.Shared/

# Restore dependencies
RUN dotnet restore

# Copy everything else
COPY src/ ./src/

# Build
WORKDIR /src/src/BombasticIFC.API
RUN dotnet build -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create storage directory
RUN mkdir -p /data/storage /app/seed-data

COPY --from=publish /app/publish .
COPY src/BombasticIFC.API/data/storage/samples/Duplex.xkt /app/seed-data/Duplex.xkt

EXPOSE 80
EXPOSE 443

ENTRYPOINT ["dotnet", "BombasticIFC.API.dll"]
```

---

### 14. frontend/Dockerfile

```dockerfile
# Build stage
FROM node:20-alpine AS build
WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm ci --legacy-peer-deps

COPY . .
RUN npm run build

# Production stage
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

---

### 15. setupKubernetes.sh

```bash```bash```bash
#!/usr/bin/env bash
# BombasticIFC Cluster — Ubuntu Server Setup (Phase 0–2)
# Run as a regular user with sudo privileges on a vanilla Ubuntu 22.04/24.04 server.
# Usage: chmod +x setupKubernetes.sh && ./setupKubernetes.sh

set -euo pipefail

MINIKUBE_CPUS=2
MINIKUBE_MEMORY=4096
MINIKUBE_DISK="50g"
API_IMAGE="bombasticifccluster-api:latest"
DOTNET_VERSION="8.0"

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

# --- .NET SDK ---
if dotnet --version 2>/dev/null | grep -q "^${DOTNET_VERSION}"; then
    ok ".NET SDK ${DOTNET_VERSION} is already installed ($(dotnet --version))"
else
    info "Installing .NET SDK ${DOTNET_VERSION}..."
    sudo apt-get install -y -qq dotnet-sdk-${DOTNET_VERSION}
    ok ".NET SDK installed ($(dotnet --version))"
fi

# --- dotnet-ef CLI tool ---
export PATH="$PATH:$HOME/.dotnet/tools"
if command -v dotnet-ef &>/dev/null; then
    ok "dotnet-ef is already installed ($(dotnet ef --version | head -1))"
else
    info "Installing dotnet-ef tool..."
    dotnet tool install --global dotnet-ef
    ok "dotnet-ef installed ($(dotnet ef --version | head -1))"
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
# Phase 1 — EF Core Migrations
# ──────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MIGRATIONS_DIR="${SCRIPT_DIR}/src/BombasticIFC.Infrastructure/Persistence/Migrations"

if [ -d "${MIGRATIONS_DIR}" ] && [ "$(ls -A "${MIGRATIONS_DIR}")" ]; then
    ok "EF Core migrations already exist — skipping"
else
    info "Restoring NuGet packages..."
    dotnet restore "${SCRIPT_DIR}"

    info "Creating initial EF Core migration (InitialCreate)..."
    dotnet ef migrations add InitialCreate \
        --project "${SCRIPT_DIR}/src/BombasticIFC.Infrastructure" \
        --startup-project "${SCRIPT_DIR}/src/BombasticIFC.API"
    ok "Migration 'InitialCreate' created"
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
echo "    kubectl apply -f kubernetes/persistent-volumes.yaml"
echo "    kubectl apply -f kubernetes/postgres-deployment.yaml"
echo "    kubectl apply -f kubernetes/api-deployment.yaml"
echo "    kubectl apply -f kubernetes/ingress.yaml"
echo ""
echo "  Add to /etc/hosts:"
echo "    ${MINIKUBE_IP} bombasticifccluster.local"
echo ""

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
```

---

### 16. setup.sh

```bash
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
```

---

### 17. tasksKubernetesDeployment.md

```markdown
# BombasticIFC Cluster — Deployment Tasks

## Phase 0 — Prerequisites (Linux)

- [ ] Install Minikube
- [ ] Install kubectl
- [ ] Install Docker and ensure user is in the `docker` group

## Phase 1 — Create Missing `kubernetes/secrets.yaml`

- [x] Create `kubernetes/secrets.yaml` with:
  - `connection-string` (PostgreSQL connection string)
  - `postgres-user`
  - `postgres-password`
  - `postgres-db`
  - `jwt-secret` (strong, random 64+ char key)
- [x] **Do NOT commit** `secrets.yaml` to version control (already in `.gitignore`)
- [x] Rotate hardcoded JWT secret in `src/BombasticIFC.API/appsettings.json`
- [x] Rotate hardcoded DB password (`postgres`) in appsettings and docker-compose
- [x] Verify `JwtSettings__Secret` env var is injected in `kubernetes/api-deployment.yaml` (added)

## Phase 2 — Start Minikube & Build Image

- [x] Start Minikube: `minikube start --cpus=2 --memory=4096 --disk-size=50g --driver=docker` (adjusted for 6.8GB system)
- [x] Enable addons: `ingress`, `storage-provisioner`, `default-storageclass`, `metrics-server`
- [x] Point Docker CLI at Minikube's daemon: `eval $(minikube docker-env)`
- [x] Build API image inside Minikube context: `docker build -t bombasticifccluster-api:latest .` (251MB)
- [x] Create host-path directories: `/mnt/data/postgres` and `/mnt/data/storage`

## Phase 3 — Apply Kubernetes Manifests (in order)

- [x] `kubectl apply -f kubernetes/namespace.yaml`
- [x] `kubectl apply -f kubernetes/secrets.yaml`
- [x] `kubectl apply -f kubernetes/configmap.yaml`
- [x] `kubectl apply -f kubernetes/persistent-volumes.yaml`
- [x] `kubectl apply -f kubernetes/postgres-deployment.yaml`
- [x] Wait for PostgreSQL: ready ✅
- [x] `kubectl apply -f kubernetes/api-deployment.yaml` (port fixed: 80→8080)
- [x] Wait for API: both pods ready ✅
- [x] `kubectl apply -f kubernetes/ingress.yaml`

## Phase 4 — Database Migrations

- [x] Confirmed `Program.cs` does NOT auto-apply migrations → added `db.Database.Migrate()` on startup
- [x] Fixed EF warning: added matching query filters for `ModelVersion` and `ConversionJob` in `ApplicationDbContext`
- [x] Created `InitialCreate` migration via `dotnet ef migrations add`
- [x] Rebuilt image (`--no-cache`) and redeployed — migration applied successfully on first pod startup

## Phase 5 — Access & Verification

- [ ] Get Minikube IP: `minikube ip`
- [ ] Add `/etc/hosts` entry: `<MINIKUBE_IP> bombasticifccluster.local`
- [ ] Verify health: `curl http://bombasticifccluster.local/api/health`
- [ ] Verify Swagger: `curl http://<MINIKUBE_IP>:30080/swagger`
- [ ] Verify PostgreSQL access: `psql -h <MINIKUBE_IP> -p 30432 -U postgres bombasticifcdb`

## Phase 6 — Frontend Deployment (not yet in k8s)

- [ ] Decide approach: local dev server / nginx pod in k8s / static files served by .NET API
- [ ] If local dev: update `vite.config.ts` proxy target to `http://<MINIKUBE_IP>:30080`
- [ ] If k8s: create `frontend-deployment.yaml` with an nginx image serving the Vite build output
- [ ] If .NET-hosted: add `app.UseStaticFiles()` and copy Vite output into the Docker image

## Known Gaps & Future Work

- [ ] No conversion worker service exists — conversion jobs are stored but never processed
- [ ] No RBAC / ServiceAccounts defined
- [ ] No NetworkPolicies for pod-to-pod segmentation
- [ ] No resource requests/limits or LimitRanges on the namespace
- [ ] hostPath storage is single-node only — not production-ready
```

---

### 18. README.md

```markdown
# BombasticIFC Cluster – IFC Model Viewer

Webbasierte Plattform zur Verwaltung, Konvertierung und Visualisierung von IFC-Gebäudemodellen (Building Information Modeling) in einem Kubernetes-Cluster.

---

## Modulübersicht

Dieses Repository wird in drei Modulen gleichzeitig verwendet:

| Modul | Inhalt |
|-------|--------|
| **PROG3** – Advanced Programming | Backend-API, Datenbankmodell, Clean Architecture, DDD, Kubernetes |
| **WEB2** – Web Engineering II | Vue.js SPA, xeokit-Viewer, Pinia State Management, Responsive UI |
| **ADP** – Agile Development Project | Projektplanung mit Scrum, Gantt-Meilensteine, GitHub + Notion |

---

## Projektbeschreibung

Die Applikation ermöglicht:

- **Upload** von IFC-Dateien über ein Webfrontend
- **Asynchrone Konvertierung** in ein browserkompatibler Format (XKT)
- **Hosting & Visualisierung** der konvertierten Modelle mit dem xeokit-SDK
- **Monitoring** der Konvertierungsprozesse im Cluster
- **Benutzerverwaltung** mit Rollen und Zugriffsrechten

---

## Architektur

Das Projekt folgt **Clean Architecture** (Onion Architecture) kombiniert mit **Domain-Driven Design (DDD)**:

```
Domain → Application → Infrastructure → API (Presentation)
```

### Schichten

| Schicht | Projekt | Inhalt |
|---------|---------|--------|
| Domain | `BombasticIFC.Domain` | Entitäten, Value Objects, Repository-Interfaces, Enums |
| Application | `BombasticIFC.Application` | Use Cases, DTOs, Application-Interfaces |
| Infrastructure | `BombasticIFC.Infrastructure` | DbContext, Repository-Implementierungen, externe Services |
| Presentation | `BombasticIFC.API` | REST-Controller, Swagger |
| Shared | `BombasticIFC.Shared` | Gemeinsame Konstanten und Hilfsfunktionen |
| Frontend | *(separates Repo / Ordner)* | Vue.js 3 SPA mit xeokit-Viewer |

### Bounded Contexts (DDD)

- **ModelImport** – Upload und Validierung von IFC-Dateien
- **ModelProcessing** – Konvertierungsjobs, Queue, Status, Logs
- **ModelHosting** – Hosting und Visualisierung der konvertierten Modelle

---

## Technologie-Stack

### Backend (PROG3)

| Bereich | Technologie |
|---------|-------------|
| Sprache / Framework | C# / .NET 8.0 |
| Datenbank | PostgreSQL 16 |
| ORM | Entity Framework Core |
| Architektur | Clean Architecture + DDD |
| Patterns | Repository Pattern, CQRS (MediatR) |
| Containerisierung | Docker |
| Orchestrierung | Kubernetes (Minikube) |
| API-Dokumentation | Swagger / OpenAPI |
| Authentifizierung | OAuth2 |

### Frontend (WEB2)

| Bereich | Technologie |
|---------|-------------|
| Framework | Vue.js 3 (Composition API) + TypeScript |
| Build Tool | Vite |
| Routing | Vue Router 4 |
| State Management | Pinia |
| CSS-Framework | Tailwind CSS |
| 3D-Viewer | xeokit-SDK |
| HTTP-Client | Axios |
| Formulare | VeeValidate + Zod |
| Icons | Heroicons / Lucide |

### Projektmanagement (ADP)

| Bereich | Tool / Methode |
|---------|----------------|
| Vorgehensmodell | Scrum |
| Zeitplanung | Gantt mit Meilensteinen |
| Plattform | GitHub + Notion |

---

## Hauptfunktionen

### Backend-Features

- REST-API mit DTOs für Upload, Statusabfragen und Resultatadabruf
- Asynchrone Hintergrundverarbeitung (Worker Nodes / Message Queue)
- CRUD-Persistenzschicht für:
  - **Modelle** (IFC-Datei, Metadaten, Status)
  - **Konvertierungsjobs** (Queueing, Status, Logs)
  - **Benutzer** (Rollen, Zugriffsrechte)
  - **Modellversionen / -varianten**
- Containerisierte Microservices (Upload, Konvertierung, Storage)

### Frontend-Features

- Benutzerauthentifizierung (Login, Registrierung, Logout)
- Modell-Dashboard mit Statusübersicht
- IFC-Datei-Upload mit Drag-and-Drop und Fortschrittsanzeige
- Echtzeit-Statusverfolgung der Konvertierung (Polling / WebSocket)
- Interaktiver 3D-Viewer (xeokit) mit Navigation und Objektselektion
- Responsive Design (Mobile, Tablet, Desktop)
- Dark Mode

### Geplante Routen (Frontend)

| Route | View | Beschreibung |
|-------|------|-------------|
| `/` | Home | Startseite |
| `/login` | Login | Login-Formular |
| `/register` | Register | Registrierung |
| `/dashboard` | Dashboard | Modellübersicht (geschützt) |
| `/upload` | Upload | Modell-Upload (geschützt) |
| `/viewer/:id` | Viewer | 3D-Viewer (geschützt) |
| `/profile` | Profil | Benutzerprofil (geschützt) |
| `/:pathMatch(.*)` | 404 | Fehlerseite |

---

## Voraussetzungen

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)
- [Docker](https://www.docker.com/get-started)
- [Minikube](https://minikube.sigs.k8s.io/docs/start/)
- [kubectl](https://kubernetes.io/docs/tasks/tools/)

---

## Schnellstart

### Lokale Entwicklung mit Docker Compose

```powershell
# Anwendung starten
docker-compose up -d

# API erreichbar unter
# http://localhost:5000/swagger
```

### Kubernetes-Deployment mit Minikube

```powershell
# Deployment-Skript ausführen
.\deploy.ps1
```

Das Skript führt folgende Schritte aus:
1. Minikube starten (falls nicht aktiv)
2. Benötigte Addons aktivieren (Ingress, Storage)
3. Docker-Image bauen
4. Storage-Verzeichnisse erstellen
5. Alle Kubernetes-Ressourcen deployen
6. Zugangsinformationen ausgeben

---

## Projektstruktur

```
BombasticIFCcluster/
├── src/
│   ├── BombasticIFC.Domain/          # Domänenlogik (Entitäten, Repositories, Enums)
│   ├── BombasticIFC.Application/     # Use Cases, DTOs, Interfaces
│   ├── BombasticIFC.Infrastructure/  # DbContext, Repository-Implementierungen, Services
│   ├── BombasticIFC.API/             # REST-Controller, Swagger
│   └── BombasticIFC.Shared/          # Gemeinsame Hilfsmittel
├── kubernetes/                        # Kubernetes-Manifeste
│   ├── namespace.yaml
│   ├── configmap.yaml
│   ├── secrets.yaml
│   ├── persistent-volumes.yaml
│   ├── postgres-deployment.yaml
│   ├── api-deployment.yaml
│   └── ingress.yaml
├── Dockerfile
├── docker-compose.yml
├── deploy.ps1                         # Windows-Deployment-Skript
└── README.md
```

---

## API-Endpunkte

### Modelle

| Methode | Endpunkt | Beschreibung |
|---------|----------|-------------|
| `POST` | `/api/models/upload` | IFC-Modell hochladen |
| `GET` | `/api/models/{id}` | Modell abrufen |

### Konvertierungen

| Methode | Endpunkt | Beschreibung |
|---------|----------|-------------|
| `POST` | `/api/conversions` | Konvertierungsjob erstellen |
| `GET` | `/api/conversions/{id}` | Status eines Jobs abrufen |

### System

| Methode | Endpunkt | Beschreibung |
|---------|----------|-------------|
| `GET` | `/health` | Health Check |

---

## Erreichbarkeit nach Deployment

| Service | URL |
|---------|-----|
| API (NodePort) | `http://<minikube-ip>:30080` |
| Swagger UI | `http://<minikube-ip>:30080/swagger` |
| PostgreSQL | `<minikube-ip>:30432` |

Minikube-IP ermitteln:
```powershell
minikube ip
```

Für Ingress-Zugriff in der `hosts`-Datei eintragen:
```
<minikube-ip> bombasticifccluster.local
```
Dann erreichbar unter: `http://bombasticifccluster.local`

---

## Entwicklung

### Solution bauen

```powershell
dotnet build BombasticIFCcluster.sln
```

### Tests ausführen

```powershell
dotnet test
```

### Datenbankmigrationen

```powershell
cd src/BombasticIFC.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../BombasticIFC.API
dotnet ef database update --startup-project ../BombasticIFC.API
```

---

## Testkonzept

| Testart | Beschreibung |
|---------|-------------|
| **Unit Tests** | Domänenlogik, Services, Hilfsfunktionen |
| **Integrationstests** | API-Endpunkte, Datenbankzugriffe, Konvertierungsprozesse |
| **End-to-End Tests** | Upload → Konvertierung → Anzeige im Browser |
| **Security Tests** | Authentifizierung, Rechteverwaltung, sichere Uploads |

---

## Clean Code & SOLID

1. **Single Responsibility** – Jede Klasse hat genau eine Verantwortung
2. **Dependency Inversion** – Abhängigkeiten zeigen stets auf Abstraktionen
3. **Separation of Concerns** – Klare Schichtengrenzen
4. **Explicit Dependencies** – Constructor Injection durchgehend
5. **Immutability** – Private Setter, Factory-Methoden
6. **Domain-Driven Design** – Reichhaltige Domänenmodelle mit Geschäftslogik

---

