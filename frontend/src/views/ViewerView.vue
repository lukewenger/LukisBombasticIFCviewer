<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowLeft, Box } from 'lucide-vue-next'
import { modelsApi } from '../api/models'
import XeokitPointViewer from '../components/XeokitPointViewer.vue'
import type { IfcModelDto } from '../types'

const route = useRoute()
const router = useRouter()

const modelId = route.params.id as string
const model = ref<IfcModelDto | null>(null)
const isLoading = ref(true)

const viewerModelName = computed(() => model.value?.fileName ?? '3D-Viewer')
const viewerSrc = computed(() => model.value?.xktOutputUrl ?? null)
const usingDemo = computed(() => !model.value?.xktOutputUrl)

onMounted(async () => {
  isLoading.value = true
  try {
    model.value = await modelsApi.getModel(modelId)
  } catch {
    // If the model cannot be loaded we still show the demo model in the shared viewer.
  } finally {
    isLoading.value = false
  }
})
</script>

<template>
  <div class="flex flex-col h-[calc(100vh-4rem)]">
    <div class="flex items-center justify-between px-4 py-2 bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
      <div class="flex items-center gap-3">
        <button
          class="p-2 rounded-lg text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
          @click="router.push('/dashboard')"
          title="Zurück"
        >
          <ArrowLeft class="w-5 h-5" />
        </button>
        <Box class="w-5 h-5 text-blue-600" />
        <div>
          <h1 class="text-sm font-semibold text-gray-900 dark:text-white truncate max-w-md">
            {{ viewerModelName }}
          </h1>
          <p v-if="usingDemo" class="text-xs text-yellow-600 dark:text-yellow-400">
            Demo-Modell (Duplex) — Konvertierung ausstehend
          </p>
        </div>
      </div>
    </div>

    <div v-if="isLoading" class="flex-1 flex items-center justify-center bg-gray-100 dark:bg-gray-900">
      <div class="text-center">
        <div class="animate-spin h-10 w-10 border-4 border-blue-600 border-t-transparent rounded-full mx-auto mb-4"></div>
        <p class="text-gray-500 dark:text-gray-400">Modell wird geladen...</p>
      </div>
    </div>

    <div v-else class="flex-1">
      <XeokitPointViewer :model-src="viewerSrc" canvas-height-class="h-full" />
    </div>
  </div>
</template>
