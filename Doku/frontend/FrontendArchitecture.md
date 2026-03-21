# Frontend Architecture

> 📊 See [frontendArchitecture.puml](frontendArchitecture.puml) for the layered architecture diagram.

## Architecture Overview

The frontend follows a **layered architecture** with clear separation of concerns:

- **View Layer**: Vue components organized by route (views) and reusable UI elements
- **Business Logic Layer**: Composables handle state, data fetching, and operations
- **State Management**: Pinia store for global authentication state
- **API Layer**: HTTP clients with centralized configuration and interceptors
- **Type Safety**: TypeScript types and DTOs for all API contracts

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
   ├─ DashboardView.vue
   │  ├─ ToastContainer.vue
   │  │  └─ Toast.vue
   │  │     (displays individual toasts)
   │  │
   │  └─ ModelTable.vue
   │     └─ ModelCard.vue × N
   │        (renders each model row)
   │
   └─ ViewerView.vue
```

---

## State Management & Composables

```
DashboardView.vue (View Logic)
│
├─ useToasts()
│  └─ { toasts, showToast, removeToast, clearAll }
│     ↓ Used by
│     (ToastContainer.vue)
│
├─ useModelPolling(models)
│  └─ { hasActiveJobs, fetchModels, startPolling, stopPolling }
│     ↓ Updates
│     (ModelTable.vue)
│
├─ useModelOperations(models, showToast)
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
│   ├── Toast.vue
│   ├── ToastContainer.vue
│   ├── ModelCard.vue
│   └── ModelTable.vue
│
├── composables/
│   ├── useToasts.ts
│   ├── useModelPolling.ts
│   ├── useModelOperations.ts
│   └── index.ts
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
│   ├── DashboardView.vue
│   ├── ViewerView.vue
│   └── NotFoundView.vue
│
├── App.vue
├── main.ts
├── style.css
└── env.d.ts
```

---

## Import Structure

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

## Performance Characteristics

- ✅ Small focused components (< 100 lines)
- ✅ Separated concerns for maintainability
- ✅ Composables can be imported selectively
- ✅ Optimized tree-shaking
- ✅ Polling only runs when needed

---

## Testing Strategy

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

## Layer Descriptions

### View Layer (`/views`)
- Page-level components corresponding to routes
- Compose reusable components
- Orchestrate composables and stores
- Handle page-specific logic and layout
- Examples: `DashboardView.vue`, `ViewerView.vue`, `LoginView.vue`

### Component Layer (`/components`)
- Reusable, presentation-focused UI elements
- Minimal business logic (props → emit pattern)
- Receive data via props, communicate via events
- Examples: `ModelCard.vue`, `Toast.vue`, `LoginForm.vue`

### Business Logic Layer (`/composables`)
- Encapsulate stateful logic and operations
- Handle API calls, data transformation, event coordination
- Reusable across components
- Examples:
  - `useToasts()`: Toast notification management
  - `useModelPolling()`: Auto-refresh models when jobs active
  - `useModelOperations()`: Delete/retry operations with UX feedback

### State Management (`/stores`)
- Global app state (Pinia store)
- Authentication: user profile, tokens, login/logout
- Centralized access across all components
- Persisted to localStorage for session management

### API Layer (`/api`)
- HTTP client configuration and interceptors
- Endpoint grouping by resource (auth, models, conversions)
- Request/response handling with error management
- Type-safe API contracts

### Types (`/types`)
- DTOs and interfaces for API contracts
- Ensures type safety across layers
- Matches backend OpenAPI schema

---

## API Integration Pattern

```typescript
// 1. API Layer (api/models.ts)
export const modelsApi = {
  getModels: async (): Promise<IfcModelDto[]> => {
    const response = await client.get('/api/models')
    return response.data
  },
  deleteModel: async (id: string): Promise<void> => {
    await client.delete(`/api/models/${id}`)
  }
}

// 2. Composable Layer (composables/useModelOperations.ts)
export function useModelOperations(models: Ref<IfcModelDto[]>) {
  async function deleteModel(model: IfcModelDto) {
    isDeleting.value = true
    try {
      await modelsApi.deleteModel(model.id)
      models.value = models.value.filter(m => m.id !== model.id)
      showToast('Model deleted successfully', 'success')
    } catch (error) {
      showToast('Delete failed: ' + error.message, 'error')
    } finally {
      isDeleting.value = false
    }
  }
  return { deleteModel, isDeleting }
}

// 3. View Layer (views/DashboardView.vue)
<script setup>
const { deleteModel } = useModelOperations(models)
</script>
```

### HTTP Client (`api/client.ts`)
- Centralized Axios instance
- Auth token injection (Authorization header)
- Response interceptor for token refresh
- Global error handling
- Base URL configuration

---

## Authentication & Routing Flow

```
Browser Request
  ↓
Router.beforeEach() Guard
  ↓
├─ Checks: to.meta.requiresAuth
│  ├─ Yes → Check authStore.isAuthenticated
│  │  ├─ False → Redirect to /login
│  │  └─ True → Proceed
│  └─ No → Check to.meta.guestOnly
│     ├─ True + isAuthenticated → Redirect to /dashboard
│     └─ False → Proceed
│  
└─ Components Access
   ├─ useAuthStore() → Gets tokens & user info
   ├─ API calls include Authorization header
   └─ 401 response → Token refresh or logout
```

**Protected Routes:**
- `/dashboard` - Dashboard view
- `/upload` - Model upload
- `/viewer/:id` - 3D viewer
- `/profile` - User profile

**Guest-Only Routes:**
- `/login` - Login page
- `/register` - Registration page

---

## Error Handling & User Feedback

### Toast System
```
User Action (e.g., delete model)
  ↓
Try-Catch in Composable
  ├─ Success → showToast(message, 'success')
  ├─ Error → showToast(errorDetails, 'error')
  └─ Loading state updated
  ↓
Toast Composable
  ├─ Add toast to array with ID
  ├─ Schedule auto-dismiss (5s default)
  └─ Emit via ToastContainer → Toast components
  ↓
UI renders toast, user can dismiss manually
```

### API Error Flow
```
API Call
  ↓
Response Interceptor (client.ts)
  ├─ 200-299: Return data
  ├─ 401: Try refresh token
  │  ├─ Success: Retry original request
  │  └─ Fail: Clear auth, redirect to login
  ├─ 4xx/5xx: Extract error message
  └─ Network error: Generic error message
  ↓
Composable Catch Block
  └─ showToast(error.message, 'error')
```

---

## Build & Runtime Configuration

### Vue Configuration (`vite.config.ts`)
- Vite for fast dev server and optimized builds
- Vue 3 plugin
- TypeScript support

### Environment Handling (`env.d.ts`)
```typescript
interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string
}
```

### API Base URL
- Development: `http://localhost:5000/api`
- Production: Uses relative path or env variable
- Set in `api/client.ts` via axios config

---

## Data Models (Types)

### User & Auth
```typescript
interface UserProfile {
  id: string
  username: string
  email: string
  role: UserRole
  isActive: boolean
  createdAt: string
}

interface AuthResponse {
  accessToken: string
  refreshToken: string
  userId: string
  username: string
  email: string
  role: UserRole
}
```

### Models
```typescript
interface IfcModelDto {
  id: string
  fileName: string
  fileSize: number
  uploadedBy: string
  uploadedAt: string
  conversionStatus: ConversionStatus
  conversionError?: string
  xktUrl?: string
}

enum ConversionStatus {
  Uploaded = 'Uploaded',
  Processing = 'Processing',
  Completed = 'Completed',
  Failed = 'Failed'
}
```


