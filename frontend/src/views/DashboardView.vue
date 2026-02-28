<script setup lang="ts">
import { useAuthStore } from '../stores/auth'
import { LayoutDashboard } from 'lucide-vue-next'

const authStore = useAuthStore()
</script>

<template>
  <div class="max-w-6xl mx-auto px-4 py-8">
    <div class="flex items-center gap-3 mb-8">
      <LayoutDashboard class="w-8 h-8 text-blue-600" />
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Dashboard</h1>
    </div>

    <div class="bg-white dark:bg-gray-800 rounded-xl shadow-md p-6">
      <p class="text-gray-600 dark:text-gray-400">
        Willkommen, <span class="font-semibold text-gray-900 dark:text-white">{{ authStore.username }}</span>!
      </p>
      <p class="mt-2 text-gray-500 dark:text-gray-400">
        Hier werden deine IFC-Modelle angezeigt. Diese Funktion wird in einem späteren Sprint implementiert.
      </p>
    </div>
  </div>
</template>
