<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Layers, Eye, EyeOff, Trash2, Download, Loader2, RefreshCw, AlertCircle } from 'lucide-vue-next'
import { modelsApi } from '../api/models'
import { ModelStatus } from '../types/models'
import type { IfcModelDto } from '../types'
import type { LoadedModelEntry } from '../composables/useXeokitViewer'

function formatBytes(bytes: number): string {
  if (bytes <= 0) return ''
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

const props = defineProps<{
  loadedModels: Record<string, LoadedModelEntry>
  fallbackSrc?: string
}>()

const emit = defineEmits<{
  (e: 'toggle-visible', id: string, visible: boolean): void
  (e: 'unload', id: string): void
  (e: 'add-model', model: IfcModelDto): void
}>()

const isOpen = ref(true)
const allModels = ref<IfcModelDto[]>([])
const fetchingModels = ref(false)
const fetchError = ref<string | null>(null)

const loadedCount = computed(() => Object.keys(props.loadedModels).length)
const anyLoading = computed(() => Object.values(props.loadedModels).some(m => m.loading))
const totalBytes = computed(() =>
  Object.values(props.loadedModels).reduce((sum, m) => sum + (m.fileSizeBytes ?? 0), 0)
)
const totalSizeLabel = computed(() => formatBytes(totalBytes.value))

// All ready models with an XKT output, sorted: loaded first
const displayModels = computed(() => {
  return allModels.value.filter(m => m.status === ModelStatus.Ready && m.xktOutputUrl)
})

async function fetchAvailableModels() {
  fetchingModels.value = true
  fetchError.value = null
  try {
    const all = await modelsApi.getModels()
    allModels.value = all.filter(m => m.status === ModelStatus.Ready && m.xktOutputUrl)
  } catch {
    fetchError.value = 'Modelle konnten nicht geladen werden.'
  } finally {
    fetchingModels.value = false
  }
}

onMounted(() => {
  fetchAvailableModels()
})
</script>

<template>
  <!-- Model List Panel -->
  <div class="absolute top-4 left-4 z-10 w-72 select-none">
    <div class="bg-white dark:bg-gray-800 rounded-xl shadow-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
      <!-- Header -->
      <div class="flex items-center px-3 py-2.5">
        <button
          class="flex-1 flex items-center gap-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:text-gray-900 dark:hover:text-white transition-colors text-left"
          @click="isOpen = !isOpen"
        >
          <Layers class="w-4 h-4 text-blue-500 shrink-0" />
          <span>Modelle</span>
          <span class="bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 text-xs font-semibold px-1.5 py-0.5 rounded-full">
            {{ loadedCount }}
          </span>
          <span v-if="totalSizeLabel" class="text-xs text-gray-400 dark:text-gray-500">{{ totalSizeLabel }}</span>
          <Loader2 v-if="anyLoading" class="w-3.5 h-3.5 text-blue-500 animate-spin shrink-0" />
        </button>
        <div class="flex items-center gap-1">
          <!-- Refresh button -->
          <button
            class="p-1 rounded text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            title="Modelliste aktualisieren"
            :disabled="fetchingModels"
            @click.stop="fetchAvailableModels"
          >
            <RefreshCw class="w-3.5 h-3.5" :class="fetchingModels ? 'animate-spin' : ''" />
          </button>
          <!-- Collapse chevron -->
          <svg
            class="w-4 h-4 text-gray-400 transition-transform shrink-0"
            :class="isOpen ? 'rotate-0' : '-rotate-90'"
            fill="none" stroke="currentColor" viewBox="0 0 24 24"
            @click="isOpen = !isOpen"
          >
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
          </svg>
        </div>
      </div>

      <!-- Body -->
      <div v-if="isOpen">
        <!-- Loading state -->
        <div v-if="fetchingModels && displayModels.length === 0" class="flex items-center justify-center py-5">
          <Loader2 class="w-5 h-5 text-blue-500 animate-spin" />
        </div>

        <!-- Fetch error -->
        <div v-else-if="fetchError" class="px-3 py-3 flex items-start gap-2">
          <AlertCircle class="w-3.5 h-3.5 text-red-500 shrink-0 mt-0.5" />
          <p class="text-xs text-red-600 dark:text-red-400">{{ fetchError }}</p>
        </div>

        <!-- No models -->
        <div
          v-else-if="displayModels.length === 0"
          class="px-3 py-3 text-xs text-gray-400 dark:text-gray-500 text-center"
        >
          Keine Modelle verfügbar
        </div>

        <!-- Unified model list -->
        <ul v-else class="divide-y divide-gray-100 dark:divide-gray-700/50 max-h-64 overflow-y-auto">
          <li
            v-for="model in displayModels"
            :key="model.id"
            class="flex items-center gap-2 px-3 py-2"
          >
            <!-- Loaded model controls -->
            <template v-if="loadedModels[model.id]">
              <!-- Spinner while loading -->
              <Loader2
                v-if="loadedModels[model.id].loading"
                class="w-3.5 h-3.5 text-blue-500 animate-spin shrink-0"
              />
              <!-- Eye / EyeOff toggle once loaded -->
              <button
                v-else
                class="shrink-0 text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 transition-colors"
                :title="loadedModels[model.id].visible ? 'Ausblenden' : 'Einblenden'"
                @click="emit('toggle-visible', model.id, !loadedModels[model.id].visible)"
              >
                <Eye v-if="loadedModels[model.id].visible" class="w-3.5 h-3.5" />
                <EyeOff v-else class="w-3.5 h-3.5" />
              </button>
            </template>

            <!-- Not loaded: Download / Load icon -->
            <button
              v-else
              class="shrink-0 text-gray-400 hover:text-blue-600 dark:hover:text-blue-400 transition-colors"
              title="Modell laden"
              @click="emit('add-model', model)"
            >
              <Download class="w-3.5 h-3.5" />
            </button>

            <!-- Model name -->
            <span
              class="flex-1 text-xs truncate min-w-0"
              :class="[
                loadedModels[model.id]?.error ? 'text-red-500 dark:text-red-400' : 'text-gray-700 dark:text-gray-300',
                loadedModels[model.id] && !loadedModels[model.id].visible ? 'opacity-50' : '',
              ]"
              :title="loadedModels[model.id]?.error ?? model.fileName"
            >
              {{ model.fileName }}
            </span>

            <!-- Error indicator -->
            <AlertCircle
              v-if="loadedModels[model.id]?.error"
              class="w-3 h-3 text-red-400 shrink-0"
              :title="loadedModels[model.id].error ?? ''"
            />

            <!-- File size -->
            <span
              v-if="model.fileSizeBytes > 0"
              class="shrink-0 text-xs text-gray-400 dark:text-gray-500 tabular-nums"
            >
              {{ formatBytes(model.fileSizeBytes) }}
            </span>

            <!-- Unload button — only for loaded models -->
            <button
              v-if="loadedModels[model.id]"
              class="shrink-0 text-gray-400 hover:text-red-500 dark:hover:text-red-400 transition-colors"
              title="Entfernen"
              @click="emit('unload', model.id)"
            >
              <Trash2 class="w-3.5 h-3.5" />
            </button>
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>
