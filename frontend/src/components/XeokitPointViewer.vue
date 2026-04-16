<script setup lang="ts">
import { ref, watch, onMounted, onUnmounted } from 'vue'
import { useXeokitViewer } from '../composables/useXeokitViewer'
import { usePointPicker } from '../composables/usePointPicker'
import { useMemoryGuard } from '../composables/useMemoryGuard'
import { downloadPickedPointsCsv, importPointsFromCsv } from '../composables/usePointCsv'
import PointPickerPanel from './PointPickerPanel.vue'
import EntityAttributesPanel from './EntityAttributesPanel.vue'
import ModelListPanel from './ModelListPanel.vue'
import MemoryWarningModal from './MemoryWarningModal.vue'
import type { IfcModelDto } from '../types'

export interface ModelEntry {
  id: string
  src: string
  label: string
  fileSizeBytes?: number
}

const props = withDefaults(
  defineProps<{
    initialModels?: ModelEntry[]
    fallbackSrc?: string
    canvasHeightClass?: string
  }>(),
  {
    initialModels: () => [],
    fallbackSrc: '/samples/Duplex.xkt',
    canvasHeightClass: 'h-[60vh]',
  }
)

const canvasRef = ref<HTMLCanvasElement | null>(null)
const csvInputRef = ref<HTMLInputElement | null>(null)
const viewerRootRef = ref<HTMLDivElement | null>(null)

function isWheelInsideRect(event: WheelEvent, rect: DOMRect): boolean {
  return event.clientX >= rect.left &&
    event.clientX <= rect.right &&
    event.clientY >= rect.top &&
    event.clientY <= rect.bottom
}

function handleViewerWheel(event: WheelEvent) {
  if (!canvasRef.value) {
    return
  }

  const target = event.target as HTMLElement | null
  if (target?.closest('[data-wheel-scroll-panel="true"]')) {
    return
  }

  const canvasRect = canvasRef.value.getBoundingClientRect()
  if (isWheelInsideRect(event, canvasRect)) {
    event.preventDefault()
  }
}

const {
  viewerLoading,
  viewerError,
  selectedEntityId,
  selectedAttributes,
  loadedModels,
  totalLoadedBytes,
  clearSelection,
  setSelectedEntity,
  initializeViewer,
  loadModel,
  loadModels,
  unloadModel,
  setModelVisible,
  destroyViewer,
} = useXeokitViewer()

const { pendingWarning, checkBeforeLoad, confirmWarning, dismissWarning } = useMemoryGuard()

const {
  pickedPoints,
  pointPickingMode,
  pointSize,
  pointTransparency,
  setViewer,
  setPointCtors,
  destroyPointCloud,
  renderPointCloud,
  clearPickedPoints,
  setPointPickingMode,
  addPickedPoint,
  appendImportedPoints,
} = usePointPicker()

function handleGlobalKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && pointPickingMode.value) {
    setPointPickingMode(false)
  }
}

function togglePicking(nextValue: boolean) {
  setPointPickingMode(nextValue)
  if (nextValue) {
    clearSelection()
  }
}

function openCsvPicker() {
  csvInputRef.value?.click()
}

async function onCsvInputChange(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  try {
    const points = await importPointsFromCsv(file)
    if (points.length === 0) {
      window.alert('Keine gueltigen Punkte im CSV gefunden.')
      return
    }
    appendImportedPoints(points)
  } finally {
    input.value = ''
  }
}

function exportCsv() {
  downloadPickedPointsCsv(pickedPoints.value)
}

async function bootViewer() {
  if (!canvasRef.value) return

  clearSelection()
  clearPickedPoints()
  setPointPickingMode(false)
  destroyPointCloud()

  const viewerInstance = await initializeViewer(
    canvasRef.value,
    (pickResult) => {
      if (pointPickingMode.value) {
        addPickedPoint(pickResult)
      } else if (pickResult?.entity) {
        setSelectedEntity(pickResult.entity)
      } else {
        clearSelection()
      }
    },
    ({ Mesh, ReadableGeometry, PointsMaterial }) => {
      setPointCtors(Mesh, ReadableGeometry, PointsMaterial)
    }
  )

  if (viewerInstance) {
    setViewer(viewerInstance)
    renderPointCloud()

    if (props.initialModels.length > 0) {
      await loadModels(props.initialModels, props.fallbackSrc)
    }
  }
}

async function handleAddModel(model: IfcModelDto) {
  if (!model.xktOutputUrl) return
  const proceed = await checkBeforeLoad(model.fileName, model.fileSizeBytes, totalLoadedBytes.value)
  if (!proceed) return
  await loadModel(model.id, model.xktOutputUrl, model.fileName, props.fallbackSrc, model.fileSizeBytes)
}

onMounted(() => {
  window.addEventListener('keydown', handleGlobalKeydown)
  viewerRootRef.value?.addEventListener('wheel', handleViewerWheel, {
    passive: false,
    capture: true,
  })
  bootViewer()
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleGlobalKeydown)
  viewerRootRef.value?.removeEventListener('wheel', handleViewerWheel, true)
  destroyPointCloud()
  destroyViewer()
})

watch(pointSize, () => {
  renderPointCloud()
})

watch(pointTransparency, () => {
  renderPointCloud()
})
</script>

<template>
  <!--
    Outer wrapper: flex-row so the canvas sits left and the right side-panel
    stack sits in a fixed-width column beside it.  The canvas fills all
    remaining width via flex-1.
  -->
  <div ref="viewerRootRef" class="flex bg-gray-100 dark:bg-gray-900">

    <!-- ─── Canvas area ──────────────────────────────────────────────── -->
    <div class="relative flex-1 min-w-0">
      <!-- Loading state -->
      <div
        v-if="viewerLoading"
        :class="[props.canvasHeightClass, 'flex items-center justify-center bg-gray-100 dark:bg-gray-900']"
      >
        <div class="text-center">
          <div class="animate-spin h-10 w-10 border-4 border-blue-600 border-t-transparent rounded-full mx-auto mb-4" />
          <p class="text-gray-500 dark:text-gray-400">Modell wird geladen...</p>
        </div>
      </div>

      <!-- Error state -->
      <div
        v-if="viewerError"
        :class="[props.canvasHeightClass, 'flex items-center justify-center bg-gray-100 dark:bg-gray-900']"
      >
        <p class="text-red-700 dark:text-red-400">{{ viewerError }}</p>
      </div>

      <!-- xeokit canvas -->
      <canvas
        ref="canvasRef"
        class="w-full block"
        :class="viewerLoading || viewerError ? 'h-0' : props.canvasHeightClass"
      />

      <!-- Model list panel — top-left overlay inside the canvas area -->
      <div v-if="!viewerLoading && !viewerError" data-wheel-scroll-panel="true">
        <ModelListPanel
          :loaded-models="loadedModels"
          :fallback-src="props.fallbackSrc"
          @toggle-visible="(id, visible) => setModelVisible(id, visible)"
          @unload="unloadModel"
          @add-model="handleAddModel"
        />
      </div>

      <!-- Entity attributes panel — top-right overlay inside the canvas area.
           It is conditionally rendered (only when an entity is selected) so it
           never clashes with the PointPickerPanel which lives outside this div. -->
      <div data-wheel-scroll-panel="true">
        <EntityAttributesPanel
          :selected-entity-id="selectedEntityId"
          :selected-attributes="selectedAttributes"
          @clear-selection="clearSelection"
        />
      </div>

      <!-- RAM overload warning modal -->
      <MemoryWarningModal
        v-if="pendingWarning"
        :warning="pendingWarning"
        @confirm="confirmWarning"
        @dismiss="dismissWarning"
      />

      <!-- Point picker floating widget — bottom-right overlay inside the canvas area -->
      <div
        v-if="!viewerLoading && !viewerError"
        class="absolute bottom-4 right-4 z-10"
      >
        <PointPickerPanel
          :point-picking-mode="pointPickingMode"
          :picked-points="pickedPoints"
          :point-size="pointSize"
          :point-transparency="pointTransparency"
          @toggle-picking="togglePicking"
          @open-csv="openCsvPicker"
          @export-csv="exportCsv"
          @clear-points="clearPickedPoints"
          @update:point-size="pointSize = $event"
          @update:point-transparency="pointTransparency = $event"
        />
      </div>
    </div>


    <!-- Hidden CSV file input -->
    <input
      ref="csvInputRef"
      type="file"
      accept=".csv,text/csv"
      class="hidden"
      @change="onCsvInputChange"
    >
  </div>
</template>
