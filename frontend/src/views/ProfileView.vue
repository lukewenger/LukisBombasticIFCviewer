<script setup lang="ts">
import { useAuthStore } from '../stores/auth'
import { onMounted, ref } from 'vue'
import { UserCircle } from 'lucide-vue-next'

const authStore = useAuthStore()
const isLoadingProfile = ref(false)

onMounted(async () => {
  isLoadingProfile.value = true
  await authStore.fetchCurrentUser()
  isLoadingProfile.value = false
})
</script>

<template>
  <div class="max-w-2xl mx-auto px-4 py-8">
    <div class="flex items-center gap-3 mb-8">
      <UserCircle class="w-8 h-8 text-blue-600" />
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Profil</h1>
    </div>

    <div v-if="isLoadingProfile" class="flex justify-center py-12">
      <div class="animate-spin h-8 w-8 border-4 border-blue-600 border-t-transparent rounded-full"></div>
    </div>

    <div v-else-if="authStore.user" class="bg-white dark:bg-gray-800 rounded-xl shadow-md p-6 space-y-4">
      <div>
        <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Benutzername</label>
        <p class="text-lg text-gray-900 dark:text-white">{{ authStore.user.username }}</p>
      </div>
      <div>
        <label class="text-sm font-medium text-gray-500 dark:text-gray-400">E-Mail</label>
        <p class="text-lg text-gray-900 dark:text-white">{{ authStore.user.email }}</p>
      </div>
      <div>
        <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Rolle</label>
        <p class="text-lg text-gray-900 dark:text-white">{{ authStore.user.role }}</p>
      </div>
      <div>
        <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Mitglied seit</label>
        <p class="text-lg text-gray-900 dark:text-white">
          {{ new Date(authStore.user.createdAt).toLocaleDateString('de-CH') }}
        </p>
      </div>
    </div>
  </div>
</template>
