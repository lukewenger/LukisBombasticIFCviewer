<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowLeft, AlertCircle, Box } from 'lucide-vue-next'
import { modelsApi } from '../api/models'
import type { IfcModelDto } from '../types'
import { ModelStatus } from '../types/models'

const DUPLEX_DEMO_URL = '/samples/Duplex.xkt'

const route = useRoute()
const router = useRouter()

const modelId = route.params.id as string
const model = ref<IfcModelDto | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)
const usingDemo = ref(false)

const canvasRef = ref<HTMLCanvasElement | null>(null)
const selectedEntityId = ref<string | null>(null)
const selectedAttributes = ref<Record<string, string>>({})

// eslint-disable-next-line @typescript-eslint/no-explicit-any
let viewer: any = null

function clearSelection() {
  if (!viewer || !selectedEntityId.value) {
    selectedEntityId.value = null
    selectedAttributes.value = {}
    return
  }

  const oldEntity = viewer.scene?.objects?.[selectedEntityId.value]
  if (oldEntity) {
    oldEntity.highlighted = false
  }

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

async function initViewer() {
  if (!canvasRef.value) return
  clearSelection()

  try {
    // Dynamic import to avoid SSR/build issues with xeokit
    const { Viewer, XKTLoaderPlugin } = await import('@xeokit/xeokit-sdk')

    viewer = new Viewer({
      canvasElement: canvasRef.value,
      transparent: true,
    })

    // Configure camera
    viewer.camera.eye = [-3.93, 2.85, 27.01]
    viewer.camera.look = [4.40, 3.72, 8.89]
    viewer.camera.up = [-0.01, 0.99, 0.03]

    const xktLoader = new XKTLoaderPlugin(viewer)

    // Determine source URL
    let src: string
    if (model.value && model.value.status === ModelStatus.Ready) {
      src = modelsApi.getModelOutputUrl(modelId)
      usingDemo.value = false
    } else {
      src = DUPLEX_DEMO_URL
      usingDemo.value = true
    }

    const sceneModel = xktLoader.load({
      id: 'model',
      src,
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
    })

    sceneModel.on('error', () => {
      // If real model fails, fall back to demo
      if (!usingDemo.value) {
        usingDemo.value = true
        xktLoader.load({
          id: 'demo-fallback',
          src: DUPLEX_DEMO_URL,
          edges: true,
        })
      }
    })
  } catch {
    error.value = 'xeokit-Viewer konnte nicht geladen werden.'
  }
}

onMounted(async () => {
  isLoading.value = true
  try {
    model.value = await modelsApi.getModel(modelId)
  } catch {
    // Model not found — we'll load the demo anyway
  }
  isLoading.value = false
  await initViewer()
})

onUnmounted(() => {
  clearSelection()
  if (viewer) {
    viewer.destroy()
    viewer = null
  }
})
</script>

<template>
  <div class="flex flex-col h-[calc(100vh-4rem)]">
    <!-- Toolbar -->
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
            {{ model?.fileName ?? '3D-Viewer' }}
          </h1>
          <p v-if="usingDemo" class="text-xs text-yellow-600 dark:text-yellow-400">
            Demo-Modell (Duplex) — Konvertierung ausstehend
          </p>
        </div>
      </div>
    </div>

    <!-- Loading overlay -->
    <div v-if="isLoading" class="flex-1 flex items-center justify-center bg-gray-100 dark:bg-gray-900">
      <div class="text-center">
        <div class="animate-spin h-10 w-10 border-4 border-blue-600 border-t-transparent rounded-full mx-auto mb-4"></div>
        <p class="text-gray-500 dark:text-gray-400">Modell wird geladen...</p>
      </div>
    </div>

    <!-- Error -->
    <div v-else-if="error" class="flex-1 flex items-center justify-center bg-gray-100 dark:bg-gray-900">
      <div class="text-center">
        <AlertCircle class="w-12 h-12 text-red-500 mx-auto mb-3" />
        <p class="text-red-700 dark:text-red-400">{{ error }}</p>
      </div>
    </div>

    <!-- Canvas -->
    <div v-show="!isLoading && !error" class="flex-1 relative bg-gray-100 dark:bg-gray-900">
      <canvas ref="canvasRef" class="w-full h-full block"></canvas>

      <div
        v-if="selectedEntityId"
        class="absolute top-4 right-4 w-80 max-h-[80%] overflow-y-auto bg-white/95 dark:bg-gray-800/95 rounded-xl shadow-xl p-4 z-10 border border-gray-200 dark:border-gray-700"
      >
        <div class="flex items-center justify-between mb-3">
          <h3 class="text-sm font-semibold text-gray-900 dark:text-white">IFC-Attribute</h3>
          <button class="text-gray-500 hover:text-gray-700 dark:hover:text-gray-300" @click="clearSelection">X</button>
        </div>
        <dl class="space-y-2 text-xs">
          <div v-for="(value, key) in selectedAttributes" :key="key">
            <dt class="text-gray-500 dark:text-gray-400">{{ key }}</dt>
            <dd class="text-gray-900 dark:text-white font-medium break-all">{{ value }}</dd>
          </div>
        </dl>
      </div>
    </div>
  </div>
</template>
