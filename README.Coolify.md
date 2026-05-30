# BombasticIFC — Coolify Deployment

Vollständige Dokumentation des Coolify-Clusters inkl. Installationsanleitung, Architektur, CI/CD-Integration und Monitoring.

---

## Inhaltsverzeichnis

1. [Architektur & Cluster-Übersicht](#architektur--cluster-übersicht)
2. [Services im Detail](#services-im-detail)
3. [Netzwerk & Routing](#netzwerk--routing)
4. [Volumes & Datenpersistenz](#volumes--datenpersistenz)
5. [Coolify installieren](#schritt-1--coolify-installieren)
6. [Coolify Ersteinrichtung](#schritt-2--coolify-einrichten-ersteinrichtung)
7. [Git-Repository verbinden](#schritt-3--source-git-repository-verbinden)
8. [Docker Compose Ressource anlegen](#schritt-4--neue-ressource-docker-compose-erstellen)
9. [Environment Variables setzen](#schritt-5--environment-variables-setzen)
10. [Erstes Deploy auslösen](#schritt-6--deploy-auslösen)
11. [Automatisches Deploy via Webhook](#schritt-7--automatisches-deploy-via-git-push-webhook)
12. [CI/CD-Pipeline (GitHub Actions)](#schritt-8--cicd-pipeline-github-actions)
13. [Monitoring](#schritt-9--monitoring)
14. [Logs & Debugging](#schritt-10--logs--debugging)
15. [Umgebungsvariablen-Referenz](#umgebungsvariablen--referenz)
16. [Troubleshooting](#troubleshooting)

---

## Architektur & Cluster-Übersicht

```
┌─────────────────────────────────────────────────────────┐
│                    Coolify Host (VM)                    │
│                                                         │
│  ┌──────────────────────────────────────────────────┐   │
│  │          Docker Compose Stack                    │   │
│  │                                                  │   │
│  │  ┌─────────────┐   Port 8090:80                 │   │
│  │  │  frontend   │◄──────────────────── Browser   │   │
│  │  │ nginx:alpine│                                 │   │
│  │  │  Vue 3 SPA  │                                 │   │
│  │  └──────┬──────┘                                 │   │
│  │         │ /api/* → http://api:8080 (intern)      │   │
│  │  ┌──────▼──────┐                                 │   │
│  │  │     api     │  expose :8080 (kein Port-Map)   │   │
│  │  │  .NET 8 /   │                                 │   │
│  │  │ ASP.NET Core│                                 │   │
│  │  └──────┬──────┘                                 │   │
│  │         │ Host=postgres;Port=5432                 │   │
│  │  ┌──────▼──────┐                                 │   │
│  │  │  postgres   │  Volume: postgres-data           │   │
│  │  │ PG 16 alpine│  Volume: ifc-storage             │   │
│  │  └─────────────┘                                 │   │
│  │                                                  │   │
│  │  ┌─────────────┐  (one-shot, läuft einmalig)     │   │
│  │  │   migrate   │  EF Core Migrations              │   │
│  │  └─────────────┘                                 │   │
│  └──────────────────────────────────────────────────┘   │
│                                                         │
│  Coolify UI :8000          Grafana :3000                │
└─────────────────────────────────────────────────────────┘
```

**Startsequenz der Services (healthcheck-gesteuert):**

```
postgres (pg_isready healthy)
    └─► migrate (EF Core --migrate, exit 0)
            └─► api (bash /dev/tcp healthy)
                    └─► frontend (startet)
```

---

## Services im Detail

### `postgres` — Datenbank

| Eigenschaft | Wert |
|---|---|
| Image | `postgres:16-alpine` |
| Datenbank | `bombasticifcdb` |
| User | `postgres` |
| Passwort | `${DB_PASSWORD}` (aus Coolify Env Var) |
| Healthcheck | `pg_isready -U postgres -d bombasticifcdb` (alle 10 s) |
| Restart | `unless-stopped` |
| Volume | `postgres-data:/var/lib/postgresql/data` |

### `migrate` — Datenbankmigrationen (One-Shot)

| Eigenschaft | Wert |
|---|---|
| Image | Wird aus `Dockerfile` gebaut (selbes Image wie `api`) |
| Entrypoint | `dotnet BombasticIFC.API.dll --migrate` |
| Restart | `no` (läuft einmalig, danach beendet) |
| Abhängigkeit | `postgres: service_healthy` |
| Exit-Code | `0` = Erfolg, `1` = Migration fehlgeschlagen |

Der `migrate`-Service implementiert das **InitContainer-Muster** aus Kubernetes in Docker Compose: Migrationen werden einmalig vor dem API-Start ausgeführt. Schlägt die Migration fehl, startet die `api` nicht (`service_completed_successfully`).

### `api` — Backend (.NET 8 / ASP.NET Core)

| Eigenschaft | Wert |
|---|---|
| Image | Wird aus `Dockerfile` gebaut (Multi-Stage, 4 Stages) |
| Interner Port | `8080` (nur via `expose`, kein Host-Port-Mapping) |
| Healthcheck | `bash -c '</dev/tcp/localhost/8080'` (alle 15 s, start_period 60 s) |
| Restart | `unless-stopped` |
| Abhängigkeit | `postgres: service_healthy` + `migrate: service_completed_successfully` |
| Volume | `ifc-storage:/data/storage` |
| Non-Root | Läuft als `appuser` (USER-Direktive im Dockerfile) |

**Wichtig:** Die API ist nicht direkt nach aussen exponiert. Alle Anfragen laufen über den nginx-Proxy im Frontend-Container (`/api/` → `http://api:8080/api/`).

### `frontend` — Vue 3 SPA (nginx)

| Eigenschaft | Wert |
|---|---|
| Image | Wird aus `frontend/Dockerfile` gebaut (Multi-Stage, 2 Stages) |
| Externer Port | `8090:80` |
| Restart | `unless-stopped` |
| Abhängigkeit | `api: service_healthy` |
| Proxy | `location /api/` → `${API_UPSTREAM}/api/` via `envsubst` |
| Security Headers | X-Frame-Options, CSP, X-Content-Type-Options, Referrer-Policy |
| Caching | Static Assets: 1 Jahr / `index.html`: no-store |

---

## Netzwerk & Routing

Docker Compose erstellt automatisch ein internes Bridge-Netzwerk. Alle Services kommunizieren über ihre Service-Namen als DNS-Hostnamen:

```
frontend  →  http://api:8080       (nginx proxy_pass)
api       →  Host=postgres;Port=5432  (EF Core Connection String)
migrate   →  Host=postgres;Port=5432  (EF Core Migration)
```

Von aussen ist **nur Port 8090** erreichbar (Frontend). Die API ist bewusst nicht direkt exponiert — alle API-Anfragen laufen durch den nginx-Reverse-Proxy des Frontend-Containers. Der Wert `API_UPSTREAM` wird beim Container-Start per `envsubst` in die nginx-Konfiguration eingesetzt.

**HTTPS:** Nicht aktiv — die VM ist intentionally nicht öffentlich erreichbar (kein Public IP, kein eingehender Traffic). HTTP-01 Let's Encrypt Challenge setzt eine öffentliche IP voraus. Alternativen (DNS-01 Challenge, Cloudflare Tunnel, Self-Signed Certificate) wurden evaluiert und als Architekturentscheidung im HauptREADME dokumentiert.

---

## Volumes & Datenpersistenz

| Volume | Mountpunkt | Inhalt |
|---|---|---|
| `postgres-data` | `/var/lib/postgresql/data` | PostgreSQL-Datenbankdateien |
| `ifc-storage` | `/data/storage` | Hochgeladene IFC-Dateien + konvertierte XKT-Dateien |

Beide Volumes werden von Docker verwaltet und überleben einen `docker compose down` (ohne `--volumes`). Beim Löschen des Stacks in Coolify müssen Volumes manuell entfernt werden, falls ein sauberer Neustart gewünscht wird:

```bash
docker volume rm <stack-name>_postgres-data
docker volume rm <stack-name>_ifc-storage
```

---

## Schritt 1 — Coolify installieren

Auf dem Server als `root` ausführen:

```bash
curl -fsSL https://cdn.coollabs.io/coolify/install.sh | bash
```

Das Skript installiert automatisch:
- Docker Engine + Docker Compose Plugin
- Coolify-Plattform (läuft selbst als Docker-Stack)
- Traefik als Reverse Proxy (intern)

Nach Abschluss ist die Coolify-UI erreichbar unter:

```
http://<server-ip>:8000
```

**Firewall öffnen** (falls `ufw` aktiv):

```bash
ufw allow 8000   # Coolify UI
ufw allow 8090   # BombasticIFC Frontend
ufw allow 3000   # Grafana (optional)
```

---

## Schritt 2 — Coolify einrichten (Ersteinrichtung)

1. Browser öffnen: `http://<server-ip>:8000`
2. **Admin-Account anlegen** — E-Mail-Adresse + sicheres Passwort eingeben
3. **Server hinzufügen:**
   - Linke Sidebar → **Servers** → **Add Server**
   - „Use localhost" wählen (Coolify läuft auf demselben Server)
   - **Validate & continue** — Coolify testet die Docker-Verbindung
4. **Projekt anlegen:**
   - Sidebar → **Projects** → **New Project**
   - Name: `BombasticIFC`
   - Environment: `production`

---

## Schritt 3 — Source (Git-Repository) verbinden

1. Sidebar → **Sources** → **Add a new Source**
2. Verbindungstyp wählen:

**Option A — GitHub App (empfohlen):**
- GitHub App auswählen → **Register Now**
- Coolify führt den OAuth-Flow auf GitHub durch
- Berechtigungen auf das Repository `LukisBombasticIFCviewer` einschränken
- Nach Abschluss: Quelle gespeichert, Coolify kann Branches und Commits lesen

**Option B — Deploy Key (ohne OAuth):**
- „Private Key" wählen → Coolify generiert ein SSH-Schlüsselpaar
- Öffentlichen Key kopieren
- GitHub → Repo → **Settings → Deploy Keys → Add deploy key**
- Titel: `Coolify`, Key einfügen, „Allow write access" **deaktiviert** lassen
- In Coolify speichern

3. Quelle speichern

---

## Schritt 4 — Neue Ressource (Docker Compose) erstellen

1. Projekt `BombasticIFC` → Environment `production` öffnen
2. **+ New Resource** klicken
3. **Docker Compose** auswählen
4. Konfiguration:

| Feld | Wert |
|---|---|
| Source | Verbundenes GitHub-Repo |
| Repository | `<github-user>/LukisBombasticIFCviewer` |
| Branch | `coolify` |
| Compose File | `docker-compose.coolify.yml` |
| Build Pack | `Docker Compose` |

5. **Continue** → Ressource wird angelegt

---

## Schritt 5 — Environment Variables setzen

Ressource öffnen → Tab **Environment Variables**.

Folgende Variablen müssen gesetzt werden — jeweils als **Secret** markieren (Schloss-Symbol):

| Variable | Beispielwert | Pflicht | Beschreibung |
|---|---|---|---|
| `DB_PASSWORD` | `s3cur3R@nd0mPw!` | Ja | PostgreSQL-Passwort — min. 16 Zeichen |
| `JWT_SECRET` | `mein-langer-zufaelliger-string-32chars` | Ja | JWT-Signing-Key — min. 32 Zeichen |

**Sichere Werte generieren (lokal ausführen):**

```bash
# DB_PASSWORD
openssl rand -base64 24

# JWT_SECRET
openssl rand -base64 32
```

> **Warum kein `JwtSettings__Secret` in der Compose-Datei?**
> Coolify injiziert Env Vars direkt als Laufzeit-Variablen in den Container. Die `${VAR}`-Substitution via `.env`-Datei ist in Coolify unzuverlässig. `Program.cs` liest deshalb zuerst `JwtSettings__Secret`, dann fällt es auf `JWT_SECRET` zurück — der flache Name, den Coolify direkt setzt.

---

## Schritt 6 — Deploy auslösen

1. Tab **General** → **Deploy** klicken
2. Coolify baut alle Images aus dem Repository (dauert beim ersten Mal 5–10 Minuten wegen xeokit-convert Native-Addon-Kompilierung)
3. Tab **Logs** zeigt den Live-Output von Build und Startup

**Erwarteter Ablauf:**

```
[postgres]   database system is ready to accept connections
[migrate]    Running EF Core migrations...
[migrate]    EF Core migrations completed successfully.
[migrate]    exited with code 0
[api]        Now listening on: http://[::]:8080
[api]        Application started.
[frontend]   /docker-entrypoint.sh: Launching /docker-entrypoint.d/20-envsubst-on-templates.sh
[frontend]   nginx: configuration file /etc/nginx/conf.d/default.conf test is successful
```

App erreichbar unter:

```
http://<server-ip>:8090
```

Swagger UI erreichbar unter:

```
http://<server-ip>:8090/api/swagger
```

---

## Schritt 7 — Automatisches Deploy via Git-Push (Webhook)

Damit jeder `git push origin coolify` automatisch ein Deploy auslöst:

1. Ressource → Tab **General** → **Webhook URL** kopieren
2. GitHub → Repo → **Settings → Webhooks → Add webhook**

| Feld | Wert |
|---|---|
| Payload URL | `<kopierte Webhook URL>` |
| Content type | `application/json` |
| Secret | leer lassen (Coolify prüft intern) |
| Events | **Just the push event** |

3. **Add webhook** speichern

Ab jetzt: `git push origin coolify` → Coolify deployt automatisch den neuen Stand.

---

## Schritt 8 — CI/CD-Pipeline (GitHub Actions)

Die Datei `.github/workflows/ci.yml` läuft auf jedem Push auf den `coolify`-Branch und prüft die Codequalität, bevor Coolify deployt.

**Jobs auf dem `coolify`-Branch:**

| Job | Runner | Was |
|---|---|---|
| `quality` | `ubuntu-latest` | .NET build + `dotnet test` + TypeScript type-check (`vue-tsc`) |
| `build` | `ubuntu-latest` | Docker-Images bauen (kein Push, nur Build-Check mit Layer-Cache) |

Push und Deploy-Jobs laufen **nur auf `main`** — Coolify übernimmt das Deployment für den `coolify`-Branch eigenständig via Webhook.

**Fail-Fast-Prinzip:** Schlägt `quality` fehl, läuft `build` nicht. Der Webhook wird trotzdem von GitHub ausgelöst — Coolify deployt unabhängig vom CI-Status. Qualitätsprobleme werden daher im GitHub Actions Tab sichtbar, blockieren aber kein Coolify-Deploy.

---

## Schritt 9 — Monitoring

### Coolify Native Metriken

Coolify überwacht alle Container automatisch ohne zusätzliche Konfiguration.

**Wo zu finden:** Ressource → Tab **Monitoring**

Verfügbare Metriken:
- CPU-Auslastung (pro Container, Live-Graph)
- RAM-Verbrauch (pro Container, Live-Graph)
- Container-Status (`running` / `stopped` / `restarting`)
- Restart-Count
- Uptime

### Grafana — Datenbankmonitoring

Für tieferes PostgreSQL-Monitoring (Tabellengrößen, aktive Verbindungen, Query-Statistiken):

**Grafana als neuen Service in Coolify hinzufügen:**

1. Projekt → **+ New Resource** → **Docker Image**
2. Image: `grafana/grafana:latest`
3. Port: `3000` nach aussen mappen
4. Deploy → Grafana erreichbar unter `http://<server-ip>:3000`

**PostgreSQL Data Source einrichten:**

1. Grafana öffnen → Standard-Login: `admin` / `admin` → Passwort sofort ändern
2. Linkes Menü → **Connections → Data Sources → Add data source**
3. **PostgreSQL** auswählen
4. Verbindungsdetails:

| Feld | Wert |
|---|---|
| Host | `postgres:5432` |
| Database | `bombasticifcdb` |
| User | `postgres` |
| Password | `<DB_PASSWORD>` |
| SSL Mode | `disable` |

5. **Save & Test** — Verbindung bestätigt

> **Hinweis zum Netzwerk:** Grafana und der Postgres-Container müssen im selben Docker-Netzwerk liegen. Falls Grafana als separate Coolify-Ressource läuft, muss das Netzwerk in der Compose-Konfiguration explizit geteilt werden, oder der Postgres-Port muss temporär nach aussen gemappt werden.

**Nützliche Grafana-Queries:**

```sql
-- Tabellengrössen
SELECT relname AS table, pg_size_pretty(pg_total_relation_size(relid)) AS size
FROM pg_catalog.pg_statio_user_tables ORDER BY pg_total_relation_size(relid) DESC;

-- Aktive Verbindungen
SELECT count(*) FROM pg_stat_activity WHERE state = 'active';

-- Konversionsjobs nach Status
SELECT status, count(*) FROM "ConversionJobs" GROUP BY status;
```

---

## Schritt 10 — Logs & Debugging

### In der Coolify UI

Ressource → Tab **Logs** → Service aus Dropdown wählen:
- `frontend` — nginx access/error logs
- `api` — ASP.NET Core structured logs
- `postgres` — PostgreSQL startup und query logs
- `migrate` — Migrationsergebnis (exit 0 oder exit 1)

### Per SSH direkt auf dem Server

```bash
# Alle laufenden Container anzeigen
docker ps

# Live-Logs eines bestimmten Containers
docker logs <container-name> -f --tail 100

# Alle Container des Compose-Stacks anzeigen
docker compose -f /path/to/docker-compose.coolify.yml ps

# In den API-Container einsteigen (Debugging)
docker exec -it <api-container-name> bash

# Health-Endpoint manuell prüfen
curl http://localhost:8090/api/health
```

### Migrations-Log prüfen

```bash
# Letzten Lauf des migrate-Containers anzeigen (auch nach exit)
docker logs $(docker ps -a --filter "name=migrate" --format "{{.Names}}" | head -1)
```

---

## Umgebungsvariablen — Referenz

| Variable | Pflicht | Default | Gesetzt durch | Beschreibung |
|---|---|---|---|---|
| `DB_PASSWORD` | Ja | — | Coolify UI | PostgreSQL-Passwort für `postgres`-User |
| `JWT_SECRET` | Ja | — | Coolify UI | JWT-Signing-Key, min. 32 Zeichen (HS256) |
| `ASPNETCORE_ENVIRONMENT` | Nein | `Production` | `docker-compose.coolify.yml` | .NET-Umgebung |
| `StoragePath` | Nein | `/data/storage` | `docker-compose.coolify.yml` | Container-Pfad für IFC/XKT-Dateien |
| `API_UPSTREAM` | Nein | `http://api:8080` | `docker-compose.coolify.yml` | nginx proxy target (Frontend → API) |
| `ConnectionStrings__DefaultConnection` | Ja | — | `docker-compose.coolify.yml` (mit `${DB_PASSWORD}`) | EF Core PostgreSQL Connection String |

---

## Troubleshooting

| Problem | Ursache | Lösung |
|---|---|---|
| Deploy bleibt bei `migrate` stecken | PostgreSQL noch nicht ready | Warten — `start_period: 15 s` im Healthcheck, dann automatisch retry |
| `migrate` exited with code 1 | Migration fehlgeschlagen (Schema-Konflikt oder DB nicht erreichbar) | Logs des `migrate`-Containers prüfen |
| API-Container startet nicht | `JWT_SECRET` nicht gesetzt oder leer | Environment Variables in Coolify UI prüfen → Deploy neu auslösen |
| Frontend zeigt `502 Bad Gateway` | API noch nicht healthy | Logs der `api`-Instanz prüfen; `start_period: 60 s` abwarten |
| `DB_PASSWORD` leer im Container | Compose `${}`-Substitution fehlgeschlagen | Variable als **Secret** (nicht plain text) in Coolify UI speichern |
| Port 8090 nicht erreichbar | Firewall blockiert | `ufw allow 8090` auf dem Server ausführen |
| Build schlägt fehl bei xeokit-convert | Node.js ABI-Mismatch | Dockerfile verwendet exakt `node:20-alpine` — keine Änderung nötig, ABI 115 ist fest gepinnt |
| Grafana kann Postgres nicht erreichen | Unterschiedliche Docker-Netzwerke | Postgres-Port temporär mappen oder beide Services in dasselbe Netzwerk legen |
| `index.html` zeigt alten Stand nach Deploy | Browser-Cache | `Cache-Control: no-store` ist gesetzt — Hard-Reload (Ctrl+Shift+R) im Browser |
