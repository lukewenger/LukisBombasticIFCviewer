<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { LayoutDashboard, Eye, Upload, RefreshCw } from 'lucide-vue-next'
import { modelsApi } from '../api/models'
import type { IfcModelDto } from '../types'
import { ModelStatus } from '../types/models'

const router = useRouter()
const authStore = useAuthStore()

const models = ref<IfcModelDto[]>([])
const isLoading = ref(false)
const error = ref<string | null>(null)
let pollTimer: ReturnType<typeof setInterval> | null = null

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

onMounted(async () => {
  isLoading.value = true
  await fetchModels()
  isLoading.value = false
  startPolling()
})

onUnmounted(() => {
  stopPolling()
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
    <!-- Header -->
    <div class="flex items-center justify-between mb-8">
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
                <button
                  v-if="model.status === ModelStatus.Ready"
                  class="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-blue-600 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded-lg transition-colors"
                  @click="router.push(`/viewer/${model.id}`)"
                >
                  <Eye class="w-4 h-4" />
                  Anzeigen
                </button>
                <span
                  v-else-if="model.status === ModelStatus.Processing || model.status === ModelStatus.Uploaded"
                  class="text-sm text-gray-400 dark:text-gray-500"
                >
                  Warten...
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
