<script setup lang="ts">
import { computed } from 'vue'
import { AlertTriangle, X } from 'lucide-vue-next'
import type { MemoryWarning } from '../composables/useMemoryGuard'

const props = defineProps<{
  warning: MemoryWarning
}>()

const emit = defineEmits<{
  (e: 'confirm'): void
  (e: 'dismiss'): void
}>()

function formatMb(bytes: number): string {
  return (bytes / (1024 * 1024)).toFixed(0)
}

const thresholdMb = computed(() => formatMb(props.warning.thresholdBytes))
const currentMb = computed(() => formatMb(props.warning.currentTotalBytes))
const modelMb = computed(() => formatMb(props.warning.modelSizeBytes))
const projectedMb = computed(() =>
  formatMb(props.warning.currentTotalBytes + props.warning.modelSizeBytes)
)
</script>

<template>
  <Teleport to="body">
    <div
      class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50"
      @click.self="emit('dismiss')"
    >
      <div class="bg-white dark:bg-gray-800 rounded-xl shadow-2xl w-full max-w-md border border-gray-200 dark:border-gray-700 overflow-hidden">
        <!-- Header -->
        <div class="flex items-center justify-between px-5 py-4 border-b border-gray-200 dark:border-gray-700 bg-amber-50 dark:bg-amber-900/20">
          <div class="flex items-center gap-2.5">
            <AlertTriangle class="w-5 h-5 text-amber-500 shrink-0" />
            <h2 class="text-base font-semibold text-gray-900 dark:text-white">Hoher Speicherverbrauch</h2>
          </div>
          <button
            class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 transition-colors"
            @click="emit('dismiss')"
          >
            <X class="w-5 h-5" />
          </button>
        </div>

        <!-- Body -->
        <div class="p-5 space-y-4">
          <p class="text-sm text-gray-700 dark:text-gray-300">
            Das Laden von
            <span class="font-medium text-gray-900 dark:text-white">{{ warning.modelLabel }}</span>
            ({{ modelMb }} MB) wurde gestoppt, weil die Gesamtgrösse aller geladenen Modelle
            den Schwellenwert von <span class="font-medium">{{ thresholdMb }} MB</span> überschreiten würde.
          </p>

          <div class="rounded-lg bg-gray-50 dark:bg-gray-700/50 px-4 py-3 text-sm space-y-1.5">
            <div class="flex justify-between text-gray-600 dark:text-gray-400">
              <span>Aktuell geladen</span>
              <span class="tabular-nums font-medium">{{ currentMb }} MB</span>
            </div>
            <div class="flex justify-between text-gray-600 dark:text-gray-400">
              <span>Neues Modell</span>
              <span class="tabular-nums font-medium">+ {{ modelMb }} MB</span>
            </div>
            <div class="flex justify-between border-t border-gray-200 dark:border-gray-600 pt-1.5 text-amber-700 dark:text-amber-400 font-semibold">
              <span>Projizierter Gesamtverbrauch</span>
              <span class="tabular-nums">{{ projectedMb }} MB</span>
            </div>
            <div class="flex justify-between text-gray-500 dark:text-gray-400">
              <span>Schwellenwert</span>
              <span class="tabular-nums">{{ thresholdMb }} MB</span>
            </div>
          </div>

          <p class="text-xs text-gray-500 dark:text-gray-400">
            Die Groesse basiert auf der Originaldatei und dient als Naeherungswert fuer den Speicherbedarf.
            Laden Sie zuerst ein anderes Modell aus, um Speicher freizugeben.
          </p>
        </div>

        <!-- Footer -->
        <div class="flex items-center justify-end gap-3 px-5 py-4 border-t border-gray-200 dark:border-gray-700">
          <button
            class="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors min-h-[36px]"
            @click="emit('dismiss')"
          >
            Abbrechen
          </button>
          <button
            class="px-4 py-2 text-sm font-medium bg-amber-500 hover:bg-amber-600 text-white rounded-lg transition-colors min-h-[36px]"
            @click="emit('confirm')"
          >
            Trotzdem laden
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
