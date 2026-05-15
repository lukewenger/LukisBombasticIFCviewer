# Lokales VM-Deployment mit Minikube und GitHub Actions

**Zweck:** Diese Dokumentation beschreibt das vollstaendige Setup fuer das lokale VM-Deployment von BombasticIFC — von der Einrichtung des Minikube-Clusters bis zum automatisierten CD-Pipeline-Ablauf.

**Zielgruppe:** Entwickler und Betreiber, die den Self-hosted Runner oder den Minikube-Cluster einrichten oder warten.

---

## 1. Ueberblick

Das Deployment laeuft auf einer lokalen Ubuntu-VM. Auf dieser VM laeuft Minikube (ein Einzel-Node-Kubernetes-Cluster), der die drei Anwendungskomponenten betreibt: PostgreSQL, die .NET-API und das Vue-Frontend.

Die Continuous-Delivery-Integration erfolgt ueber einen Self-hosted GitHub Actions Runner, der ebenfalls auf der VM installiert ist. Dieser Runner-Prozess verbindet sich aktiv nach aussen zu GitHub und fragt regelmaessig an, ob Jobs fuer ihn bereitstehen. Es wird keine eingehende Verbindung, kein offener Port und kein Tunnel benoetigt. Sobald ein Push auf `main` erfolgt, werden die Jobs von GitHub an den Runner verteilt, der sie lokal ausfuehrt — dort, wo Minikube und `kubectl` bereits vorhanden sind.

---

## 2. Voraussetzungen

| Software | Mindestversion | Hinweis |
|---|---|---|
| Ubuntu | 22.04 | Empfohlen als Betriebssystem der VM |
| Docker | 24.x | Minikube nutzt den Docker-Driver |
| Minikube | 1.32 | Verwaltet den lokalen Kubernetes-Cluster |
| kubectl | 1.28 | Kommuniziert mit dem Cluster-API-Server |
| Git | beliebig | Fuer Checkout durch den Runner benoetigt |

Minikube verwendet den Docker-Driver. Docker muss daher vor dem ersten `minikube start` installiert und der aktuelle Benutzer muss Mitglied der Gruppe `docker` sein.

---

## 3. Minikube einrichten

**Cluster starten**

Der Cluster wird mit ausreichend Ressourcen gestartet, damit API und Frontend parallel laufen koennen:

```bash
minikube start --cpus=2 --memory=4096 --driver=docker
```

Nach dem ersten Start erstellt Minikube automatisch einen `kubectl`-Kontext namens `minikube` und setzt ihn als aktiv.

**Ingress-Addon aktivieren**

Das nginx-Ingress-Addon ist Pflicht. Ohne es werden die Ingress-Controller-Pods nicht gestartet, und das `Ingress`-Objekt in `kubernetes/ingress.yaml` hat keinerlei Wirkung — HTTP-Anfragen kommen nie bei den Services an.

```bash
minikube addons enable ingress
```

Bevor das Ingress-Manifest angewendet wird, sicherstellen, dass der Controller laeuft:

```bash
kubectl get pods -n ingress-nginx
```

Der Pod mit dem Namen `ingress-nginx-controller-*` muss den Status `Running` haben.

**Minikube-IP ermitteln**

Der Browser muss auf die IP des Minikube-Nodes zeigen, um die Applikation zu erreichen:

```bash
minikube ip
```

Die ausgegebene IP (z.B. `192.168.49.2`) ist der Einstiegspunkt fuer alle HTTP-Anfragen an den Ingress.

---

## 4. Self-hosted GitHub Actions Runner

Ein Self-hosted Runner ist ein Agentenprozess, der auf der VM installiert wird. Er verbindet sich regelmaessig mit der GitHub API, um zu pruefen, ob Jobs fuer dieses Repository in der Warteschlange stehen. Sobald ein Job bereitsteht, laedt der Runner die Schritte herunter und fuehrt sie lokal aus — mit direktem Zugriff auf Minikube, `kubectl` und Docker, die auf der VM vorhanden sind. Es wird kein eingehender Port geoeffnet.

**Einrichtung**

1. Im Repository: **Settings → Actions → Runners → New self-hosted runner**
2. Betriebssystem **Linux** auswaehlen. GitHub zeigt einen temporaeren Registrierungstoken und die exakten Befehle an.
3. Die von GitHub angezeigten `config.sh`- und `run.sh`-Befehle auf der VM ausfuehren.
4. Den Runner als systemd-Service einrichten, damit er nach Neustarts der VM automatisch wiederlaeuft:

```bash
sudo ./svc.sh install
sudo ./svc.sh start
```

Nach diesem Schritt erscheint der Runner im Repository unter **Settings → Actions → Runners** mit dem Status "Idle".

---

## 5. CI/CD-Pipeline — Ablauf

Die Pipeline ist in `.github/workflows/ci.yml` definiert und besteht aus vier aufeinanderfolgenden Jobs. Sie wird bei jedem Push auf `main` und bei Pull Requests gegen `main` ausgefuehrt.

**Job 1: quality** (laeuft auf `ubuntu-latest`)

Laeuft auf GitHub-eigener Infrastruktur. Stellt die NuGet-Pakete wieder her, baut die .NET-Solution im Release-Modus und fuehrt alle xUnit-Tests aus. Anschliessend werden die npm-Abhaengigkeiten installiert und ein TypeScript-Typ-Check mit `vue-tsc` durchgefuehrt. Schlaegt dieser Job fehl, werden alle nachfolgenden Jobs nicht gestartet. Das ist bewusst so gestaltet: teure Build- und Push-Schritte sollen nicht stattfinden, wenn der Code bereits Tests oder Typfehler enthaelt.

**Job 2: build** (laeuft auf `ubuntu-latest`)

Baut beide Docker-Images — das API-Image ab dem Repository-Root und das Frontend-Image aus dem `frontend/`-Verzeichnis — mit Docker Buildx und dem GHA-Layer-Cache. Es wird nichts in eine Registry gepusht. Dieser Job dient ausschliesslich dazu, sicherzustellen, dass die Dockerfiles gueltig sind und das Build durchlaeuft. Er laeuft auch bei Pull Requests, damit Dockerfile-Fehler fruehzeitig auffallen.

**Job 3: push** (laeuft auf `ubuntu-latest`, nur bei Push auf `main`)

Authentifiziert sich mit `docker/login-action` und dem automatischen `GITHUB_TOKEN` bei der GitHub Container Registry (GHCR). Baut beide Images erneut (mit Layer-Cache) und pusht sie mit zwei Tags: `latest` und `sha-<short-sha>`. Der SHA-Tag erlaubt es, jeden gepushten Stand eindeutig zu identifizieren und bei Bedarf auf eine frueheren Build zurueckzurollen.

**Job 4: deploy** (laeuft auf `self-hosted`, nur bei Push auf `main`)

Laeuft direkt auf der VM. Der Job zieht beide Images von GHCR mit `docker pull`, laedt sie dann mit `minikube image load` in den lokalen Image-Store von Minikube, wendet alle Kubernetes-Manifeste an und loest einen Rolling Restart der beiden Deployments aus. Abschliessend wartet er mit `kubectl rollout status` bis zu fuenf Minuten darauf, dass die neuen Pods bereit sind.

**Warum `minikube image load` statt direktem Pull durch Kubernetes?**

Der Minikube-Cluster laeuft in einer isolierten Docker-Umgebung und hat kein GHCR-Login innerhalb seines internen Kontexts konfiguriert. Wuerde Kubernetes versuchen, das Image selbst von GHCR zu ziehen, schluege das fehl, weil die Registry-Credentials nicht im Cluster-Kontext vorhanden sind. Durch `minikube image load` wird das bereits lokal vorhandene Image direkt in den Image-Store des Minikube-Nodes kopiert. Mit `imagePullPolicy: IfNotPresent` in den Deployment-Manifesten nutzt Kubernetes dieses lokale Image und versucht keinen weiteren Pull.

---

## 6. Secrets und Konfiguration

**GitHub Actions Secrets**

Folgende Secrets muessen im Repository unter **Settings → Secrets and variables → Actions** hinterlegt sein:

| Secret | Beschreibung |
|---|---|
| `GHCR_PAT` | Personal Access Token mit `read:packages`-Berechtigung, damit der deploy-Job Images von GHCR pullen kann |
| `DB_PASSWORD` | Passwort fuer den PostgreSQL-Datenbankbenutzer |
| `JWT_SECRET` | Signierschluessel fuer JWT-Tokens (HS256, mindestens 32 Zeichen) |

Das `GITHUB_TOKEN` wird automatisch von GitHub bereitgestellt und braucht nicht manuell angelegt zu werden. Es wird vom `push`-Job verwendet, um sich bei GHCR zu authentifizieren und Images hochzuladen.

**Kubernetes Secrets**

Die Datei `kubernetes/secrets.yaml` muss manuell auf der VM erstellt und einmalig angewendet werden. Sie ist gitignoriert und darf niemals ins Repository committed werden.

Die Datei muss die folgenden Schluesseln im Secret `bombasticifccluster-secrets` enthalten:

- `connection-string` — PostgreSQL-Verbindungszeichenfolge (base64-kodiert), z.B. `Host=postgres-service;Port=5432;Database=bombasticifcdb;Username=postgres;Password=DEIN_PASSWORT`
- `jwt-secret` — HS256-Signierschluessel fuer JWT-Tokens (base64-kodiert)

Nach der Erstellung einmalig anwenden:

```bash
kubectl apply -f kubernetes/secrets.yaml
```

---

## 7. Bekannte Probleme

**EF Core Migrationen**

Die API fuehrt beim Start `db.Database.Migrate()` aus und wendet damit alle ausstehenden Datenbankmigrationen an. Dieser Mechanismus funktioniert nur fuer Migrationen, die im laufenden Image enthalten sind. Wurde nach dem letzten Image-Push eine neue Migration hinzugefuegt, ist die Datenbankstruktur veraltet, und die API liefert auf Endpunkten, die die Datenbank beruehren, einen 500-Fehler.

Loesung im Normalfall: neuen Code pushen. Die Pipeline baut und pusht ein frisches Image, das die neue Migration enthaelt. Der deploy-Job laedt es in Minikube, der Rolling Restart startet neue Pods, und beim Start wird die Migration automatisch angewendet.

Sofortloesung ohne Code-Push: Per Port-Forward direkt auf den Postgres-Pod verbinden und `dotnet ef database update` vom lokalen Rechner aus gegen die Cluster-Datenbank ausfuehren.

**Ingress-Addon nicht aktiviert**

Wird das `Ingress`-Manifest angewendet, ohne dass das Addon aktiviert wurde, schlaegt das gesamte HTTP-Routing stillschweigend fehl. Es gibt keinen Controller-Prozess, der das `Ingress`-Objekt auswertet, weshalb kein Fehler sichtbar ist — Anfragen kommen einfach nicht an.

Ueberpruefen mit:

```bash
kubectl get pods -n ingress-nginx
```

Der Pod `ingress-nginx-controller-*` muss den Status `Running` haben. Falls er fehlt, das Addon nachtraeglich aktivieren (`minikube addons enable ingress`) und warten, bis der Pod bereit ist.

---

## 8. Manuelles Deployment (ohne Pipeline)

Fuer die Ersteinrichtung oder wenn die Pipeline nicht verfuegbar ist, koennen die Manifeste direkt in der richtigen Abhaengigkeitsreihenfolge angewendet werden:

```bash
kubectl apply -f kubernetes/namespace.yaml
kubectl apply -f kubernetes/configmap.yaml
kubectl apply -f kubernetes/persistent-volumes.yaml
kubectl apply -f kubernetes/postgres-deployment.yaml
kubectl apply -f kubernetes/api-deployment.yaml
kubectl apply -f kubernetes/frontend-deployment.yaml
kubectl apply -f kubernetes/ingress.yaml
```

`kubernetes/secrets.yaml` muss separat und vor dem API-Deployment angewendet werden, da die API-Pods sonst nicht starten koennen (fehlende Secret-Referenzen).
