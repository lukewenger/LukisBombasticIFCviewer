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

## Cluster-Monitoring

```powershell
# Alle Ressourcen im Namespace anzeigen
kubectl get all -n bombasticifccluster

# API-Logs
kubectl logs -f deployment/api-deployment -n bombasticifccluster

# PostgreSQL-Logs
kubectl logs -f deployment/postgres-deployment -n bombasticifccluster

# Minikube-Dashboard öffnen
minikube dashboard
```

---

## Cluster zurücksetzen

```powershell
# Minikube stoppen
minikube stop

# Cluster löschen
minikube delete

# Docker Compose herunterfahren
docker-compose down -v
```

---

## Autor

**Lukas Wenger**
Kontakt: lwe046484@students.gibb.ch

## Projektkontext

| Modul | Kurs | Betreuer |
|-------|------|----------|
| PROG3 – Advanced Programming | GIBB Sem. 5 | Reto Glarner |
| WEB2 – Web Engineering II | GIBB Sem. 5 | Nicolas Dumermuth |
| ADP – Agile Development Project | GIBB Sem. 5 | Reto Glarner |