<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue'

const props = withDefaults(defineProps<{
  modelSrc?: string | null
  fallbackSrc?: string
  canvasHeightClass?: string
}>(), {
  modelSrc: null,
  fallbackSrc: '/samples/Duplex.xkt',
  canvasHeightClass: 'h-[60vh]',
})

const canvasRef = ref<HTMLCanvasElement | null>(null)
const viewerLoading = ref(false)
const viewerError = ref<string | null>(null)
const selectedEntityId = ref<string | null>(null)
const selectedAttributes = ref<Record<string, string>>({})

interface PickedPoint {
  id: number
  x: number
  y: number
  z: number
  entityId: string | null
  pickedAt: string
}

const pickedPoints = ref<PickedPoint[]>([])
let pickedPointId = 0
const pointPickingMode = ref(false)
const csvInputRef = ref<HTMLInputElement | null>(null)
const pointRadius = ref(7)
const pointTransparency = ref(0.45)

// eslint-disable-next-line @typescript-eslint/no-explicit-any
let viewer: any = null
// eslint-disable-next-line @typescript-eslint/no-explicit-any
let xktLoader: any = null
// eslint-disable-next-line @typescript-eslint/no-explicit-any
let MeshCtor: any = null
// eslint-disable-next-line @typescript-eslint/no-explicit-any
let ReadableGeometryCtor: any = null
// eslint-disable-next-line @typescript-eslint/no-explicit-any
let PointsMaterialCtor: any = null
// eslint-disable-next-line @typescript-eslint/no-explicit-any
let pointCloudMesh: any = null
// eslint-disable-next-line @typescript-eslint/no-explicit-any
let pointCloudGeometry: any = null
// eslint-disable-next-line @typescript-eslint/no-explicit-any
let pointCloudMaterial: any = null

function destroyPointCloud() {
  pointCloudMesh?.destroy?.()
  pointCloudMesh = null
  pointCloudGeometry?.destroy?.()
  pointCloudGeometry = null
  pointCloudMaterial?.destroy?.()
  pointCloudMaterial = null
}

function renderPointCloud() {
  if (!viewer || !ReadableGeometryCtor || !PointsMaterialCtor || !MeshCtor) {
    return
  }

  destroyPointCloud()

  if (pickedPoints.value.length === 0) {
    return
  }

  const positions: number[] = []
  const colors: number[] = []

  for (const point of pickedPoints.value) {
    positions.push(point.x, point.y, point.z)
    colors.push(1.0, 0.2, 0.2)
  }

  pointCloudGeometry = new ReadableGeometryCtor(viewer.scene, {
    primitive: 'points',
    positions,
    colors,
  })

  pointCloudMaterial = new PointsMaterialCtor(viewer.scene, {
    pointSize: Math.max(2, pointRadius.value * 2),
    roundPoints: true,
    perspectivePoints: true,
    minPerspectivePointSize: Math.max(1, pointRadius.value),
    maxPerspectivePointSize: Math.max(2, pointRadius.value * 4),
  })

  pointCloudMesh = new MeshCtor(viewer.scene, {
    id: `picked-point-cloud-${Date.now()}`,
    geometry: pointCloudGeometry,
    material: pointCloudMaterial,
    visible: true,
    layer: 2,
    pickable: false,
    collidable: false,
    clippable: false,
    colorize: [1.0, 0.2, 0.2],
    opacity: 1 - pointTransparency.value,
  })

  if ('pointsMaterial' in pointCloudMesh) {
    pointCloudMesh.pointsMaterial = pointCloudMaterial
  }
}

function clearPickedPoints() {
  pickedPoints.value = []
  renderPointCloud()
}

function setPointPickingMode(enabled: boolean) {
  pointPickingMode.value = enabled
  if (viewer?.cameraControl) {
    viewer.cameraControl.active = !enabled
  }

  if (enabled) {
    clearSelection()
  }
}

function handleGlobalKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && pointPickingMode.value) {
    setPointPickingMode(false)
  }
}

function addPickedPoint(pickResult: any) {
  const worldPos = pickResult?.worldPos
  if (!worldPos || worldPos.length < 3) {
    return
  }

  pickedPoints.value.push({
    id: ++pickedPointId,
    x: Number(worldPos[0]),
    y: Number(worldPos[1]),
    z: Number(worldPos[2]),
    entityId: pickResult?.entity ? String(pickResult.entity.id) : null,
    pickedAt: new Date().toISOString(),
  })

  renderPointCloud()
}

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

function toCsvValue(value: string): string {
  return `"${value.replace(/"/g, '""')}"`
}

function downloadPickedPointsCsv() {
  if (pickedPoints.value.length === 0) {
    return
  }

  const headers = ['index', 'x', 'y', 'z', 'entityId', 'pickedAt']
  const rows = pickedPoints.value.map((point, index) => [
    String(index + 1),
    point.x.toFixed(6),
    point.y.toFixed(6),
    point.z.toFixed(6),
    point.entityId ?? '',
    point.pickedAt,
  ])

  const csv = [
    headers.join(','),
    ...rows.map((row) => row.map(toCsvValue).join(',')),
  ].join('\n')

  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const url = window.URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `picked-points-${new Date().toISOString().replace(/[:.]/g, '-')}.csv`
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  window.URL.revokeObjectURL(url)
}

function parseCsvRow(line: string): string[] {
  const result: string[] = []
  let current = ''
  let inQuotes = false

  for (let i = 0; i < line.length; i++) {
    const char = line[i]
    const next = line[i + 1]

    if (char === '"') {
      if (inQuotes && next === '"') {
        current += '"'
        i++
      } else {
        inQuotes = !inQuotes
      }
      continue
    }

    if (char === ',' && !inQuotes) {
      result.push(current.trim())
      current = ''
      continue
    }

    current += char
  }

  result.push(current.trim())
  return result
}

function parsePointsFromCsv(content: string): Array<{ x: number; y: number; z: number; entityId: string | null; pickedAt: string }> {
  const lines = content
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0)

  if (lines.length === 0) {
    return []
  }

  const firstLine = lines[0] ?? ''
  const firstRow = parseCsvRow(firstLine).map((value) => value.toLowerCase())
  const hasHeader = firstRow.includes('x') && firstRow.includes('y') && firstRow.includes('z')

  const xIndex = hasHeader ? firstRow.indexOf('x') : 0
  const yIndex = hasHeader ? firstRow.indexOf('y') : 1
  const zIndex = hasHeader ? firstRow.indexOf('z') : 2
  const entityIndex = hasHeader ? firstRow.indexOf('entityid') : -1
  const pickedAtIndex = hasHeader ? firstRow.indexOf('pickedat') : -1

  const points: Array<{ x: number; y: number; z: number; entityId: string | null; pickedAt: string }> = []

  for (const line of lines.slice(hasHeader ? 1 : 0)) {
    const row = parseCsvRow(line)
    const x = Number(row[xIndex])
    const y = Number(row[yIndex])
    const z = Number(row[zIndex])

    if (!Number.isFinite(x) || !Number.isFinite(y) || !Number.isFinite(z)) {
      continue
    }

    points.push({
      x,
      y,
      z,
      entityId: entityIndex >= 0 ? (row[entityIndex] || null) : null,
      pickedAt: pickedAtIndex >= 0 && row[pickedAtIndex]
        ? row[pickedAtIndex]
        : new Date().toISOString(),
    })
  }

  return points
}

function openCsvPicker() {
  csvInputRef.value?.click()
}

async function importPointsFromCsv(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) {
    return
  }

  try {
    const content = await file.text()
    const importedPoints = parsePointsFromCsv(content)
    if (importedPoints.length === 0) {
      window.alert('Keine gueltigen Punkte im CSV gefunden.')
      return
    }

    for (const point of importedPoints) {
      pickedPoints.value.push({
        id: ++pickedPointId,
        x: point.x,
        y: point.y,
        z: point.z,
        entityId: point.entityId,
        pickedAt: point.pickedAt,
      })
    }

    renderPointCloud()
  } finally {
    input.value = ''
  }
}

async function initializeViewer() {
  if (!canvasRef.value) {
    return
  }

  viewerLoading.value = true
  viewerError.value = null
  clearSelection()
  clearPickedPoints()
  setPointPickingMode(false)
  destroyPointCloud()

  try {
    if (viewer) {
      viewer.destroy()
      viewer = null
    }

    const { Viewer, XKTLoaderPlugin, Mesh, ReadableGeometry, PointsMaterial } = await import('@xeokit/xeokit-sdk')

    MeshCtor = Mesh
    ReadableGeometryCtor = ReadableGeometry
    PointsMaterialCtor = PointsMaterial

    viewer = new Viewer({
      canvasElement: canvasRef.value,
      transparent: true,
    })

    viewer.camera.eye = [-3.93, 2.85, 27.01]
    viewer.camera.look = [4.40, 3.72, 8.89]
    viewer.camera.up = [-0.01, 0.99, 0.03]

    xktLoader = new XKTLoaderPlugin(viewer)

    const loadUrl = props.modelSrc ?? props.fallbackSrc

    const sceneModel = xktLoader.load({
      id: 'model',
      src: loadUrl,
      edges: true,
    })

    viewer.scene.input.on('mouseclicked', (coords: number[]) => {
      const pickResult = viewer.scene.pick({
        canvasPos: coords,
        pickSurface: pointPickingMode.value,
      })

      if (pointPickingMode.value) {
        addPickedPoint(pickResult)
      } else {
        if (pickResult?.entity) {
          setSelectedEntity(pickResult.entity)
        } else {
          clearSelection()
        }
      }
    })

    sceneModel.on('loaded', () => {
      viewer.cameraFlight.flyTo(sceneModel)
      viewerLoading.value = false
      renderPointCloud()
    })

    sceneModel.on('error', () => {
      if (props.modelSrc) {
        xktLoader.load({ id: 'demo-fallback', src: props.fallbackSrc, edges: true })
      }
      viewerLoading.value = false
    })
  } catch {
    viewerError.value = 'xeokit-Viewer konnte nicht geladen werden.'
    viewerLoading.value = false
  }
}

onMounted(() => {
  window.addEventListener('keydown', handleGlobalKeydown)
  initializeViewer()
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleGlobalKeydown)
  destroyPointCloud()
  clearSelection()
  if (viewer) {
    viewer.destroy()
    viewer = null
  }
})

watch(() => props.modelSrc, () => {
  initializeViewer()
})

watch(pointRadius, () => {
  renderPointCloud()
})

watch(pointTransparency, () => {
  renderPointCloud()
})
</script>

<template>
  <div class="relative bg-gray-100 dark:bg-gray-900">
    <div v-if="viewerLoading" :class="[canvasHeightClass, 'flex items-center justify-center bg-gray-100 dark:bg-gray-900']">
      <div class="text-center">
        <div class="animate-spin h-10 w-10 border-4 border-blue-600 border-t-transparent rounded-full mx-auto mb-4"></div>
        <p class="text-gray-500 dark:text-gray-400">Modell wird geladen...</p>
      </div>
    </div>

    <div v-if="viewerError" :class="[canvasHeightClass, 'flex items-center justify-center bg-gray-100 dark:bg-gray-900']">
      <p class="text-red-700 dark:text-red-400">{{ viewerError }}</p>
    </div>

    <canvas ref="canvasRef" class="w-full block" :class="viewerLoading || viewerError ? 'h-0' : canvasHeightClass"></canvas>

    <div
      class="absolute top-4 left-4 w-80 max-h-[80%] overflow-y-auto bg-white/95 dark:bg-gray-800/95 rounded-xl shadow-xl p-4 z-10 border border-gray-200 dark:border-gray-700"
    >
      <div class="flex items-center justify-between mb-3 gap-2">
        <h3 class="text-sm font-semibold text-gray-900 dark:text-white">Point Picker</h3>
        <div class="flex items-center gap-2">
          <button
            class="text-xs px-2 py-1 rounded border transition-colors"
            :class="pointPickingMode
              ? 'border-blue-600 bg-blue-600 text-white hover:bg-blue-700'
              : 'border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700'"
            @click="setPointPickingMode(!pointPickingMode)"
          >
            {{ pointPickingMode ? 'Picking aktiv' : 'Picking starten' }}
          </button>
          <button
            class="text-xs px-2 py-1 rounded border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700"
            @click="openCsvPicker"
          >
            CSV laden
          </button>
          <button
            class="text-xs px-2 py-1 rounded border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 disabled:opacity-50"
            :disabled="pickedPoints.length === 0"
            @click="downloadPickedPointsCsv"
          >
            CSV
          </button>
          <button
            class="text-xs px-2 py-1 rounded border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 disabled:opacity-50"
            :disabled="pickedPoints.length === 0"
            @click="clearPickedPoints"
          >
            Leeren
          </button>
        </div>
      </div>
      <input
        ref="csvInputRef"
        type="file"
        accept=".csv,text/csv"
        class="hidden"
        @change="importPointsFromCsv"
      >
      <div class="grid grid-cols-2 gap-2 mb-3">
        <label class="text-xs text-gray-600 dark:text-gray-300">
          Radius
          <input v-model.number="pointRadius" type="range" min="2" max="20" step="1" class="w-full">
        </label>
        <label class="text-xs text-gray-600 dark:text-gray-300">
          Transparenz
          <input v-model.number="pointTransparency" type="range" min="0" max="0.9" step="0.05" class="w-full">
        </label>
      </div>
      <p class="text-xs text-gray-500 dark:text-gray-400 mb-3">
        {{ pointPickingMode
          ? 'Picking-Modus aktiv: Kamera-Navigation ist deaktiviert, Klick speichert Punkte.'
          : 'Picking-Modus starten oder CSV laden, um 3D-Punkte darzustellen.' }}
      </p>
      <div v-if="pickedPoints.length === 0" class="text-xs text-gray-500 dark:text-gray-400">
        Noch keine Punkte gespeichert.
      </div>
      <ol v-else class="space-y-2 text-xs">
        <li
          v-for="(point, index) in pickedPoints"
          :key="point.id"
          class="rounded-lg border border-gray-200 dark:border-gray-700 p-2"
        >
          <p class="font-medium text-gray-900 dark:text-white">#{{ index + 1 }}</p>
          <p class="text-gray-700 dark:text-gray-300">x: {{ point.x.toFixed(3) }}</p>
          <p class="text-gray-700 dark:text-gray-300">y: {{ point.y.toFixed(3) }}</p>
          <p class="text-gray-700 dark:text-gray-300">z: {{ point.z.toFixed(3) }}</p>
        </li>
      </ol>
    </div>

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
</template>
