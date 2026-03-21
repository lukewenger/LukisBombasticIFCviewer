export interface CsvPoint {
  x: number
  y: number
  z: number
  entityId: string | null
  pickedAt: string
}

function toCsvValue(value: string): string {
  return `"${value.replace(/"/g, '""')}"`
}

export function parseCsvRow(line: string): string[] {
  const result: string[] = []
  let current = ''
  let inQuotes = false

  for (let i = 0; i < line.length; i++) {
    const char = line[i]
    const next = line[i + 1]

    if (char === '"') {
      if (inQuotes && next === '"') {
        current += '"'
        i++
      } else {
        inQuotes = !inQuotes
      }
      continue
    }

    if (char === ',' && !inQuotes) {
      result.push(current.trim())
      current = ''
      continue
    }

    current += char
  }

  result.push(current.trim())
  return result
}

export function parsePointsFromCsv(content: string): CsvPoint[] {
  const lines = content
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0)

  if (lines.length === 0) return []

  const firstRow = parseCsvRow(lines[0] ?? '').map((value) => value.toLowerCase())
  const hasHeader = firstRow.includes('x') && firstRow.includes('y') && firstRow.includes('z')

  const xIndex = hasHeader ? firstRow.indexOf('x') : 0
  const yIndex = hasHeader ? firstRow.indexOf('y') : 1
  const zIndex = hasHeader ? firstRow.indexOf('z') : 2
  const entityIndex = hasHeader ? firstRow.indexOf('entityid') : -1
  const pickedAtIndex = hasHeader ? firstRow.indexOf('pickedat') : -1

  const points: CsvPoint[] = []

  for (const line of lines.slice(hasHeader ? 1 : 0)) {
    const row = parseCsvRow(line)
    const x = Number(row[xIndex])
    const y = Number(row[yIndex])
    const z = Number(row[zIndex])

    if (!Number.isFinite(x) || !Number.isFinite(y) || !Number.isFinite(z)) {
      continue
    }

    points.push({
      x,
      y,
      z,
      entityId: entityIndex >= 0 ? (row[entityIndex] || null) : null,
      pickedAt: pickedAtIndex >= 0 && row[pickedAtIndex] ? row[pickedAtIndex] : new Date().toISOString(),
    })
  }

  return points
}

export async function importPointsFromCsv(file: File): Promise<CsvPoint[]> {
  const content = await file.text()
  return parsePointsFromCsv(content)
}

export function downloadPickedPointsCsv(points: CsvPoint[]) {
  if (points.length === 0) return

  const headers = ['index', 'x', 'y', 'z', 'entityId', 'pickedAt']
  const rows = points.map((point, index) => [
    String(index + 1),
    point.x.toFixed(6),
    point.y.toFixed(6),
    point.z.toFixed(6),
    point.entityId ?? '',
    point.pickedAt,
  ])

  const csv = [headers.join(','), ...rows.map((row) => row.map(toCsvValue).join(','))].join('\n')

  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const url = window.URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `picked-points-${new Date().toISOString().replace(/[:.]/g, '-')}.csv`
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  window.URL.revokeObjectURL(url)
}
