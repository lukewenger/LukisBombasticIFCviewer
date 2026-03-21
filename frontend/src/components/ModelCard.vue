<script setup lang="ts">
import { Eye, RefreshCw, Trash2 } from 'lucide-vue-next'
import type { IfcModelDto } from '../types'
import { ModelStatus } from '../types/models'

interface Props {
  model: IfcModelDto
  isDeleting?: boolean
  isRetrying?: boolean
  isViewable?: boolean
}

interface Emits {
  (e: 'view'): void
  (e: 'retry'): void
  (e: 'delete'): void
}

defineProps<Props>()
defineEmits<Emits>()

function statusLabel(status: ModelStatus): string {
  switch (status) {
    case ModelStatus.Uploaded: return 'Hochgeladen'
    case ModelStatus.Processing: return 'Verarbeitung'
    case ModelStatus.Ready: return 'Bereit'
    case ModelStatus.Failed: return 'Fehlgeschlagen'
    case ModelStatus.Archived: return 'Archiviert'
    default: return 'Unbekannt'
  }
}

function statusColor(status: ModelStatus): string {
  switch (status) {
    case ModelStatus.Uploaded: return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400'
    case ModelStatus.Processing: return 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400'
    case ModelStatus.Ready: return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400'
    case ModelStatus.Failed: return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400'
    default: return 'bg-gray-100 text-gray-800'
  }
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('de-CH', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}
</script>

<template>
  <tr class="border-b border-gray-100 dark:border-gray-700/50 last:border-0 hover:bg-gray-50 dark:hover:bg-gray-700/30 transition-colors">
    <td class="px-6 py-4">
      <p class="font-medium text-gray-900 dark:text-white truncate max-w-xs">{{ model.fileName }}</p>
    </td>
    <td class="px-6 py-4 text-sm text-gray-500 dark:text-gray-400 hidden sm:table-cell">
      {{ formatSize(model.fileSizeBytes) }}
    </td>
    <td class="px-6 py-4">
      <span
        class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium"
        :class="statusColor(model.status)"
      >
        {{ statusLabel(model.status) }}
      </span>
    </td>
    <td class="px-6 py-4 text-sm text-gray-500 dark:text-gray-400 hidden md:table-cell">
      {{ formatDate(model.createdAt) }}
    </td>
    <td class="px-6 py-4 text-right">
      <div class="inline-flex items-center gap-1.5">
        <button
          v-if="isViewable"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-blue-600 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded-lg transition-colors min-h-[44px]"
          @click="$emit('view')"
        >
          <Eye class="w-4 h-4" />
          Anzeigen
        </button>

        <button
          v-if="model.status === ModelStatus.Ready || model.status === ModelStatus.Failed"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-amber-700 hover:bg-amber-50 dark:hover:bg-amber-900/20 rounded-lg transition-colors disabled:opacity-60 min-h-[44px]"
          :disabled="isRetrying"
          @click="$emit('retry')"
        >
          <RefreshCw class="w-4 h-4" :class="isRetrying ? 'animate-spin' : ''" />
          Redo
        </button>

        <button
          class="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-lg transition-colors disabled:opacity-60 min-h-[44px]"
          :disabled="isDeleting"
          @click="$emit('delete')"
        >
          <Trash2 class="w-4 h-4" :class="isDeleting ? 'animate-pulse' : ''" />
          Loeschen
        </button>

        <span
          v-if="model.status === ModelStatus.Processing || model.status === ModelStatus.Uploaded"
          class="text-xs text-gray-500 dark:text-gray-400"
        >
          <span class="animate-spin inline-block h-3 w-3 border-2 border-gray-500 border-t-transparent rounded-full"></span>
        </span>
      </div>
    </td>
  </tr>
</template>
