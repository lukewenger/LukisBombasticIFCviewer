import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authApi } from '../api/auth'
import type { AuthResponse, UserProfile, LoginRequest, RegisterRequest } from '../types'

export const useAuthStore = defineStore('auth', () => {
  // State
  const user = ref<UserProfile | null>(null)
  const accessToken = ref<string | null>(null)
  const refreshToken = ref<string | null>(null)
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  // Getters
  const isAuthenticated = computed(() => !!accessToken.value && !!user.value)
  const userRole = computed(() => user.value?.role ?? null)
  const username = computed(() => user.value?.username ?? null)

  // Actions
  function setAuthData(response: AuthResponse) {
    accessToken.value = response.accessToken
    refreshToken.value = response.refreshToken
    user.value = {
      id: response.userId,
      username: response.username,
      email: response.email,
      role: response.role,
      isActive: true,
      createdAt: new Date().toISOString(),
    }

    // Persist to localStorage
    localStorage.setItem('accessToken', response.accessToken)
    localStorage.setItem('refreshToken', response.refreshToken)
    localStorage.setItem('user', JSON.stringify(user.value))
  }

  function clearAuthData() {
    user.value = null
    accessToken.value = null
    refreshToken.value = null
    error.value = null

    localStorage.removeItem('accessToken')
    localStorage.removeItem('refreshToken')
    localStorage.removeItem('user')
  }

  /** Initialize auth state from localStorage on app start */
  function initializeFromStorage() {
    const storedToken = localStorage.getItem('accessToken')
    const storedRefresh = localStorage.getItem('refreshToken')
    const storedUser = localStorage.getItem('user')

    if (storedToken && storedUser) {
      accessToken.value = storedToken
      refreshToken.value = storedRefresh
      try {
        user.value = JSON.parse(storedUser)
      } catch {
        clearAuthData()
      }
    }
  }

  /** Login with username and password */
  async function login(data: LoginRequest) {
    isLoading.value = true
    error.value = null

    try {
      const response = await authApi.login(data)
      setAuthData(response)
    } catch (err: unknown) {
      const axiosError = err as { response?: { data?: { message?: string } } }
      error.value = axiosError.response?.data?.message ?? 'Login fehlgeschlagen'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  /** Register a new account */
  async function register(data: RegisterRequest) {
    isLoading.value = true
    error.value = null

    try {
      const response = await authApi.register(data)
      setAuthData(response)
    } catch (err: unknown) {
      const axiosError = err as { response?: { data?: { message?: string } } }
      error.value = axiosError.response?.data?.message ?? 'Registrierung fehlgeschlagen'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  /** Logout and clear all auth state */
  function logout() {
    clearAuthData()
  }

  /** Fetch and refresh the current user profile */
  async function fetchCurrentUser() {
    if (!accessToken.value) return

    try {
      const profile = await authApi.getCurrentUser()
      user.value = profile
      localStorage.setItem('user', JSON.stringify(profile))
    } catch {
      clearAuthData()
    }
  }

  return {
    // State
    user,
    accessToken,
    refreshToken,
    isLoading,
    error,
    // Getters
    isAuthenticated,
    userRole,
    username,
    // Actions
    initializeFromStorage,
    login,
    register,
    logout,
    fetchCurrentUser,
  }
})
