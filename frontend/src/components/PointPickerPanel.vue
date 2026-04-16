<script setup lang="ts">
import { ref } from 'vue'
import { MapPin, Play, Square, FileDown, FileUp, Trash2 } from 'lucide-vue-next'
import type { PickedPoint } from '../composables/usePointPicker'

interface Props {
  pointPickingMode: boolean
  pickedPoints: PickedPoint[]
  pointSize: number
  pointTransparency: number
}

interface Emits {
  (e: 'toggle-picking', value: boolean): void
  (e: 'open-csv'): void
  (e: 'export-csv'): void
  (e: 'clear-points'): void
  (e: 'update:pointSize', value: number): void
  (e: 'update:pointTransparency', value: number): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const isOpen = ref(false)
</script>

<template>
  <div class="select-none" :class="isOpen ? 'w-72' : 'w-auto'">
    <div
      class="bg-white dark:bg-gray-800 shadow-lg border border-gray-200 dark:border-gray-700 overflow-hidden transition-all duration-200"
      :class="isOpen ? 'rounded-xl' : 'rounded-full'"
    >

      <!-- Header — acts as the pill when collapsed, as the panel title bar when expanded -->
      <button
        class="flex items-center gap-2 px-3 py-2 w-full text-sm font-medium text-gray-700 dark:text-gray-300 hover:text-gray-900 dark:hover:text-white transition-colors text-left"
        :class="isOpen ? '' : 'justify-center'"
        @click="isOpen = !isOpen"
      >
        <MapPin class="w-4 h-4 text-blue-500 shrink-0" />
        <span>Punkt-Messung</span>
        <span
          v-if="props.pickedPoints.length > 0"
          class="bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 text-xs font-semibold px-1.5 py-0.5 rounded-full"
        >
          {{ props.pickedPoints.length }}
        </span>
        <span
          v-if="props.pointPickingMode"
          class="flex items-center gap-1 text-xs text-green-600 dark:text-green-400 font-medium"
        >
          <span class="inline-block w-1.5 h-1.5 rounded-full bg-green-500 animate-pulse" />
          aktiv
        </span>

        <!-- Collapse chevron — only visible when expanded -->
        <svg
          v-if="isOpen"
          class="w-4 h-4 text-gray-400 shrink-0 ml-auto"
          fill="none" stroke="currentColor" viewBox="0 0 24 24"
        >
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
        </svg>
      </button>

      <!-- Body — only rendered when expanded -->
      <Transition
        enter-active-class="transition-all duration-200 overflow-hidden"
        leave-active-class="transition-all duration-200 overflow-hidden"
        enter-from-class="max-h-0 opacity-0"
        enter-to-class="max-h-screen opacity-100"
        leave-from-class="max-h-screen opacity-100"
        leave-to-class="max-h-0 opacity-0"
      >
        <div v-if="isOpen">

          <!-- Action bar -->
          <div class="flex items-center gap-1 px-3 pb-2.5 flex-wrap">
            <button
              class="flex items-center gap-1 text-xs px-2.5 py-1.5 rounded-lg border font-medium transition-colors"
              :class="props.pointPickingMode
                ? 'border-green-500 bg-green-500 text-white hover:bg-green-600'
                : 'border-blue-500 bg-blue-500 text-white hover:bg-blue-600'"
              :title="props.pointPickingMode ? 'Picking beenden (Esc)' : 'Picking starten'"
              @click="emit('toggle-picking', !props.pointPickingMode)"
            >
              <Square v-if="props.pointPickingMode" class="w-3 h-3" />
              <Play v-else class="w-3 h-3" />
              {{ props.pointPickingMode ? 'Stop' : 'Start' }}
            </button>

            <button
              class="flex items-center gap-1 text-xs px-2.5 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
              title="CSV-Datei laden"
              @click="emit('open-csv')"
            >
              <FileUp class="w-3 h-3" />
              CSV laden
            </button>

            <button
              class="flex items-center gap-1 text-xs px-2.5 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              :disabled="props.pickedPoints.length === 0"
              title="Punkte als CSV exportieren"
              @click="emit('export-csv')"
            >
              <FileDown class="w-3 h-3" />
              Export
            </button>

            <button
              class="flex items-center gap-1 text-xs px-2.5 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-red-50 hover:border-red-400 hover:text-red-600 dark:hover:bg-red-900/20 dark:hover:text-red-400 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              :disabled="props.pickedPoints.length === 0"
              title="Alle Punkte entfernen"
              @click="emit('clear-points')"
            >
              <Trash2 class="w-3 h-3" />
              Leeren
            </button>
          </div>

          <!-- Sliders -->
          <div class="px-3 pb-2.5 space-y-2.5 border-t border-gray-100 dark:border-gray-700 pt-2.5">
            <label class="block text-xs text-gray-600 dark:text-gray-400">
              <div class="flex justify-between mb-1">
                <span>Punktgrösse</span>
                <span class="tabular-nums text-gray-500 dark:text-gray-500">{{ props.pointSize }}</span>
              </div>
              <input
                :value="props.pointSize"
                type="range"
                min="2"
                max="20"
                step="1"
                class="w-full accent-blue-500"
                @input="emit('update:pointSize', Number(($event.target as HTMLInputElement).value))"
              >
            </label>
            <label class="block text-xs text-gray-600 dark:text-gray-400">
              <div class="flex justify-between mb-1">
                <span>Transparenz</span>
                <span class="tabular-nums text-gray-500 dark:text-gray-500">{{ Math.round(props.pointTransparency * 100) }}%</span>
              </div>
              <input
                :value="props.pointTransparency"
                type="range"
                min="0"
                max="0.9"
                step="0.05"
                class="w-full accent-blue-500"
                @input="emit('update:pointTransparency', Number(($event.target as HTMLInputElement).value))"
              >
            </label>
          </div>

          <!-- Status hint -->
          <div class="px-3 pb-2.5 border-t border-gray-100 dark:border-gray-700 pt-2">
            <p class="text-xs text-gray-500 dark:text-gray-400">
              <template v-if="props.pointPickingMode">
                Picking aktiv — Navigation deaktiviert. Klick speichert Punkte. <kbd class="font-mono bg-gray-100 dark:bg-gray-700 px-1 rounded text-gray-600 dark:text-gray-300">Esc</kbd> zum Beenden.
              </template>
              <template v-else-if="props.pickedPoints.length === 0">
                Picking starten oder CSV laden, um 3D-Punkte darzustellen.
              </template>
              <template v-else>
                {{ props.pickedPoints.length }} Punkt{{ props.pickedPoints.length === 1 ? '' : 'e' }} gesetzt.
              </template>
            </p>
          </div>

          <!-- Point list -->
          <div v-if="props.pickedPoints.length > 0" class="border-t border-gray-100 dark:border-gray-700">
            <ol class="max-h-52 overflow-y-auto divide-y divide-gray-100 dark:divide-gray-700/50" data-wheel-scroll-panel="true">
              <li
                v-for="(point, index) in props.pickedPoints"
                :key="point.id"
                class="flex items-start gap-2 px-3 py-2"
              >
                <span class="shrink-0 w-5 h-5 rounded-full bg-red-100 dark:bg-red-900/30 text-red-600 dark:text-red-400 text-xs font-semibold flex items-center justify-center mt-0.5">
                  {{ index + 1 }}
                </span>
                <div class="text-xs text-gray-700 dark:text-gray-300 tabular-nums leading-relaxed">
                  <span class="text-gray-400 dark:text-gray-500">x </span>{{ point.x.toFixed(3) }}&ensp;
                  <span class="text-gray-400 dark:text-gray-500">y </span>{{ point.y.toFixed(3) }}&ensp;
                  <span class="text-gray-400 dark:text-gray-500">z </span>{{ point.z.toFixed(3) }}
                </div>
              </li>
            </ol>
          </div>

        </div>
      </Transition>
    </div>
  </div>
</template>
