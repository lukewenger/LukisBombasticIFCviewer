import { ref } from 'vue'
import type { CsvPoint } from './usePointCsv'

export interface PickedPoint {
  id: number
  x: number
  y: number
  z: number
  entityId: string | null
  pickedAt: string
}

export function usePointPicker() {
  const pickedPoints = ref<PickedPoint[]>([])
  const pointPickingMode = ref(false)
  const pointRadius = ref(7)
  const pointTransparency = ref(0.45)

  let pickedPointId = 0

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let viewerRef: any = null
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

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  function setViewer(viewer: any) {
    viewerRef = viewer
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  function setPointCtors(meshCtor: any, geometryCtor: any, materialCtor: any) {
    MeshCtor = meshCtor
    ReadableGeometryCtor = geometryCtor
    PointsMaterialCtor = materialCtor
  }

  function destroyPointCloud() {
    pointCloudMesh?.destroy?.()
    pointCloudMesh = null
    pointCloudGeometry?.destroy?.()
    pointCloudGeometry = null
    pointCloudMaterial?.destroy?.()
    pointCloudMaterial = null
  }

  function renderPointCloud() {
    if (!viewerRef || !ReadableGeometryCtor || !PointsMaterialCtor || !MeshCtor) return

    destroyPointCloud()
    if (pickedPoints.value.length === 0) return

    const positions: number[] = []
    const colors: number[] = []

    for (const point of pickedPoints.value) {
      positions.push(point.x, point.y, point.z)
      colors.push(1.0, 0.2, 0.2)
    }

    pointCloudGeometry = new ReadableGeometryCtor(viewerRef.scene, {
      primitive: 'points',
      positions,
      colors,
    })

    pointCloudMaterial = new PointsMaterialCtor(viewerRef.scene, {
      pointSize: Math.max(2, pointRadius.value * 2),
      roundPoints: true,
      perspectivePoints: true,
      minPerspectivePointSize: Math.max(1, pointRadius.value),
      maxPerspectivePointSize: Math.max(2, pointRadius.value * 4),
    })

    pointCloudMesh = new MeshCtor(viewerRef.scene, {
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
    if (viewerRef?.cameraControl) {
      viewerRef.cameraControl.active = !enabled
    }
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  function addPickedPoint(pickResult: any) {
    const worldPos = pickResult?.worldPos
    if (!worldPos || worldPos.length < 3) return

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

  function appendImportedPoints(points: CsvPoint[]) {
    for (const point of points) {
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
  }

  return {
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
  }
}
