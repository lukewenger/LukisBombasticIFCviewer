<script setup lang="ts">
import { ref } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { z } from 'zod'
import ErrorMessage from './ErrorMessage.vue'

defineProps<{
  isLoading: boolean
  error: string | null
}>()

const emit = defineEmits<{
  submit: [data: { username: string; password: string }]
}>()

const showPassword = ref(false)

const schema = toTypedSchema(
  z.object({
    username: z.string().min(1, 'Benutzername ist erforderlich'),
    password: z.string().min(1, 'Passwort ist erforderlich'),
  })
)

const { handleSubmit, defineField, errors: fieldErrors } = useForm({
  validationSchema: schema,
})

const [username, usernameAttrs] = defineField('username')
const [password, passwordAttrs] = defineField('password')

const onSubmit = handleSubmit((values) => {
  emit('submit', { username: values.username, password: values.password })
})
</script>

<template>
  <form @submit.prevent="onSubmit" class="space-y-5">
    <ErrorMessage v-if="error" :message="error" />

    <!-- Username -->
    <div>
      <label for="username" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
        Benutzername
      </label>
      <input
        id="username"
        v-model="username"
        v-bind="usernameAttrs"
        type="text"
        autocomplete="username"
        :class="[
          'w-full rounded-lg border px-4 py-2.5 text-gray-900 dark:text-white bg-white dark:bg-gray-700 focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-colors',
          fieldErrors.username ? 'border-red-500' : 'border-gray-300 dark:border-gray-600'
        ]"
        placeholder="Dein Benutzername"
      />
      <p v-if="fieldErrors.username" class="mt-1 text-sm text-red-500">{{ fieldErrors.username }}</p>
    </div>

    <!-- Password -->
    <div>
      <label for="password" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
        Passwort
      </label>
      <div class="relative">
        <input
          id="password"
          v-model="password"
          v-bind="passwordAttrs"
          :type="showPassword ? 'text' : 'password'"
          autocomplete="current-password"
          :class="[
            'w-full rounded-lg border px-4 py-2.5 pr-12 text-gray-900 dark:text-white bg-white dark:bg-gray-700 focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-colors',
            fieldErrors.password ? 'border-red-500' : 'border-gray-300 dark:border-gray-600'
          ]"
          placeholder="Dein Passwort"
        />
        <button
          type="button"
          class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-700 dark:hover:text-gray-300 text-sm"
          @click="showPassword = !showPassword"
        >
          {{ showPassword ? 'Verbergen' : 'Anzeigen' }}
        </button>
      </div>
      <p v-if="fieldErrors.password" class="mt-1 text-sm text-red-500">{{ fieldErrors.password }}</p>
    </div>

    <!-- Submit -->
    <button
      type="submit"
      :disabled="isLoading"
      class="w-full py-2.5 px-4 bg-blue-600 text-white font-medium rounded-lg hover:bg-blue-700 focus:ring-4 focus:ring-blue-300 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
    >
      <span v-if="isLoading" class="flex items-center justify-center gap-2">
        <span class="animate-spin h-4 w-4 border-2 border-white border-t-transparent rounded-full"></span>
        Anmelden...
      </span>
      <span v-else>Anmelden</span>
    </button>
  </form>
</template>
