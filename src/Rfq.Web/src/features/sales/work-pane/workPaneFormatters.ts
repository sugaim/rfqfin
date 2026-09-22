export const million = 1_000_000

export function quoteValue(
  value: number | null | undefined,
  suffix = '',
): string {
  return value === null || value === undefined
    ? '—'
    : `${value.toLocaleString()}${suffix}`
}

export function compactText(value: string, empty = '—'): string {
  return value.trim() || empty
}
