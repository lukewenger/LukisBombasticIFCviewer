PROG3 Projektdeklaration

Liste der Mitglieder
(3-4 Personen)	Lukas Wenger
Projektthema
•	Ziel der Applikation
•	Hauptfunktionen
•	Auftraggeber	Ziel der Applikation
Die Applikation ermöglicht die Konvertierung von IFC-Modellen in ein webkompatibles Format sowie deren Hosting in einem Kubernetes-Cluster, um eine performante, browserbasierte Betrachtung und Weiterverarbeitung von BIM-Modellen sicherzustellen.
Hauptfunktionen
-	Import von IFC-Modellen über ein Webfrontend direkt ins Cluster
-	Konvertierung der Modelle im API-Node in ein Browser-kompatibles Format (z. B. xkt)
-	Hosting & Visualisierung der konvertierten Modelle über einen Webserver mit xeokit
-	Monitoring der Konvertierungsprozesse im Cluster
Auftraggeber
Reto Glarner
Challenges
(min. 1 Challenge pro Teammitglied)	Modellkonverter auf dem API Cluster
Webfrontend
Plattform
Lernumgebung	1x Ubuntu Server
1x Linux Desktop (Kali oder ähnlich, nur für visuelle Tests im LAN)
Bemerkungen	





Architektur (Client-Server, Onion, DDD)
•	Client-Server-Architektur
Webclient kommuniziert über REST-API mit dem Backend-Cluster.
•	Onion Architecture / Clean Architecture
Strikte Trennung der Schichten:
Domain → Application → Infrastructure → Presentation
•	Domain Driven Design (DDD)
Modellverwaltung, Konvertierungsprozesse und Hosting als eigene Bounded Contexts (z. B. ModelImport, ModelProcessing, ModelHosting).
 
 

Backend
Persistenzschicht mit CRUD auf mehreren, verbundenen Objekten
•	Modelle (IFC-Datei, Metadaten, Status)
•	Konvertierungsjobs (Queueing, Status, Logs)
•	Benutzer (für Zugriff, Rollen, Rights Management)
•	Versionen / Modellvarianten
→ Umsetzung in relationaler DB (z. B. PostgreSQL)
Backend-Features
•	REST-API mit DTOs für Upload, Statusabfragen und Resultatabruf
•	Asynchrone Verarbeitung im Hintergrund (Worker Nodes / Message Queue)
•	Nutzung eines ORM (z. B. Entity Framework, TypeORM oder Prisma)
•	Containerisierte Microservices für:
o	Upload
o	Konvertierung
o	Storage / Hosting

Frontend (Web)
•	Web-Oberfläche zur Modellverwaltung
•	Upload von IFC-Dateien
•	Einsicht des Konvertierungsstatus
•	Anzeige der Modelle mit xeokit-Viewer
•	Fokus auf Usability (UX), einfache Bedienbarkeit für Nicht-Techniker
•	Responsive UI (Desktop / optional Mobile)
Testkonzept
•	Unit Tests
Logik der Domain-Modelle, Services und Utilities
•	Integration Tests
API-Endpoints, Datenbankzugriffe, Konvertierungsprozesse
•	End-to-End Tests
Browsergestützt: Modell-Upload → Konvertierung → Anzeige
•	Security Tests
Authentifizierung, Rechteverwaltung, sichere Uploads
