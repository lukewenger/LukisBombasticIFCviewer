<script setup lang="ts">
import { useRouter } from 'vue-router'
import { AlertTriangle } from 'lucide-vue-next'

const router = useRouter()
</script>

<template>
  <div class="min-h-[calc(100vh-4rem)] flex items-center justify-center px-4">
    <div class="text-center">
      <AlertTriangle class="w-16 h-16 text-yellow-500 mx-auto mb-4" />
      <h1 class="text-6xl font-bold text-gray-900 dark:text-white mb-2">404</h1>
      <p class="text-xl text-gray-600 dark:text-gray-400 mb-8">Seite nicht gefunden</p>
      <button
        class="px-6 py-3 bg-blue-600 text-white rounded-lg font-medium hover:bg-blue-700 transition-colors"
        @click="router.push('/')"
      >
        Zur Startseite
      </button>
    </div>
  </div>
</template>
