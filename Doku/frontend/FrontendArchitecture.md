# Frontend Architecture — After Refactoring

## Component Hierarchy

```
App.vue
├─ AppHeader.vue (Navigation)
└─ RouterView
   ├─ HomeView.vue
   ├─ LoginView.vue
   │  └─ LoginForm.vue
   ├─ RegisterView.vue
   │  └─ RegisterForm.vue
   ├─ UploadView.vue
   ├─ ProfileView.vue
   ├─ DashboardView.vue (REFACTORED) 285 lines
   │  ├─ ToastContainer.vue ← New!
   │  │  └─ Toast.vue ← New!
   │  │     (displays individual toasts)
   │  │
   │  └─ ModelTable.vue ← New!
   │     └─ ModelCard.vue × N ← New!
   │        (renders each model row)
   │
   └─ ViewerView.vue
```

---

## State Management & Composables

```
DashboardView.vue (View Logic)
│
├─ useToasts() ← New Composable
│  └─ { toasts, showToast, removeToast, clearAll }
│     ↓ Used by
│     (ToastContainer.vue)
│
├─ useModelPolling(models) ← New Composable
│  └─ { hasActiveJobs, fetchModels, startPolling, stopPolling }
│     ↓ Updates
│     (ModelTable.vue)
│
├─ useModelOperations(models, showToast) ← New Composable
│  └─ { isDeleting, isRetrying, deleteModel, retryConversion }
│     ↓ Used by
│     (ModelCard.vue buttons)
│
└─ useAuthStore() (Pinia)
   └─ { isAuthenticated, username, logout, ... }
```

---

## Data Flow Diagram

```
┌─────────────────────────────────────────────────────┐
│        DashboardView.vue (Container)                │
├─────────────────────────────────────────────────────┤
│                                                     │
│  ┌──────────────────────────────────────────────┐   │
│  │ State Management                             │   │
│  ├──────────────────────────────────────────────┤   │
│  │ • models (Ref<IfcModelDto[]>)                │   │
│  │ • activeTab ('models' | 'viewer')            │   │
│  │ • isLoading, error, viewerLoading           │   │
│  └──────────────────────────────────────────────┘   │
│             ↓ passes via props                      │
│  ┌──────────────────────────────────────────────┐   │
│  │ ToastContainer                               │   │
│  ├──────────────────────────────────────────────┤   │
│  │ :toasts    ← gets from useToasts()          │   │
│  │ @remove    ← calls removeToast()            │   │
│  └──────────────────────────────────────────────┘   │
│             ├─ Toast                                │
│             ├─ Toast                                │
│             └─ Toast                                │
│                                                     │
│  ┌──────────────────────────────────────────────┐   │
│  │ ModelTable                                   │   │
│  ├──────────────────────────────────────────────┤   │
│  │ :models         ← from useModelPolling()    │   │
│  │ @view           → openModelInViewer()       │   │
│  │ @retry          → retryConversion()         │   │
│  │ @delete         → handleDeleteModel()       │   │
│  └──────────────────────────────────────────────┘   │
│             ├─ ModelCard (for each model)           │
│             │   ├─ File name                        │
│             │   ├─ Status badge                     │
│             │   ├─ Actions (View, Retry, Delete)    │
│             │   └─ Loading spinner                  │
│             ├─ ModelCard                            │
│             └─ ...                                  │
│                                                     │
│  3D Viewer Canvas (xeokit)                          │
│  ├─ Loading overlay                                 │
│  ├─ Entity selection panel                          │
│  └─ Property inspector                              │
│                                                     │
└─────────────────────────────────────────────────────┘
```

---

## Composable Lifecycle

### useToasts()

```
┌─ showToast(message, type, duration)
│  ├─ Generate ID
│  ├─ Add to toasts[]
│  └─ Schedule timer → removeToast(id) after duration
│
├─ removeToast(id)
│  ├─ Filter from toasts[]
│  ├─ Clear timer
│  └─ Update UI
│
└─ clearAll()
   ├─ Clear all timers
   └─ Empty toasts[]
```

### useModelPolling(models)

```
┌─ startPolling()
│  └─ setInterval(5000)
│     ├─ Check hasActiveJobs
│     └─ If true: fetchModels()
│
├─ stopPolling()
│  └─ clearInterval()
│
├─ fetchModels()
│  ├─ modelsApi.getModels()
│  └─ Update models.value
│
└─ hasActiveJobs (computed)
   ├─ Check status === Uploaded
   └─ Or status === Processing
```

### useModelOperations(models, showToast)

```
┌─ deleteModel(model)
│  ├─ window.confirm()
│  ├─ API call: modelsApi.deleteModel()
│  ├─ Filter models[]
│  └─ showToast('Deleted')
│
└─ retryConversion(model)
   ├─ API call: conversionsApi.createConversionJob()
   ├─ Refresh models
   ├─ Restart polling
   └─ showToast('Retry started')
```

---

## Component Props & Events

### Toast.vue
```typescript
Props {
  toast: ToastMessage { id: number, message: string, type: 'success' | 'error' }
  onRemove: (id: number) => void
}

Events:
  @remove -> (id: number)
```

### ToastContainer.vue
```typescript
Props {
  toasts: ToastMessage[]
}

Events:
  @remove -> (id: number)
  
Renders:
  Toast v-for each toast
```

### ModelCard.vue
```typescript
Props {
  model: IfcModelDto
  isDeleting?: boolean
  isRetrying?: boolean
  isViewable?: boolean
}

Events:
  @view -> () void
  @retry -> () void
  @delete -> () void
```

### ModelTable.vue
```typescript
Props {
  models: IfcModelDto[]
  deletingIds?: Record<string, boolean>
  retryingIds?: Record<string, boolean>
}

Events:
  @view -> (model: IfcModelDto) void
  @retry -> (model: IfcModelDto) void
  @delete -> (model: IfcModelDto) void
```

---

## File Organization

```
frontend/src/
├── api/
│   ├── auth.ts
│   ├── models.ts
│   ├── conversions.ts
│   └── client.ts
│
├── components/
│   ├── AppHeader.vue
│   ├── LoginForm.vue
│   ├── RegisterForm.vue
│   ├── ErrorMessage.vue
│   ├── Toast.vue ← NEW
│   ├── ToastContainer.vue ← NEW
│   ├── ModelCard.vue ← NEW
│   └── ModelTable.vue ← NEW
│
├── composables/
│   ├── useToasts.ts ← NEW
│   ├── useModelPolling.ts ← NEW
│   ├── useModelOperations.ts ← NEW
│   └── index.ts ← NEW
│
├── stores/
│   └── auth.ts (Pinia)
│
├── router/
│   └── index.ts
│
├── types/
│   ├── index.ts
│   ├── auth.ts
│   └── models.ts
│
├── views/
│   ├── HomeView.vue
│   ├── LoginView.vue
│   ├── RegisterView.vue
│   ├── UploadView.vue
│   ├── ProfileView.vue
│   ├── DashboardView.vue ← REFACTORED (495 → 285 lines)
│   ├── ViewerView.vue
│   └── NotFoundView.vue
│
├── App.vue
├── main.ts
├── style.css
└── env.d.ts
```

---

## Import Structure (Clean Imports)

### Before (Monolithic)
```typescript
import { ref, computed, onMounted, ... } from 'vue'
import { modelsApi, conversionsApi } from '../api'
import { ... } from '../types'
```

### After (Organized)
```typescript
// Core imports
import { ref, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

// API
import { modelsApi } from '../api/models'

// Composables (organized logic)
import { useToasts } from '../composables/useToasts'
import { useModelPolling } from '../composables/useModelPolling'
import { useModelOperations } from '../composables/useModelOperations'

// Components (presentation)
import ToastContainer from '../components/ToastContainer.vue'
import ModelTable from '../components/ModelTable.vue'
```

---

## Performance Considerations

### Before Refactoring
- ❌ 495-line component hard to optimize
- ❌ Mixed concerns slow to debug
- ❌ Large bundle impact
- ❌ Difficult to tree-shake

### After Refactoring
- ✅ Small focused components (< 100 lines)
- ✅ Separated concerns easy to debug
- ✅ Composables can be imported selectively
- ✅ Better tree-shaking potential
- ✅ Polling only runs when needed

---

## Testing Strategy (Ready for Task 5)

```
Unit Tests (Vitest)
├─ useToasts.test.ts
│  ├─ showToast() adds to array
│  ├─ removeToast() removes from array
│  └─ clearAll() clears all timers
│
├─ useModelPolling.test.ts
│  ├─ hasActiveJobs computed
│  ├─ startPolling() sets interval
│  └─ stopPolling() clears interval
│
├─ useModelOperations.test.ts
│  ├─ deleteModel() calls API
│  ├─ retryConversion() calls API
│  └─ Loading states set correctly
│
├─ components/Toast.test.ts
│  ├─ Renders message
│  ├─ Close button works
│  └─ Correct styling by type
│
├─ components/ModelCard.test.ts
│  ├─ Renders model data
│  ├─ Emits correct events
│  └─ Shows/hides buttons by status
│
└─ components/ModelTable.test.ts
   ├─ Renders all models
   ├─ Forwards events to parent
   └─ Responsive breakpoints work
```

---

## Summary

**Architecture Evolution:**
- Monolithic view → Component-based + Composable logic
- Mixed concerns → Separated state, UI, business logic
- Hard to test → Easily testable composables
- 495 lines → 285 lines (−42%)
- 4 components → 9 components (+125%)

**Result:** More maintainable, testable, and scalable codebase!
