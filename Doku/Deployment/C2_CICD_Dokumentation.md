# C2 — CI/CD mit GitHub Actions
**Modul:** HFI_DEP | **Auftrag:** C2 | **Punkte:** max. 10

---

## Was wurde umgesetzt und warum

Für BombasticIFC wurde eine GitHub-Actions-Pipeline implementiert, die bei jedem Push auf `main` automatisch vier Jobs ausführt: Code-Qualität prüfen, Images bauen, Images in GHCR veröffentlichen und das Deployment auf dem lokalen VM-Cluster durchführen. Die Pipeline liegt unter `.github/workflows/ci.yml`.

---

## Pipeline-Diagramm

```
git push main
      │
      ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Job 1: quality                                                      │
│  Runner: ubuntu-latest                                               │
│  ─────────────────────────────────────────────────────────────────  │
│  ✓ Cache NuGet packages                                              │
│  ✓ dotnet restore → dotnet build → dotnet test                       │
│  ✓ npm ci --legacy-peer-deps (npm cache)                             │
│  ✓ vue-tsc --noEmit (TypeScript type-check)                          │
│                                                                      │
│  Schlägt fehl → Build + Push + Deploy laufen NICHT                   │
└─────────────────────────┬───────────────────────────────────────────┘
                          │ needs: quality
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Job 2: build                                                        │
│  Runner: ubuntu-latest                                               │
│  ─────────────────────────────────────────────────────────────────  │
│  ✓ Docker Buildx setup                                               │
│  ✓ Metadata-Action: Tags sha-<SHORT_SHA> + latest                    │
│  ✓ Build API image (Docker layer caching via GHA cache)              │
│  ✓ Build Frontend image (Docker layer caching)                       │
│  (kein Push — nur Artefakt-Verifikation)                             │
└─────────────────────────┬───────────────────────────────────────────┘
                          │ needs: build
                          │ only on: push to main (not PRs)
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Job 3: push                                                         │
│  Runner: ubuntu-latest                                               │
│  ─────────────────────────────────────────────────────────────────  │
│  ✓ Login zu ghcr.io mit GITHUB_TOKEN                                 │
│  ✓ Build + Push API image → ghcr.io/<user>/bombasticifccluster-api   │
│  ✓ Build + Push Frontend image → ghcr.io/<user>/...-frontend        │
│  Tags: sha-abc1234 (eindeutig) + latest (menschenlesbar)             │
└─────────────────────────┬───────────────────────────────────────────┘
                          │ needs: push
                          │ only on: push to main (not PRs)
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Job 4: deploy                                                       │
│  Runner: self-hosted (läuft direkt auf der VM)                       │
│  ─────────────────────────────────────────────────────────────────  │
│  ✓ Checkout repository (aktuelle Manifests)                          │
│  ✓ docker/login-action → GHCR via GITHUB_TOKEN                      │
│  ✓ docker pull: api + frontend images                                │
│  ✓ minikube image load: beide Images in Minikube laden              │
│  ✓ kubectl apply: alle Manifests                                     │
│  ✓ kubectl rollout restart: api + frontend                           │
│  ✓ kubectl rollout status --timeout=5m (wartet auf Abschluss)       │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Setup-Anleitung

### Voraussetzungen

- GitHub-Repository (public oder private)
- `GITHUB_TOKEN` ist automatisch verfügbar — keine manuellen Secrets für Jobs 1–3 nötig
- Für Job 4: self-hosted Runner auf der VM installiert und registriert (siehe `LocalVM_Pipeline_Dokumentation.md`)

### Schritte

Ein Push auf `main` genügt, um die gesamte Pipeline zu starten. Die Workflow-Datei liegt bereits im Repository unter `.github/workflows/ci.yml`. Pull Requests triggern nur `quality` und `build`, nicht `push` und `deploy` — so landen ausschliesslich verifizierten Builds in der Registry und auf dem Cluster.

### Self-hosted Runner (Job 4)

Job 4 verwendet `runs-on: self-hosted` und läuft deshalb direkt auf der lokalen VM. Die VM muss einmalig einen GitHub Actions Runner installiert und beim Repository registriert haben. Die Einrichtung ist in `LocalVM_Pipeline_Dokumentation.md` beschrieben. Ausser dem automatischen `GITHUB_TOKEN` sind keine zusätzlichen GitHub-Secrets für den Deploy-Job nötig.

### Benötigte Secrets

| Secret | Woher | Verwendung |
|---|---|---|
| `GITHUB_TOKEN` | Automatisch von GitHub bereitgestellt | GHCR Login (packages: write / read) |

---

## Wichtige Workflow-Ausschnitte

### Tagging-Strategie

```yaml
tags: |
  type=sha,prefix=sha-,format=short   # Eindeutig: sha-abc1234
  type=raw,value=latest               # Menschenlesbar: latest
```

Zwei Tags pro Image: ein unveränderlicher (Git-SHA) für Nachvollziehbarkeit und `latest` für einfachen Zugriff durch den Deploy-Job.

### Docker Layer Caching (GHA Cache)

```yaml
- uses: docker/build-push-action@v5
  with:
    cache-from: type=gha,scope=api
    cache-to: type=gha,mode=max,scope=api
```

GitHub Actions Cache speichert Docker-Layer jobübergreifend. Builds ohne Code-Änderung laufen dadurch 3–5x schneller.

### Login im Deploy-Job

Der Deploy-Job führt `docker/login-action` aus, obwohl er auf der VM läuft. Der Grund: Der Docker-Daemon des self-hosted Runners ist nicht vorauthentifiziert gegenüber GHCR. Die `login-action` authentifiziert per `GITHUB_TOKEN` mit `packages: read`-Berechtigung für genau diesen Job-Lauf. So braucht die VM keine dauerhaft gespeicherten Registry-Credentials.

---

## Begründung der wichtigsten Entscheidungen

### Warum GHCR statt Docker Hub?
GHCR ist direkt in GitHub integriert. Der `GITHUB_TOKEN` reicht für Login und Push — kein separater Account, keine Rate-Limit-Probleme. Images und Code liegen im selben GitHub-Namespace.

### Warum vier separate Jobs?
Fail-fast-Prinzip: Scheitert `quality`, läuft `build` gar nicht. Das spart teure Build-Minuten und gibt sofort klares Feedback, wo das Problem liegt. Der Deploy-Job ist bewusst vom Push getrennt, weil er auf einem anderen Runner-Typ läuft.

### Warum self-hosted Runner statt SSH?
Die VM ist lokal und hat keine öffentliche IP. Cloud-Runner könnten sie nicht per SSH erreichen. Ein self-hosted Runner initiiert eine ausgehende Verbindung zu GitHub und empfängt Jobs — kein Port-Forwarding, kein VPN nötig.

### Warum vue-tsc statt ESLint?
Das Projekt hat kein ESLint konfiguriert. `vue-tsc --noEmit` prüft TypeScript-Typen über das gesamte Vue-Projekt und fängt fehlende Properties und falsche Interface-Implementierungen auf.

---

## Reflexion

**Was gut funktioniert hat:**
- `GITHUB_TOKEN`-basiertes GHCR-Login ohne manuelle Secret-Verwaltung
- GHA Docker-Layer-Cache beschleunigt Rebuilds erheblich
- Job-Dependencies erzwingen die richtige Reihenfolge und verhindern fehlerhafte Deployments

**Was rückblickend anders gelöst würde:**
- Separate Vitest-Tests für das Frontend wären sinnvoll (aktuell nur vue-tsc)
- Das in C4 beschriebene Migrations-Problem zeigt: ein Pre-Deploy-Schritt, der prüft ob die DB-Schema-Version mit dem Image übereinstimmt, würde 500-Fehler nach Deployments verhindern
- Semantic Versioning (v1.2.3-Tags) für produktionsreife Image-Versionen
