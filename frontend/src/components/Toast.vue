<script setup lang="ts">
import type { ToastMessage } from '../composables/useToasts'

interface Props {
  toast: ToastMessage
  onRemove: (id: number) => void
}

defineProps<Props>()
</script>

<template>
  <div
    class="rounded-xl shadow-lg border px-4 py-3 text-sm flex items-start justify-between gap-3 animate-in fade-in slide-in-from-top"
    :class="
      toast.type === 'success'
        ? 'bg-emerald-50 border-emerald-200 text-emerald-900 dark:bg-emerald-900/30 dark:border-emerald-700 dark:text-emerald-200'
        : 'bg-red-50 border-red-200 text-red-900 dark:bg-red-900/30 dark:border-red-700 dark:text-red-200'
    "
  >
    <span>{{ toast.message }}</span>
    <button
      class="text-current/70 hover:text-current transition-colors"
      @click="onRemove(toast.id)"
    >
      ✕
    </button>
  </div>
</template>
