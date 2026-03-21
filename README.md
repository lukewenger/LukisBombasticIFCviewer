# BombasticIFC Cluster — IFC Model Viewer

Webbasierte Plattform zur Verwaltung, Konvertierung und Visualisierung von IFC-Gebäudemodellen (Building Information Modeling), betrieben in einem Kubernetes-Cluster auf Ubuntu Server mit Minikube.

---

## Modulübersicht

| Modul | Inhalt |
|-------|--------|
| **PROG3** – Advanced Programming | Backend-API, Clean Architecture, DDD, Kubernetes |
| **WEB2** – Web Engineering II | Vue.js SPA, xeokit-Viewer, Pinia, Responsive UI |
| **ADP** – Agile Development Project | Scrum, Gantt-Meilensteine, GitHub + Notion |

---

## Architektur

Das Projekt folgt **Clean Architecture** (Onion Architecture) mit **Domain-Driven Design (DDD)**:

```
Domain → Application → Infrastructure → API (Presentation)
```

### Backend-Schichten

| Schicht | Projekt | Inhalt |
|---------|---------|--------|
| Domain | `BombasticIFC.Domain` | Entitäten, Value Objects, Repository-Interfaces, Enums |
| Application | `BombasticIFC.Application` | Use Cases, DTOs, Application-Interfaces |
| Infrastructure | `BombasticIFC.Infrastructure` | DbContext, Repository-Implementierungen, Services |
| Presentation | `BombasticIFC.API` | REST-Controller, Swagger |
| Shared | `BombasticIFC.Shared` | Gemeinsame Konstanten und Hilfsfunktionen |

### Kubernetes-Cluster (Namespace: `bombasticifccluster`)

```
Client (Browser)
     │
     ├─── :30080 (NodePort) ──────────────► api-service (ClusterIP :80)
     │                                            │
     └─── Ingress (nginx, Catch-All) ─────┬───── api-service  (/api, /health)
                                           └───── frontend-service  (/)
                                                       │
                                               Frontend Pod (Vue.js + nginx)

api-service ──► API Pod 1 (.NET 8, :8080) ─┐
             ──► API Pod 2 (.NET 8, :8080) ─┴──► postgres-service ──► PostgreSQL 16
                         │                                                   │
                    storage-pvc                                          postgres-pvc
                 (/mnt/data/storage)                                  (/mnt/data/postgres)
```

| Ressource | Replicas | Image |
|-----------|----------|-------|
| Frontend | 1 | `bombasticifccluster-frontend:latest` |
| API | 2 | `bombasticifccluster-api:latest` |
| PostgreSQL | 1 | `postgres:16-alpine` |

---

## Technologie-Stack

### Backend

| Bereich | Technologie |
|---------|-------------|
| Sprache / Framework | C# / .NET 8.0 |
| Datenbank | PostgreSQL 16 |
| ORM | Entity Framework Core |
| Authentifizierung | JWT (Bearer Token) |
| API-Dokumentation | Swagger / OpenAPI |
| Containerisierung | Docker (Multi-Stage Build) |
| Orchestrierung | Kubernetes — Minikube (Ubuntu) |

### Frontend

| Bereich | Technologie |
|---------|-------------|
| Framework | Vue.js 3 (Composition API) + TypeScript |
| Build Tool | Vite |
| Routing | Vue Router 4 |
| State Management | Pinia |
| 3D-Viewer | xeokit-SDK |
| HTTP-Client | Axios |

---

## Projektstruktur

```
LukisBombasticIFCviewer/
├── src/
│   ├── BombasticIFC.Domain/          # Entitäten, Repositories, Enums
│   ├── BombasticIFC.Application/     # Use Cases, DTOs, Interfaces
│   ├── BombasticIFC.Infrastructure/  # DbContext, Repositories, Migrations
│   ├── BombasticIFC.API/             # REST-Controller, Swagger, Program.cs
│   └── BombasticIFC.Shared/          # Gemeinsame Konstanten
├── frontend/                          # Vue.js 3 SPA
│   ├── src/
│   │   ├── views/                    # LoginView, DashboardView, ViewerView, …
│   │   ├── components/               # AppHeader, LoginForm, …
│   │   ├── stores/                   # Pinia Stores (auth)
│   │   ├── api/                      # Axios-Client, Models
│   │   └── router/                   # Vue Router
│   └── Dockerfile
├── kubernetes/                        # K8s-Manifeste (Minikube / K3s)
│   ├── namespace.yaml
│   ├── secrets.yaml                  # ⚠ Nicht in Git committen
│   ├── configmap.yaml
│   ├── persistent-volumes.yaml
│   ├── postgres-deployment.yaml
│   ├── api-deployment.yaml
│   ├── frontend-deployment.yaml
│   ├── ingress.yaml
│   ├── CLUSTER-DOKUMENTATION.md     # Vollständige Clusterdokumentation
│   └── architecture.puml
├── Dockerfile                         # API Multi-Stage Build
├── docker-compose.yml
├── setupKubernetes.sh                 # Setup-Script (Phasen 0–2)
└── BombasticIFCcluster.sln
```

---

## Deployment — Minikube (Ubuntu Server)

### Erstinstallation (einmalig)

```bash
chmod +x setupKubernetes.sh && ./setupKubernetes.sh
```

Installiert Docker, .NET SDK 8, kubectl und Minikube, erstellt EF Core Migrations, startet Minikube (`--cpus=2 --memory=4096 --driver=docker`) und baut das erste API-Image.

---

### Deploy-Scripts

| Script | Zweck |
|--------|-------|
| `deploy.sh` | **Vollständiger Redeploy** — Minikube prüfen, alle Manifeste anwenden, API + Frontend neu bauen und ausrollen |
| `deployAPI.sh` | API-Image neu bauen und nur das API-Deployment neu starten |
| `update-frontend.sh` | Frontend-Image neu bauen und nur das Frontend-Deployment neu starten |

#### Vollständiger Redeploy

```bash
bash deploy.sh
```

Führt folgende Schritte aus:
1. Minikube-Status prüfen, ggf. starten
2. Host-Path-Verzeichnisse auf dem Minikube-Node anlegen (`/mnt/data/postgres`, `/mnt/data/storage`)
3. Alle Kubernetes-Manifeste idempotent anwenden (`kubectl apply`)
4. Auf PostgreSQL warten
5. API-Image neu bauen (`--no-cache`) und ausrollen
6. Frontend-Image neu bauen (`--no-cache`) und ausrollen

#### Nur API aktualisieren

```bash
bash deployAPI.sh
```

#### Nur Frontend aktualisieren

```bash
bash update-frontend.sh
```

> **Hinweis:** Beide Image-Build-Schritte laufen mit `--no-cache`, damit Vite/Webpack alle Assets neu kompiliert und die Chunk-Hashes in `index.html` stets mit den tatsächlich ausgelieferten Dateien übereinstimmen. Ohne `--no-cache` kann es zu 404-Fehlern beim Laden von dynamisch importierten JS-Chunks kommen.

---

### Zugriff nach dem Deployment

Nach dem ersten Deployment die Minikube-IP in `/etc/hosts` eintragen:

```bash
echo "$(minikube ip) bombasticifccluster.local" | sudo tee -a /etc/hosts
```

| Ziel | URL |
|------|-----|
| Frontend | http://bombasticifccluster.local/ |
| API (NodePort) | http://$(minikube ip):30080/ |
| Swagger UI | http://bombasticifccluster.local/api/swagger |
| Health Check | http://bombasticifccluster.local/health |

```bash
# Minikube-IP anzeigen
minikube ip
```

---

## API-Endpunkte

### Authentifizierung

| Methode | Endpunkt | Beschreibung |
|---------|----------|-------------|
| `POST` | `/api/auth/register` | Benutzer registrieren |
| `POST` | `/api/auth/login` | Login, JWT Token erhalten |

### Modelle

| Methode | Endpunkt | Beschreibung |
|---------|----------|-------------|
| `POST` | `/api/models/upload` | IFC-Modell hochladen |
| `GET` | `/api/models` | Alle Modelle abrufen |
| `GET` | `/api/models/{id}` | Einzelnes Modell abrufen |

### Konvertierungen

| Methode | Endpunkt | Beschreibung |
|---------|----------|-------------|
| `POST` | `/api/conversions` | Konvertierungsjob erstellen |
| `GET` | `/api/conversions/{id}` | Status abrufen |

### System

| Methode | Endpunkt | Beschreibung |
|---------|----------|-------------|
| `GET` | `/health` | Health Check |

---

## Frontend-Routen

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

## Entwicklung

### Solution bauen

```bash
dotnet build BombasticIFCcluster.sln
```

### EF Core Migrations

```bash
dotnet ef migrations add <Name> \
  --project src/BombasticIFC.Infrastructure \
  --startup-project src/BombasticIFC.API

dotnet ef database update \
  --startup-project src/BombasticIFC.API
```

### Cluster-Status

```bash
kubectl get all -n bombasticifccluster
kubectl logs -f deployment/api-deployment -n bombasticifccluster
minikube dashboard
```

---

## Weiterführende Dokumentation

### Architecture & Design

- **Backend Architecture:** [`Doku/backend/csharp-doku.md`](Doku/backend/csharp-doku.md) — Clean Architecture, CQRS, Domain Layer
- **Backend Architecture Diagram:** [`Doku/backend/csharp-architecture.puml`](Doku/backend/csharp-architecture.puml)
- **Frontend Architecture:** [`Doku/frontend/FrontendArchitecture.md`](Doku/frontend/FrontendArchitecture.md) — Vue 3, Composables, Pinia
- **Frontend Architecture Diagram:** [`Doku/frontend/frontendArchitecture.puml`](Doku/frontend/frontendArchitecture.puml)
- **Cluster Architecture:** [`Doku/Cluster/CLUSTER-DOKUMENTATION.md`](Doku/Cluster/CLUSTER-DOKUMENTATION.md) — Kubernetes, Deployments, Networking
- **Cluster Architecture Diagram:** [`Doku/Cluster/architecture.puml`](Doku/Cluster/architecture.puml)

### Additional Resources

- **Cluster-Dokumentation (vollständig):** [`kubernetes/CLUSTER-DOKUMENTATION.md`](kubernetes/CLUSTER-DOKUMENTATION.md)
- **Architekturdiagramm (PlantUML):** [`kubernetes/architecture.puml`](kubernetes/architecture.puml)
- **Frontend-Aufgaben:** [`frontend/TasksVueFrontend.md`](frontend/TasksVueFrontend.md)
- **Kubernetes-Aufgaben:** [`tasksKubernetesDeployment.md`](tasksKubernetesDeployment.md)
- **Changelog:** [`Changelog.md`](Changelog.md)

---

## Autor

**Lukas Wenger** — lwe046484@students.gibb.ch

| Modul | Kurs | Betreuer |
|-------|------|----------|
| PROG3 – Advanced Programming | GIBB Sem. 5 | Reto Glarner |
| WEB2 – Web Engineering II | GIBB Sem. 5 | Nicolas Dumermuth |
| ADP – Agile Development Project | GIBB Sem. 5 | Reto Glarner |


---
