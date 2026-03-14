# Sprint Tasks — BombasticIFC Frontend

> Basis: Vue 3 + TypeScript · Tailwind CSS · xeokit-SDK ^2.6.0 · .NET 8 Backend  
> Bestehende Architektur: `DashboardView.vue` (Modellliste + eingebetteter Viewer) · `ViewerView.vue` (Vollbild-Viewer) · `XKTLoaderPlugin`

---

## Task 1 — Lösch-Buttons für Modelle

**Ziel:** Jede Modellzeile in der Tabelle des Dashboards erhält einen Löschen-Button. Ein Bestätigungsdialog verhindert versehentliches Löschen.

### Backend

- [ ] **1.1** `DeleteModelCommand` + Handler anlegen  
  `src/BombasticIFC.Application/UseCases/Models/DeleteModelCommand.cs`
  - Löscht das `IfcModel`-Eintrag aus der DB (via `IIfcModelRepository`)
  - Löscht zugehörige Dateien vom Dateisystem (`InputFilePath`, `OutputFilePath`) via `IFileStorageService`
  - Wirft `NotFoundException` wenn Modell nicht gefunden

- [ ] **1.2** `DeleteAsync(Guid id)` zu `IIfcModelRepository` + `IfcModelRepository` hinzufügen

- [ ] **1.3** `DELETE /api/models/{id}` in `ModelsController.cs` verdrahten  
  - `[HttpDelete("{id:guid}")]`, `[Authorize]`
  - Gibt `204 No Content` zurück

### Frontend

- [ ] **1.4** `deleteModel(id: string)` in `src/api/models.ts` ergänzen  
  ```ts
  deleteModel: (id: string) =>
    apiClient.delete(`/api/models/${id}`)
  ```

- [ ] **1.5** Löschen-Button in `DashboardView.vue` — Modelltabelle (Spalte „Aktion")  
  - Roter Icon-Button (Trash-Icon aus `lucide-vue-next`) neben „Anzeigen"-Button
  - Sichtbar für alle Status-Werte (nicht nur Ready)
  - Deaktiviert (`disabled`, Spinner) während `isDeleting[model.id]` aktiv

- [ ] **1.6** Bestätigungsdialog implementieren  
  - Einfaches Inline-Modal oder `window.confirm()` als Mindestlösung
  - Zeigt Dateiname des Modells an: `„Modell «{model.fileName}» wirklich löschen?"`

- [ ] **1.7** Nach erfolgreichem Löschen  
  - `models.value` lokal aktualisieren (Eintrag entfernen)
  - Falls das gelöschte Modell gerade im Viewer angezeigt wird: Tab zu „Modelle" wechseln, Demo laden

---

## Task 2 — Redo-Button für Konvertierungsjobs

**Ziel:** Modelle mit Status `Failed` oder `Ready` erhalten einen Button, um den Konvertierungsjob erneut anzustossen. Nützlich bei fehlgeschlagener Konvertierung oder wenn ein Modell mit anderem Zielformat neu konvertiert werden soll.

### Frontend

- [ ] **2.1** Redo-Button in `DashboardView.vue` — Modelltabelle (Spalte „Aktion")  
  - Icon-Button (RefreshCw aus `lucide-vue-next`, bereits importiert)
  - Einblenden für Status `Failed` und optional `Ready`  
  - Deaktiviert während `isRetrying[model.id]` aktiv (Spinner)

- [ ] **2.2** `retryConversion(model: IfcModelDto)` Funktion in `DashboardView.vue`  
  ```ts
  async function retryConversion(model: IfcModelDto) {
    isRetrying[model.id] = true
    try {
      await conversionsApi.createConversionJob(model.id, ConversionFormat.XKT)
      await fetchModels()  // sofort aktualisieren
      startPolling()       // Polling wieder aktivieren
    } catch {
      error.value = 'Konvertierung konnte nicht neu gestartet werden.'
    } finally {
      isRetrying[model.id] = false
    }
  }
  ```

- [ ] **2.3** `import { conversionsApi } from '../api/conversions'` in `DashboardView.vue` sicherstellen  
  - `ConversionFormat` aus `../types/models` importieren

- [ ] **2.4** `isRetrying` als reaktives Objekt deklarieren  
  ```ts
  const isRetrying = ref<Record<string, boolean>>({})
  ```

### Backend (optional, falls noch nicht behandelt)

- [ ] **2.5** Prüfen ob `POST /api/conversions` idempotent ist bei bereits existierenden Jobs  
  - Falls nicht: entweder bestehenden Job zurückgeben oder vorherigen Job auf `Archived` setzen vor Neuerstellung

---

## Task 3 — Attribut-Palette & klickbare IFC-Objekte im xeokit-Viewer

**Ziel:** Im xeokit-Viewer (DashboardView + ViewerView) können einzelne IFC-Bauteile per Mausklick ausgewählt werden. Eine Seitenleiste zeigt die IFC-Attribute des angeklickten Elements an (Name, Typ, GlobalId, Properties).

### xeokit Picking & Metadaten

- [ ] **3.1** Picking auf Mausklick aktivieren in `initViewer()` (DashboardView.vue)  
  ```ts
  viewer.scene.input.on('mouseclicked', (coords: number[]) => {
    const pickResult = viewer.scene.pick({ canvasPos: coords })
    if (pickResult?.entity) {
      selectedEntityId.value = pickResult.entity.id
      loadEntityAttributes(pickResult.entity.id)
    }
  })
  ```

- [ ] **3.2** Gleiches Picking in `ViewerView.vue` implementieren (Vollbild-Viewer)

- [ ] **3.3** Metadaten-Datei beim Laden mitgeben (falls vorhanden)  
  - XKTLoaderPlugin unterstützt `metaModelSrc` Parameter:  
    ```ts
    xktLoader.load({
      id: 'model',
      src: loadUrl,
      metaModelSrc: `/api/models/${modelId}/metadata`,  // neuer Endpunkt
      edges: true,
    })
    ```
  - Solange kein Metadaten-Endpunkt existiert: Attribute aus `entity.id` und `viewer.metaScene.metaObjects` lesen

- [ ] **3.4** Visuelles Highlighting des angeklickten Objekts  
  ```ts
  // Alle vorherigen Highlights zurücksetzen
  viewer.scene.setObjectsHighlighted(viewer.scene.highlightedObjectIds, false)
  // Neues Objekt highlighten
  pickResult.entity.highlighted = true
  ```

### Frontend — Attribut-Palette (Sidebar)

- [ ] **3.5** Reaktiver State in DashboardView.vue / ViewerView.vue  
  ```ts
  const selectedEntityId = ref<string | null>(null)
  const selectedAttributes = ref<Record<string, string>>({})
  
  function loadEntityAttributes(entityId: string) {
    const metaObject = viewer.metaScene?.metaObjects?.[entityId]
    selectedAttributes.value = {
      'IFC-Typ':    metaObject?.type      ?? '–',
      'Name':       metaObject?.name      ?? '–',
      'GlobalId':   entityId,
      'Parent':     metaObject?.parent?.name ?? '–',
    }
    // Falls metaObject.propertySets vorhanden: alle Properties extrahieren
    if (metaObject?.propertySets) {
      for (const ps of metaObject.propertySets) {
        for (const prop of ps.properties ?? []) {
          selectedAttributes.value[prop.name] = String(prop.value)
        }
      }
    }
  }
  ```

- [ ] **3.6** Attribut-Palette als Overlay/Sidebar neben dem Canvas — Template  
  - Einblenden wenn `selectedEntityId !== null`
  - Schliessen-Button (X) setzt `selectedEntityId = null` und entfernt Highlighting
  - Layout: feste Breite rechts (z.B. `w-72`), scrollbar, über dem Canvas oder als Side-Panel
  - Zeigt alle Einträge aus `selectedAttributes` als Key-Value-Liste
  - Tailwind-Beispiel:
    ```html
    <div v-if="selectedEntityId"
         class="absolute top-4 right-4 w-72 bg-white dark:bg-gray-800 rounded-xl shadow-xl p-4 z-10 overflow-y-auto max-h-[80%]">
      <div class="flex items-center justify-between mb-3">
        <h3 class="font-semibold text-gray-900 dark:text-white text-sm">IFC-Attribute</h3>
        <button @click="clearSelection" class="text-gray-400 hover:text-gray-600">✕</button>
      </div>
      <dl class="text-xs space-y-1.5">
        <div v-for="(value, key) in selectedAttributes" :key="key" class="flex flex-col">
          <dt class="text-gray-500 dark:text-gray-400">{{ key }}</dt>
          <dd class="text-gray-900 dark:text-white font-medium break-all">{{ value }}</dd>
        </div>
      </dl>
    </div>
    ```

- [ ] **3.7** `clearSelection()` Funktion  
  ```ts
  function clearSelection() {
    if (selectedEntityId.value && viewer) {
      viewer.scene.setObjectsHighlighted([selectedEntityId.value], false)
    }
    selectedEntityId.value = null
    selectedAttributes.value = {}
  }
  ```

### Backend (optional)

- [ ] **3.8** `GET /api/models/{id}/metadata` Endpunkt (optional)  
  - Gibt die JSON-Metadaten-Datei des Modells zurück (`application/json`)
  - Ermöglicht xeokit `metaModelSrc` für vollständige Property Sets
  - Erfordert separate Speicherung der Metadaten-Datei beim Konvertierungsschritt

---

## Reihenfolge / Priorität

| # | Task | Komplexität | Abhängigkeiten |
|---|------|-------------|----------------|
| 1 | Lösch-Buttons | Mittel | Backend-Endpunkt nötig |
| 2 | Redo-Button | Niedrig | Nur Frontend (API existiert bereits) |
| 3 | Attribut-Palette | Hoch | xeokit Picking, optional Backend-Metadaten |

> **Empfehlung:** Task 2 zuerst (reines Frontend, wiederverwendet bestehende API), dann Task 1 (Backend + Frontend), dann Task 3 (grösstes Feature).
