<script setup lang="ts">
import ModelCard from './ModelCard.vue'
import type { IfcModelDto } from '../types'
import { ModelStatus } from '../types/models'

interface Props {
  models: IfcModelDto[]
  deletingIds?: Record<string, boolean>
  retryingIds?: Record<string, boolean>
  selectedIds?: Set<string>
}

interface Emits {
  (e: 'view', model: IfcModelDto): void
  (e: 'retry', model: IfcModelDto): void
  (e: 'delete', model: IfcModelDto): void
  (e: 'toggle-select', model: IfcModelDto): void
}

defineProps<Props>()
defineEmits<Emits>()

const ModelStatusConst = ModelStatus
</script>

<template>
  <div class="bg-white dark:bg-gray-800 rounded-xl shadow-md overflow-hidden">
    <table class="w-full">
      <thead>
        <tr class="border-b border-gray-200 dark:border-gray-700 text-left text-sm text-gray-500 dark:text-gray-400">
          <th class="pl-4 pr-2 py-3 w-8"></th>
          <th class="px-4 py-3 font-medium">Dateiname</th>
          <th class="px-6 py-3 font-medium hidden sm:table-cell">Grösse</th>
          <th class="px-6 py-3 font-medium">Status</th>
          <th class="px-6 py-3 font-medium hidden md:table-cell">Hochgeladen</th>
          <th class="px-6 py-3 font-medium text-right">Aktion</th>
        </tr>
      </thead>
      <tbody>
        <ModelCard
          v-for="model in models"
          :key="model.id"
          :model="model"
          :is-deleting="deletingIds?.[model.id] ?? false"
          :is-retrying="retryingIds?.[model.id] ?? false"
          :is-viewable="model.status === ModelStatusConst.Ready"
          :is-selected="selectedIds?.has(model.id) ?? false"
          @view="$emit('view', model)"
          @retry="$emit('retry', model)"
          @delete="$emit('delete', model)"
          @toggle-select="$emit('toggle-select', model)"
        />
      </tbody>
    </table>
  </div>
</template>
