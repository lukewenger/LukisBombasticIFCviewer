# Anforderungen und Zielerreichung

## Ausgangslage

Im Rahmen der Projekteingabe wurde eine Single Page Application zur Verwaltung und Visualisierung von IFC-Modellen geplant. Die Kernziele waren eine vollständige Authentifizierung, ein stabiler Upload- und Konvertierungsprozess, eine interaktive 3D-Visualisierung im Browser sowie eine responsive und benutzerfreundliche Oberfläche. Zusätzlich wurden konkrete Hauptfeatures, Komponenten, Routen und der geplante Technologie-Stack definiert.

## Zielerreichung pro Projektziel

### 1. Benutzerauthentifizierung

Das Ziel wurde erreicht. Login, Registrierung und Logout sind implementiert und an die REST-API angebunden. Der Benutzerstatus wird global im Pinia-Store verwaltet. Geschützte Routen funktionieren über Router-Guards, sodass nicht authentifizierte Benutzer auf Login umgeleitet werden.

Nachweise:
- frontend/src/stores/auth.ts
- frontend/src/api/auth.ts
- frontend/src/api/client.ts
- frontend/src/router/index.ts

### 2. Modell-Upload und Statusüberwachung

Das Ziel wurde erreicht. Authentifizierte Benutzer können IFC-Dateien hochladen. Dateityp und Dateigröße werden validiert. Der Uploadfortschritt wird angezeigt. Der Konvertierungsstatus wird über Polling überwacht und in den Zuständen wartend, in Bearbeitung, abgeschlossen und fehlgeschlagen visuell dargestellt.

Nachweise:
- frontend/src/views/UploadView.vue
- frontend/src/views/DashboardView.vue
- frontend/src/composables/useModelPolling.ts
- frontend/src/types/models.ts

### 3. 3D-Modellvisualisierung mit xeokit

Das Ziel wurde erreicht. Konvertierte Modelle werden im Browser im xeokit-Viewer dargestellt. Navigation und Objektselektion sind vorhanden. Zusätzlich wurden erweiterte Interaktionen ergänzt, etwa Punktaufnahme sowie CSV-Import und CSV-Export für Punkte.

Nachweise:
- frontend/src/views/ViewerView.vue
- frontend/src/components/XeokitPointViewer.vue
- frontend/src/components/PointPickerPanel.vue
- frontend/src/components/EntityAttributesPanel.vue

### 4. Responsive und benutzerfreundliche Oberfläche

Das Ziel wurde erreicht. Die Anwendung ist für mehrere Breakpoints ausgelegt und besitzt konsistente UI-Patterns. Ladezustände, Fehlermeldungen und leere Zustände sind in den Hauptviews vorhanden. Interaktive Elemente wurden auf touchfreundliche Zielgrößen optimiert.

Nachweise:
- frontend/src/components/AppHeader.vue
- frontend/src/components/ModelCard.vue
- frontend/src/views/DashboardView.vue
- frontend/src/views/UploadView.vue

## Abgleich der geplanten Hauptfeatures

- Authentifizierung: erreicht
- Modellverwaltung inklusive Status und Löschen: erreicht
- IFC-Datei-Upload mit Drag-and-Drop, Progress und Validierung: erreicht
- Konvertierungsstatus-Tracker per Polling: erreicht
- 3D-Viewer mit Interaktion und Selektion: erreicht
- Benutzerprofil: Grundfunktion erreicht, Bearbeitungs- und Aktivitätsfunktionen teilweise umgesetzt

## Abgleich der geplanten Komponenten

Der geplante Funktionsumfang der Komponenten ist inhaltlich weitgehend erreicht. Teilweise wurden Komponenten anders geschnitten als ursprünglich benannt, um bessere Wartbarkeit und klarere Verantwortlichkeiten zu erzielen.

Beispiele für gleichwertige Umsetzung:
- Geplante ModelList wurde durch ModelTable plus ModelCard umgesetzt.
- Geplante ModelStatusBadge ist in ModelCard als wiederverwendete Statusdarstellung integriert.
- Geplante ModelUpload ist funktional in der Upload-View umgesetzt.
- Geplante ModelTreePanel-Funktion wurde durch selektions- und attributbasierte Viewer-Panels teilweise abgedeckt.

Diese Abweichungen sind architektonisch begründet und verbessern Lesbarkeit, Wiederverwendbarkeit und Testbarkeit.

## Abgleich der geplanten Routen

Alle in der Projekteingabe genannten Routen sind vorhanden, inklusive 404-Route und Auth-Schutz für geschützte Bereiche.

Nachweis:
- frontend/src/router/index.ts

## Technologie-Stack: Abgleich und begründete Differenzen

### JWT Token statt OAuth2 in der Entwicklungsphase

Die Authentifizierung wurde mit JWT Bearer Tokens umgesetzt. Das ist für die vorliegende Projektgröße und die direkte API-Anbindung eine sinnvolle, schlanke und technisch saubere Lösung.

Begründung:
- OAuth2 hätte zusätzliche Infrastruktur benötigt, etwa Authorization Server, Client-Registrierung und komplexere Flows.
- Für den definierten Umfang war der Zusatzaufwand unverhältnismäßig.
- Die Sicherheitslogik im Frontend ist konsistent umgesetzt, einschließlich Token-Handling, Interceptor und Route-Schutz.

Nachweise:
- frontend/src/api/client.ts
- frontend/src/stores/auth.ts
- frontend/src/router/index.ts

### Backend-Technologie in der Umsetzung

In der Projekteingabe war Node.js als API-Option beschrieben. In der Umsetzung wurde die API aus dem PROG3-Kontext mit .NET 8 angebunden.

Begründung:
- Die modulübergreifende Zielsetzung war explizit die API-Anbindung aus PROG3.
- Die funktionalen Anforderungen der WEB2-Anwendung werden vollständig erfüllt.
- Die Schnittstellen sind dokumentiert und konsistent integriert.

Nachweise:
- README.md
- Doku/backend/csharp-doku.md

## Zusammenfassung

Die Anforderungen aus der Projekteingabe wurden im Frontend insgesamt in hohem Maß erreicht. Alle zentralen Produktziele sind funktionsfähig umgesetzt. Einzelne Differenzen betreffen vor allem die konkrete technische Ausprägung, nicht jedoch den geforderten Funktionsumfang. Diese Differenzen sind nachvollziehbar begründet und aus Architektur- sowie Projektumfangssicht sinnvoll.