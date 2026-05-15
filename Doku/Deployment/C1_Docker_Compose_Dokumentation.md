# C1 — Multi-Service-Architektur mit Docker Compose
**Modul:** HFI_DEP | **Auftrag:** C1 | **Punkte:** max. 10

---

## Was wurde umgesetzt und warum

BombasticIFC besteht aus drei Services, die lokal per Docker Compose zusammenspielen. Docker Compose dient als Entwicklungsumgebung — Kubernetes (C4) ist die Zielplattform.

| Service | Image/Build | Aufgabe |
|---|---|---|
| `postgres` | `postgres:16-alpine` | Persistente Datenhaltung (User, Modelle, Konvertierungsjobs) |
| `api` | eigenes Dockerfile (multi-stage) | .NET 8 REST-API, Authentifizierung, Datei-Upload, IFC-Konvertierung |
| `frontend` | eigenes Dockerfile (multi-stage) | Vue.js SPA via nginx, Reverse-Proxy zu /api |

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
                    │  │  Port 8080 (intern) / 5000 (extern)      │   │
                    │  │  - REST API (/api/*)                      │   │
                    │  │  - JWT-Auth, IFC-Upload & Konvertierung   │   │
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

## Setup-Anleitung

Voraussetzungen: Docker Engine 24.x+ und Git.

Das Repository enthält eine `.env.example` mit allen benötigten Variablen. Diese Datei wird nach `.env` kopiert und mit sicheren Werten befüllt — insbesondere `POSTGRES_PASSWORD` und `JWT_SECRET`. Anschliessend genügt `docker compose up -d`. Nach etwa 30–60 Sekunden sind alle Services bereit: das Frontend ist unter `http://localhost` erreichbar, die API direkt unter `http://localhost:5000`. Mit `docker compose down -v` werden Services und Volumes gemeinsam entfernt (setzt die Datenbank zurück).

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
      condition: service_healthy   # API startet erst wenn Postgres bereit ist
```

### Netzwerk-Isolation

```yaml
# frontend-net: frontend ↔ api
# backend-net:  api ↔ postgres
# Das Frontend kann postgres NICHT direkt erreichen.
networks:
  frontend-net:
  backend-net:
```

---

## Begründung der wichtigsten Entscheidungen

### Warum PostgreSQL statt SQLite?
PostgreSQL ist identisch mit der Kubernetes-Produktionsumgebung. Dev/Prod-Parität verhindert Überraschungen beim Deployment. SQLite ist ausserdem nicht multi-replica-fähig.

### Warum zwei Netzwerke?
Security-by-default: Das Frontend soll keinen direkten Datenbankzugriff haben. Zwei isolierte Netzwerke erzwingen diese Trennung auf Netzwerkebene, ohne zusätzliche Firewall-Regeln.

### Warum depends_on mit condition: service_healthy?
`service_started` garantiert nur, dass der Container läuft — nicht, dass PostgreSQL Verbindungen annimmt. Mit `service_healthy` und `pg_isready` startet die API erst, wenn die Datenbank tatsächlich bereit ist. Das verhindert Verbindungsfehler beim ersten Start.

### Warum eine separate nginx.compose.conf?
Die produktive nginx-Konfiguration proxied `/api/` auf den K8s-DNS-Namen. In Docker Compose lautet der Hostname schlicht `api`. Ein Bind-Mount einer eigenen Compose-Konfiguration löst diesen Unterschied, ohne das Image zu verändern.

---

## Reflexion

**Was gut funktioniert hat:**
- Multi-Stage Builds halten die Images schlank (API ~280 MB statt ~1,2 GB mit SDK)
- Healthchecks und depends_on sorgen für eine reproduzierbare Startreihenfolge
- Netzwerk-Isolation verhindert direkte Datenbankzugriffe vom Frontend

**Was rückblickend anders gelöst würde:**
- Die ursprüngliche Compose-Datei hatte das PostgreSQL-Passwort hartcodiert — das wurde korrigiert
- Eine Redis-Cache-Schicht fehlt aktuell; für Produktionslast wäre sie sinnvoll
