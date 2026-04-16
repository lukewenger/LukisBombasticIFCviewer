<script setup lang="ts">
import { ref, computed } from 'vue'
import { Layers, Eye, EyeOff, Trash2, Plus, X, Loader2 } from 'lucide-vue-next'
import { modelsApi } from '../api/models'
import { ModelStatus } from '../types/models'
import type { IfcModelDto } from '../types'
import type { LoadedModelEntry } from '../composables/useXeokitViewer'

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
const showAddModal = ref(false)
const availableModels = ref<IfcModelDto[]>([])
const fetchingModels = ref(false)
const fetchError = ref<string | null>(null)

const loadedCount = computed(() => Object.keys(props.loadedModels).length)
const anyLoading = computed(() => Object.values(props.loadedModels).some(m => m.loading))

async function openAddModal() {
  showAddModal.value = true
  fetchingModels.value = true
  fetchError.value = null
  try {
    const all = await modelsApi.getModels()
    availableModels.value = all.filter(
      m => m.status === ModelStatus.Ready && m.xktOutputUrl && !props.loadedModels[m.id]
    )
  } catch {
    fetchError.value = 'Modelle konnten nicht geladen werden.'
  } finally {
    fetchingModels.value = false
  }
}

function closeAddModal() {
  showAddModal.value = false
  availableModels.value = []
  fetchError.value = null
}

function selectModel(model: IfcModelDto) {
  emit('add-model', model)
  closeAddModal()
}
</script>

<template>
  <!-- Model List Panel -->
  <div class="absolute top-4 left-4 z-10 w-64 select-none">
    <div class="bg-white dark:bg-gray-800 rounded-xl shadow-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
      <!-- Header -->
      <button
        class="w-full flex items-center justify-between px-3 py-2.5 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors"
        @click="isOpen = !isOpen"
      >
        <div class="flex items-center gap-2">
          <Layers class="w-4 h-4 text-blue-500" />
          <span>Modelle</span>
          <span class="bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 text-xs font-semibold px-1.5 py-0.5 rounded-full">
            {{ loadedCount }}
          </span>
          <Loader2 v-if="anyLoading" class="w-3.5 h-3.5 text-blue-500 animate-spin" />
        </div>
        <svg
          class="w-4 h-4 transition-transform"
          :class="isOpen ? 'rotate-0' : '-rotate-90'"
          fill="none" stroke="currentColor" viewBox="0 0 24 24"
        >
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
        </svg>
      </button>

      <!-- Model list -->
      <div v-if="isOpen">
        <div
          v-if="loadedCount === 0"
          class="px-3 py-3 text-xs text-gray-400 dark:text-gray-500 text-center"
        >
          Keine Modelle geladen
        </div>

        <ul v-else class="divide-y divide-gray-100 dark:divide-gray-700/50 max-h-48 overflow-y-auto">
          <li
            v-for="entry in loadedModels"
            :key="entry.id"
            class="flex items-center gap-2 px-3 py-2"
          >
            <Loader2
              v-if="entry.loading"
              class="w-3.5 h-3.5 text-blue-500 animate-spin shrink-0"
            />
            <button
              v-else
              class="shrink-0 text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 transition-colors"
              :title="entry.visible ? 'Ausblenden' : 'Einblenden'"
              @click="emit('toggle-visible', entry.id, !entry.visible)"
            >
              <Eye v-if="entry.visible" class="w-3.5 h-3.5" />
              <EyeOff v-else class="w-3.5 h-3.5" />
            </button>

            <span
              class="flex-1 text-xs truncate"
              :class="[
                entry.error ? 'text-red-500 dark:text-red-400' : 'text-gray-700 dark:text-gray-300',
                !entry.visible ? 'opacity-50' : '',
              ]"
              :title="entry.error ?? entry.label"
            >
              {{ entry.label }}
            </span>

            <button
              class="shrink-0 text-gray-400 hover:text-red-500 dark:hover:text-red-400 transition-colors"
              title="Entfernen"
              @click="emit('unload', entry.id)"
            >
              <Trash2 class="w-3.5 h-3.5" />
            </button>
          </li>
        </ul>

        <!-- Add model button -->
        <div class="px-3 py-2 border-t border-gray-100 dark:border-gray-700/50">
          <button
            class="w-full flex items-center justify-center gap-1.5 px-3 py-1.5 text-xs font-medium text-blue-600 dark:text-blue-400 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded-lg transition-colors"
            @click="openAddModal"
          >
            <Plus class="w-3.5 h-3.5" />
            Modell hinzufügen
          </button>
        </div>
      </div>
    </div>
  </div>

  <!-- Add Model Modal -->
  <Teleport to="body">
    <div
      v-if="showAddModal"
      class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50"
      @click.self="closeAddModal"
    >
      <div class="bg-white dark:bg-gray-800 rounded-xl shadow-2xl w-full max-w-md border border-gray-200 dark:border-gray-700 overflow-hidden">
        <!-- Modal header -->
        <div class="flex items-center justify-between px-5 py-4 border-b border-gray-200 dark:border-gray-700">
          <div class="flex items-center gap-2">
            <Plus class="w-5 h-5 text-blue-500" />
            <h2 class="text-base font-semibold text-gray-900 dark:text-white">Modell hinzufügen</h2>
          </div>
          <button
            class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 transition-colors"
            @click="closeAddModal"
          >
            <X class="w-5 h-5" />
          </button>
        </div>

        <!-- Modal body -->
        <div class="p-5">
          <div v-if="fetchingModels" class="flex items-center justify-center py-8">
            <Loader2 class="w-6 h-6 text-blue-500 animate-spin" />
          </div>

          <p v-else-if="fetchError" class="text-sm text-red-600 dark:text-red-400 text-center py-4">
            {{ fetchError }}
          </p>

          <p v-else-if="availableModels.length === 0" class="text-sm text-gray-500 dark:text-gray-400 text-center py-4">
            Keine weiteren Modelle verfügbar.
          </p>

          <ul v-else class="space-y-1 max-h-72 overflow-y-auto">
            <li v-for="model in availableModels" :key="model.id">
              <button
                class="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg hover:bg-blue-50 dark:hover:bg-blue-900/20 text-left transition-colors group"
                @click="selectModel(model)"
              >
                <Layers class="w-4 h-4 text-gray-400 group-hover:text-blue-500 shrink-0" />
                <span class="flex-1 text-sm text-gray-700 dark:text-gray-300 truncate group-hover:text-blue-700 dark:group-hover:text-blue-300">
                  {{ model.fileName }}
                </span>
              </button>
            </li>
          </ul>
        </div>
      </div>
    </div>
  </Teleport>
</template>
