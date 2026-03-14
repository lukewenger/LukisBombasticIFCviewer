<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { LayoutDashboard, Eye, Upload, RefreshCw, Box, List, Trash2 } from 'lucide-vue-next'
import { modelsApi } from '../api/models'
import { conversionsApi } from '../api/conversions'
import XeokitPointViewer from '../components/XeokitPointViewer.vue'
import type { IfcModelDto } from '../types'
import { ConversionFormat, ModelStatus } from '../types/models'

const router = useRouter()
const authStore = useAuthStore()

// --- Tab state ---
const activeTab = ref<'models' | 'viewer'>('models')

// --- Models state ---
const models = ref<IfcModelDto[]>([])
const isLoading = ref(false)
const error = ref<string | null>(null)
const isDeleting = ref<Record<string, boolean>>({})
const isRetrying = ref<Record<string, boolean>>({})
let pollTimer: ReturnType<typeof setInterval> | null = null

type ToastType = 'success' | 'error'
interface ToastMessage {
  id: number
  message: string
  type: ToastType
}

const toasts = ref<ToastMessage[]>([])
let toastId = 0
const toastTimers = new Map<number, ReturnType<typeof setTimeout>>()

function removeToast(id: number) {
  toasts.value = toasts.value.filter((toast) => toast.id !== id)
  const timer = toastTimers.get(id)
  if (timer) {
    clearTimeout(timer)
    toastTimers.delete(id)
  }
}

function showToast(message: string, type: ToastType = 'success', duration = 3000) {
  const id = ++toastId
  toasts.value.push({ id, message, type })
  const timer = setTimeout(() => removeToast(id), duration)
  toastTimers.set(id, timer)
}

const hasActiveJobs = computed(() =>
  models.value.some(
    (m) => m.status === ModelStatus.Uploaded || m.status === ModelStatus.Processing
  )
)

async function fetchModels() {
  try {
    error.value = null
    models.value = await modelsApi.getModels()
  } catch {
    error.value = 'Modelle konnten nicht geladen werden.'
  }
}

function startPolling() {
  stopPolling()
  pollTimer = setInterval(async () => {
    if (hasActiveJobs.value) {
      await fetchModels()
    }
  }, 5000)
}

function stopPolling() {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
}

// --- Viewer state ---
const viewerModelName = ref('Duplex (Demo)')
const viewerSrc = ref<string | null>(null)
const selectedViewerModelId = ref<string | null>(null)

function loadDemoInViewer() {
  selectedViewerModelId.value = null
  viewerSrc.value = null
  viewerModelName.value = 'Duplex (Demo)'
}

function openModelInViewer(model: IfcModelDto) {
  if (!model.xktOutputUrl) return
  selectedViewerModelId.value = model.id
  viewerSrc.value = model.xktOutputUrl
  viewerModelName.value = model.fileName
  activeTab.value = 'viewer'
}

async function deleteModel(model: IfcModelDto) {
  const confirmed = window.confirm(`Modell "${model.fileName}" wirklich loeschen?`)
  if (!confirmed) return

  isDeleting.value[model.id] = true
  try {
    await modelsApi.deleteModel(model.id)
    models.value = models.value.filter((m) => m.id !== model.id)
    showToast(`Modell "${model.fileName}" wurde geloescht.`, 'success')

    if (selectedViewerModelId.value === model.id) {
      loadDemoInViewer()
      activeTab.value = 'models'
    }
  } catch {
    error.value = 'Modell konnte nicht geloescht werden.'
    showToast('Loeschen fehlgeschlagen.', 'error')
  } finally {
    isDeleting.value[model.id] = false
  }
}

async function retryConversion(model: IfcModelDto) {
  isRetrying.value[model.id] = true
  try {
    await conversionsApi.createConversionJob(model.id, ConversionFormat.XKT)
    await fetchModels()
    startPolling()
    showToast(`Konvertierung fuer "${model.fileName}" wurde neu gestartet.`, 'success')
  } catch {
    error.value = 'Konvertierung konnte nicht neu gestartet werden.'
    showToast('Konvertierung konnte nicht neu gestartet werden.', 'error')
  } finally {
    isRetrying.value[model.id] = false
  }
}

onMounted(async () => {
  isLoading.value = true
  await fetchModels()
  isLoading.value = false
  startPolling()
})

onUnmounted(() => {
  stopPolling()
  for (const timer of toastTimers.values()) {
    clearTimeout(timer)
  }
  toastTimers.clear()
})

function statusLabel(status: ModelStatus): string {
  switch (status) {
    case ModelStatus.Uploaded: return 'Hochgeladen'
    case ModelStatus.Processing: return 'Verarbeitung'
    case ModelStatus.Ready: return 'Bereit'
    case ModelStatus.Failed: return 'Fehlgeschlagen'
    case ModelStatus.Archived: return 'Archiviert'
    default: return 'Unbekannt'
  }
}

function statusColor(status: ModelStatus): string {
  switch (status) {
    case ModelStatus.Uploaded: return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400'
    case ModelStatus.Processing: return 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400'
    case ModelStatus.Ready: return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400'
    case ModelStatus.Failed: return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400'
    case ModelStatus.Archived: return 'bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-400'
    default: return 'bg-gray-100 text-gray-800'
  }
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('de-CH', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}
</script>

<template>
  <div class="max-w-6xl mx-auto px-4 py-8">
    <div class="fixed top-20 right-4 z-50 space-y-2 w-[22rem]">
      <div
        v-for="toast in toasts"
        :key="toast.id"
        class="rounded-xl shadow-lg border px-4 py-3 text-sm flex items-start justify-between gap-3"
        :class="toast.type === 'success'
          ? 'bg-emerald-50 border-emerald-200 text-emerald-900 dark:bg-emerald-900/30 dark:border-emerald-700 dark:text-emerald-200'
          : 'bg-red-50 border-red-200 text-red-900 dark:bg-red-900/30 dark:border-red-700 dark:text-red-200'"
      >
        <span>{{ toast.message }}</span>
        <button class="text-current/70 hover:text-current" @click="removeToast(toast.id)">X</button>
      </div>
    </div>

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
          v-if="activeTab === 'models'"
          class="p-2 rounded-lg text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
          title="Aktualisieren"
          @click="fetchModels"
        >
          <RefreshCw class="w-5 h-5" />
        </button>
        <button
          class="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 transition-colors"
          @click="router.push('/upload')"
        >
          <Upload class="w-4 h-4" />
          Hochladen
        </button>
      </div>
    </div>

    <!-- Tabs -->
    <div class="flex border-b border-gray-200 dark:border-gray-700 mb-6">
      <button
        class="flex items-center gap-2 px-4 py-2.5 text-sm font-medium border-b-2 transition-colors -mb-px"
        :class="activeTab === 'models'
          ? 'border-blue-600 text-blue-600'
          : 'border-transparent text-gray-500 hover:text-gray-700 dark:hover:text-gray-300'"
        @click="activeTab = 'models'"
      >
        <List class="w-4 h-4" />
        Modelle
      </button>
      <button
        class="flex items-center gap-2 px-4 py-2.5 text-sm font-medium border-b-2 transition-colors -mb-px"
        :class="activeTab === 'viewer'
          ? 'border-blue-600 text-blue-600'
          : 'border-transparent text-gray-500 hover:text-gray-700 dark:hover:text-gray-300'"
        @click="activeTab = 'viewer'"
      >
        <Box class="w-4 h-4" />
        3D-Viewer
      </button>
    </div>

    <!-- ===== MODELS TAB ===== -->
    <div v-show="activeTab === 'models'">
      <!-- Loading -->
      <div v-if="isLoading" class="flex justify-center py-16">
        <div class="animate-spin h-8 w-8 border-4 border-blue-600 border-t-transparent rounded-full"></div>
      </div>

      <!-- Error -->
      <div v-else-if="error" class="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-xl p-6 text-center">
        <p class="text-red-700 dark:text-red-400">{{ error }}</p>
        <button class="mt-3 text-sm text-blue-600 hover:underline" @click="fetchModels">Erneut versuchen</button>
      </div>

      <!-- Empty state -->
      <div v-else-if="models.length === 0" class="bg-white dark:bg-gray-800 rounded-xl shadow-md p-12 text-center">
        <Upload class="w-12 h-12 text-gray-400 mx-auto mb-4" />
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white mb-2">Keine Modelle vorhanden</h2>
        <p class="text-gray-500 dark:text-gray-400 mb-6">Lade dein erstes IFC-Modell hoch, um loszulegen.</p>
        <button
          class="px-6 py-3 bg-blue-600 text-white font-medium rounded-lg hover:bg-blue-700 transition-colors"
          @click="router.push('/upload')"
        >
          Modell hochladen
        </button>
      </div>

      <!-- Model list -->
      <div v-else class="space-y-4">
        <!-- Polling indicator -->
        <p v-if="hasActiveJobs" class="text-sm text-blue-600 dark:text-blue-400 flex items-center gap-2">
          <span class="animate-spin h-3 w-3 border-2 border-blue-600 border-t-transparent rounded-full"></span>
          Automatische Aktualisierung aktiv...
        </p>

        <div class="bg-white dark:bg-gray-800 rounded-xl shadow-md overflow-hidden">
          <table class="w-full">
            <thead>
              <tr class="border-b border-gray-200 dark:border-gray-700 text-left text-sm text-gray-500 dark:text-gray-400">
                <th class="px-6 py-3 font-medium">Dateiname</th>
                <th class="px-6 py-3 font-medium hidden sm:table-cell">Grösse</th>
                <th class="px-6 py-3 font-medium">Status</th>
                <th class="px-6 py-3 font-medium hidden md:table-cell">Hochgeladen</th>
                <th class="px-6 py-3 font-medium text-right">Aktion</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="model in models"
                :key="model.id"
                class="border-b border-gray-100 dark:border-gray-700/50 last:border-0 hover:bg-gray-50 dark:hover:bg-gray-700/30 transition-colors"
              >
                <td class="px-6 py-4">
                  <p class="font-medium text-gray-900 dark:text-white truncate max-w-xs">{{ model.fileName }}</p>
                </td>
                <td class="px-6 py-4 text-sm text-gray-500 dark:text-gray-400 hidden sm:table-cell">
                  {{ formatSize(model.fileSizeBytes) }}
                </td>
                <td class="px-6 py-4">
                  <span
                    class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium"
                    :class="statusColor(model.status)"
                  >
                    {{ statusLabel(model.status) }}
                  </span>
                </td>
                <td class="px-6 py-4 text-sm text-gray-500 dark:text-gray-400 hidden md:table-cell">
                  {{ formatDate(model.createdAt) }}
                </td>
                <td class="px-6 py-4 text-right">
                  <div class="inline-flex items-center gap-1.5">
                    <button
                      v-if="model.status === ModelStatus.Ready"
                      class="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-blue-600 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded-lg transition-colors"
                      @click="openModelInViewer(model)"
                    >
                      <Eye class="w-4 h-4" />
                      Anzeigen
                    </button>

                    <button
                      v-if="model.status === ModelStatus.Ready || model.status === ModelStatus.Failed"
                      class="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-amber-700 hover:bg-amber-50 dark:hover:bg-amber-900/20 rounded-lg transition-colors disabled:opacity-60"
                      :disabled="isRetrying[model.id]"
                      @click="retryConversion(model)"
                    >
                      <RefreshCw class="w-4 h-4" :class="isRetrying[model.id] ? 'animate-spin' : ''" />
                      Redo
                    </button>

                    <button
                      class="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-lg transition-colors disabled:opacity-60"
                      :disabled="isDeleting[model.id]"
                      @click="deleteModel(model)"
                    >
                      <Trash2 class="w-4 h-4" :class="isDeleting[model.id] ? 'animate-pulse' : ''" />
                      Loeschen
                    </button>

                    <span
                      v-if="model.status === ModelStatus.Processing || model.status === ModelStatus.Uploaded"
                      class="text-sm text-gray-400 dark:text-gray-500"
                    >
                      Warten...
                    </span>
                  </div>
                </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
    </div>

    <!-- ===== VIEWER TAB ===== -->
    <div v-show="activeTab === 'viewer'" class="rounded-xl overflow-hidden shadow-md bg-white dark:bg-gray-800">
      <!-- Viewer toolbar -->
      <div class="flex items-center gap-3 px-4 py-2.5 border-b border-gray-200 dark:border-gray-700">
        <Box class="w-5 h-5 text-blue-600" />
        <span class="text-sm font-semibold text-gray-900 dark:text-white">{{ viewerModelName }}</span>
        <button
          class="ml-auto text-xs text-blue-600 hover:underline"
          @click="loadDemoInViewer"
        >
          Demo laden
        </button>
      </div>

      <XeokitPointViewer :model-src="viewerSrc" canvas-height-class="h-[60vh]" />
    </div>
  </div>
</template>
