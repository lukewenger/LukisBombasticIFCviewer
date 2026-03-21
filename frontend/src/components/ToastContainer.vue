<script setup lang="ts">
import Toast from './Toast.vue'
import type { ToastMessage } from '../composables/useToasts'

interface Props {
  toasts: ToastMessage[]
}

interface Emits {
  (e: 'remove', id: number): void
}

defineProps<Props>()
defineEmits<Emits>()
</script>

<template>
  <div class="fixed top-20 right-4 z-50 space-y-2 w-[22rem]">
    <Toast
      v-for="toast in toasts"
      :key="toast.id"
      :toast="toast"
      @remove="$emit('remove', $event)"
    />
  </div>
</template>
