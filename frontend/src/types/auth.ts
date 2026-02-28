/** Auth response from the API */
export interface AuthResponse {
  userId: string
  username: string
  email: string
  role: string
  accessToken: string
  refreshToken: string
}

/** User profile from GET /api/auth/me */
export interface UserProfile {
  id: string
  username: string
  email: string
  role: string
  isActive: boolean
  createdAt: string
}

/** Login request payload */
export interface LoginRequest {
  username: string
  password: string
}

/** Register request payload */
export interface RegisterRequest {
  username: string
  email: string
  password: string
}

/** Generic API error response */
export interface ApiError {
  message: string
}
