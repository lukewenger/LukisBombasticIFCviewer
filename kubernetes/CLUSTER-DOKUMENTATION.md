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

