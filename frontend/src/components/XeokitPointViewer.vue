<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { useXeokitViewer } from '../composables/useXeokitViewer'
import { usePointPicker } from '../composables/usePointPicker'
import { downloadPickedPointsCsv, importPointsFromCsv } from '../composables/usePointCsv'
import PointPickerPanel from './PointPickerPanel.vue'
import EntityAttributesPanel from './EntityAttributesPanel.vue'

const props = withDefaults(
  defineProps<{
    modelSrc?: string | null
    fallbackSrc?: string
    canvasHeightClass?: string
  }>(),
  {
    modelSrc: null,
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
  clearSelection,
  setSelectedEntity,
  initializeViewer,
  destroyViewer,
} = useXeokitViewer()

const {
  pickedPoints,
  pointPickingMode,
  pointRadius,
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
    props.modelSrc,
    props.fallbackSrc,
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
  }
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

watch(() => props.modelSrc, () => {
  bootViewer()
})

watch(pointRadius, () => {
  renderPointCloud()
})

watch(pointTransparency, () => {
  renderPointCloud()
})
</script>

<template>
  <div ref="viewerRootRef" class="relative bg-gray-100 dark:bg-gray-900">
    <div v-if="viewerLoading" :class="[props.canvasHeightClass, 'flex items-center justify-center bg-gray-100 dark:bg-gray-900']">
      <div class="text-center">
        <div class="animate-spin h-10 w-10 border-4 border-blue-600 border-t-transparent rounded-full mx-auto mb-4"></div>
        <p class="text-gray-500 dark:text-gray-400">Modell wird geladen...</p>
      </div>
    </div>

    <div v-if="viewerError" :class="[props.canvasHeightClass, 'flex items-center justify-center bg-gray-100 dark:bg-gray-900']">
      <p class="text-red-700 dark:text-red-400">{{ viewerError }}</p>
    </div>

    <canvas ref="canvasRef" class="w-full block" :class="viewerLoading || viewerError ? 'h-0' : props.canvasHeightClass"></canvas>

    <div data-wheel-scroll-panel="true">
      <PointPickerPanel
        :point-picking-mode="pointPickingMode"
        :picked-points="pickedPoints"
        :point-radius="pointRadius"
        :point-transparency="pointTransparency"
        @toggle-picking="togglePicking"
        @open-csv="openCsvPicker"
        @export-csv="exportCsv"
        @clear-points="clearPickedPoints"
        @update:point-radius="pointRadius = $event"
        @update:point-transparency="pointTransparency = $event"
      />
    </div>

    <input
      ref="csvInputRef"
      type="file"
      accept=".csv,text/csv"
      class="hidden"
      @change="onCsvInputChange"
    >

    <div data-wheel-scroll-panel="true">
      <EntityAttributesPanel
        :selected-entity-id="selectedEntityId"
        :selected-attributes="selectedAttributes"
        @clear-selection="clearSelection"
      />
    </div>
  </div>
</template>
