import { ref } from 'vue'

export function useXeokitViewer() {
  const viewerLoading = ref(false)
  const viewerError = ref<string | null>(null)
  const selectedEntityId = ref<string | null>(null)
  const selectedAttributes = ref<Record<string, string>>({})

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let viewer: any = null
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let xktLoader: any = null

  async function isModelOutputReachable(url: string): Promise<boolean> {
    try {
      const headResponse = await fetch(url, { method: 'HEAD' })
      if (headResponse.ok) return true

      if (headResponse.status === 405) {
        const rangeResponse = await fetch(url, {
          method: 'GET',
          headers: { Range: 'bytes=0-0' },
        })
        return rangeResponse.ok || rangeResponse.status === 206
      }

      return false
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
    modelSrc: string | null | undefined,
    fallbackSrc: string,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    onPick: (pickResult: any) => void,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    onSdkLoaded: (sdk: { Mesh: any; ReadableGeometry: any; PointsMaterial: any }) => void
  ) {
    viewerLoading.value = true
    viewerError.value = null
    clearSelection()

    try {
      if (viewer) {
        viewer.destroy()
        viewer = null
      }

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

      let loadUrl = modelSrc ?? fallbackSrc
      if (modelSrc) {
        const isReachable = await isModelOutputReachable(modelSrc)
        if (!isReachable) {
          viewerError.value = 'Konvertierte Datei nicht gefunden (404). Demo-Modell wird angezeigt.'
          loadUrl = fallbackSrc
        }
      }

      const sceneModel = xktLoader.load({
        id: 'model',
        src: loadUrl,
        edges: true,
      })

      viewer.scene.input.on('mouseclicked', (coords: number[]) => {
        const pickResult = viewer.scene.pick({
          canvasPos: coords,
          pickSurface: true,
        })
        onPick(pickResult)
      })

      sceneModel.on('loaded', () => {
        viewer.cameraFlight.flyTo(sceneModel)
        viewerLoading.value = false
      })

      sceneModel.on('error', () => {
        if (modelSrc) {
          viewerError.value = 'Modell konnte nicht geladen werden. Demo-Modell wird angezeigt.'
          xktLoader.load({ id: 'demo-fallback', src: fallbackSrc, edges: true })
        }
        viewerLoading.value = false
      })

      return viewer
    } catch {
      viewerError.value = 'xeokit-Viewer konnte nicht geladen werden.'
      viewerLoading.value = false
      return null
    }
  }

  function destroyViewer() {
    clearSelection()
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
    clearSelection,
    setSelectedEntity,
    initializeViewer,
    destroyViewer,
  }
}
