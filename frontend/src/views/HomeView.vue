<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { Building2, Upload, Eye } from 'lucide-vue-next'

const router = useRouter()
const authStore = useAuthStore()
</script>

<template>
  <div class="max-w-6xl mx-auto px-4 py-16">
    <!-- Hero -->
    <div class="text-center mb-16">
      <div class="flex justify-center mb-6">
        <Building2 class="w-16 h-16 text-blue-600" />
      </div>
      <h1 class="text-4xl md:text-5xl font-bold text-gray-900 dark:text-white mb-4">
        IFC Model Viewer
      </h1>
      <p class="text-xl text-gray-600 dark:text-gray-400 max-w-2xl mx-auto">
        Webbasierte BIM-Modellverwaltung — Laden Sie IFC-Dateien hoch,
        verfolgen Sie die Konvertierung und betrachten Sie 3D-Modelle direkt im Browser.
      </p>
      <div class="mt-8 flex justify-center gap-4">
        <router-link
          v-if="!authStore.isAuthenticated"
          to="/register"
          class="px-6 py-3 bg-blue-600 text-white rounded-lg font-medium hover:bg-blue-700 transition-colors"
        >
          Jetzt starten
        </router-link>
        <router-link
          v-if="!authStore.isAuthenticated"
          to="/login"
          class="px-6 py-3 bg-gray-200 dark:bg-gray-700 text-gray-900 dark:text-white rounded-lg font-medium hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors"
        >
          Anmelden
        </router-link>
        <button
          v-if="authStore.isAuthenticated"
          class="px-6 py-3 bg-blue-600 text-white rounded-lg font-medium hover:bg-blue-700 transition-colors"
          @click="router.push('/dashboard')"
        >
          Zum Dashboard
        </button>
      </div>
    </div>

    <!-- Features -->
    <div class="grid md:grid-cols-3 gap-8">
      <div class="bg-white dark:bg-gray-800 rounded-xl p-6 shadow-md">
        <Upload class="w-10 h-10 text-blue-600 mb-4" />
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-2">Modell-Upload</h3>
        <p class="text-gray-600 dark:text-gray-400">
          Laden Sie IFC-Dateien per Drag-and-Drop hoch. Unterstützt alle gängigen IFC-Versionen.
        </p>
      </div>
      <div class="bg-white dark:bg-gray-800 rounded-xl p-6 shadow-md">
        <Building2 class="w-10 h-10 text-green-600 mb-4" />
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-2">Konvertierung</h3>
        <p class="text-gray-600 dark:text-gray-400">
          Automatische Konvertierung in browserkompatible Formate mit Echtzeit-Statusverfolgung.
        </p>
      </div>
      <div class="bg-white dark:bg-gray-800 rounded-xl p-6 shadow-md">
        <Eye class="w-10 h-10 text-purple-600 mb-4" />
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-2">3D-Viewer</h3>
        <p class="text-gray-600 dark:text-gray-400">
          Interaktive 3D-Betrachtung mit xeokit — Drehen, Zoomen, Elemente auswählen.
        </p>
      </div>
    </div>
  </div>
</template>
