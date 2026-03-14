/** Status of an IFC model */
export const ModelStatus = {
  Uploaded: 0,
  Processing: 1,
  Ready: 2,
  Failed: 3,
  Archived: 4,
} as const
export type ModelStatus = (typeof ModelStatus)[keyof typeof ModelStatus]

/** Status of a conversion job */
export const ConversionStatus = {
  Queued: 0,
  Processing: 1,
  Completed: 2,
  Failed: 3,
  Cancelled: 4,
} as const
export type ConversionStatus = (typeof ConversionStatus)[keyof typeof ConversionStatus]

/** Target format for conversion */
export const ConversionFormat = {
  XKT: 0,
  GLTF: 1,
  GLB: 2,
  JSON: 3,
} as const
export type ConversionFormat = (typeof ConversionFormat)[keyof typeof ConversionFormat]

/** IFC model DTO from the API */
export interface IfcModelDto {
  id: string
  fileName: string
  fileSizeBytes: number
  status: ModelStatus
  createdAt: string
  updatedAt: string | null
  metadata: ModelMetadataDto | null
  /** Relative URL to download the converted XKT file. Null until a conversion job completes. */
  xktOutputUrl: string | null
  /** Relative URL to download the original IFC file. */
  originalFileUrl: string | null
}

/** Model metadata */
export interface ModelMetadataDto {
  ifcSchema: string
  projectName: string
  applicationName: string
  numberOfElements: number
  elementTypeCounts: Record<string, number>
}

/** Conversion job DTO from the API */
export interface ConversionJobDto {
  id: string
  modelId: string
  targetFormat: ConversionFormat
  status: ConversionStatus
  progressPercentage: number
  createdAt: string
  startedAt: string | null
  completedAt: string | null
  outputFilePath: string | null
  errorMessage: string | null
}
