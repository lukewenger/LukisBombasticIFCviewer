# C4 — Kubernetes
**Modul:** HFI_DEP | **Auftrag:** C4 | **Punkte:** max. 10

---

## Was wurde umgesetzt und warum

BombasticIFC läuft in einem Kubernetes-Cluster im Namespace `bombasticifccluster`. Die Manifests im Verzeichnis `kubernetes/` beschreiben den vollständigen Cluster-Zustand deklarativ. Das Setup wurde mit Minikube entwickelt und ist für K3s (Produktion) dokumentiert.

---

## Architektur-Diagramm (Kubernetes-Ressourcen)

```
┌───────────────────────────────────────────────────────────────────────────┐
│  Namespace: bombasticifccluster                                            │
│                                                                            │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │  Ingress (nginx)                                                     │  │
│  │  /        → frontend-service:80                                      │  │
│  │  /api      → api-service:80                                          │  │
│  │  /health   → api-service:80                                          │  │
│  └──────────────────────┬──────────────────────┬────────────────────────┘  │
│                         │                      │                           │
│  ┌──────────────────────▼──────┐  ┌────────────▼──────────────────────┐   │
│  │  frontend-service (ClusterIP│  │  api-service (ClusterIP :80)       │   │
│  │  :80)                       │  │  api-nodeport (NodePort :30080)    │   │
│  └──────────────────────┬──────┘  └────────────┬──────────────────────┘   │
│                         │                      │                           │
│  ┌──────────────────────▼──────┐  ┌────────────▼──────────────────────┐   │
│  │  Frontend Deployment         │  │  API Deployment                    │   │
│  │  replicas: 2                 │  │  replicas: 2                       │   │
│  │  nginx:alpine (Vue SPA)      │  │  .NET 8 + xeokit-convert           │   │
│  │  Liveness: GET / :80         │  │  Liveness: GET /health :8080       │   │
│  │  Readiness: GET / :80        │  │  Readiness: GET /health :8080      │   │
│  │  CPU: 100m / 250m            │  │  Startup: GET /health :8080        │   │
│  │  Mem: 128Mi / 256Mi          │  │  CPU: 500m / 1000m                 │   │
│  └──────────────────────────────┘  │  Mem: 512Mi / 1Gi                  │   │
│                                    │  Volume: storage-pvc               │   │
│                                    └────────────┬──────────────────────┘   │
│                                                 │                          │
│  ┌──────────────────────────────────────────────▼──────────────────────┐  │
│  │  postgres-service (ClusterIP :5432)                                  │  │
│  └──────────────────────────────────────────────┬──────────────────────┘  │
│                                                 │                          │
│  ┌──────────────────────────────────────────────▼──────────────────────┐  │
│  │  PostgreSQL Deployment (replicas: 1)                                  │  │
│  │  postgres:16-alpine                                                   │  │
│  │  Liveness: pg_isready | Readiness: pg_isready                         │  │
│  │  CPU: 250m / 500m | Mem: 256Mi / 512Mi                                │  │
│  │  Volume: postgres-pvc → /var/lib/postgresql/data                      │  │
│  └──────────────────────────────────────────────┬──────────────────────┘  │
│                                                 │                          │
│  ┌──────────────────────────┐  ┌────────────────▼──────────────────────┐  │
│  │  ConfigMap               │  │  PersistentVolumes                     │  │
│  │  - POSTGRES_DB           │  │  postgres-pv: 10Gi hostPath            │  │
│  │  - POSTGRES_USER         │  │  storage-pv: 50Gi hostPath             │  │
│  └──────────────────────────┘  └────────────────────────────────────────┘  │
│  ┌──────────────────────────┐                                              │
│  │  Secret (gitignored!)    │                                              │
│  │  - postgres-user/pw/db   │                                              │
│  │  - connection-string     │                                              │
│  │  - jwt-secret            │                                              │
│  └──────────────────────────┘                                              │
└───────────────────────────────────────────────────────────────────────────┘
```

---

## Setup-Anleitung (Reproduzierbar durch Dritte)

### Voraussetzungen

| Tool | Version |
|---|---|
| Docker | 24.x+ |
| Minikube | 1.32+ oder K3s 1.28+ |
| kubectl | 1.28+ |

### Option A: Minikube (lokal)

```bash
# 1. Minikube starten
minikube start --cpus=2 --memory=4096 --disk-size=50g --driver=docker
minikube addons enable ingress
minikube addons enable metrics-server

# 2. Docker-Umgebung auf Minikube zeigen
eval $(minikube docker-env)

# 3. Images bauen (lokal in Minikube)
docker build -t bombasticifccluster-api:latest .
docker build -t bombasticifccluster-frontend:latest ./frontend

# 4. Storage-Verzeichnisse auf Minikube-Node erstellen
minikube ssh "sudo mkdir -p /mnt/data/postgres /mnt/data/storage && sudo chmod -R 777 /mnt/data"

# 5. Secrets erstellen (NICHT in Git committen!)
kubectl create namespace bombasticifccluster 2>/dev/null || true
kubectl create secret generic bombasticifccluster-secrets \
  --namespace=bombasticifccluster \
  --from-literal=postgres-user=postgres \
  --from-literal=postgres-password=$(openssl rand -base64 16) \
  --from-literal=postgres-db=bombasticifcdb \
  --from-literal=connection-string="Host=postgres-service;Port=5432;Database=bombasticifcdb;Username=postgres;Password=$(openssl rand -base64 16)" \
  --from-literal=jwt-secret=$(openssl rand -base64 32)

# 6. Alle Manifests anwenden (Reihenfolge beachten!)
kubectl apply -f kubernetes/namespace.yaml
kubectl apply -f kubernetes/configmap.yaml
kubectl apply -f kubernetes/persistent-volumes.yaml
kubectl apply -f kubernetes/postgres-deployment.yaml
kubectl wait --for=condition=ready pod -l app=postgres -n bombasticifccluster --timeout=300s
kubectl apply -f kubernetes/api-deployment.yaml
kubectl wait --for=condition=ready pod -l app=bombasticifccluster-api -n bombasticifccluster --timeout=300s
kubectl apply -f kubernetes/frontend-deployment.yaml
kubectl apply -f kubernetes/ingress.yaml

# 7. Zugriff
minikube ip   # z.B. 192.168.49.2
# Frontend: http://192.168.49.2/
# API: http://192.168.49.2/api
# Swagger: http://192.168.49.2:30080/swagger
```

### Option B: K3s (Produktion)

```bash
# K3s installieren
curl -sfL https://get.k3s.io | sh -
mkdir -p ~/.kube
sudo cp /etc/rancher/k3s/k3s.yaml ~/.kube/config
sudo chown $USER:$USER ~/.kube/config

# Images importieren (K3s nutzt containerd, nicht Docker)
docker build -t bombasticifccluster-api:latest .
docker build -t bombasticifccluster-frontend:latest ./frontend
docker save bombasticifccluster-api:latest | sudo k3s ctr images import -
docker save bombasticifccluster-frontend:latest | sudo k3s ctr images import -

# Rest identisch zu Minikube (ab Schritt 4)
```

---

## Wichtige Manifest-Ausschnitte

### Liveness + Readiness Probes (api-deployment.yaml)

```yaml
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
startupProbe:
  httpGet:
    path: /health
    port: 8080
  failureThreshold: 30
  periodSeconds: 5
```

- **Readiness**: Verhindert Traffic zu einem Pod, der noch nicht bereit ist (z.B. DB-Migration läuft noch)
- **Liveness**: Erkennt hängende Pods und startet sie automatisch neu
- **Startup**: Gibt der .NET-App 150 Sekunden Zeit zum Starten (Migrationen + Seeding)

### Resource Requests und Limits (api-deployment.yaml)

```yaml
resources:
  requests:
    memory: "512Mi"
    cpu: "500m"
  limits:
    memory: "1Gi"
    cpu: "1000m"
```

Requests reservieren Kapazität; Limits verhindern, dass ein Pod die gesamte Node-Ressource belegt. Die Werte wurden basierend auf lokalen Last-Tests gewählt: Die .NET-App benötigt minimal ~300Mi RAM, xeokit-Convert-Jobs können CPU-intensiv sein.

### Rolling Update (api-deployment.yaml)

```yaml
strategy:
  type: RollingUpdate
  rollingUpdate:
    maxUnavailable: 0   # Kein Pod wird beendet, bevor ein neuer läuft
    maxSurge: 1         # Maximal ein zusätzlicher Pod während des Updates
```

Mit `maxUnavailable: 0` ist die Anwendung während des gesamten Rolling Updates erreichbar (Zero-Downtime).

### ConfigMap vs. Secret

```yaml
# configmap.yaml — nicht-sensitive Werte
data:
  POSTGRES_DB: "bombasticifcdb"
  POSTGRES_USER: "postgres"

# api-deployment.yaml — sensitive Werte via Secret-Referenz
env:
- name: JwtSettings__Secret
  valueFrom:
    secretKeyRef:
      name: bombasticifccluster-secrets
      key: jwt-secret
```

---

## Begründung der wichtigsten Entscheidungen

### Warum Minikube für Entwicklung, K3s für Produktion?
Minikube läuft als Docker-in-Docker und ist einfach installierbar. K3s ist ein produktionstaugliches, CNCF-zertifiziertes Kubernetes in einem einzigen Binary und eignet sich für Single-Node-Server ohne VM-Overhead.

### Warum kubectl apply statt Helm?
Für ein Projekt mit ~7 Manifests ist Helm-Overhead nicht gerechtfertigt. Die Manifests sind direkt lesbar und versioniert. Bei einem grösseren Projekt mit Umgebungs-Overrides würde Helm/Kustomize sinnvoll.

### Warum maxUnavailable: 0?
Die API hat einen Startup-Probe mit 30x5s = 150s Toleranz. Mit `maxUnavailable: 0` bleibt immer mindestens ein Pod erreichbar, auch wenn der neue Pod lange zum Starten braucht.

### Warum sind Secrets gitignored?
`kubernetes/secrets.yaml` enthält Klartext-Credentials (stringData). Einmal in Git committed, sind Credentials permanent im History. Die Datei wird deshalb gitignored. Stattdessen steht `kubernetes/secrets.yaml.template` im Repository als Dokumentation der Struktur.

---

## Rolling Update demonstrieren

```bash
# 1. Image neu bauen (z.B. nach Code-Änderung)
eval $(minikube docker-env)
docker build -t bombasticifccluster-api:latest .

# 2. Rollout triggern durch Annotation-Update
kubectl rollout restart deployment/api-deployment -n bombasticifccluster

# 3. Fortschritt beobachten
kubectl rollout status deployment/api-deployment -n bombasticifccluster
kubectl get pods -n bombasticifccluster -w

# Ausgabe während Rolling Update:
# api-deployment-xxx-old   Running   → Terminating
# api-deployment-xxx-new   Pending   → Running
```

---

## Reflexion

**Was gut funktioniert hat:**
- Liveness/Readiness/Startup-Probes funktionieren zuverlässig — der Cluster erkennt unhealthy Pods
- Rolling Update mit maxUnavailable=0 sorgt für Zero-Downtime-Deployments
- Trennung von ConfigMap und Secret hält nicht-sensitive Konfig von sensitiven Credentials getrennt

**Was rückblickend anders gelöst würde:**
- `hostPath`-Volumes sind an die Node gebunden — für Multi-Node würde Longhorn oder ein Cloud-StorageProvider verwendet
- Sealed Secrets (bitnami-labs) oder External Secrets Operator würde secrets.yaml sicher commitbar machen
- NetworkPolicies würden die Pod-zu-Pod-Kommunikation auf das Notwendige beschränken
- Der PostgreSQL-NodePort-Service (30432) sollte in Produktion entfernt werden
