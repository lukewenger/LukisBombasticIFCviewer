<script setup lang="ts">
import { AlertCircle } from 'lucide-vue-next'

defineProps<{
  message: string
}>()
</script>

<template>
  <div class="flex items-center gap-3 p-4 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
    <AlertCircle class="w-5 h-5 text-red-500 shrink-0" />
    <p class="text-sm text-red-700 dark:text-red-400">{{ message }}</p>
  </div>
</template>
