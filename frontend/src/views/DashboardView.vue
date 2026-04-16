<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { LayoutDashboard, Upload, RefreshCw, Eye, X } from 'lucide-vue-next'
import type { IfcModelDto } from '../types'
import { ModelStatus } from '../types/models'
import { useToasts } from '../composables/useToasts'
import { useModelPolling } from '../composables/useModelPolling'
import { useModelOperations } from '../composables/useModelOperations'
import ToastContainer from '../components/ToastContainer.vue'
import ModelTable from '../components/ModelTable.vue'

const router = useRouter()
const authStore = useAuthStore()

// --- State ---
const models = ref<IfcModelDto[]>([])
const isLoading = ref(false)
const error = ref<string | null>(null)

// --- Multi-select ---
const selectedIds = ref<Set<string>>(new Set())

const selectedCount = computed(() => selectedIds.value.size)
const canViewTogether = computed(() => selectedCount.value >= 2)

function toggleSelect(model: IfcModelDto) {
  const next = new Set(selectedIds.value)
  if (next.has(model.id)) {
    next.delete(model.id)
  } else {
    next.add(model.id)
  }
  selectedIds.value = next
}

function viewSelectedTogether() {
  const ids = [...selectedIds.value]
  if (ids.length === 0) return
  const [first, ...rest] = ids
  const query = rest.length > 0 ? `?also=${rest.join(',')}` : ''
  router.push(`/viewer/${first}${query}`)
}

// --- Composables ---
const { toasts, showToast, removeToast, clearAll } = useToasts()
const { hasActiveJobs, fetchModels: fetchModelList, startPolling, stopPolling } = useModelPolling(models)
const { isDeleting, isRetrying, deleteModel, retryConversion } = useModelOperations(models, showToast)

async function fetchModels() {
  try {
    error.value = null
    await fetchModelList()
  } catch {
    error.value = 'Modelle konnten nicht geladen werden.'
  }
}

function openModelInViewer(model: IfcModelDto) {
  if (selectedCount.value === 0) {
    // No selection active — open this model directly
    router.push(`/viewer/${model.id}`)
    return
  }
  // Selection is active — add this model to the selection and open all together
  const next = new Set(selectedIds.value)
  next.add(model.id)
  selectedIds.value = next
  const ids = [...selectedIds.value]
  const [first, ...rest] = ids
  const query = rest.length > 0 ? `?also=${rest.join(',')}` : ''
  router.push(`/viewer/${first}${query}`)
}

async function handleDeleteModel(model: IfcModelDto) {
  await deleteModel(model)
  selectedIds.value.delete(model.id)
}

onMounted(async () => {
  isLoading.value = true
  await fetchModels()
  isLoading.value = false
  startPolling()
})

onUnmounted(() => {
  stopPolling()
  clearAll()
})
</script>

<template>
  <div class="max-w-6xl mx-auto px-4 py-8">
    <ToastContainer :toasts="toasts" @remove="removeToast" />

    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div class="flex items-center gap-3">
        <LayoutDashboard class="w-8 h-8 text-blue-600" />
        <div>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Dashboard</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400">
            Willkommen, <span class="font-semibold">{{ authStore.username }}</span>
          </p>
        </div>
      </div>
      <div class="flex items-center gap-3">
        <button
          class="p-2 rounded-lg text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors min-h-[44px] min-w-[44px]"
          title="Aktualisieren"
          @click="fetchModels"
        >
          <RefreshCw class="w-5 h-5" />
        </button>

        <button
          class="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 transition-colors min-h-[44px]"
          @click="router.push('/upload')"
        >
          <Upload class="w-4 h-4" />
          Hochladen
        </button>
      </div>
    </div>

    <!-- Multi-select action bar — shown when at least one model is selected -->
    <div
      v-if="selectedCount > 0"
      class="flex items-center justify-between mb-4 px-4 py-3 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-xl"
    >
      <p class="text-sm text-blue-700 dark:text-blue-300">
        {{ selectedCount }} Modell{{ selectedCount !== 1 ? 'e' : '' }} ausgewählt
      </p>
      <div class="flex items-center gap-2">
        <button
          class="text-xs text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 underline"
          @click="selectedIds = new Set()"
        >
          Auswahl aufheben
        </button>
        <button
          class="flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg transition-colors min-h-[36px]"
          :class="canViewTogether
            ? 'bg-blue-600 text-white hover:bg-blue-700'
            : 'bg-blue-100 dark:bg-blue-900/30 text-blue-600 dark:text-blue-400 hover:bg-blue-200 dark:hover:bg-blue-900/50'"
          @click="viewSelectedTogether"
        >
          <Eye class="w-4 h-4" />
          {{ canViewTogether ? `Ausgewählte anzeigen (${selectedCount})` : 'Anzeigen' }}
        </button>
        <button
          class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200"
          title="Auswahl aufheben"
          @click="selectedIds = new Set()"
        >
          <X class="w-4 h-4" />
        </button>
      </div>
    </div>

    <div v-if="isLoading" class="flex justify-center py-16">
      <div class="animate-spin h-8 w-8 border-4 border-blue-600 border-t-transparent rounded-full"></div>
    </div>

    <div
      v-else-if="error"
      class="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-xl p-6 text-center"
    >
      <p class="text-red-700 dark:text-red-400">{{ error }}</p>
      <button class="mt-3 text-sm text-blue-600 hover:underline" @click="fetchModels">Erneut versuchen</button>
    </div>

    <div
      v-else-if="models.length === 0"
      class="bg-white dark:bg-gray-800 rounded-xl shadow-md p-12 text-center"
    >
      <Upload class="w-12 h-12 text-gray-400 mx-auto mb-4" />
      <h2 class="text-lg font-semibold text-gray-900 dark:text-white mb-2">Keine Modelle vorhanden</h2>
      <p class="text-gray-500 dark:text-gray-400 mb-6">Lade dein erstes IFC-Modell hoch, um loszulegen.</p>
      <button
        class="px-6 py-3 bg-blue-600 text-white font-medium rounded-lg hover:bg-blue-700 transition-colors min-h-[44px]"
        @click="router.push('/upload')"
      >
        Modell hochladen
      </button>
    </div>

    <div v-else class="space-y-4">
      <p v-if="hasActiveJobs" class="text-sm text-blue-600 dark:text-blue-400 flex items-center gap-2">
        <span class="animate-spin h-3 w-3 border-2 border-blue-600 border-t-transparent rounded-full"></span>
        Automatische Aktualisierung aktiv...
      </p>

      <ModelTable
        :models="models"
        :deleting-ids="isDeleting"
        :retrying-ids="isRetrying"
        :selected-ids="selectedIds"
        @view="openModelInViewer"
        @retry="retryConversion"
        @delete="handleDeleteModel"
        @toggle-select="toggleSelect"
      />
    </div>
  </div>
</template>
