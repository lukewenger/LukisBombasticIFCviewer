<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { Upload, FileUp, CheckCircle, AlertCircle } from 'lucide-vue-next'
import { modelsApi } from '../api/models'
import { conversionsApi } from '../api/conversions'
import { ConversionFormat } from '../types/models'

const router = useRouter()

const file = ref<File | null>(null)
const isDragging = ref(false)
const uploadProgress = ref(0)
const isUploading = ref(false)
const error = ref<string | null>(null)
const uploadSuccess = ref(false)

const MAX_FILE_SIZE = 500 * 1024 * 1024 // 500 MB

function validateFile(f: File): string | null {
  if (!f.name.toLowerCase().endsWith('.ifc')) {
    return 'Nur IFC-Dateien sind erlaubt.'
  }
  if (f.size > MAX_FILE_SIZE) {
    return `Datei ist zu gross (max. ${MAX_FILE_SIZE / 1024 / 1024} MB).`
  }
  return null
}

function selectFile(f: File) {
  const validationError = validateFile(f)
  if (validationError) {
    error.value = validationError
    file.value = null
    return
  }
  error.value = null
  file.value = f
}

function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  if (input.files?.[0]) {
    selectFile(input.files[0])
  }
}

function onDrop(event: DragEvent) {
  isDragging.value = false
  const droppedFile = event.dataTransfer?.files[0]
  if (droppedFile) {
    selectFile(droppedFile)
  }
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

async function upload() {
  if (!file.value) return

  isUploading.value = true
  error.value = null
  uploadProgress.value = 0

  try {
    const model = await modelsApi.uploadModel(file.value, (percent) => {
      uploadProgress.value = percent
    })

    // Trigger XKT conversion
    await conversionsApi.createConversionJob(model.id, ConversionFormat.XKT)

    uploadSuccess.value = true
    setTimeout(() => router.push('/dashboard'), 1500)
  } catch (err: unknown) {
    const axiosError = err as { response?: { data?: { message?: string } } }
    error.value = axiosError.response?.data?.message ?? 'Upload fehlgeschlagen. Bitte erneut versuchen.'
  } finally {
    isUploading.value = false
  }
}
</script>

<template>
  <div class="max-w-4xl mx-auto px-4 py-8">
    <div class="flex items-center gap-3 mb-8">
      <Upload class="w-8 h-8 text-blue-600" />
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Modell hochladen</h1>
    </div>

    <!-- Success message -->
    <div v-if="uploadSuccess" class="bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-xl p-6 text-center">
      <CheckCircle class="w-12 h-12 text-green-500 mx-auto mb-3" />
      <p class="text-lg font-semibold text-green-700 dark:text-green-400">Upload erfolgreich!</p>
      <p class="text-sm text-green-600 dark:text-green-500 mt-1">Weiterleitung zum Dashboard...</p>
    </div>

    <div v-else class="space-y-6">
      <!-- Drop zone -->
      <div
        class="border-2 border-dashed rounded-xl p-12 text-center transition-colors cursor-pointer"
        :class="isDragging
          ? 'border-blue-500 bg-blue-50 dark:bg-blue-900/20'
          : 'border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 hover:border-blue-400'"
        @dragover.prevent="isDragging = true"
        @dragleave.prevent="isDragging = false"
        @drop.prevent="onDrop"
        @click="($refs.fileInput as HTMLInputElement).click()"
      >
        <FileUp class="w-12 h-12 text-gray-400 mx-auto mb-4" />
        <p class="text-gray-600 dark:text-gray-400 mb-2">
          IFC-Datei hierher ziehen oder <span class="text-blue-600 font-medium">durchsuchen</span>
        </p>
        <p class="text-sm text-gray-400 dark:text-gray-500">Maximal {{ MAX_FILE_SIZE / 1024 / 1024 }} MB</p>
        <input
          ref="fileInput"
          type="file"
          accept=".ifc"
          class="hidden"
          @change="onFileChange"
        />
      </div>

      <!-- Selected file info -->
      <div v-if="file" class="bg-white dark:bg-gray-800 rounded-xl shadow-md p-4 flex items-center justify-between">
        <div>
          <p class="font-medium text-gray-900 dark:text-white">{{ file.name }}</p>
          <p class="text-sm text-gray-500 dark:text-gray-400">{{ formatSize(file.size) }}</p>
        </div>
        <button
          v-if="!isUploading"
          class="text-sm text-red-500 hover:text-red-700"
          @click="file = null"
        >
          Entfernen
        </button>
      </div>

      <!-- Error message -->
      <div v-if="error" class="flex items-center gap-3 p-4 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
        <AlertCircle class="w-5 h-5 text-red-500 shrink-0" />
        <p class="text-sm text-red-700 dark:text-red-400">{{ error }}</p>
      </div>

      <!-- Progress bar -->
      <div v-if="isUploading" class="space-y-2">
        <div class="flex justify-between text-sm text-gray-600 dark:text-gray-400">
          <span>Hochladen...</span>
          <span>{{ uploadProgress }}%</span>
        </div>
        <div class="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-3">
          <div
            class="bg-blue-600 h-3 rounded-full transition-all duration-300"
            :style="{ width: `${uploadProgress}%` }"
          ></div>
        </div>
      </div>

      <!-- Upload button -->
      <button
        :disabled="!file || isUploading"
        class="w-full py-3 px-4 bg-blue-600 text-white font-medium rounded-lg hover:bg-blue-700 focus:ring-4 focus:ring-blue-300 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        @click="upload"
      >
        <span v-if="isUploading" class="flex items-center justify-center gap-2">
          <span class="animate-spin h-4 w-4 border-2 border-white border-t-transparent rounded-full"></span>
          Hochladen...
        </span>
        <span v-else>Modell hochladen</span>
      </button>
    </div>
  </div>
</template>
