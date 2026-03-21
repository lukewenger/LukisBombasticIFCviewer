<script setup lang="ts">
interface Props {
  selectedEntityId: string | null
  selectedAttributes: Record<string, string>
}

interface Emits {
  (e: 'clear-selection'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()
</script>

<template>
  <div
    v-if="props.selectedEntityId"
    class="absolute top-4 right-4 w-80 max-h-[80%] overflow-y-auto bg-white/95 dark:bg-gray-800/95 rounded-xl shadow-xl p-4 z-10 border border-gray-200 dark:border-gray-700"
  >
    <div class="flex items-center justify-between mb-3">
      <h3 class="text-sm font-semibold text-gray-900 dark:text-white">IFC-Attribute</h3>
      <button class="text-gray-500 hover:text-gray-700 dark:hover:text-gray-300" @click="emit('clear-selection')">X</button>
    </div>
    <dl class="space-y-2 text-xs">
      <div v-for="(value, key) in props.selectedAttributes" :key="key">
        <dt class="text-gray-500 dark:text-gray-400">{{ key }}</dt>
        <dd class="text-gray-900 dark:text-white font-medium break-all">{{ value }}</dd>
      </div>
    </dl>
  </div>
</template>
