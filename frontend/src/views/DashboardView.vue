<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { LayoutDashboard, Upload, RefreshCw, Box, List } from 'lucide-vue-next'
import type { IfcModelDto } from '../types'
import { useToasts } from '../composables/useToasts'
import { useModelPolling } from '../composables/useModelPolling'
import { useModelOperations } from '../composables/useModelOperations'
import ToastContainer from '../components/ToastContainer.vue'
import ModelTable from '../components/ModelTable.vue'

const DUPLEX_DEMO_URL = '/samples/Duplex.xkt'

const router = useRouter()
const authStore = useAuthStore()

// --- State ---
const activeTab = ref<'models' | 'viewer'>('models')
const models = ref<IfcModelDto[]>([])
const isLoading = ref(false)
const error = ref<string | null>(null)
const selectedViewerModelId = ref<string | null>(null)

const canvasRef = ref<HTMLCanvasElement | null>(null)
const viewerLoading = ref(false)
const viewerError = ref<string | null>(null)
const viewerModelName = ref('Duplex (Demo)')
const selectedEntityId = ref<string | null>(null)
const selectedAttributes = ref<Record<string, string>>({})

// eslint-disable-next-line @typescript-eslint/no-explicit-any
let viewer: any = null
let viewerInitialized = false

// --- Composables ---
const { toasts, showToast, removeToast, clearAll } = useToasts()
const { hasActiveJobs, fetchModels: fetch, startPolling, stopPolling } = useModelPolling(models)
const { isDeleting, isRetrying, deleteModel, retryConversion } = useModelOperations(models, showToast)

async function fetchModels() {
  try {
    error.value = null
    await fetch()
  } catch {
    error.value = 'Modelle konnten nicht geladen werden.'
  }
}

function clearSelection() {
  if (!viewer || !selectedEntityId.value) {
    selectedEntityId.value = null
    selectedAttributes.value = {}
    return
  }
  const oldEntity = viewer.scene?.objects?.[selectedEntityId.value]
  if (oldEntity) oldEntity.highlighted = false
  selectedEntityId.value = null
  selectedAttributes.value = {}
}

function setSelectedEntity(entity: any) {
  const entityId = String(entity.id)

  if (selectedEntityId.value) {
    const oldEntity = viewer.scene?.objects?.[selectedEntityId.value]
    if (oldEntity) {
      oldEntity.highlighted = false
    }
  }

  entity.highlighted = true
  selectedEntityId.value = entityId

  const metaObject = viewer.metaScene?.metaObjects?.[entityId]
  const attributes: Record<string, string> = {
    'GlobalId': entityId,
    'IFC-Typ': metaObject?.type ?? '-',
    'Name': metaObject?.name ?? '-',
  }

  const parentName = metaObject?.parent?.name
  if (parentName) {
    attributes.Parent = String(parentName)
  }

  const propertySets = metaObject?.propertySets ?? []
  for (const propertySet of propertySets) {
    for (const property of propertySet.properties ?? []) {
      const key = property.name ? String(property.name) : 'Property'
      attributes[key] = property.value != null ? String(property.value) : '-'
    }
  }

  selectedAttributes.value = attributes
}

async function initViewer(src?: string, modelName?: string) {
  if (!canvasRef.value) return

  viewerLoading.value = true
  viewerError.value = null
  viewerModelName.value = modelName ?? 'Duplex (Demo)'
  clearSelection()

  try {
    // Destroy previous viewer if exists
    if (viewer) {
      viewer.destroy()
      viewer = null
      viewerInitialized = false
    }

    const { Viewer, XKTLoaderPlugin } = await import('@xeokit/xeokit-sdk')

    viewer = new Viewer({
      canvasElement: canvasRef.value,
      transparent: true,
    })

    viewer.camera.eye = [-3.93, 2.85, 27.01]
    viewer.camera.look = [4.40, 3.72, 8.89]
    viewer.camera.up = [-0.01, 0.99, 0.03]

    const xktLoader = new XKTLoaderPlugin(viewer)

    const loadUrl = src ?? DUPLEX_DEMO_URL

    const sceneModel = xktLoader.load({
      id: 'model',
      src: loadUrl,
      edges: true,
    })

    viewer.scene.input.on('mouseclicked', (coords: number[]) => {
      const pickResult = viewer.scene.pick({ canvasPos: coords })
      if (pickResult?.entity) {
        setSelectedEntity(pickResult.entity)
      }
    })

    sceneModel.on('loaded', () => {
      viewer.cameraFlight.flyTo(sceneModel)
      viewerLoading.value = false
      viewerInitialized = true
    })

    sceneModel.on('error', () => {
      if (src) {
        // Fallback to demo on error
        viewerModelName.value = 'Duplex (Demo)'
        xktLoader.load({ id: 'demo-fallback', src: DUPLEX_DEMO_URL, edges: true })
      }
      viewerLoading.value = false
    })
  } catch {
    viewerError.value = 'xeokit-Viewer konnte nicht geladen werden.'
    viewerLoading.value = false
  }
}

function openModelInViewer(model: IfcModelDto) {
  selectedViewerModelId.value = model.id
  if (!model.xktOutputUrl) return
  activeTab.value = 'viewer'
  nextTick(() => initViewer(model.xktOutputUrl!, model.fileName))
}

async function handleDeleteModel(model: IfcModelDto) {
  await deleteModel(model)
  if (selectedViewerModelId.value === model.id) {
    selectedViewerModelId.value = null
    activeTab.value = 'models'
    nextTick(() => initViewer())
  }
}

watch(activeTab, (tab) => {
  if (tab === 'viewer' && !viewerInitialized) {
    nextTick(() => initViewer())
  }
})

onMounted(async () => {
  isLoading.value = true
  await fetchModels()
  isLoading.value = false
  startPolling()
})

onUnmounted(() => {
  stopPolling()
  clearAll()
  clearSelection()
  if (viewer) {
    viewer.destroy()
    viewer = null
  }
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
          v-if="activeTab === 'models'"
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

    <!-- Tabs -->
    <div class="flex border-b border-gray-200 dark:border-gray-700 mb-6">
      <button
        class="flex items-center gap-2 px-4 py-2.5 text-sm font-medium border-b-2 transition-colors -mb-px min-h-[44px]"
        :class="
          activeTab === 'models'
            ? 'border-blue-600 text-blue-600'
            : 'border-transparent text-gray-500 hover:text-gray-700 dark:hover:text-gray-300'
        "
        @click="activeTab = 'models'"
      >
        <List class="w-4 h-4" />
        Modelle
      </button>
      <button
        class="flex items-center gap-2 px-4 py-2.5 text-sm font-medium border-b-2 transition-colors -mb-px min-h-[44px]"
        :class="
          activeTab === 'viewer'
            ? 'border-blue-600 text-blue-600'
            : 'border-transparent text-gray-500 hover:text-gray-700 dark:hover:text-gray-300'
        "
        @click="activeTab = 'viewer'"
      >
        <Box class="w-4 h-4" />
        3D-Viewer
      </button>
    </div>

    <!-- MODELS TAB -->
    <div v-show="activeTab === 'models'">
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
          @view="openModelInViewer"
          @retry="retryConversion"
          @delete="handleDeleteModel"
        />
      </div>
    </div>

    <!-- 3D VIEWER TAB -->
    <div v-show="activeTab === 'viewer'">
      <div v-if="viewerError" class="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-xl p-6 text-center">
        <p class="text-red-700 dark:text-red-400">{{ viewerError }}</p>
      </div>
      <div v-else class="space-y-4">
        <div class="text-sm text-gray-600 dark:text-gray-400">
          Modell: <span class="font-semibold">{{ viewerModelName }}</span>
        </div>
        <div class="relative bg-gray-100 dark:bg-gray-900 rounded-xl overflow-hidden" style="height: 600px">
          <canvas
            ref="canvasRef"
            class="w-full h-full"
          ></canvas>
          <div v-if="viewerLoading" class="absolute inset-0 flex items-center justify-center bg-black/20">
            <div class="animate-spin h-8 w-8 border-4 border-white border-t-transparent rounded-full"></div>
          </div>
        </div>
        <div v-if="Object.keys(selectedAttributes).length > 0" class="bg-white dark:bg-gray-800 rounded-xl p-4">
          <h3 class="font-semibold mb-3">Entity Properties</h3>
          <div class="grid grid-cols-2 gap-3 text-sm">
            <div v-for="(value, key) in selectedAttributes" :key="key">
              <span class="text-gray-500">{{ key }}:</span>
              <span class="font-medium">{{ value }}</span>
            </div>
          </div>
          <button
            class="mt-4 text-sm text-blue-600 hover:underline"
            @click="clearSelection"
          >
            Clear Selection
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
