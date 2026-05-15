# C4 — Kubernetes
**Modul:** HFI_DEP | **Auftrag:** C4 | **Punkte:** max. 10

---

## Was wurde umgesetzt und warum

BombasticIFC läuft in einem Kubernetes-Cluster im Namespace `bombasticifccluster`. Die Manifests im Verzeichnis `kubernetes/` beschreiben den vollständigen Cluster-Zustand deklarativ. Als Laufzeitumgebung wird Minikube auf der lokalen VM eingesetzt.

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

## Setup-Anleitung (Minikube)

Voraussetzungen: Docker 24.x+, Minikube 1.32+, kubectl 1.28+.

**Schritt 1 — Minikube starten und Ingress aktivieren**

```bash
minikube start --cpus=2 --memory=4096 --disk-size=50g --driver=docker
minikube addons enable ingress
```

Ohne das Ingress-Addon werden die Ingress-Controller-Pods nicht gestartet und die Ingress-Ressource hat keine Wirkung. Das ist der häufigste Grund, warum `http://<minikube-ip>/` nicht erreichbar ist.

**Schritt 2 — Storage-Verzeichnisse und Secrets anlegen**

Die `hostPath`-Volumes benötigen existierende Verzeichnisse auf der Minikube-Node. Secrets werden niemals committet und müssen einmalig manuell erstellt werden — vor dem ersten `kubectl apply`.

**Schritt 3 — Manifests anwenden**

Die Manifests werden in der Reihenfolge angewendet, wie sie im Repository-Verzeichnis `kubernetes/` aufgelistet sind: Namespace, ConfigMap, PersistentVolumes, Postgres, API, Frontend, Ingress. Die Pipeline übernimmt das ab dem zweiten Deployment automatisch (C2/C3).

**Zugriff:** `minikube ip` liefert die Cluster-IP (z.B. `192.168.49.2`). Frontend: `http://192.168.49.2/`, API: `http://192.168.49.2/api`, Swagger: `http://192.168.49.2:30080/swagger`.

---

## Bekannte Probleme

### EF Core Migrationen und Image-Staleness

Die API ruft beim Start `db.Database.Migrate()` auf. Dadurch werden nur Migrationen angewendet, die im laufenden Image enthalten sind. Wird nach dem letzten Image-Push eine neue Migration zum Codebase hinzugefügt, fehlen die entsprechenden Spalten in der Datenbank — die API liefert HTTP 500 auf alle Endpunkte, die diese Spalten anfassen.

Konkretes Beispiel aus diesem Projekt: Die Spalten `RefreshTokenHash` und `RefreshTokenExpiresAt` fehlten in der `Users`-Tabelle, weil das laufende Image älter war als die Migration, die sie hinzufügte. Die Migration existierte im Code, war aber nicht im Image kompiliert.

**Lösung:** Einen neuen Commit pushen. Die Pipeline baut ein aktuelles Image, das die Migration enthält. Nach dem Rolling Update wendet der neue Pod sie beim Start an. Für sofortige Hotfixes ohne Push: Port-Forward zu Postgres öffnen und `dotnet ef database update` lokal ausführen:

```bash
kubectl port-forward svc/postgres-service 5432:5432 -n bombasticifccluster
# dann lokal:
dotnet ef database update --startup-project src/BombasticIFC.API
```

---

## Wichtige Manifest-Ausschnitte

### Liveness + Readiness + Startup Probes (api-deployment.yaml)

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

Die Readiness-Probe verhindert Traffic zu einem Pod, der noch nicht bereit ist. Die Liveness-Probe erkennt hängende Pods und startet sie neu. Die Startup-Probe gibt der .NET-App bis zu 150 Sekunden Zeit zum Hochfahren (30 × 5 s), bevor Liveness greift.

### Rolling Update (api-deployment.yaml)

```yaml
strategy:
  type: RollingUpdate
  rollingUpdate:
    maxUnavailable: 0
    maxSurge: 1
```

`maxUnavailable: 0` garantiert, dass während des gesamten Updates mindestens ein Pod erreichbar bleibt (Zero-Downtime). `maxSurge: 1` erlaubt einen temporär zusätzlichen Pod.

Nicht-sensitive Werte (Datenbankname, Benutzername) liegen im ConfigMap; sensitive Werte (Passwörter, JWT-Secret, Connection-String) liegen im Secret und werden per Name-Referenz in den Deployment-Env-Block eingebunden.

---

## Begründung der wichtigsten Entscheidungen

### Warum kubectl apply statt Helm?
Für ~7 Manifests ist Helm-Overhead nicht gerechtfertigt. Die Manifests sind direkt lesbar und versioniert. Bei einem grösseren Projekt mit Umgebungs-Overrides würde Helm sinnvoll.

### Warum imagePullPolicy: IfNotPresent?
Der Deploy-Job lädt Images per `minikube image load` in die interne Minikube-Registry, bevor er `kubectl apply` ausführt. `IfNotPresent` weist Kubernetes an, das bereits vorhandene Image zu verwenden statt einen Registry-Pull zu versuchen. Das vermeidet, GHCR-Zugangsdaten als Secret im Cluster zu hinterlegen.

### Warum maxUnavailable: 0?
Die Startup-Probe gibt dem API-Pod 150 Sekunden Zeit. Mit `maxUnavailable: 0` bleibt immer mindestens ein alter Pod erreichbar, während der neue hochfährt — auch wenn das lange dauert.

### Warum sind Secrets gitignored?
`kubernetes/secrets.yaml` enthält Klartext-Credentials. Einmal in Git committet, bleiben sie dauerhaft in der History. Die Datei wird deshalb gitignored. `kubernetes/secrets.yaml.template` dokumentiert die Struktur ohne echte Werte.

---

## Reflexion

**Was gut funktioniert hat:**
- Liveness/Readiness/Startup-Probes funktionieren zuverlässig — unhealthy Pods werden automatisch ersetzt
- Rolling Update mit `maxUnavailable=0` sorgt für Zero-Downtime-Deployments
- Die saubere Trennung von ConfigMap und Secret hält Konfiguration und Credentials auseinander

**Was rückblickend anders gelöst würde:**
- `hostPath`-Volumes sind an die Node gebunden; für echte Ausfallsicherheit wäre Longhorn oder ein NFS-Mount nötig
- Das Migrations-Problem (Image-Staleness) zeigt, dass ein Pre-Deploy-Check des DB-Schemas eine ganze Klasse von 500-Fehlern verhindern würde
- Sealed Secrets oder der External Secrets Operator würde `secrets.yaml` sicher commitbar machen
- NetworkPolicies würden die Pod-zu-Pod-Kommunikation auf das Notwendige beschränken
