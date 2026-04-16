import { ref, watch } from 'vue'

const STORAGE_KEY = 'ifccm_multi_model_mode'

/**
 * Persists the "multi-model mode" toggle across sessions via localStorage.
 *
 * When multiModelMode is false (default), loading a new model from the
 * dashboard replaces the previous one (single-model behaviour).
 * When true, models are accumulated in the viewer scene.
 */
export function useMultiModelMode() {
  const stored = localStorage.getItem(STORAGE_KEY)
  const multiModelMode = ref<boolean>(stored === 'true')

  watch(multiModelMode, (val) => {
    localStorage.setItem(STORAGE_KEY, String(val))
  })

  return { multiModelMode }
}
