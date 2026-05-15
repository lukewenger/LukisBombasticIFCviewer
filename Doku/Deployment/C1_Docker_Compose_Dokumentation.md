# C1 — Multi-Service-Architektur mit Docker Compose
**Modul:** HFI_DEP | **Auftrag:** C1 | **Punkte:** max. 10

---

## Was wurde umgesetzt und warum

BombasticIFC ist eine SPA für die Verwaltung und Visualisierung von IFC-Gebäudemodellen. Die Anwendung besteht aus drei Services, die sinnvoll zusammenarbeiten:

| Service | Image/Build | Aufgabe |
|---|---|---|
| `postgres` | `postgres:16-alpine` | Persistente Datenhaltung (User, Modelle, Konvertierungsjobs) |
| `api` | eigenes Dockerfile (multi-stage) | .NET 8 REST-API, Authentifizierung, Datei-Upload, IFC-Konvertierung |
| `frontend` | eigenes Dockerfile (multi-stage) | Vue.js SPA via nginx, Reverse-Proxy zu /api |

Die Services haben echte gegenseitige Abhängigkeiten: Das Frontend kommuniziert ausschliesslich über den API-Service mit der Datenbank. PostgreSQL ist von aussen nicht erreichbar.

---

## Architektur-Diagramm

```
                    ┌─────────────────────────────────────────────────┐
                    │           Docker Compose                        │
                    │           Network: frontend-net + backend-net   │
                    │                                                 │
  Browser           │  ┌─────────────────────────────────────────┐   │
     │              │  │  frontend (nginx:alpine)                 │   │
     │  :80         │  │  Port 80                                 │   │
     └──────────────│──►  - Statische Vue.js SPA                  │   │
                    │  │  - Reverse-Proxy /api → api:80            │   │
                    │  └──────────────────┬──────────────────────┘   │
                    │         frontend-net │                          │
                    │  ┌──────────────────▼──────────────────────┐   │
                    │  │  api (.NET 8 ASP.NET Core)               │   │
                    │  │  Port 80 (intern) / 5000 (extern/debug)  │   │
                    │  │  - REST API (/api/*)                      │   │
                    │  │  - JWT-Auth                               │   │
                    │  │  - IFC-Upload & Konvertierung             │   │
                    │  │  Volume: storage-data → /data/storage     │   │
                    │  └──────────────────┬──────────────────────┘   │
                    │         backend-net  │                          │
                    │  ┌──────────────────▼──────────────────────┐   │
                    │  │  postgres (postgres:16-alpine)            │   │
                    │  │  Port 5432 (nur intern)                   │   │
                    │  │  Volume: postgres-data                    │   │
                    │  └──────────────────────────────────────────┘   │
                    └─────────────────────────────────────────────────┘
```

---

## Setup-Anleitung (Reproduzierbar durch Dritte)

### Voraussetzungen

- Docker Desktop oder Docker Engine ≥ 24.x
- Git

### Schritte

```bash
# 1. Repository klonen
git clone https://github.com/<your-github-user>/BombasticIFCcluster.git
cd BombasticIFCcluster

# 2. Umgebungsvariablen konfigurieren
cp .env.example .env
# .env öffnen und sichere Passwörter setzen:
#   POSTGRES_PASSWORD=<sicheres-passwort>
#   JWT_SECRET=<min-32-zeichen-secret>
# Beispiel: openssl rand -base64 32

# 3. Services starten
docker compose up -d

# 4. Warten bis alle Services bereit sind (ca. 30-60 Sek)
docker compose ps

# 5. Zugriff
# Frontend: http://localhost
# API/Swagger: http://localhost:5000/swagger
# API Health: http://localhost:5000/health
```

### Stoppen

```bash
docker compose down       # Services stoppen, Volumes behalten
docker compose down -v    # Services stoppen + Volumes löschen (DB zurücksetzen)
```

---

## Wichtige Konfigurationsausschnitte

### Healthcheck mit depends_on

```yaml
postgres:
  healthcheck:
    test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-postgres}"]
    interval: 10s
    timeout: 5s
    retries: 5

api:
  depends_on:
    postgres:
      condition: service_healthy   # API startet erst wenn Postgres bereit
```

### Netzwerk-Isolation

```yaml
# Nur api und frontend sind im frontend-net → frontend kann api ansprechen
# Nur api und postgres sind im backend-net → api kann postgres ansprechen
# frontend kann postgres NICHT direkt erreichen

networks:
  frontend-net:    # frontend ↔ api
  backend-net:     # api ↔ postgres
```

### Secrets via .env (niemals hardcoded)

```yaml
# docker-compose.yml referenziert nur Variablen
api:
  environment:
    - ConnectionStrings__DefaultConnection=...Password=${POSTGRES_PASSWORD:?Pflichtfeld}
    - JwtSettings__Secret=${JWT_SECRET:?Pflichtfeld}
```

Die tatsächlichen Werte stehen in `.env` (gitignored). `.env.example` dient als Vorlage.

### Multi-Stage Dockerfile (API)

```dockerfile
# Stage 1: Build (SDK ~900MB)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# ... dotnet build, dotnet publish

# Stage 2: Runtime (~200MB) + Node.js 20 (für xeokit-convert)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
# Nur die Published-Ausgabe, kein SDK-Overhead
```

---

## Begründung der wichtigsten Entscheidungen

### Warum PostgreSQL statt SQLite?
PostgreSQL wird auch in der Kubernetes-Produktion eingesetzt. Docker Compose spiegelt damit die Produktionsarchitektur (Dev/Prod-Parität). SQLite ist nicht für Multi-Replica-Deployments geeignet.

### Warum zwei Netzwerke?
Security-by-default: Das Frontend soll nicht direkt auf die Datenbank zugreifen können. Durch `frontend-net` (frontend ↔ api) und `backend-net` (api ↔ postgres) ist die Datenbankebene vollständig isoliert.

### Warum depends_on mit condition: service_healthy statt nur service_started?
`service_started` garantiert nur, dass der Container läuft — nicht, dass PostgreSQL Verbindungen annimmt. Mit `service_healthy` und `pg_isready`-Healthcheck startet die API erst, wenn die Datenbank tatsächlich bereit ist.

### Warum separate nginx.compose.conf?
Die Production-nginx.conf proxied `/api/` auf den K8s-DNS-Namen (`api-service.bombasticifccluster.svc.cluster.local`). In Docker Compose lautet der DNS-Name schlicht `api`. Eine separate `nginx.compose.conf` wird via Bind-Mount eingehängt, ohne das Image zu verändern.

---

## Reflexion

**Was gut funktioniert hat:**
- Multi-Stage Builds halten die Images schlank (API ~280MB statt ~1.2GB mit SDK)
- Healthchecks + depends_on sorgen für reproduzierbare Startreihenfolge
- Netzwerk-Isolation verhindert direkte DB-Zugriffe vom Frontend

**Was rückblickend anders gelöst würde:**
- Die ursprüngliche docker-compose.yml hatte das PostgreSQL-Passwort hartcodiert — das wurde korrigiert
- Für echte Produktion würde Docker Compose durch Kubernetes ersetzt (C4)
- Ein Redis-Service für Session-Caching wäre sinnvoll (aktuell keine Cache-Schicht)
