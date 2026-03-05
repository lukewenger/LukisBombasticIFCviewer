import api from './client'
import type { IfcModelDto } from '../types'

export const modelsApi = {
  async getModels(): Promise<IfcModelDto[]> {
    const response = await api.get<IfcModelDto[]>('/models')
    return response.data
  },

  async getModel(id: string): Promise<IfcModelDto> {
    const response = await api.get<IfcModelDto>(`/models/${id}`)
    return response.data
  },

  async uploadModel(file: File, onProgress?: (percent: number) => void): Promise<IfcModelDto> {
    const formData = new FormData()
    formData.append('file', file)

    const response = await api.post<IfcModelDto>('/models/upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: (event) => {
        if (event.total && onProgress) {
          onProgress(Math.round((event.loaded * 100) / event.total))
        }
      },
    })
    return response.data
  },

  getModelOutputUrl(id: string): string {
    return `/api/models/${id}/output`
  },
}
