# C2 — CI/CD mit GitHub Actions
**Modul:** HFI_DEP | **Auftrag:** C2 | **Punkte:** max. 10

---

## Was wurde umgesetzt und warum

Für BombasticIFC wurde eine GitHub-Actions-Pipeline implementiert, die bei jedem Push auf den `main`-Branch automatisch:
1. Code-Qualität prüft (dotnet test + TypeScript-Typprüfung)
2. Docker Images baut
3. Images in GitHub Container Registry (GHCR) veröffentlicht

Die Pipeline liegt unter `.github/workflows/ci.yml`.

---

## Pipeline-Diagramm

```
git push main
      │
      ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Job 1: quality                                                      │
│  ─────────────────────────────────────────────────────────────────  │
│  ✓ Cache NuGet packages (speeds up restore)                          │
│  ✓ dotnet restore → dotnet build → dotnet test                       │
│  ✓ npm ci --legacy-peer-deps (npm cache)                             │
│  ✓ vue-tsc --noEmit (TypeScript type-check)                          │
│                                                                      │
│  Schlägt fehl → Build + Push laufen NICHT                            │
└─────────────────────────┬───────────────────────────────────────────┘
                          │ needs: quality
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Job 2: build                                                        │
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
│  ─────────────────────────────────────────────────────────────────  │
│  ✓ Login zu ghcr.io mit GITHUB_TOKEN                                 │
│  ✓ Build + Push API image → ghcr.io/<user>/bombasticifccluster-api   │
│  ✓ Build + Push Frontend image → ghcr.io/<user>/...-frontend        │
│  Tags: sha-abc1234 (eindeutig) + latest (menschenlesbar)             │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Setup-Anleitung (Reproduzierbar durch Dritte)

### Voraussetzungen

- GitHub Repository (public oder private)
- Für public repos: GHCR ist kostenlos nutzbar
- Für private repos: `GITHUB_TOKEN` hat automatisch `packages: write` Permission

### Schritte

```bash
# 1. Repository klonen und auf main pushen
git clone https://github.com/<user>/BombasticIFCcluster.git
cd BombasticIFCcluster

# 2. Pipeline ist bereits unter .github/workflows/ci.yml vorhanden
# Keine weitere Konfiguration nötig — GITHUB_TOKEN ist automatisch verfügbar

# 3. Push auf main triggert die Pipeline
git push origin main

# 4. Pipeline-Status prüfen
# GitHub → Actions Tab → "CI/CD — BombasticIFC"
```

### Benötigte Secrets

| Secret | Woher | Verwendung |
|---|---|---|
| `GITHUB_TOKEN` | Automatisch von GitHub bereitgestellt | GHCR Login (packages: write) |

Keine manuellen Secrets nötig. Der `GITHUB_TOKEN` hat für dasselbe Repository automatisch Schreibrechte auf GHCR.

### Images in GHCR einsehen

Nach erfolgreichem Push sind die Images sichtbar unter:
```
https://github.com/<user>/BombasticIFCcluster/pkgs/container/bombasticifccluster-api
https://github.com/<user>/BombasticIFCcluster/pkgs/container/bombasticifccluster-frontend
```

---

## Wichtige Workflow-Ausschnitte

### Tagging-Strategie

```yaml
tags: |
  type=sha,prefix=sha-,format=short   # Eindeutig: sha-abc1234
  type=raw,value=latest               # Menschenlesbar: latest
```

Zwei Tags pro Image: ein unveränderlicher (Git-SHA) für Nachvollziehbarkeit, und `latest` für einfachen Zugriff.

### Docker Layer Caching (GHA Cache)

```yaml
- uses: docker/build-push-action@v5
  with:
    cache-from: type=gha,scope=api
    cache-to: type=gha,mode=max,scope=api
```

GitHub Actions Cache speichert Docker-Layer. Zweite Builds ohne Code-Änderung laufen 3-5x schneller.

### NuGet + npm Caching

```yaml
- uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj') }}
```

Cache-Key basiert auf `.csproj`-Hashes. Ändert sich keine Projekt-Datei, wird der Cache vollständig wiederverwendet.

### Push nur auf main (nicht auf PRs)

```yaml
push:
  if: github.event_name == 'push' && github.ref == 'refs/heads/main'
```

Pull Requests triggern quality + build, aber keinen Push. So werden nur verifizierten Builds in der Registry gespeichert.

---

## Begründung der wichtigsten Entscheidungen

### Warum GHCR statt Docker Hub?
GHCR ist direkt in GitHub integriert — kein separater Account, GITHUB_TOKEN reicht für Login. Für öffentliche Repos kostenlos und ohne Rate-Limit-Probleme. Die Images liegen im selben GitHub-Namespace wie der Code.

### Warum drei separate Jobs (quality / build / push)?
Fail-fast-Prinzip: Scheitert quality (z.B. Test-Fehler), läuft build gar nicht erst. Das spart teure Build-Minuten und gibt sofort klares Feedback wo das Problem liegt.

### Warum vue-tsc statt ESLint?
Das Projekt hat kein ESLint konfiguriert. vue-tsc --noEmit prüft TypeScript-Typen über das gesamte Vue-Projekt und fängt Typ-Fehler, fehlende Properties und falsche Interface-Implementierungen auf — aussagekräftiger als reines Linting.

### Warum workflow_dispatch als Trigger?
Erlaubt manuelle Pipeline-Starts über die GitHub UI ohne Code-Push. Nützlich für Debugging und Re-Deployments.

---

## Reflexion

**Was gut funktioniert hat:**
- GITHUB_TOKEN-basiertes GHCR-Login ohne manuelle Secret-Verwaltung
- GHA Docker-Layer-Cache beschleunigt Rebuilds erheblich
- Job-Dependencies (needs: quality) erzwingen die richtige Reihenfolge

**Was rückblickend anders gelöst würde:**
- Separate Vitest/Jest-Tests für das Frontend wären sinnvoll (aktuell nur vue-tsc)
- Automatisches Deployment auf Kubernetes via kubectl oder Helm wäre die nächste Stufe (Continuous Deployment statt Delivery)
- Semantic Versioning (v1.2.3-Tags) für produktionsreife Image-Versionen
