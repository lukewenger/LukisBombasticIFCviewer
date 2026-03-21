import { ref, computed, Ref } from 'vue'
import { modelsApi } from '../api/models'
import type { IfcModelDto } from '../types'
import { ModelStatus } from '../types/models'

export function useModelPolling(models: Ref<IfcModelDto[]>) {
  let pollTimer: ReturnType<typeof setInterval> | null = null

  const hasActiveJobs = computed(() =>
    models.value.some(
      (m) => m.status === ModelStatus.Uploaded || m.status === ModelStatus.Processing
    )
  )

  async function fetchModels() {
    models.value = await modelsApi.getModels()
  }

  function startPolling() {
    stopPolling()
    pollTimer = setInterval(async () => {
      if (hasActiveJobs.value) {
        await fetchModels()
      }
    }, 5000)
  }

  function stopPolling() {
    if (pollTimer) {
      clearInterval(pollTimer)
      pollTimer = null
    }
  }

  return {
    hasActiveJobs,
    fetchModels,
    startPolling,
    stopPolling,
  }
}
