import api from './client'
import type { AuthResponse, LoginRequest, RegisterRequest, UserProfile } from '../types'

export const authApi = {
  async login(data: LoginRequest): Promise<AuthResponse> {
    const response = await api.post<AuthResponse>('/auth/login', data)
    return response.data
  },

  async register(data: RegisterRequest): Promise<AuthResponse> {
    const response = await api.post<AuthResponse>('/auth/register', data)
    return response.data
  },

  async getCurrentUser(): Promise<UserProfile> {
    const response = await api.get<UserProfile>('/auth/me')
    return response.data
  },
}
