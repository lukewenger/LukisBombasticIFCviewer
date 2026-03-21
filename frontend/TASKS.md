# Frontend — Tasks zur Erreichung der Projektauftrag-Kriterien

## 📊 Aktueller Status

- ✅ Technische Grundlage: Vue 3, TypeScript, Pinia, Routing vorhanden
- ✅ Funktionalität: Upload, Dashboard, Viewer, Auth funktionsfähig
- ✅ Design: Responsive, Dark Mode, Tailwind
- ❌ Komponenten-Anforderung: 4 vorhanden, **mindestens 5 erforderlich**
- ❌ DashboardView: **495 Zeilen** (Maximum: **300 Zeilen**)
- ❌ Tests: **0 Unit-Tests** (für +1 Bonus erforderlich)
- ⚠️ README: Frontend-spezifische Dokumentation unvollständig

---

## 🎯 Tasks zur Punkteverbesserung

### Priorität: 🔴 KRITISCH

#### Task 1: Komponenten-Architektur — Mindestanforderung erfüllen
**Ziel:** Mindestens **5 wiederverwendbare Komponenten** implementieren

**Status:** 4/5 vorhanden (AppHeader, ErrorMessage, LoginForm, RegisterForm)

**Umfang:**
- [ ] `src/components/ModelCard.vue` — Wiederverwendbare Kartei für Model-Listing
  - Props: `model: IfcModelDto`, `onView: () => void`, `onDelete: () => void`
  - Template mit Skeleton-Loading State
  - Größe: ~80–100 Zeilen
  
- [ ] `src/components/FormInput.vue` — Wiederverwendbares Formularelement
  - Props: `label`, `type`, `placeholder`, `required`, `error`
  - Fokus-Styling für Accessibility
  - Größe: ~50–70 Zeilen

- [ ] `src/components/Button.vue` — Standardisierter Button (variant: primary/secondary/danger)
  - Props: `variant`, `size`, `loading`, `disabled`
  - Größe: ~40–60 Zeilen

**Akzeptanzkriterium:** 
- Alle 3 Komponenten < 100 Zeilen
- Verwendet in Views (Login, Upload, Dashboard)
- Props/Events korrekt dokumentiert

---

#### Task 2: DashboardView refaktorieren — < 300 Zeilen
**Ziel:** DashboardView von **495 → < 300 Zeilen** reduzieren

**Status:** Kritisch

**Strategie:**
- [ ] Polling-Logik → `src/composables/useModelPolling.ts`
  - `useModelPolling(models: Ref<IfcModelDto[]>)`
  - Returns: `{ isPolling, hasActiveJobs, fetch, start, stop }`
  - Größe: ~80 Zeilen
  
- [ ] Toast-System → `src/composables/useToasts.ts`
  - `useToasts()`
  - Returns: `{ toasts, showToast, removeToast }`
  - Größe: ~40 Zeilen
  
- [ ] Model-Operationen → `src/composables/useModelOperations.ts`
  - `useModelOperations(models, showToast)`
  - Methoden: `deleteModel()`, `retryConversion()`, `refreshModels()`
  - Größe: ~60 Zeilen

- [ ] DashboardView neu strukturieren
  - nur noch: Props vom Store, render, Event-Handler
  - Zielgröße: **< 300 Zeilen**

**Akzeptanzkriterium:**
- DashboardView.vue: < 300 Zeilen
- Alle Composables exportiert aus `src/composables/index.ts`
- Tests für Composables vorhanden (Task 6)

---

### Priorität: 🟡 HÖHER

#### Task 3: Komponenten-UI verbessern — Consistency & Touch-Friendliness
**Ziel:** UI/UX auf 5 Punkte bringen

**Status:** 4/5

**Umfang:**
- [ ] Mindestgröße für Touch-Elemente: **44px × 44px**
  - Button in `Button.vue`: `min-h-[44px] min-w-[44px]`
  - Alle Router-Links nachprüfen
  
- [ ] Hover- und Focus-States standardisieren
  - `:focus-visible` Ring auf allen interaktiven Elementen
  - Transition-Effekte konsistent
  
- [ ] Ladeanimation für ViewerView
  - `Spinner` oder `Skeleton` während Model lädt
  - bereits: `viewerLoading`, aber visuelle Darstellung
  
- [ ] Fehlerzustände mit `ErrorMessage.vue`
  - Retry-Button in ModelCard (404 bei Viewer)

**Akzeptanzkriterium:**
- Alle Buttons/Links: mindestens 44px
- Focus-States auf allen Inputs
- Loading States sichtbar
- Error States elegant behandelt

---

#### Task 4: Frontend README vervollständigen
**Ziel:** README mit Installation, Komponenten-Übersicht, Architektur

**Status:** Teilweise

**Umfang:**
- [ ] `frontend/README.md` erstellen mit:
  - **Projektbeschreibung** (1 Absatz)
  - **Anforderungen:** Node.js 20+, npm
  - **Installation:** `npm install`, `npm run dev`
  - **Kommandos:**
    - `npm run dev` → Vite Dev Server
    - `npm run build` → Production Build
    - `npm run preview` → Build Preview
  - **Komponentenliste:**
    | Komponente | Pfad | Zweck | Props |
    | --- | --- | --- | --- |
    | AppHeader | `components/AppHeader.vue` | Navigation | — |
    | ... | ... | ... | ... |
  - **Stores:**
    - `auth` → Authentifizierung, Token, User
  - **API-Layer:**
    - `api/auth.ts` → Login, Register, Logout
    - `api/models.ts` → Upload, Listing, Viewer-Stream
    - `api/conversions.ts` → Job-Status
  - **Umgebungsvariablen:** `.env` Beispiel
    ```env
    VITE_API_URL=http://localhost:5000/api
    ```
  - **Troubleshooting** (häufige Probleme)

**Akzeptanzkriterium:**
- Mindestens 4–5 Seiten (wenn ausgedruckt)
- Vollständige Setup-Anleitung ohne Rückfragen
- Komponenten vollständig dokumentiert

---

### Priorität: 🟢 BONUS

#### Task 5: Unit-Tests implementieren (+1 Bonus)
**Ziel:** Mindestens **3 Komponenten-/Unit-Tests**

**Status:** 0 Tests

**Umfang:**
- [ ] Test-Setup: `vitest` + `@vue/test-utils`
  - `package.json` devDependencies erweitern
  - `vitest.config.ts` erstellen
  - `test/` Verzeichnis für Test-Dateien

- [ ] 3 Test-Dateien:
  - [ ] `test/stores/auth.test.ts` — Auth Store
    - Login/Logout Zustandsänderungen
    - Token Persistierung
    - ~50 Zeilen
    
  - [ ] `test/components/Button.test.ts` — Button Komponente
    - Props: variant, size, loading
    - Events: click
    - ~40 Zeilen
    
  - [ ] `test/api/models.test.ts` — Models API
    - Mock Axios
    - `getModels()` Erfolgfall
    - ~60 Zeilen

- [ ] `package.json` scripts aktualisieren:
  ```json
  "scripts": {
    "test": "vitest",
    "test:ui": "vitest --ui"
  }
  ```

**Akzeptanzkriterium:**
- Mindestens 3 Test-Dateien
- Alle Tests grün
- Coverage > 50% (optional)
- Tests laufen mit `npm run test`

---

#### Task 6: TypeScript Strict Mode + Linting
**Ziel:** Zero TS-Fehler, Code-Quality

**Status:** Most implementiert, optimierbar

**Umfang:**
- [ ] `tsconfig.json` überprüfen
  - `"strict": true`
  - `"noImplicitAny": true`
  - `"strictNullChecks": true`
  
- [ ] ESLint konfigurieren
  - `.eslintrc.cjs` erstellen
  - Rules: Vue 3, TypeScript
  - Presets: `@vue/eslint-config-typescript`
  
- [ ] Prüfen mit:
  ```bash
  npm run build  # sollte zero TS errors
  ```

**Akzeptanzkriterium:**
- `npm run build` ohne Fehler oder Warnungen
- Alle `.vue` und `.ts` Dateien sauber

---

## 📅 Implementierungs-Roadmap

### Phase 1: Komponenten + DashboardView Refactoring (Kritisch)
**Dauer:** 6–8 Stunden
1. ModelCard.vue, FormInput.vue, Button.vue erstellen
2. Composables schreiben
3. DashboardView umbauen
4. Tests für Composables

### Phase 2: UI Polishing + README (Höher)
**Dauer:** 4–6 Stunden
1. Button-Größen auf 44px anpassen
2. Focus-States erweitern
3. Frontend README schreiben

### Phase 3: Tests + Bonus (Bonus)
**Dauer:** 3–4 Stunden
1. Vitest Setup
2. 3 Unit-Tests schreiben
3. Linting überprüfen

---

## 🎓 Geschätzte Punkteverbesserung

| Task | Bereich | Punkte-Gain | Nach-Umsetzung |
|------|---------|------------|-----------------|
| 1 + 2 | Komponentenarchitektur | +2–3 | 5/5 ✅ |
| 3 | UI/UX Design | +1 | 5/5 ✅ |
| 4 | README + Doku | +1 | 4/5 |
| 5 | Tests (Bonus) | +1 | Bonus ✅ |
| 6 | Code Quality | +0–1 | 5/5 |
| **Total** | **Technische Umsetzung** | **+5–6** | **33–38/40** |

---

## ✅ Abnahme-Checkliste

- [ ] Task 1: 5+ Komponenten, alle < 100 Zeilen
- [ ] Task 2: DashboardView < 300 Zeilen, Composables > 80% Tests-grün
- [ ] Task 3: Alle Buttons 44px, Focus-States sichtbar
- [ ] Task 4: Frontend/README.md mit vollständiger Setup-Anleitung
- [ ] Task 5: `npm test` — 3+ Tests grün
- [ ] Task 6: `npm run build` — zero TS errors
- [ ] Git: Commits pro Task sauber gepusht

---

**Aktualisiert:** 21.03.2026  
**Geschätzter Aufwand:** 13–18 Stunden  
**Priorität:** 🔴 KRITISCH — ohne Tasks 1+2 können max. 3 von 5 Punkten erreicht werden
