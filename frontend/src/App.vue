<script setup lang="ts">
import AppHeader from './components/AppHeader.vue'
import { useAuthStore } from './stores/auth'
import { onMounted } from 'vue'

const authStore = useAuthStore()

onMounted(() => {
  authStore.initializeFromStorage()
})
</script>

<template>
  <div class="min-h-screen bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-gray-100">
    <AppHeader />
    <main>
      <router-view />
    </main>
  </div>
</template>
