import api from './client'
import type { ConversionJobDto } from '../types'
import { ConversionFormat } from '../types/models'

export const conversionsApi = {
  async createConversionJob(modelId: string, targetFormat: ConversionFormat = ConversionFormat.XKT): Promise<ConversionJobDto> {
    const response = await api.post<ConversionJobDto>('/conversions', {
      modelId,
      targetFormat,
    })
    return response.data
  },

  async getConversionJob(id: string): Promise<ConversionJobDto> {
    const response = await api.get<ConversionJobDto>(`/conversions/${id}`)
    return response.data
  },
}
