<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowLeft, Box } from 'lucide-vue-next'
import { modelsApi } from '../api/models'
import XeokitPointViewer from '../components/XeokitPointViewer.vue'
import type { IfcModelDto } from '../types'

const route = useRoute()
const router = useRouter()

// Primary model ID from route param; optional additional IDs from ?also=id2,id3
const primaryId = route.params.id as string
const alsoParam = route.query.also as string | undefined
const allIds = [primaryId, ...(alsoParam ? alsoParam.split(',').filter(Boolean) : [])]

const models = ref<IfcModelDto[]>([])
const isLoading = ref(true)

const viewerTitle = computed(() => {
  if (models.value.length === 0) return '3D-Viewer'
  if (models.value.length === 1) return models.value[0].fileName
  return `${models.value.length} Modelle`
})

const initialModels = computed(() =>
  models.value
    .filter(m => m.xktOutputUrl)
    .map(m => ({ id: m.id, src: m.xktOutputUrl!, label: m.fileName }))
)

const usingDemo = computed(() => models.value.length === 0 || models.value.every(m => !m.xktOutputUrl))

onMounted(async () => {
  isLoading.value = true
  try {
    // Fetch all model DTOs in parallel
    const results = await Promise.allSettled(allIds.map(id => modelsApi.getModel(id)))
    models.value = results
      .filter((r): r is PromiseFulfilledResult<IfcModelDto> => r.status === 'fulfilled')
      .map(r => r.value)
  } catch {
    // Viewer will fall back to demo model
  } finally {
    isLoading.value = false
  }
})
</script>

<template>
  <div class="max-w-7xl mx-auto px-4 py-6">
    <div class="flex items-center justify-between mb-4">
      <div class="flex items-center gap-3">
        <button
          class="p-2 rounded-lg text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
          @click="router.push('/dashboard')"
          title="Zurueck zum Dashboard"
        >
          <ArrowLeft class="w-5 h-5" />
        </button>
        <Box class="w-6 h-6 text-blue-600" />
        <div>
          <h1 class="text-xl font-semibold text-gray-900 dark:text-white">{{ viewerTitle }}</h1>
          <p v-if="usingDemo" class="text-xs text-yellow-600 dark:text-yellow-400">
            Demo-Modell (Duplex) wird angezeigt, da keine XKT-Ausgabe verfuegbar ist.
          </p>
        </div>
      </div>
    </div>

    <div v-if="isLoading" class="flex items-center justify-center rounded-xl bg-white dark:bg-gray-800 h-[70vh]">
      <div class="text-center">
        <div class="animate-spin h-10 w-10 border-4 border-blue-600 border-t-transparent rounded-full mx-auto mb-4"></div>
        <p class="text-gray-500 dark:text-gray-400">Modell wird geladen...</p>
      </div>
    </div>

    <div v-else class="rounded-xl overflow-hidden border border-gray-200 dark:border-gray-700 bg-gray-100 dark:bg-gray-900">
      <XeokitPointViewer :initial-models="initialModels" canvas-height-class="h-[72vh]" />
    </div>
  </div>
</template>
