import { ref, computed } from 'vue'
import { DEFAULT_MEMORY_THRESHOLD_BYTES } from './useXeokitViewer'

export interface MemoryWarning {
  modelLabel: string
  modelSizeBytes: number
  currentTotalBytes: number
  thresholdBytes: number
  /** Resolve callback: true = proceed, false = cancel */
  resolve: (proceed: boolean) => void
}

/**
 * Tracks loaded model memory (via fileSizeBytes proxies) and provides a
 * threshold-based warning before additional models are loaded.
 *
 * Usage: call `checkBeforeLoad(label, sizeBytes, currentTotalBytes)` before
 * loading a model.  It returns a Promise<boolean> — true means proceed.
 * When the threshold would be exceeded, `pendingWarning` is populated so the
 * UI can render a dismissible modal.
 */
export function useMemoryGuard(thresholdBytes = DEFAULT_MEMORY_THRESHOLD_BYTES) {
  const configuredThreshold = ref(thresholdBytes)
  const pendingWarning = ref<MemoryWarning | null>(null)

  const thresholdMb = computed(() => Math.round(configuredThreshold.value / (1024 * 1024)))

  /**
   * Returns true immediately if the new load would stay under threshold.
   * Returns a Promise that resolves when the user confirms or dismisses the
   * warning modal when the threshold would be exceeded.
   */
  function checkBeforeLoad(
    modelLabel: string,
    modelSizeBytes: number,
    currentTotalBytes: number
  ): Promise<boolean> {
    const projectedTotal = currentTotalBytes + modelSizeBytes
    if (projectedTotal <= configuredThreshold.value) {
      return Promise.resolve(true)
    }

    return new Promise<boolean>((resolve) => {
      pendingWarning.value = {
        modelLabel,
        modelSizeBytes,
        currentTotalBytes,
        thresholdBytes: configuredThreshold.value,
        resolve: (proceed) => {
          pendingWarning.value = null
          resolve(proceed)
        },
      }
    })
  }

  function confirmWarning() {
    pendingWarning.value?.resolve(true)
  }

  function dismissWarning() {
    pendingWarning.value?.resolve(false)
  }

  return {
    configuredThreshold,
    thresholdMb,
    pendingWarning,
    checkBeforeLoad,
    confirmWarning,
    dismissWarning,
  }
}
