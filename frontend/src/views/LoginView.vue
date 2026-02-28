<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import LoginForm from '../components/LoginForm.vue'

const router = useRouter()
const authStore = useAuthStore()

async function handleLogin(data: { username: string; password: string }) {
  try {
    await authStore.login(data)
    const redirect = (router.currentRoute.value.query.redirect as string) || '/dashboard'
    router.push(redirect)
  } catch {
    // Error is handled by the store
  }
}
</script>

<template>
  <div class="min-h-[calc(100vh-4rem)] flex items-center justify-center px-4 py-12">
    <div class="w-full max-w-md">
      <div class="bg-white dark:bg-gray-800 rounded-2xl shadow-lg p-8">
        <div class="text-center mb-8">
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Anmelden</h1>
          <p class="mt-2 text-gray-600 dark:text-gray-400">
            Melde dich an, um deine IFC-Modelle zu verwalten
          </p>
        </div>

        <LoginForm
          :is-loading="authStore.isLoading"
          :error="authStore.error"
          @submit="handleLogin"
        />

        <p class="mt-6 text-center text-sm text-gray-600 dark:text-gray-400">
          Noch kein Konto?
          <router-link
            to="/register"
            class="font-medium text-blue-600 hover:text-blue-500 dark:text-blue-400"
          >
            Registrieren
          </router-link>
        </p>
      </div>
    </div>
  </div>
</template>
