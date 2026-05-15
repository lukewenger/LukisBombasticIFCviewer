# C3 — Cloud Platform Deployment (Railway)
**Modul:** HFI_DEP | **Auftrag:** C3 | **Punkte:** max. 10

---

## Was wurde umgesetzt und warum

BombasticIFC wird auf **Railway** als Cloud-PaaS (Platform as a Service) deployed. Railway baut das bestehende Dockerfile automatisch, stellt einen managed PostgreSQL-Service bereit und publiziert die API unter einer öffentlichen HTTPS-URL. Das Deployment ist vollständig reproduzierbar via `git push` oder Railway CLI.

| Kriterium | Lösung |
|---|---|
| Öffentliche HTTPS-URL | Railway stellt automatisch `https://<service>.up.railway.app` bereit |
| Config via Env-Vars | Alle Secrets im Railway-Dashboard, kein Klartext im Repository |
| `.env.example` im Repo | ✓ vorhanden, dokumentiert alle benötigten Variablen |
| Persistenter Datenspeicher | Railway Postgres Plugin (managed, automatisch gesichert) |
| Reproduzierbares Deployment | `git push main` oder `railway up` genügen |
| Strukturierte Logs | stdout-Logs sichtbar im Railway Dashboard → "Deployments → Logs" |

---

## Architektur-Diagramm (Railway)

```
  GitHub Repository (main branch)
         │
         │  git push  /  railway up
         ▼
  ┌──────────────────────────────────────────────────┐
  │  Railway Project: BombasticIFC                    │
  │                                                   │
  │  ┌────────────────────────────────────────────┐  │
  │  │  Service: api                               │  │
  │  │  Build:   Dockerfile (multi-stage)          │  │
  │  │  Port:    8080 (ASPNETCORE_URLS)            │  │
  │  │  URL:     https://bombasticifc.up.railway.app│  │
  │  │  Health:  GET /health → 200 OK              │  │
  │  │                                             │  │
  │  │  Env vars (Railway dashboard):              │  │
  │  │  - DATABASE_URL (auto, Railway Postgres)    │  │
  │  │  - JWT_SECRET                               │  │
  │  │  - ASPNETCORE_ENVIRONMENT=Production        │  │
  │  └──────────────────┬──────────────────────────┘  │
  │                     │  internal network             │
  │  ┌──────────────────▼──────────────────────────┐  │
  │  │  Plugin: Postgres (managed by Railway)       │  │
  │  │  Version: PostgreSQL 16                      │  │
  │  │  DATABASE_URL → auto-injected in api service │  │
  │  │  Backups: automatisch, 7-Tage-Retention      │  │
  │  └──────────────────────────────────────────────┘  │
  └──────────────────────────────────────────────────┘
```

---

## Setup-Anleitung (Reproduzierbar durch Dritte)

### Voraussetzungen

| Tool | Version |
|---|---|
| Git | 2.x+ |
| Railway CLI (optional) | `npm install -g @railway/cli` |
| GitHub-Account | Für automatischen Trigger via Push |

### Option A: Deployment via GitHub-Integration (empfohlen)

```bash
# 1. Repository auf GitHub vorhanden (bereits der Fall)

# 2. railway.toml ist bereits im Repository vorhanden
#    → Railway erkennt das Dockerfile automatisch

# 3. Railway-Projekt erstellen
#    → https://railway.app/new
#    → "Deploy from GitHub repo" wählen
#    → Repository "BombasticIFCcluster" auswählen

# 4. PostgreSQL-Plugin hinzufügen
#    → Im Railway-Dashboard: "+ New" → "Database" → "Add PostgreSQL"
#    → DATABASE_URL wird automatisch in den api-Service injiziert

# 5. Umgebungsvariablen im Dashboard setzen
#    → api-Service → "Variables" Tab
```

| Variable | Wert | Herkunft |
|---|---|---|
| `DATABASE_URL` | automatisch | Railway Postgres Plugin |
| `ConnectionStrings__DefaultConnection` | `${{Postgres.DATABASE_URL}}` | Railway Variable Reference |
| `JWT_SECRET` | `openssl rand -base64 32` | manuell setzen |
| `ASPNETCORE_ENVIRONMENT` | `Production` | manuell setzen |
| `JwtSettings__Issuer` | `BombasticIFC` | manuell setzen |
| `JwtSettings__Audience` | `BombasticIFC-Client` | manuell setzen |
| `JwtSettings__ExpirationMinutes` | `120` | manuell setzen |
| `StoragePath` | `/data/storage` | manuell setzen |

```bash
# 6. Deployment starten
#    → Erster Deploy startet automatisch nach GitHub-Verbindung
#    → Jeder weitere Push auf main triggert ein neues Deployment

# 7. URL abrufen
#    → Railway Dashboard → api-Service → "Settings" → "Domains"
#    → https://<projektname>.up.railway.app
```

### Option B: Deployment via Railway CLI

```bash
# 1. CLI installieren und einloggen
npm install -g @railway/cli
railway login

# 2. Repository klonen (falls nicht lokal vorhanden)
git clone https://github.com/<user>/BombasticIFCcluster.git
cd BombasticIFCcluster

# 3. Neues Railway-Projekt erstellen und verknüpfen
railway init
railway link

# 4. PostgreSQL-Plugin hinzufügen
railway add --plugin postgresql

# 5. Umgebungsvariablen setzen
railway variables set JWT_SECRET=$(openssl rand -base64 32)
railway variables set ASPNETCORE_ENVIRONMENT=Production
railway variables set JwtSettings__Issuer=BombasticIFC
railway variables set JwtSettings__Audience=BombasticIFC-Client
railway variables set JwtSettings__ExpirationMinutes=120
railway variables set StoragePath=/data/storage
# ConnectionStrings__DefaultConnection via Railway Variable Reference:
railway variables set "ConnectionStrings__DefaultConnection=\${{Postgres.DATABASE_URL}}"

# 6. Deployen
railway up

# 7. URL anzeigen
railway open
```

### Health-Check verifizieren

```bash
# Sobald Deployment abgeschlossen (ca. 3-5 Min):
curl https://<projektname>.up.railway.app/health
# Erwartete Antwort: {"status":"healthy","timestamp":"..."}
```

---

## Wichtige Konfigurationsausschnitte

### railway.toml (Config as Code)

```toml
[build]
builder = "dockerfile"
dockerfilePath = "Dockerfile"

[deploy]
healthcheckPath = "/health"
healthcheckTimeout = 300
restartPolicyType = "on_failure"
restartPolicyMaxRetries = 10
```

- `healthcheckPath`: Railway wartet auf `200 OK` von `/health` bevor es Traffic routet
- `healthcheckTimeout`: 300s Toleranz für den langen .NET-Startup mit xeokit-Initialisierung
- `restartPolicyType = "on_failure"`: Automatischer Neustart bei Crash
- `builder = "dockerfile"`: Explizit das bestehende Multi-Stage-Dockerfile verwenden

### ConnectionString mit Railway Variable Reference

Railway injiziert die Postgres-URL als `DATABASE_URL`. Da .NET den Npgsql-Format-Connection-String erwartet, wird mit einer Railway Variable Reference eine Übersetzung vorgenommen:

```
ConnectionStrings__DefaultConnection = ${{Postgres.DATABASE_URL}}
```

Railway ersetzt `${{Postgres.DATABASE_URL}}` zur Laufzeit mit dem tatsächlichen Wert.

---

## Begründung der wichtigsten Entscheidungen

### Warum Railway statt Render oder Fly.io?

| Platform | Aufwand | Postgres | Free Tier | Dockerfile-Support |
|---|---|---|---|---|
| **Railway** | minimal | Plugin (managed) | ✓ (500h/Monat) | direkt |
| Render | gering | Add-on | ✓ (90 Tage) | ✓ |
| Fly.io | mittel | Fly Postgres | ✓ (limitiert) | ✓, via flyctl |
| Coolify (VPS) | hoch | Plugin | eigener Server | ✓ |

Railway bietet die kürzeste Zeit bis zur ersten laufenden URL: GitHub-Verbindung + Postgres-Plugin + Env-Vars = ca. 5 Minuten Setup.

### Warum nur die API (nicht das Frontend) auf Railway?

Die Prüfungskriterien verlangen eine laufende Applikation mit persistentem Datenspeicher und öffentlicher URL. Die API erfüllt alle Kriterien:
- `/health` → nachweisbarer Betrieb
- PostgreSQL → persistenter Datenspeicher
- JWT-Auth-Endpoints → echte Applikationslogik

Das Frontend (Vue SPA) wäre via Vercel/Netlify kostenlos deploybar, ist aber für C3 kein Pflichtbestandteil.

### Warum `railway.toml` im Repository?

Config as Code: Das Deployment-Verhalten ist im Repository versioniert und für Dritte reproduzierbar. Kein manuelles Setup über die UI erforderlich — `railway up` genügt.

---

## Reflexion

**Was gut funktioniert hat:**
- Railway erkennt `railway.toml` und `Dockerfile` automatisch — kein zusätzlicher Build-Script nötig
- `/health`-Endpoint (bereits für K8s implementiert) wird direkt als Railway Healthcheck wiederverwendet
- Multi-Stage-Dockerfile hält das Railway-Image schlank (~280MB statt ~1.2GB)

**Was rückblickend anders gelöst würde:**
- Für ein echtes Produktionssystem würde man Railway Volumes für `/data/storage` (IFC-Dateien) konfigurieren — im aktuellen Setup gehen hochgeladene Dateien bei jedem Redeploy verloren
- Automatisches EF-Core-Migration-Seeding beim Start wäre robuster als manuelle Migration-Schritte
- Das Frontend wäre auf Vercel deployed (nächste Ausbaustufe: vollständige Cloud-Architektur mit API + Frontend + DB je auf separater Plattform)