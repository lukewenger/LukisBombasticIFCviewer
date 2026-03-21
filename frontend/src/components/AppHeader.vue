<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { Building2, Menu, X, Sun, Moon } from 'lucide-vue-next'

const router = useRouter()
const authStore = useAuthStore()

const isMobileMenuOpen = ref(false)
const isDark = ref(document.documentElement.classList.contains('dark'))

function toggleDarkMode() {
  isDark.value = !isDark.value
  document.documentElement.classList.toggle('dark', isDark.value)
  localStorage.setItem('theme', isDark.value ? 'dark' : 'light')
}

function logout() {
  authStore.logout()
  isMobileMenuOpen.value = false
  router.push('/')
}

// Initialize dark mode from localStorage
const savedTheme = localStorage.getItem('theme')
if (savedTheme === 'dark' || (!savedTheme && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
  document.documentElement.classList.add('dark')
  isDark.value = true
}
</script>

<template>
  <header class="bg-white dark:bg-gray-800 shadow-sm border-b border-gray-200 dark:border-gray-700">
    <div class="max-w-6xl mx-auto px-4">
      <div class="flex items-center justify-between h-16">
        <!-- Logo -->
        <router-link to="/" class="flex items-center gap-2 text-blue-600 font-bold text-lg">
          <Building2 class="w-6 h-6" />
          <span class="hidden sm:inline">IFC Model Viewer</span>
        </router-link>

        <!-- Desktop Navigation -->
        <nav class="hidden md:flex items-center gap-6">
          <template v-if="authStore.isAuthenticated">
            <router-link
              to="/dashboard"
              class="text-gray-600 dark:text-gray-300 hover:text-blue-600 dark:hover:text-blue-400 font-medium transition-colors"
              active-class="text-blue-600 dark:text-blue-400"
            >
              Dashboard
            </router-link>
            <router-link
              to="/upload"
              class="text-gray-600 dark:text-gray-300 hover:text-blue-600 dark:hover:text-blue-400 font-medium transition-colors"
              active-class="text-blue-600 dark:text-blue-400"
            >
              Upload
            </router-link>
            <router-link
              to="/profile"
              class="text-gray-600 dark:text-gray-300 hover:text-blue-600 dark:hover:text-blue-400 font-medium transition-colors"
              active-class="text-blue-600 dark:text-blue-400"
            >
              Profil
            </router-link>
          </template>
        </nav>

        <!-- Right side -->
        <div class="flex items-center gap-3">
          <!-- Dark mode toggle -->
          <button
            @click="toggleDarkMode"
            class="p-2 rounded-lg text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            :title="isDark ? 'Light Mode' : 'Dark Mode'"
          >
            <Sun v-if="isDark" class="w-5 h-5" />
            <Moon v-else class="w-5 h-5" />
          </button>

          <!-- Auth buttons (desktop) -->
          <div class="hidden md:flex items-center gap-3">
            <template v-if="authStore.isAuthenticated">
              <span class="text-sm text-gray-600 dark:text-gray-400">
                {{ authStore.username }}
              </span>
              <button
                @click="logout"
                class="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
              >
                Abmelden
              </button>
            </template>
            <template v-else>
              <router-link
                to="/login"
                class="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:text-blue-600 transition-colors"
              >
                Anmelden
              </router-link>
              <router-link
                to="/register"
                class="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors"
              >
                Registrieren
              </router-link>
            </template>
          </div>

          <!-- Mobile menu toggle -->
          <button
            class="md:hidden p-2 rounded-lg text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            @click="isMobileMenuOpen = !isMobileMenuOpen"
          >
            <X v-if="isMobileMenuOpen" class="w-5 h-5" />
            <Menu v-else class="w-5 h-5" />
          </button>
        </div>
      </div>

      <!-- Mobile Menu -->
      <div
        v-if="isMobileMenuOpen"
        class="md:hidden border-t border-gray-200 dark:border-gray-700 py-4 space-y-2"
      >
        <template v-if="authStore.isAuthenticated">
          <router-link
            to="/dashboard"
            class="block px-3 py-2 rounded-lg text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700"
            @click="isMobileMenuOpen = false"
          >
            Dashboard
          </router-link>
          <router-link
            to="/upload"
            class="block px-3 py-2 rounded-lg text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700"
            @click="isMobileMenuOpen = false"
          >
            Upload
          </router-link>
          <router-link
            to="/profile"
            class="block px-3 py-2 rounded-lg text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700"
            @click="isMobileMenuOpen = false"
          >
            Profil
          </router-link>
          <div class="border-t border-gray-200 dark:border-gray-700 pt-2 mt-2">
            <p class="px-3 py-1 text-sm text-gray-500 dark:text-gray-400">
              Angemeldet als <strong>{{ authStore.username }}</strong>
            </p>
            <button
              @click="logout"
              class="block w-full text-left px-3 py-2 rounded-lg text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20"
            >
              Abmelden
            </button>
          </div>
        </template>
        <template v-else>
          <router-link
            to="/login"
            class="block px-3 py-2 rounded-lg text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700"
            @click="isMobileMenuOpen = false"
          >
            Anmelden
          </router-link>
          <router-link
            to="/register"
            class="block px-3 py-2 rounded-lg text-blue-600 font-medium hover:bg-blue-50 dark:hover:bg-blue-900/20"
            @click="isMobileMenuOpen = false"
          >
            Registrieren
          </router-link>
        </template>
      </div>
    </div>
  </header>
</template>
