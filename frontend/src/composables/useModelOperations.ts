import { ref } from 'vue'
import type { Ref } from 'vue'
import { modelsApi } from '../api/models'
import { conversionsApi } from '../api/conversions'
import type { IfcModelDto } from '../types'
import { ConversionFormat } from '../types/models'
import type { ToastType } from './useToasts'

export function useModelOperations(
  models: Ref<IfcModelDto[]>,
  showToast: (msg: string, type: ToastType) => void
) {
  const isDeleting = ref<Record<string, boolean>>({})
  const isRetrying = ref<Record<string, boolean>>({})

  async function deleteModel(model: IfcModelDto) {
    const confirmed = window.confirm(`Modell "${model.fileName}" wirklich loeschen?`)
    if (!confirmed) return

    isDeleting.value[model.id] = true
    try {
      await modelsApi.deleteModel(model.id)
      models.value = models.value.filter((m) => m.id !== model.id)
      showToast(`Modell "${model.fileName}" wurde geloescht.`, 'success')
    } catch {
      showToast('Loeschen fehlgeschlagen.', 'error')
    } finally {
      isDeleting.value[model.id] = false
    }
  }

  async function retryConversion(model: IfcModelDto) {
    isRetrying.value[model.id] = true
    try {
      await conversionsApi.createConversionJob(model.id, ConversionFormat.XKT)
      await new Promise((resolve) => setTimeout(resolve, 500))
      models.value = await modelsApi.getModels()
      showToast(`Konvertierung fuer "${model.fileName}" wurde neu gestartet.`, 'success')
    } catch {
      showToast('Konvertierung konnte nicht neu gestartet werden.', 'error')
    } finally {
      isRetrying.value[model.id] = false
    }
  }

  return {
    isDeleting,
    isRetrying,
    deleteModel,
    retryConversion,
  }
}
