import { ref } from 'vue'

export type ToastType = 'success' | 'error'

export interface ToastMessage {
  id: number
  message: string
  type: ToastType
}

export function useToasts() {
  const toasts = ref<ToastMessage[]>([])
  let toastId = 0
  const toastTimers = new Map<number, ReturnType<typeof setTimeout>>()

  function removeToast(id: number) {
    toasts.value = toasts.value.filter((toast) => toast.id !== id)
    const timer = toastTimers.get(id)
    if (timer) {
      clearTimeout(timer)
      toastTimers.delete(id)
    }
  }

  function showToast(message: string, type: ToastType = 'success', duration = 3000) {
    const id = ++toastId
    toasts.value.push({ id, message, type })
    const timer = setTimeout(() => removeToast(id), duration)
    toastTimers.set(id, timer)
  }

  function clearAll() {
    for (const timer of toastTimers.values()) {
      clearTimeout(timer)
    }
    toastTimers.clear()
    toasts.value = []
  }

  return {
    toasts,
    showToast,
    removeToast,
    clearAll,
  }
}
