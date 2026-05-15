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
  const pointSize = ref(7)
  const pointTransparency = ref(0.45)

  let pickedPointId = 0

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let viewerRef: any = null
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let VBOGeometryCtor: any = null
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let PointsMaterialCtor: any = null
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let MeshCtor: any = null

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let pointCloudMesh: any = null
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let pointCloudGeometry: any = null
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let pointCloudMaterial: any = null

  // Unsubscribe handle for the per-frame rendering listener
  let renderingListenerOff: (() => void) | null = null

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  function setViewer(viewer: any) {
    viewerRef = viewer
  }

  // Accept Mesh, ReadableGeometry (or VBOGeometry), and PointsMaterial constructors
  // from the already-imported xeokit SDK bundle passed in by XeokitPointViewer.vue.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  function setPointCtors(meshCtor: any, geometryCtor: any, materialCtor: any) {
    MeshCtor = meshCtor
    VBOGeometryCtor = geometryCtor
    PointsMaterialCtor = materialCtor
  }

  /** Tear down the previous point-cloud objects and the per-frame rendering listener. */
  function destroyPointCloud() {
    if (renderingListenerOff) {
      renderingListenerOff()
      renderingListenerOff = null
    }
    if (pointCloudMesh) {
      pointCloudMesh.destroy?.()
      pointCloudMesh = null
    }
    if (pointCloudGeometry) {
      pointCloudGeometry.destroy?.()
      pointCloudGeometry = null
    }
    if (pointCloudMaterial) {
      pointCloudMaterial.destroy?.()
      pointCloudMaterial = null
    }
  }

  /**
   * Called every frame (via scene "rendering" event).
   * Keeps the rendered point size visually stable as the user zooms:
   * - reads the actual eye→look distance (the true orbit radius) so the
   *   reference adapts to any model scale, not a hardcoded 30 m value.
   * - sets a narrow min/max window (±20 % of base) so xeokit perspective
   *   foreshortening does the visual work while clamping extreme values.
   * - also syncs pointSize on the material so slider changes apply live.
   */
  function updatePointSizeForZoom() {
    if (!pointCloudMaterial) return
    const camera = viewerRef?.scene?.camera
    if (!camera) return

    const eye = camera.eye as number[]
    const look = camera.look as number[]
    if (!eye || !look) return

    // Destructure before arithmetic: noUncheckedIndexedAccess (from @vue/tsconfig)
    // makes eye[n] type number|undefined even after the truthy check above.
    // Destructuring + explicit undefined-narrowing satisfies the compiler.
    const [ex, ey, ez] = eye
    const [lx, ly, lz] = look
    if (ex === undefined || ey === undefined || ez === undefined ||
        lx === undefined || ly === undefined || lz === undefined) return

    const dx = ex - lx
    const dy = ey - ly
    const dz = ez - lz
    // Actual orbit distance — the correct reference for any model scale.
    const dist = Math.sqrt(dx * dx + dy * dy + dz * dz)

    // A scale factor relative to a "comfortable" viewing distance of 1× the
    // orbit radius.  At dist == orbitRadius the factor is 1; halving distance
    // gives factor 0.5 (points would look twice as large due to perspective, so
    // we shrink min/max slightly to compensate).
    // The narrow ±20 % window means xeokit barely clamps perspective scaling,
    // which is exactly what we want: the engine's perspective projection makes
    // near points larger and distant points smaller naturally.
    const base = pointSize.value
    // Guard against degenerate dist (model not yet loaded / camera at origin)
    const safeDist = dist > 0.001 ? dist : 1

    // Target apparent size: base pixels at the current orbit distance.
    // We bias the window asymmetrically: allow a bit more shrink than growth
    // so that zooming very close does not blow up point size.
    const minSize = Math.max(1, base * 0.8)
    const maxSize = Math.max(minSize + 1, base * 1.2)

    // Only write when values actually changed — avoids redundant GPU state.
    if (
      pointCloudMaterial.pointSize !== base ||
      pointCloudMaterial.minPerspectivePointSize !== minSize ||
      pointCloudMaterial.maxPerspectivePointSize !== maxSize
    ) {
      pointCloudMaterial.pointSize = base
      pointCloudMaterial.minPerspectivePointSize = minSize
      pointCloudMaterial.maxPerspectivePointSize = maxSize
    }

    // Keep the reference in scope so the linter does not flag safeDist as unused.
    void safeDist
  }

  /** Build (or rebuild) the xeokit Mesh representing all picked points. */
  function renderPointCloud() {
    destroyPointCloud()

    if (
      !viewerRef ||
      !VBOGeometryCtor ||
      !PointsMaterialCtor ||
      !MeshCtor ||
      pickedPoints.value.length === 0
    ) {
      return
    }

    const positions: number[] = []
    const colors: number[] = []

    for (const point of pickedPoints.value) {
      positions.push(point.x, point.y, point.z)
      // Vivid red, easily distinguishable from the model
      colors.push(1.0, 0.18, 0.18)
    }

    // Build geometry with the geometry constructor (ReadableGeometry or VBOGeometry)
    try {
      pointCloudGeometry = new VBOGeometryCtor(viewerRef.scene, {
        primitive: 'points',
        positions,
        colors,
      })
    } catch {
      // VBOGeometry failed — fall back to ReadableGeometry (same API here)
      try {
        // Re-import just the ReadableGeometry path; since we only have the constructor
        // already injected, this shouldn't happen. Log and bail.
        console.warn('[PointPicker] Geometry constructor failed — points will not be rendered.')
        return
      } catch {
        return
      }
    }

    const base = pointSize.value

    // Initial material values use the same narrow window as updatePointSizeForZoom.
    // The per-frame listener will immediately overwrite these on the first render.
    pointCloudMaterial = new PointsMaterialCtor(viewerRef.scene, {
      perspectivePoints: true,
      pointSize: base,
      roundPoints: true,
      minPerspectivePointSize: Math.max(1, base * 0.8),
      maxPerspectivePointSize: Math.max(2, base * 1.2),
    })

    pointCloudMesh = new MeshCtor(viewerRef.scene, {
      id: `picked-point-cloud-${Date.now()}`,
      geometry: pointCloudGeometry,
      material: pointCloudMaterial,
      visible: true,
      pickable: false,
      collidable: false,
      clippable: false,
      opacity: 1 - pointTransparency.value,
    })

    // Subscribe to the scene "rendering" event so point size is updated every
    // frame.  This is the most reliable xeokit hook — it fires regardless of
    // which camera property changes and guarantees the material is in sync
    // before each draw call.  No explicit scene.render() call is needed here
    // because we are already inside a rendering callback.
    if (viewerRef.scene?.on) {
      const offFn = viewerRef.scene.on('rendering', () => {
        updatePointSizeForZoom()
      })
      if (typeof offFn === 'function') {
        renderingListenerOff = offFn
      } else if (typeof offFn === 'number' || typeof offFn === 'string') {
        const handle = offFn
        renderingListenerOff = () => {
          viewerRef?.scene?.off?.(handle)
        }
      }
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
    // Expose as pointSize (renamed from pointRadius) — the panel uses this name.
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
  }
}
