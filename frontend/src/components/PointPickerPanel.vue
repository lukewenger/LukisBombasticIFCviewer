<script setup lang="ts">
import type { PickedPoint } from '../composables/usePointPicker'

interface Props {
  pointPickingMode: boolean
  pickedPoints: PickedPoint[]
  pointRadius: number
  pointTransparency: number
}

interface Emits {
  (e: 'toggle-picking', value: boolean): void
  (e: 'open-csv'): void
  (e: 'export-csv'): void
  (e: 'clear-points'): void
  (e: 'update:pointRadius', value: number): void
  (e: 'update:pointTransparency', value: number): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()
</script>

<template>
  <div class="absolute top-4 left-4 w-80 max-h-[80%] overflow-y-auto bg-white/95 dark:bg-gray-800/95 rounded-xl shadow-xl p-4 z-10 border border-gray-200 dark:border-gray-700">
    <div class="flex items-center justify-between mb-3 gap-2">
      <h3 class="text-sm font-semibold text-gray-900 dark:text-white">Point Picker</h3>
      <div class="flex items-center gap-2">
        <button
          class="text-xs px-2 py-1 rounded border transition-colors"
          :class="props.pointPickingMode
            ? 'border-blue-600 bg-blue-600 text-white hover:bg-blue-700'
            : 'border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700'"
          @click="emit('toggle-picking', !props.pointPickingMode)"
        >
          {{ props.pointPickingMode ? 'Picking aktiv' : 'Picking starten' }}
        </button>
        <button
          class="text-xs px-2 py-1 rounded border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700"
          @click="emit('open-csv')"
        >
          CSV laden
        </button>
        <button
          class="text-xs px-2 py-1 rounded border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 disabled:opacity-50"
          :disabled="props.pickedPoints.length === 0"
          @click="emit('export-csv')"
        >
          CSV
        </button>
        <button
          class="text-xs px-2 py-1 rounded border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 disabled:opacity-50"
          :disabled="props.pickedPoints.length === 0"
          @click="emit('clear-points')"
        >
          Leeren
        </button>
      </div>
    </div>

    <div class="grid grid-cols-2 gap-2 mb-3">
      <label class="text-xs text-gray-600 dark:text-gray-300">
        Radius
        <input
          :value="props.pointRadius"
          type="range"
          min="2"
          max="20"
          step="1"
          class="w-full"
          @input="emit('update:pointRadius', Number(($event.target as HTMLInputElement).value))"
        >
      </label>
      <label class="text-xs text-gray-600 dark:text-gray-300">
        Transparenz
        <input
          :value="props.pointTransparency"
          type="range"
          min="0"
          max="0.9"
          step="0.05"
          class="w-full"
          @input="emit('update:pointTransparency', Number(($event.target as HTMLInputElement).value))"
        >
      </label>
    </div>

    <p class="text-xs text-gray-500 dark:text-gray-400 mb-3">
      {{ props.pointPickingMode
        ? 'Picking-Modus aktiv: Kamera-Navigation ist deaktiviert, Klick speichert Punkte.'
        : 'Picking-Modus starten oder CSV laden, um 3D-Punkte darzustellen.' }}
    </p>

    <div v-if="props.pickedPoints.length === 0" class="text-xs text-gray-500 dark:text-gray-400">
      Noch keine Punkte gespeichert.
    </div>

    <ol v-else class="space-y-2 text-xs">
      <li
        v-for="(point, index) in props.pickedPoints"
        :key="point.id"
        class="rounded-lg border border-gray-200 dark:border-gray-700 p-2"
      >
        <p class="font-medium text-gray-900 dark:text-white">#{{ index + 1 }}</p>
        <p class="text-gray-700 dark:text-gray-300">x: {{ point.x.toFixed(3) }}</p>
        <p class="text-gray-700 dark:text-gray-300">y: {{ point.y.toFixed(3) }}</p>
        <p class="text-gray-700 dark:text-gray-300">z: {{ point.z.toFixed(3) }}</p>
      </li>
    </ol>
  </div>
</template>
