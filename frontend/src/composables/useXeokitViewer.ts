import { ref, computed } from 'vue'
import api from '../api/client'

export interface LoadedModelEntry {
  id: string
  src: string
  label: string
  visible: boolean
  loading: boolean
  error: string | null
  /** Original IFC file size in bytes — used as a RAM-usage proxy */
  fileSizeBytes: number
}

/** Default memory warning threshold: 500 MB expressed in bytes */
export const DEFAULT_MEMORY_THRESHOLD_BYTES = 500 * 1024 * 1024

export function useXeokitViewer() {
  const viewerInitializing = ref(false)
  const viewerError = ref<string | null>(null)
  const selectedEntityId = ref<string | null>(null)
  const selectedAttributes = ref<Record<string, string>>({})
  const loadedModels = ref<Record<string, LoadedModelEntry>>({})

  /**
   * Returns the total fileSizeBytes of all currently loaded (non-errored) models.
   * This is used as a proxy for RAM / VRAM usage.
   */
  const totalLoadedBytes = computed(() =>
    Object.values(loadedModels.value)
      .filter(m => !m.error)
      .reduce((sum, m) => sum + m.fileSizeBytes, 0)
  )

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let viewer: any = null
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let xktLoader: any = null

  const viewerLoading = computed(
    () => viewerInitializing.value || Object.values(loadedModels.value).some(m => m.loading)
  )

  async function isModelOutputReachable(url: string): Promise<boolean> {
    try {
      // Use the authenticated Axios instance so the JWT token is attached.
      // A successful 2xx response means the file is ready; any error means it is not.
      await api.get(url, { responseType: 'arraybuffer', headers: { Range: 'bytes=0-0' } })
      return true
    } catch {
      return false
    }
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

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
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
      GlobalId: entityId,
      'IFC-Typ': metaObject?.type ?? '-',
      Name: metaObject?.name ?? '-',
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

  async function initializeViewer(
    canvasElement: HTMLCanvasElement,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    onPick: (pickResult: any) => void,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    onSdkLoaded: (sdk: { Mesh: any; ReadableGeometry: any; PointsMaterial: any }) => void
  ) {
    viewerInitializing.value = true
    viewerError.value = null
    clearSelection()
    loadedModels.value = {}

    if (viewer) {
      viewer.destroy()
      viewer = null
    }

    try {
      const sdk = await import('@xeokit/xeokit-sdk')
      const { Viewer, XKTLoaderPlugin, Mesh, ReadableGeometry, PointsMaterial } = sdk
      onSdkLoaded({ Mesh, ReadableGeometry, PointsMaterial })

      viewer = new Viewer({
        canvasElement,
        transparent: true,
      })

      viewer.camera.eye = [-3.93, 2.85, 27.01]
      viewer.camera.look = [4.40, 3.72, 8.89]
      viewer.camera.up = [-0.01, 0.99, 0.03]

      xktLoader = new XKTLoaderPlugin(viewer)

      viewer.scene.input.on('mouseclicked', (coords: number[]) => {
        const pickResult = viewer.scene.pick({
          canvasPos: coords,
          pickSurface: true,
        })
        onPick(pickResult)
      })

      viewerInitializing.value = false
      return viewer
    } catch {
      viewerError.value = 'xeokit-Viewer konnte nicht geladen werden.'
      viewerInitializing.value = false
      return null
    }
  }

  /** Core loading logic — no camera flyTo. */
  async function loadModelInternal(
    id: string,
    src: string,
    label: string,
    fallbackSrc?: string,
    fileSizeBytes = 0
  ): Promise<void> {
    if (!viewer || !xktLoader) return
    if (loadedModels.value[id]) return

    loadedModels.value[id] = { id, src, label, visible: true, loading: true, error: null, fileSizeBytes }

    let loadUrl = src
    if (src) {
      const reachable = await isModelOutputReachable(src)
      if (!reachable) {
        if (fallbackSrc) {
          loadedModels.value[id].error = 'Datei nicht erreichbar. Demo-Modell wird geladen.'
          loadUrl = fallbackSrc
        } else {
          loadedModels.value[id].error = 'Datei nicht erreichbar.'
          loadedModels.value[id].loading = false
          return
        }
      }
    }

    await new Promise<void>((resolve) => {
      const sceneModel = xktLoader.load({ id, src: loadUrl, edges: true })
      sceneModel.on('loaded', () => {
        if (loadedModels.value[id]) loadedModels.value[id]!.loading = false
        resolve()
      })
      sceneModel.on('error', () => {
        if (loadedModels.value[id]) {
          loadedModels.value[id]!.error = 'Modell konnte nicht geladen werden.'
          loadedModels.value[id]!.loading = false
        }
        resolve()
      })
    })
  }

  /** Load multiple models in parallel, then fly to the full scene. */
  async function loadModels(
    entries: { id: string; src: string; label: string; fileSizeBytes?: number }[],
    fallbackSrc?: string
  ): Promise<void> {
    await Promise.all(entries.map(e => loadModelInternal(e.id, e.src, e.label, fallbackSrc, e.fileSizeBytes ?? 0)))
    if (viewer) viewer.cameraFlight.flyTo(viewer.scene)
  }

  /** Load a single model and fly to the full scene after it finishes. */
  async function loadModel(
    id: string,
    src: string,
    label: string,
    fallbackSrc?: string,
    fileSizeBytes = 0
  ): Promise<void> {
    await loadModelInternal(id, src, label, fallbackSrc, fileSizeBytes)
    if (viewer) viewer.cameraFlight.flyTo(viewer.scene)
  }

  function unloadModel(id: string): void {
    if (!viewer) return
    const sceneModel = viewer.scene.models[id]
    if (sceneModel) sceneModel.destroy()
    delete loadedModels.value[id]
    if (selectedEntityId.value) clearSelection()
  }

  function setModelVisible(id: string, visible: boolean): void {
    if (!viewer) return
    const sceneModel = viewer.scene.models[id]
    if (sceneModel) sceneModel.visible = visible
    if (loadedModels.value[id]) loadedModels.value[id].visible = visible
  }

  function destroyViewer() {
    clearSelection()
    loadedModels.value = {}
    if (viewer) {
      viewer.destroy()
      viewer = null
    }
  }

  return {
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
  }
}
