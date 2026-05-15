# C3 — Deployment auf lokalem VM (Ubuntu + Minikube)
**Modul:** HFI_DEP | **Auftrag:** C3 | **Punkte:** max. 10

---

## Was wurde umgesetzt und warum

BombasticIFC läuft nicht auf einem Cloud-PaaS, sondern auf einer lokalen Ubuntu-VM. Die VM betreibt Minikube als Kubernetes-Laufzeitumgebung und einen self-hosted GitHub Actions Runner als CD-Agent. Jeder Push auf `main` triggert die Pipeline — die ersten drei Jobs laufen auf GitHub-Cloud-Runnern und pushen Images nach GHCR; der vierte Job läuft auf dem self-hosted Runner der VM, zieht die Images und rollt das Deployment aus.

Kein öffentlicher Endpunkt, keine Cloud-Kosten, volle Kontrolle über die Umgebung.

---

## Architektur-Diagramm

```
GitHub (cloud)                    VM (lokal)
─────────────────                 ──────────────────────────────────
CI-Runner (ubuntu-latest)         Self-hosted Runner
  quality job          ──push──►    deploy job
  build job                           │
  push job → GHCR ────pull────────────►  minikube image load
                                      │
                                      ▼
                                  Minikube Cluster
                                  ┌─────────────────┐
                                  │ namespace:        │
                                  │ bombasticifccluster│
                                  │ frontend (×2)    │
                                  │ api (×2)         │
                                  │ postgres (×1)    │
                                  └─────────────────┘
```

---

## Setup-Anleitung

Voraussetzungen: Ubuntu-VM mit Docker, Minikube, kubectl und einem registrierten GitHub Actions Runner.

**Phase 1 — Cluster vorbereiten**

Minikube mit ausreichend Ressourcen starten und das Ingress-Addon aktivieren. Ohne das Addon werden die Ingress-Controller-Pods nicht gestartet und die Ingress-Ressource hat keine Wirkung.

```bash
minikube start --cpus=2 --memory=4096 --disk-size=50g --driver=docker
minikube addons enable ingress
```

**Phase 2 — Secrets manuell anlegen**

Secrets werden nie committet. Vor dem ersten Deployment müssen sie einmalig auf der VM erstellt werden:

```bash
kubectl create namespace bombasticifccluster
kubectl create secret generic bombasticifccluster-secrets \
  --namespace=bombasticifccluster \
  --from-literal=postgres-user=postgres \
  --from-literal=postgres-password=<sicheres-passwort> \
  --from-literal=postgres-db=bombasticifcdb \
  --from-literal=connection-string="Host=postgres-service;..." \
  --from-literal=jwt-secret=<min-32-zeichen-secret>
```

**Phase 3 — Deployment auslösen**

Ein Push auf `main` genügt. Die Pipeline baut Images, pusht sie nach GHCR, lädt sie per `minikube image load` in den Cluster und führt ein Rolling Update durch. Der aktuelle Deployment-Status ist im GitHub Actions Tab einsehbar.

---

## Wichtige Entscheidungen

### Warum lokal statt Cloud-PaaS?
Die Prüfungsaufgabe verlangt ein Deployment auf einer lokalen VM. Darüber hinaus entfallen Cloud-Kosten und es besteht volle Kontrolle über Kubernetes-Version, Addons und Ressourcen — kein Managed-Service-Overhead.

### Warum Minikube statt Docker Compose allein?
Minikube bietet echte Kubernetes-Semantik: Namespaces, Ingress, Persistent Volumes, Health-Probes, Rolling Updates und Resource Limits. Docker Compose kann das alles nicht. Ausserdem entsprechen die Manifests direkt dem, was auf einem produktiven Cluster laufen würde.

### Warum self-hosted Runner?
Die VM hat keine öffentliche IP. Cloud-Runner von GitHub könnten sie nicht per SSH erreichen. Ein self-hosted Runner stellt eine ausgehende Verbindung zu GitHub her, empfängt den Deploy-Job und führt ihn lokal aus — kein Port-Forwarding, kein VPN.

### Warum minikube image load statt imagePullPolicy: Always?
`minikube image load` lädt das Image direkt in die interne Minikube-Registry. Kubernetes findet es bereits lokal vor und muss nicht auf GHCR zugreifen. Das vermeidet, GHCR-Zugangsdaten als Secret im Cluster zu hinterlegen — ein Betriebssicherheitsvorteil.

---

## Reflexion

**Was gut funktioniert hat:**
- Das Pipeline-zu-VM-Modell funktioniert zuverlässig: Push auf `main` führt zu einem ausgerollten Cluster-Update ohne manuelle Schritte
- Der `/health`-Endpunkt (bereits für K8s implementiert) dient gleichzeitig als Cluster-Liveness-Probe
- `minikube image load` vereinfacht die Credentials-Verwaltung gegenüber einem Pull-Secret im Cluster

**Was rückblickend anders gelöst würde:**
- `hostPath`-Volumes sind node-gebunden — bei einem Neustart der VM oder Migration auf einen anderen Host gehen Daten verloren; Longhorn oder ein NFS-Mount wäre robuster
- Eine öffentliche IP oder ein Cloudflare Tunnel würde externes Testen ohne VPN ermöglichen
